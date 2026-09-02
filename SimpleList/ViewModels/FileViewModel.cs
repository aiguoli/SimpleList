using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SimpleList.Core.Models;
using SimpleList.Helpers;
using SimpleList.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;

namespace SimpleList.ViewModels;

public partial class FileViewModel : ObservableObject
{
    public FileViewModel(DriveViewModel drive, FileItem file, bool loadThumbnail = false)
    {
        Drive = drive;
        _file = file;
    }

    [RelayCommand]
    private async Task DownloadFile(string itemId)
    {
        Window _downloadPathSelectWindow = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(_downloadPathSelectWindow);
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        savePicker.FileTypeChoices.Add(ResourceHelper.GetLocalized("FilePicker_AllFiles"), [Path.GetExtension(_file.Name)]);
        savePicker.SuggestedFileName = _file.Name;
        InitializeWithWindow.Initialize(savePicker, hwnd);
        StorageFile file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
            await manager.AddDownloadTask(Drive, itemId, file);
        }
    }

    public async Task LoadImage()
    {
        if (IsFile && _file.Image != null)
        {
            StorageResult<Stream> result = await Drive.Provider.GetItemContentAsync(_file.Id);
            if (result.IsSuccess)
            {
                using Stream stream = result.Data;
                var randomAccessStream = new InMemoryRandomAccessStream();
                await RandomAccessStream.CopyAsync(stream.AsInputStream(), randomAccessStream);
                randomAccessStream.Seek(0);
                BitmapImage img = new();
                await img.SetSourceAsync(randomAccessStream);
                Image = img;
            }
        }
    }

    [RelayCommand]
    public async Task LoadContent()
    {
        if (IsFile)
        {
            var result = await Drive.Provider.GetItemContentAsync(Id);
            Content = result.IsSuccess ? result.Data?.ToString() : null;
        }
    }

    [ObservableProperty]
    public partial BitmapImage Image { get; set; }

    [ObservableProperty]
    public partial string Content { get; set; }

    internal void Update(FileItem file)
    {
        if (ReferenceEquals(_file, file))
        {
            return;
        }

        _file = file;
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Size));
        OnPropertyChanged(nameof(Updated));
        OnPropertyChanged(nameof(IsFile));
        OnPropertyChanged(nameof(IsFolder));
        OnPropertyChanged(nameof(ChildrenCount));
        OnPropertyChanged(nameof(ItemType));
        OnPropertyChanged(nameof(IsShared));
        OnPropertyChanged(nameof(ShareStatusText));
        OnPropertyChanged(nameof(DownloadUrl));
        OnPropertyChanged(nameof(CanPreview));
        OnPropertyChanged(nameof(CanOpenExternally));
        OnPropertyChanged(nameof(CanOpen));
    }

    private FileItem _file;

    public string Id => _file.Id;
    public string Name => _file.Name;
    public long? Size => _file.Size;
    public DateTimeOffset? Updated => _file.Updated;
    public bool IsFile => !_file.IsFolder;
    public bool IsFolder => _file.IsFolder;
    public int? ChildrenCount => _file.ChildCount;
    internal bool HasImageMetadata => _file.Image != null;
    public DriveViewModel Drive { get; }
    public string ItemType => IsFile
        ? ResourceHelper.GetLocalized("ItemType_File")
        : ResourceHelper.GetLocalized("ItemType_Folder");
    public bool? IsShared => _file.IsShared;
    public string ShareStatusText
    {
        get
        {
            if (Drive.Provider.ProviderType == ProviderType.Local)
            {
                return ResourceHelper.GetLocalized("ShareStatus_Unsupported");
            }

            return IsShared switch
            {
                true => ResourceHelper.GetLocalized("ShareStatus_Shared"),
                false => ResourceHelper.GetLocalized("ShareStatus_NotShared"),
                _ => ResourceHelper.GetLocalized("ShareStatus_Unknown"),
            };
        }
    }

    public string DownloadUrl
    {
        get
        {
            if (_file.ProviderTokens != null && _file.ProviderTokens.TryGetValue("downloadUrl", out var url))
            {
                return url;
            }
            return null;
        }
    }

    public async Task<string> GetDownloadUrlAsync()
    {
        StorageResult<string> result = await GetDownloadUrlResultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<StorageResult<string>> GetDownloadUrlResultAsync()
    {
        if (Drive.Provider.ProviderType == ProviderType.Local)
        {
            return StorageResult<string>.Success(_file.Id);
        }

        if (!string.IsNullOrEmpty(DownloadUrl))
        {
            return StorageResult<string>.Success(DownloadUrl);
        }

        StorageResult<string> result = await Drive.Provider.GetDownloadUrlAsync(Id);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Data))
        {
            return result;
        }

        return StorageResult<string>.Failure(
            string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? ResourceHelper.GetLocalized("ExternalDownloader_NoDownloadUrl")
                : result.ErrorMessage,
            result.ErrorType,
            result.Exception);
    }

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(CanShare));
        OnPropertyChanged(nameof(CanCopy));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanRestoreFromTrash));
        OnPropertyChanged(nameof(CanPermanentDeleteFromTrash));
        OnPropertyChanged(nameof(CanUseNormalActions));
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanOpenExternally));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(CanDownloadWithExternalTool));
    }

    public void UpdateShareStatus(bool? isShared)
    {
        _file.IsShared = isShared;
        OnPropertyChanged(nameof(IsShared));
        OnPropertyChanged(nameof(ShareStatusText));
    }

    public bool CanUseNormalActions => !Drive.IsTrashMode;
    public bool CanPreview => CanUseNormalActions && IsFile && Drive.Provider.ProviderType != ProviderType.Local && Utils.GetFileType(Path.GetExtension(Name)) != Models.FileType.Unknown;
    public bool CanRename => Drive.CanEditCurrentFolder && Drive.SelectedItems.Count == 1;
    public bool CanShare => Drive.CanEditCurrentFolder && Drive.SelectedItems.Count == 1 && Drive.Provider.ShareCapabilities.CanCreatePublicLink;
    public bool CanCopy => CanUseNormalActions && Drive.SelectedItems.Count == 1;
    public bool CanDelete => Drive.CanEditCurrentFolder;
    public bool CanRestoreFromTrash => Drive.IsTrashMode && Drive.CanManageTrash;
    public bool CanPermanentDeleteFromTrash => Drive.IsTrashMode && Drive.CanManageTrash;
    public bool CanConvert => Drive.CanEditCurrentFolder && IsFile && CanConvertToPdf(Name);
    public bool CanOpenExternally => CanUseNormalActions && IsFile && Drive.Provider.ProviderType == ProviderType.Local;
    public bool CanOpen => CanPreview || CanOpenExternally;
    public bool CanDownloadWithExternalTool => CanUseNormalActions
        && Drive.Provider.ProviderType != ProviderType.Local
        && Drive.SelectedItems.Count > 0
        && Drive.SelectedItems.All(item => item.IsFile);

    public async Task OpenExternallyAsync()
    {
        if (Drive.Provider.ProviderType == ProviderType.Local)
        {
            await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(_file.Id));
        }
    }

    private static bool CanConvertToPdf(string fileName)
    {
        string[] allowedExtensions = { ".csv", ".doc", ".docx", ".odp", ".ods", ".odt", ".pot", ".potm", ".potx", ".pps", ".ppsx", ".ppsxm", ".ppt", ".pptm", ".pptx", ".rtf", ".xls", ".xlsx" };
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return allowedExtensions.Contains(extension);
    }

}
