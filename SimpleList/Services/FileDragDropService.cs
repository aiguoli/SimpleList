using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Core.Models;
using SimpleList.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using WinUICommunity;

namespace SimpleList.Services;

internal static class FileDragDropService
{
    public static void ConfigureDragItems(DragItemsStartingEventArgs args, DriveViewModel drive)
    {
        List<FileViewModel> items = args.Items.OfType<FileViewModel>().ToList();
        if (drive == null || items.Count == 0)
        {
            args.Cancel = true;
            return;
        }

        if (drive.Provider.ProviderType != ProviderType.Local && items.Any(item => item.IsFolder))
        {
            args.Cancel = true;
            ShowError(Helpers.ResourceHelper.GetLocalized("DragCloudFoldersNotSupported"));
            return;
        }

        DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
        TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        args.Data.Properties.Title = items.Count == 1
            ? items[0].Name
            : string.Format(Helpers.ResourceHelper.GetLocalized("FileCountFormat"), items.Count);
        foreach (string fileType in items
            .Where(item => item.IsFile)
            .Select(item => Path.GetExtension(item.Name))
            .Where(fileType => !string.IsNullOrWhiteSpace(fileType))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            args.Data.Properties.FileTypes.Add(fileType);
        }

        args.Data.SetDataProvider(StandardDataFormats.StorageItems, request =>
        {
            _ = ProvideStorageItemsAsync(request, drive, items, manager, dispatcher);
        });
    }

    private static async Task ProvideStorageItemsAsync(
        DataProviderRequest request,
        DriveViewModel drive,
        IReadOnlyList<FileViewModel> items,
        TaskManagerViewModel manager,
        DispatcherQueue dispatcher)
    {
        DataProviderDeferral deferral = request.GetDeferral();
        try
        {
            List<IStorageItem> storageItems = [];
            foreach (FileViewModel item in items)
            {
                storageItems.Add(await CreateStorageItemAsync(drive, item, manager));
            }

            request.SetData(storageItems);
        }
        catch (Exception ex)
        {
            Enqueue(dispatcher, () => ShowError(ex.Message));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static async Task<IStorageItem> CreateStorageItemAsync(DriveViewModel drive, FileViewModel item, TaskManagerViewModel manager)
    {
        if (drive.Provider.ProviderType == ProviderType.Local)
        {
            if (item.IsFolder)
            {
                return await StorageFolder.GetFolderFromPathAsync(item.Id);
            }

            return await StorageFile.GetFileFromPathAsync(item.Id);
        }

        return await StorageFile.CreateStreamedFileAsync(
            GetSafeFileName(item.Name),
            request => _ = WriteVirtualFileAsync(request, item, manager),
            null);
    }

    private static string GetSafeFileName(string name)
    {
        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safeName) ? "download" : safeName;
    }

    private static async Task WriteVirtualFileAsync(StreamedFileDataRequest request, FileViewModel item, TaskManagerViewModel manager)
    {
        try
        {
            using (Stream stream = request.AsStreamForWrite())
            {
                await manager.AddStreamDownloadTask(item.Drive, item.Id, stream);
            }

            request.Dispose();
        }
        catch
        {
            request.FailAndClose(StreamedFileFailureMode.Failed);
        }
    }

    private static void Enqueue(DispatcherQueue dispatcher, Action action)
    {
        if (dispatcher == null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcher.TryEnqueue(() => action());
    }

    private static void ShowError(string message)
    {
        Growl.Error(new GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("Error"),
            Message = message,
            StaysOpen = false,
            Token = "DriveGrowl"
        });
    }
}
