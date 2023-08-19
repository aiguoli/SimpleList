using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Dispatching;
using SimpleList.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
    public class UploadTaskViewModel : ObservableObject
    {
        public UploadTaskViewModel(string itemId, IStorageItem item)
        {
            _itemId = itemId;
            _item = item;
            Drive = Ioc.Default.GetService<OneDrive>();
        }
        
        public async Task StartUpload()
        {
            _isUploading = true;
            IProgress<long> progress = new Progress<long>(value =>
            {
                _dispatcher.TryEnqueue(async () =>
                {
                    Progress = _item is StorageFile ? (int)((ulong)value * 100 / (await _item.GetBasicPropertiesAsync()).Size) : (int)value;
                });
            });
            if (_item is StorageFile file)
            {

                await Drive.UploadFileAsync(file, _itemId, progress);
            } else if (_item is StorageFolder folder)
            {
                await Drive.UploadFolderAsync(folder, _itemId, progress);
            }
            Completed = true;
            _isUploading = false;
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
        private readonly TaskManagerViewModel _manager = Ioc.Default.GetService<TaskManagerViewModel>();
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        private int _progress;
        private bool _completed = false;
        private bool _isUploading = true;

        public OneDrive Drive;
        public string Name => _item.Name;
        public bool Completed { get => _completed; private set => SetProperty(ref _completed, value); }
        public int Progress { get => _progress; private set => SetProperty(ref _progress, value); }
        public bool IsUploading { get => _isUploading; private set => SetProperty(ref _isUploading, value); }
    }
}
