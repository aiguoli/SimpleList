using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
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
            IProgress<long> progress = new Progress<long>(value =>
            {
                _dispatcher.TryEnqueue(async () =>
                {
                    Progress = _item is StorageFile ? (int)((ulong)value * 100 / (await _item.GetBasicPropertiesAsync()).Size) : (int)value;
                });
            });
            if (_item is StorageFile file)
            {

                await Drive.Provider.UploadFileAsync(file, _itemId, progress);
            } else if (_item is StorageFolder folder)
            {
                await Drive.Provider.UploadFolderAsync(folder, _itemId, progress);
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
}
