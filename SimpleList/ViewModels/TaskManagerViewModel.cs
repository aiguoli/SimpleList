using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using WinUICommunity;

namespace SimpleList.ViewModels
{
    public partial class TaskManagerViewModel : ObservableObject
    {
        [ObservableProperty] private bool _shutdownAfterDownload = false;
        public ObservableCollection<DownloadTaskViewModel> DownloadTasks { get; } = [];
        public ObservableCollection<UploadTaskViewModel> UploadTasks { get; } = [];

        private void CheckShutdown(object sender, AsyncCompletedEventArgs e)
        {
            Debug.WriteLine("check shutdown");
            if (ShutdownAfterDownload && DownloadTasks.All(task => task.Completed))
            {
                Process.Start("shutdown", "/s /t 0");
            }
        }

        public async Task AddDownloadTask(DriveViewModel drive, string itemId, IStorageItem target)
        {
            DownloadTaskViewModel task = new(drive, itemId, target, CheckShutdown);
            DownloadTasks.Add(task);
            await task.StartDownload(false);
        }

        public async Task StartAllDownloadTasks()
        {
            foreach (DownloadTaskViewModel task in DownloadTasks)
            {
                if (!task.Completed)
                {
                    await task.ResumeDownload();
                }
            }
        }

        public void RemoveSelectedDownloadTasks(DownloadTaskViewModel task)
        {
            DownloadTasks.Remove(task);
        }

        public void RemoveCompletedDonwloadTasks()
        {
            foreach (DownloadTaskViewModel completedTask in DownloadTasks.Where(t => t.Completed).ToList())
            {
                DownloadTasks.Remove(completedTask);
            }
        }


        public async Task AddUploadTask(DriveViewModel drive, string itemId, IStorageItem item)
        {
            UploadTaskViewModel task = new(drive, itemId, item);
            UploadTasks.Add(task);
            await task.StartUpload();
        }

        public void RemoveSelectedUploadTasks(UploadTaskViewModel task)
        {
            UploadTasks.Remove(task);
        }

        [RelayCommand]
        private void ChangeShuwdownBehavious(bool canShutdown)
        {
            ShutdownAfterDownload = canShutdown;
        }
    }
}
