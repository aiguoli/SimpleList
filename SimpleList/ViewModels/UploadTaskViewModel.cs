using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Graph.Models;
using Microsoft.UI.Dispatching;
using SimpleList.Models;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class UploadTaskViewModel : ObservableObject
{
    public UploadTaskViewModel(DriveViewModel drive,string itemId, IStorageItem item)
    {
        _itemId = itemId;
        _item = item;
        Drive = drive;
    }
    
    public async Task StartUpload()
    {
        IsUploading = true;
        Growl.Info(new GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_Upload"),
            Message = string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartUploadDesc"), _item.Name),
            IsClosable = true,
            ShowDateTime = true,
            Token = "DriveGrowl"
        });
        IProgress<long> progress = new Progress<long>(value =>
        {
            _dispatcher.TryEnqueue(async () =>
            {
                Progress = _item is StorageFile ? (int)((ulong)value * 100 / (await _item.GetBasicPropertiesAsync()).Size) : (int)value;
            });
        });
        if (_item is StorageFile file)
        {
            OneDriveResult<DriveItem> result = await Drive.Provider.UploadFileAsync(file, _itemId, progress);
            if (result.IsSuccess)
            {
                Growl.Success(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("Success"),
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            } else
            {
                Growl.Error(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("Error"),
                    Message = result.ErrorMessage,
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            }
        } else if (_item is StorageFolder folder)
        {
            OneDriveResult<DriveItem> result = await Drive.Provider.UploadFolderAsync(folder, _itemId, progress);
            if (result.IsSuccess)
            {
                Growl.Success(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("Success"),
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            }
            else
            {
                Growl.Error(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("Error"),
                    Message = result.ErrorMessage,
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            }

        }
        Completed = true;
        IsUploading = false;
        await Task.CompletedTask;
    }

    public static void PauseUpload()
    {
        // Seems that Microsoft Graph API doesn't support pause upload.
    }

    public void CancelTask()
    {
        // https://github.com/microsoftgraph/msgraph-sdk-dotnet/issues/1678
        // Unfortunately, Microsoft Graph v4 doesn't support cancel upload while CommunityToolkit.Graph is still using this version.
        // So before it updates to v5, we can only remove the task from the list.
        _manager.RemoveSelectedUploadTasks(this);
    }

    private readonly string _itemId;
    private readonly IStorageItem _item;
    private readonly TaskManagerViewModel _manager = App.GetService<TaskManagerViewModel>();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    [ObservableProperty] private int _progress;
    [ObservableProperty] private bool _completed = false;
    [ObservableProperty] private bool _isUploading = true;

    public DriveViewModel Drive;
    public string Name => _item.Name;
}
