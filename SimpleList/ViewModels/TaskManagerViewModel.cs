using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using WinUICommunity;

namespace SimpleList.ViewModels
{
    public partial class TaskManagerViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial bool ShutdownAfterDownload { get; set; } = false;
        public ObservableCollection<DownloadTaskViewModel> DownloadTasks { get; } = [];
        public ObservableCollection<UploadTaskViewModel> UploadTasks { get; } = [];
        public ObservableCollection<MigrationTaskViewModel> MigrationTasks { get; } = [];
        private readonly SemaphoreSlim _uploadSemaphore = new(3, 3);
        private readonly SemaphoreSlim _migrationSemaphore = new(2, 2);
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

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

        public async Task AddStreamDownloadTask(DriveViewModel drive, string itemId, Stream destination)
        {
            DownloadTaskViewModel task = null;
            await RunOnUiThreadAsync(() =>
            {
                task = new DownloadTaskViewModel(drive, itemId, destination, CheckShutdown);
                DownloadTasks.Add(task);
            });

            Task downloadTask = null;
            await RunOnUiThreadAsync(() =>
            {
                downloadTask = task.StartStreamDownload(false);
            });

            await downloadTask;
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

        [RelayCommand]
        private void ClearCompletedDownloadTasks()
        {
            foreach (DownloadTaskViewModel completedTask in DownloadTasks.Where(t => t.Completed).ToList())
            {
                DownloadTasks.Remove(completedTask);
            }
        }

        [RelayCommand]
        private void ClearCompletedUploadTasks()
        {
            foreach (UploadTaskViewModel completedTask in UploadTasks.Where(t => t.Completed).ToList())
            {
                UploadTasks.Remove(completedTask);
            }
        }

        [RelayCommand]
        private void ClearCompletedMigrationTasks()
        {
            foreach (MigrationTaskViewModel completedTask in MigrationTasks.Where(t => t.Completed).ToList())
            {
                MigrationTasks.Remove(completedTask);
            }
        }

        public async Task AddUploadTask(DriveViewModel drive, string itemId, IStorageItem item)
        {
            UploadTaskViewModel task = null;
            await RunOnUiThreadAsync(() =>
            {
                task = new UploadTaskViewModel(drive, itemId, item);
                UploadTasks.Add(task);
            });

            await _uploadSemaphore.WaitAsync();
            try
            {
                Task uploadTask = null;
                await RunOnUiThreadAsync(() => uploadTask = task.StartUpload());
                await uploadTask;
            }
            finally
            {
                _uploadSemaphore.Release();
            }
        }

        public void RemoveSelectedUploadTasks(UploadTaskViewModel task)
        {
            UploadTasks.Remove(task);
        }

        public async Task AddMigrationTasks(IEnumerable<FileViewModel> sourceItems, DriveViewModel sourceDrive, DriveViewModel targetDrive, string targetParentId, string targetPathText)
        {
            List<MigrationTaskViewModel> tasks = sourceItems
                .Select(item => new MigrationTaskViewModel(item, sourceDrive, targetDrive, targetParentId, targetPathText, _migrationSemaphore, this))
                .ToList();

            foreach (MigrationTaskViewModel task in tasks)
            {
                MigrationTasks.Add(task);
            }

            Growl.Info(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartMigration"),
                Message = string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartMigrationDesc"), tasks.Count),
                IsClosable = true,
                ShowDateTime = true,
                Token = "DriveGrowl",
                UseBlueColorForInfo = true
            });

            foreach (MigrationTaskViewModel task in tasks)
            {
                _ = task.StartAsync();
            }

            await Task.CompletedTask;
        }

        public void RemoveSelectedMigrationTask(MigrationTaskViewModel task)
        {
            if (task.CanCancel)
            {
                task.CancelTaskCommand.Execute(null);
            }
            MigrationTasks.Remove(task);
        }

        [RelayCommand]
        private void ChangeShuwdownBehavious(bool canShutdown)
        {
            ShutdownAfterDownload = canShutdown;
        }

        private Task RunOnUiThreadAsync(Action action)
        {
            if (_dispatcher == null || _dispatcher.HasThreadAccess)
            {
                action();
                return Task.CompletedTask;
            }

            TaskCompletionSource completion = new();
            if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
            {
                completion.SetException(new InvalidOperationException("Unable to schedule download task on the UI thread."));
            }

            return completion.Task;
        }
    }
}
