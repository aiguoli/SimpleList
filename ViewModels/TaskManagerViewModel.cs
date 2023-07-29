using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
    public class TaskManagerViewModel : ObservableObject
    {
        public ObservableCollection<DownloadTaskViewModel> DownloadTasks { get; } = new();

        public async Task AddDownloadTask(string itemId, StorageFile file)
        {
            DownloadTaskViewModel task = new(itemId, file);
            DownloadTasks.Add(task);
            await task.StartDownload();
        }   

        public async Task StartAllAsync()
        {
            foreach (DownloadTaskViewModel task in DownloadTasks)
            {
                if (!task.Completed)
                {
                    await task.ResumeDownload();
                }
            }
        }

        public void RemoveCompletedTasks()
        {
            foreach (DownloadTaskViewModel completedTask in DownloadTasks.Where(t => t.Completed).ToList())
            {
                DownloadTasks.Remove(completedTask);
            }
        }
    }
}
