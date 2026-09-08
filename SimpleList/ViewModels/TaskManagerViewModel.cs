using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using DevWinUI;
using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using SimpleList.Core.Services;
using SimpleList.Helpers;

namespace SimpleList.ViewModels
{
    public partial class TaskManagerViewModel : ObservableObject
    {
        public TaskManagerViewModel()
        {
            DownloadTasks.CollectionChanged += TasksCollectionChanged;
            UploadTasks.CollectionChanged += TasksCollectionChanged;
            MigrationTasks.CollectionChanged += TasksCollectionChanged;
        }

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
            await SaveTasksAsync();
            OperationLogger.Record(
                "OperationLog_Category_Task",
                "OperationLog_Action_DownloadQueued",
                task.Name,
                OperationLogLevel.Success);
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

            await SaveTasksAsync();

            OperationLogger.Record(
                "OperationLog_Category_Task",
                "OperationLog_Action_UploadQueued",
                task.Name,
                OperationLogLevel.Success);

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

            await SaveTasksAsync();

            OperationLogger.Record(
                "OperationLog_Category_Task",
                "OperationLog_Action_MigrationQueued",
                $"{sourceDrive.DisplayName} → {targetDrive.DisplayName} · {tasks.Count}",
                OperationLogLevel.Success);

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

        public async Task LoadTasksFromDiskAsync(CloudViewModel cloud)
        {
            if (_tasksLoaded)
            {
                return;
            }

            _tasksLoaded = true;
            PersistentTaskListDTO persisted = await _taskStore.LoadAsync();
            if (persisted.Tasks == null || persisted.Tasks.Count == 0)
            {
                return;
            }

            _isRestoring = true;
            try
            {
                foreach (PersistentTaskDTO record in persisted.Tasks)
                {
                    try
                    {
                        bool restored = record.Kind switch
                        {
                            PersistentTaskKind.Download => await RestoreDownloadTaskAsync(record, cloud),
                            PersistentTaskKind.Upload => await RestoreUploadTaskAsync(record, cloud),
                            PersistentTaskKind.Migration => RestoreMigrationTask(record, cloud),
                            _ => false,
                        };
                        if (!restored)
                        {
                            _unresolvedTasks.Add(record);
                        }
                    }
                    catch (Exception ex)
                    {
                        _unresolvedTasks.Add(record);
                        App.LogError("Unable to restore persisted task", record.Name, ex);
                    }
                }
            }
            finally
            {
                _isRestoring = false;
            }

            await SaveTasksAsync();
        }

        private async Task<bool> RestoreDownloadTaskAsync(PersistentTaskDTO record, CloudViewModel cloud)
        {
            DriveViewModel drive = ResolveDrive(cloud, record.Drive);
            if (drive == null || string.IsNullOrWhiteSpace(record.ItemId))
            {
                return false;
            }

            IStorageItem target = null;
            if (!record.Completed)
            {
                target = await ResolveStorageItemAsync(record.Path, record.IsFolder, createFileIfMissing: true);
                if (target == null)
                {
                    return false;
                }
            }

            DownloadTaskViewModel task = new(drive, record, target, CheckShutdown);
            DownloadTasks.Add(task);
            if (!record.Completed && !record.IsPaused)
            {
                _ = RestartDownloadAsync(task);
            }
            return true;
        }

        private static async Task RestartDownloadAsync(DownloadTaskViewModel task)
        {
            try
            {
                await task.StartDownload(false);
            }
            catch (Exception ex)
            {
                App.LogError("Unable to restart download task", task.Name, ex);
            }
        }

        private async Task<bool> RestoreUploadTaskAsync(PersistentTaskDTO record, CloudViewModel cloud)
        {
            DriveViewModel drive = ResolveDrive(cloud, record.Drive);
            if (drive == null || string.IsNullOrWhiteSpace(record.ItemId))
            {
                return false;
            }

            IStorageItem item = null;
            if (!record.Completed)
            {
                item = await ResolveStorageItemAsync(record.Path, record.IsFolder, createFileIfMissing: false);
                if (item == null)
                {
                    return false;
                }
            }

            UploadTaskViewModel task = new(drive, record, item);
            UploadTasks.Add(task);
            if (!record.Completed && !record.IsPaused)
            {
                _ = RestartUploadAsync(task);
            }
            return true;
        }

        private async Task RestartUploadAsync(UploadTaskViewModel task)
        {
            await _uploadSemaphore.WaitAsync();
            try
            {
                await task.StartUpload();
            }
            catch (Exception ex)
            {
                App.LogError("Unable to restart upload task", task.Name, ex);
            }
            finally
            {
                _uploadSemaphore.Release();
            }
        }

        private bool RestoreMigrationTask(PersistentTaskDTO record, CloudViewModel cloud)
        {
            DriveViewModel sourceDrive = ResolveDrive(cloud, record.SourceDrive);
            DriveViewModel targetDrive = ResolveDrive(cloud, record.TargetDrive);
            if (sourceDrive == null || targetDrive == null || string.IsNullOrWhiteSpace(record.ItemId))
            {
                return false;
            }

            FileItem source = new()
            {
                Id = record.ItemId,
                Name = record.Name,
                Size = record.TotalBytes,
                IsFolder = record.IsFolder,
                Provider = sourceDrive.Provider.ProviderType,
            };
            MigrationTaskViewModel task = new(
                new FileViewModel(sourceDrive, source),
                sourceDrive,
                targetDrive,
                record.TargetParentId,
                record.TargetPathText,
                _migrationSemaphore,
                this);

            MigrationStatus status = Enum.TryParse(record.Status, out MigrationStatus parsed)
                ? parsed
                : MigrationStatus.Pending;
            bool interrupted = status is MigrationStatus.Pending or MigrationStatus.Running;
            task.RestoreState(
                interrupted ? MigrationStatus.Pending : status,
                interrupted ? 0 : record.Progress,
                interrupted ? 0 : record.TransferredBytes,
                record.TotalBytes,
                record.ErrorMessage);
            MigrationTasks.Add(task);
            if (interrupted)
            {
                _ = task.StartAsync();
            }
            return true;
        }

        private static DriveViewModel ResolveDrive(CloudViewModel cloud, DriveReferenceDTO reference)
        {
            if (reference == null)
            {
                return null;
            }

            return cloud.GetDrive(reference.ProviderType, reference.AccountId, reference.DriveId)
                ?? cloud.Drives.FirstOrDefault(drive =>
                    drive.Provider.ProviderType == reference.ProviderType
                    && string.Equals(drive.DisplayName, reference.DisplayName, StringComparison.Ordinal));
        }

        private static async Task<IStorageItem> ResolveStorageItemAsync(string path, bool isFolder, bool createFileIfMissing)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (isFolder)
            {
                return Directory.Exists(path) ? await StorageFolder.GetFolderFromPathAsync(path) : null;
            }

            if (File.Exists(path))
            {
                return await StorageFile.GetFileFromPathAsync(path);
            }

            string parentPath = Path.GetDirectoryName(path);
            if (!createFileIfMissing || string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
            {
                return null;
            }

            StorageFolder parent = await StorageFolder.GetFolderFromPathAsync(parentPath);
            return await parent.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.OpenIfExists);
        }

        private void TasksCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
            {
                foreach (INotifyPropertyChanged task in args.OldItems.OfType<INotifyPropertyChanged>())
                {
                    task.PropertyChanged -= TaskPropertyChanged;
                }
            }
            if (args.NewItems != null)
            {
                foreach (INotifyPropertyChanged task in args.NewItems.OfType<INotifyPropertyChanged>())
                {
                    task.PropertyChanged += TaskPropertyChanged;
                }
            }
            ScheduleSave();
        }

        private void TaskPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            bool terminalChange = args.PropertyName == nameof(DownloadTaskViewModel.Completed)
                || args.PropertyName == nameof(UploadTaskViewModel.Completed)
                || args.PropertyName == nameof(MigrationTaskViewModel.Status);
            if (terminalChange)
            {
                _ = SaveTasksAsync();
            }
            else
            {
                ScheduleSave();
            }
        }

        private void ScheduleSave()
        {
            if (_isRestoring)
            {
                return;
            }

            CancellationTokenSource next = new();
            CancellationTokenSource previous = Interlocked.Exchange(ref _saveDebounceCancellation, next);
            previous?.Cancel();
            previous?.Dispose();
            _ = SaveAfterDelayAsync(next);
        }

        private async Task SaveAfterDelayAsync(CancellationTokenSource delayCancellation)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), delayCancellation.Token);
                await SaveTasksAsync();
            }
            catch (OperationCanceledException) when (delayCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                Interlocked.CompareExchange(ref _saveDebounceCancellation, null, delayCancellation);
                delayCancellation.Dispose();
            }
        }

        private async Task SaveTasksAsync()
        {
            if (_isRestoring)
            {
                return;
            }

            await _persistenceSemaphore.WaitAsync();
            try
            {
                PersistentTaskListDTO snapshot = CreateSnapshot();
                await _taskStore.SaveAsync(snapshot);
            }
            catch (Exception ex)
            {
                App.LogError("Unable to persist task list", this, ex);
            }
            finally
            {
                _persistenceSemaphore.Release();
            }
        }

        private PersistentTaskListDTO CreateSnapshot()
        {
            PersistentTaskListDTO result = new();
            result.Tasks.AddRange(_unresolvedTasks);
            result.Tasks.AddRange(DownloadTasks.Where(task => task.CanPersist).Select(task => new PersistentTaskDTO
            {
                Kind = PersistentTaskKind.Download,
                Drive = CreateDriveReference(task.PersistentDrive),
                ItemId = task.PersistentItemId,
                Name = task.Name,
                Path = task.PersistentTargetPath,
                IsFolder = task.PersistentTargetIsFolder,
                Completed = task.Completed,
                IsPaused = task.IsPaused,
                Progress = task.Progress,
                TransferredBytes = task.DownloadedBytes,
                TotalBytes = task.TotalBytes,
            }));
            result.Tasks.AddRange(UploadTasks.Select(task => new PersistentTaskDTO
            {
                Kind = PersistentTaskKind.Upload,
                Drive = CreateDriveReference(task.Drive),
                ItemId = task.PersistentItemId,
                Name = task.Name,
                Path = task.PersistentItemPath,
                IsFolder = task.IsFolder,
                Completed = task.Completed,
                IsPaused = task.IsPaused,
                Progress = task.ProgressValue,
                TransferredBytes = (long)Math.Min(task.UploadedBytes, long.MaxValue),
                TotalBytes = (long)Math.Min(task.TotalBytes, long.MaxValue),
                ResumeToken = task.ResumeToken,
            }));
            result.Tasks.AddRange(MigrationTasks.Select(task => new PersistentTaskDTO
            {
                Kind = PersistentTaskKind.Migration,
                SourceDrive = CreateDriveReference(task.SourceDrive),
                TargetDrive = CreateDriveReference(task.TargetDrive),
                ItemId = task.SourceItem.Id,
                Name = task.Name,
                IsFolder = task.SourceItem.IsFolder,
                Completed = task.Completed,
                Progress = task.ProgressValue,
                TransferredBytes = task.MigratedBytes,
                TotalBytes = task.TotalBytes > 0 ? task.TotalBytes : task.SourceItem.Size ?? 0,
                TargetParentId = task.TargetParentId,
                TargetPathText = task.TargetPathText,
                Status = task.Status.ToString(),
                ErrorMessage = task.ErrorMessage,
            }));
            return result;
        }

        private static DriveReferenceDTO CreateDriveReference(DriveViewModel drive) => new()
        {
            ProviderType = drive.Provider.ProviderType,
            AccountId = drive.Provider.AccountId,
            DriveId = drive.Provider.DriveId,
            DisplayName = drive.DisplayName,
        };

        private readonly PersistentTaskStore _taskStore = new(Path.Combine(
            Directory.GetCurrentDirectory(),
            "cache",
            "tasks.json"));
        private readonly SemaphoreSlim _persistenceSemaphore = new(1, 1);
        private readonly List<PersistentTaskDTO> _unresolvedTasks = [];
        private CancellationTokenSource _saveDebounceCancellation;
        private bool _tasksLoaded;
        private bool _isRestoring;
    }
}
