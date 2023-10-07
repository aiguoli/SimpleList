using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
    public class TaskManagerViewModel : ObservableObject
    {
        public ObservableCollection<DownloadTaskViewModel> DownloadTasks { get; } = new();
        public ObservableCollection<UploadTaskViewModel> UploadTasks { get; } = new();

        public async Task AddDownloadTask(string itemId, StorageFile file)
        {
            DownloadTaskViewModel task = new(itemId, file);
            DownloadTasks.Add(task);
            await task.StartDownload();
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


        public async Task AddUploadTask(string itemId, IStorageItem item)
        {
            UploadTaskViewModel task = new(itemId, item);
            UploadTasks.Add(task);
            await task.StartUpload();
        }

        public void RemoveSelectedUploadTasks(UploadTaskViewModel task)
        {
            UploadTasks.Remove(task);
        }
    }
}
