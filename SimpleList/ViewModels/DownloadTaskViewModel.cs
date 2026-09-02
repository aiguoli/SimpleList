using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using Microsoft.UI.Dispatching;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using SimpleList.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class DownloadTaskViewModel : ObservableObject
{
    private const string PartialDownloadExtension = ".download";
    private static readonly TimeSpan ProgressStaleThreshold = TimeSpan.FromSeconds(3);

    public DownloadTaskViewModel(DriveViewModel drive, string itemId, IStorageItem target, EventHandler<AsyncCompletedEventArgs> onCompleted = null)
    {
        _itemId = itemId;
        _target = target;
        Drive = drive;
        _onCompleted = onCompleted;
    }

    public DownloadTaskViewModel(DriveViewModel drive, string itemId, Stream destination, EventHandler<AsyncCompletedEventArgs> onCompleted = null)
    {
        _itemId = itemId;
        _streamTarget = destination;
        Drive = drive;
        _onCompleted = onCompleted;
    }

    private DownloadService CreateDownloadService(string itemId)
    {
        var downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8,
            ParallelDownload = true,
            DownloadFileExtension = PartialDownloadExtension
        };
        DownloadService downloader = new(downloadOpt);
        downloader.DownloadFileCompleted += DownloadFileCompleted(itemId);
        downloader.DownloadProgressChanged += DownloadProgressChanged(itemId);
        if (_onCompleted != null)
        {
            downloader.DownloadFileCompleted += _onCompleted;
        }
        return downloader;
    }

    public async Task StartDownload(bool notify = true)
    {
        StorageResult<FileItem> result = await Drive.Provider.GetItemAsync(_itemId);
        if (result.IsSuccess && !_isCancellationRequested)
        {
            FileItem item = result.Data;
            Name = item.Name;
            OnPropertyChanged(nameof(Name));
            TotalBytes = item.Size ?? 0;
            SetDownloadRootPath(item);
            await WalkDownloadItem(item, _target);
            if (_isCancellationRequested)
            {
                return;
            }
            TotalBytes = DownloadList.Sum(i => i.Size);
            ResetSpeedTracking();
            if (notify)
            {
                Growl.Info(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartDownload"),
                    Message = string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartDownloadDesc"), DownloadList.Count),
                    IsClosable = true,
                    ShowDateTime = true,
                    Token = "DriveGrowl"
                });
            }
            foreach (DownloadItem downloadItem in DownloadList)
            {
                if (_isCancellationRequested)
                {
                    break;
                }

                downloadItem.Started = true;
                Task downloadTask;
                if (Drive.Provider.ProviderType == ProviderType.GoogleDrive || Drive.Provider.ProviderType == ProviderType.PikPak)
                {
                    downloadTask = DownloadWithProviderAsync(downloadItem);
                }
                else if (Drive.Provider.ProviderType == ProviderType.Local)
                {
                    downloadTask = CopyLocalItemAsync(downloadItem);
                }
                else
                {
                    downloadTask = downloadItem.DownloadService.DownloadFileTaskAsync(downloadItem.DownloadUrl, downloadItem.Path);
                }

                await TrackDownloadOperationAsync(downloadTask);
                if (!_isCancellationRequested && downloadItem.DownloadService != null && !downloadItem.Completed)
                {
                    MarkCompletedDownloaderItem(downloadItem);
                }
            }
        }
    }

    public async Task StartStreamDownload(bool notify = true)
    {
        StorageResult<FileItem> result = await Drive.Provider.GetItemAsync(_itemId);
        if (!result.IsSuccess)
        {
            ShowDownloadError(result.ErrorMessage);
            return;
        }

        FileItem item = result.Data;
        if (item.IsFolder)
        {
            ShowDownloadError(Helpers.ResourceHelper.GetLocalized("DragCloudFoldersNotSupported"));
            return;
        }

        Name = item.Name;
        OnPropertyChanged(nameof(Name));
        TotalBytes = item.Size ?? 0;
        ResetSpeedTracking();
        _downloadCts = new CancellationTokenSource();

        DownloadItem downloadItem = new()
        {
            ItemId = item.Id,
            Path = item.Name,
            Size = item.Size ?? 0
        };
        DownloadList.Add(downloadItem);

        IProgress<long> progress = new System.Progress<long>(bytes =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                downloadItem.ReceivedBytes = bytes;
                UpdateProgress();
            });
        });

        Task<StorageResult<bool>> providerDownloadTask = Drive.Provider.DownloadFileAsync(item.Id, _streamTarget, progress, _downloadCts.Token);
        StorageResult<bool> downloadResult;
        try
        {
            downloadResult = await TrackDownloadOperationAsync(providerDownloadTask);
        }
        catch (OperationCanceledException) when (_isCancellationRequested)
        {
            return;
        }
        if (!downloadResult.IsSuccess)
        {
            IsDownloading = false;
            ClearEstimatedTimeRemaining();
            if (!_isCancellationRequested)
            {
                ShowDownloadError(downloadResult.ErrorMessage);
            }
            return;
        }

        if (_isCancellationRequested)
        {
            return;
        }

        await _streamTarget.FlushAsync();
        long receivedBytes = TotalBytes > 0 ? TotalBytes : downloadItem.ReceivedBytes;
        downloadItem.ReceivedBytes = receivedBytes;
        downloadItem.Completed = true;
        DownloadedBytes = receivedBytes;
        Progress = 100;
        Completed = true;
        IsDownloading = false;
        FinishTime = DateTime.Now;
        ClearEstimatedTimeRemaining();
        _onCompleted?.Invoke(this, new AsyncCompletedEventArgs(null, false, null));
    }

    private async Task<string> ResolveDownloadUrl(FileItem item)
    {
        if (item.ProviderTokens != null && item.ProviderTokens.TryGetValue("downloadUrl", out var url) && !string.IsNullOrEmpty(url))
        {
            return url;
        }
        var result = await Drive.Provider.GetDownloadUrlAsync(item.Id);
        return result.IsSuccess ? result.Data : null;
    }

    private async Task WalkDownloadItem(FileItem item, IStorageItem target)
    {
        if (_isCancellationRequested)
        {
            return;
        }

        if (!item.IsFolder)
        {
            string path = target.IsOfType(StorageItemTypes.Folder) ? Path.Combine(target.Path, item.Name) : target.Path;
            string downloadUrl = Drive.Provider.ProviderType == ProviderType.GoogleDrive || Drive.Provider.ProviderType == ProviderType.PikPak ? null : await ResolveDownloadUrl(item);
            DownloadItem downloadItem = new()
            {
                ItemId = item.Id,
                DownloadUrl = downloadUrl,
                Path = path,
                Size = item.Size ?? 0,
                DownloadService = Drive.Provider.ProviderType == ProviderType.GoogleDrive || Drive.Provider.ProviderType == ProviderType.Local || Drive.Provider.ProviderType == ProviderType.PikPak ? null : CreateDownloadService(item.Id)
            };
            DownloadList.Add(downloadItem);
        }
        else
        {
            if (target.IsOfType(StorageItemTypes.File))
            {
                return;
            }
            if (target.IsOfType(StorageItemTypes.Folder))
            {
                StorageFolder targetFolder = await (target as StorageFolder).CreateFolderAsync(item.Name, CreationCollisionOption.OpenIfExists);
                StorageResult<PageResult<FileItem>> result = await Drive.Provider.ListAllChildrenAsync(item.Id);
                if (result.IsSuccess)
                {
                    foreach (FileItem child in result.Data.Items)
                    {
                        if (_isCancellationRequested)
                        {
                            break;
                        }
                        await WalkDownloadItem(child, targetFolder);
                    }
                }
            }
        }
    }

    private async Task CopyLocalItemAsync(DownloadItem downloadItem)
    {
        string sourcePath = downloadItem.ItemId;
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(downloadItem.Path), StringComparison.OrdinalIgnoreCase))
        {
            downloadItem.ReceivedBytes = downloadItem.Size;
            downloadItem.Completed = true;
            Completed = true;
            IsDownloading = false;
            if (TotalBytes <= 0) Progress = 100;
            FinishTime = DateTime.Now;
            ClearEstimatedTimeRemaining();
            return;
        }
        if (File.Exists(sourcePath))
        {
            string destDir = Path.GetDirectoryName(downloadItem.Path);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            File.Copy(sourcePath, downloadItem.Path, true);
        }
        else if (Directory.Exists(sourcePath))
        {
            CopyDirectory(sourcePath, downloadItem.Path);
        }
        downloadItem.ReceivedBytes = downloadItem.Size;
        downloadItem.Completed = true;
        UpdateProgress();
        if (DownloadList.All(i => i.Completed))
        {
            Completed = true;
            IsDownloading = false;
            if (TotalBytes <= 0) Progress = 100;
            FinishTime = DateTime.Now;
            ClearEstimatedTimeRemaining();
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private async Task DownloadWithProviderAsync(DownloadItem downloadItem)
    {
        _downloadCts = new CancellationTokenSource();
        IProgress<long> progress = new System.Progress<long>(bytes =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                downloadItem.ReceivedBytes = bytes;
                UpdateProgress();
            });
        });

        await using FileStream destination = new(downloadItem.Path, FileMode.Create, FileAccess.Write, FileShare.Read);
        StorageResult<bool> result = await Drive.Provider.DownloadFileAsync(downloadItem.ItemId, destination, progress, _downloadCts.Token);
        if (!result.IsSuccess)
        {
            IsDownloading = false;
            ClearEstimatedTimeRemaining();
            if (!_isCancellationRequested)
            {
                Growl.Error(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("Error"),
                    Message = result.ErrorMessage,
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            }
            return;
        }

        if (_isCancellationRequested)
        {
            return;
        }

        long actualSize = File.Exists(downloadItem.Path) ? new FileInfo(downloadItem.Path).Length : 0;
        downloadItem.ReceivedBytes = downloadItem.Size > 0 ? Math.Min(actualSize, downloadItem.Size) : actualSize;
        if (downloadItem.Size > 0 && actualSize < downloadItem.Size)
        {
            IsDownloading = false;
            UpdateProgress();
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = string.Format(Helpers.ResourceHelper.GetLocalized("DownloadIncompleteFormat"), downloadItem.Path),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
            return;
        }

        downloadItem.Completed = true;
        UpdateProgress();
        if (DownloadList.All(i => i.Completed))
        {
            Completed = true;
            IsDownloading = false;
            if (TotalBytes <= 0) Progress = 100;
            FinishTime = DateTime.Now;
            ClearEstimatedTimeRemaining();
        }
    }

    private void MarkCompletedDownloaderItem(DownloadItem downloadItem)
    {
        long actualSize = File.Exists(downloadItem.Path) ? new FileInfo(downloadItem.Path).Length : 0;
        if (downloadItem.Size > 0 && actualSize < downloadItem.Size)
        {
            return;
        }

        downloadItem.ReceivedBytes = downloadItem.Size > 0 ? downloadItem.Size : actualSize;
        downloadItem.Completed = true;
        UpdateProgress();
        if (DownloadList.All(i => i.Completed))
        {
            Completed = true;
            IsDownloading = false;
            if (TotalBytes <= 0) Progress = 100;
            FinishTime = DateTime.Now;
            ClearEstimatedTimeRemaining();
        }
    }

    private EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted(string itemId)
    {
        return (object sender, AsyncCompletedEventArgs e) =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                DownloadItem downloadItem = DownloadList.Find(i => i.ItemId == itemId);
                if (downloadItem == null) return;

                if (e.Cancelled || e.Error != null)
                {
                    IsDownloading = false;
                    ClearEstimatedTimeRemaining();
                    if (!_isCancellationRequested)
                    {
                        Growl.Error(new GrowlInfo
                        {
                            Title = Helpers.ResourceHelper.GetLocalized("Error"),
                            Message = e.Error?.Message ?? Helpers.ResourceHelper.GetLocalized("DownloadCancelled"),
                            StaysOpen = false,
                            Token = "DriveGrowl"
                        });
                    }
                    return;
                }

                if ((sender as DownloadService).Status == DownloadStatus.Completed)
                {
                    long actualSize = File.Exists(downloadItem.Path) ? new FileInfo(downloadItem.Path).Length : 0;
                    if (downloadItem.Size > 0 && actualSize < downloadItem.Size)
                    {
                        downloadItem.ReceivedBytes = actualSize;
                        IsDownloading = false;
                        UpdateProgress();
                        Growl.Error(new GrowlInfo
                        {
                            Title = Helpers.ResourceHelper.GetLocalized("Error"),
                            Message = string.Format(Helpers.ResourceHelper.GetLocalized("DownloadIncompleteFormat"), downloadItem.Path),
                            StaysOpen = false,
                            Token = "DriveGrowl"
                        });
                        return;
                    }

                    downloadItem.ReceivedBytes = downloadItem.Size > 0 ? downloadItem.Size : actualSize;
                    downloadItem.Completed = true;
                    UpdateProgress();
                    if (DownloadList.All(i => i.Completed))
                    {
                        Completed = true;
                        IsDownloading = false;
                        if (TotalBytes <= 0) Progress = 100;
                        FinishTime = DateTime.Now;
                        ClearEstimatedTimeRemaining();
                    }
                }
            });
        };
    }

    private EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged(string itemId)
    {
        return (object sender, DownloadProgressChangedEventArgs e) =>
        {
            if (DateTime.Now - _lastUpdate >= _updateInterval)
            {
                _lastUpdate = DateTime.Now;
                _dispatcher.TryEnqueue(() =>
                {
                    DownloadItem downloadItem = DownloadList.Find(i => i.ItemId == itemId);
                    if (downloadItem == null) return;
                    if (e.TotalBytesToReceive > 0)
                    {
                        downloadItem.Size = e.TotalBytesToReceive;
                    }
                    downloadItem.ReceivedBytes = e.ReceivedBytesSize;

                    double reportedSpeed = e.BytesPerSecondSpeed > 0
                        ? e.BytesPerSecondSpeed
                        : e.AverageBytesPerSecondSpeed;
                    long? speed = double.IsFinite(reportedSpeed) && reportedSpeed > 0
                        ? (long)Math.Min(reportedSpeed, long.MaxValue)
                        : null;
                    UpdateProgress(speed);
                });
            }
        };
    }

    private void UpdateProgress(long? reportedSpeed = null)
    {
        DateTime now = DateTime.Now;
        _lastUpdate = now;
        DownloadedBytes = DownloadList.Sum(i => i.ReceivedBytes);
        bool madeProgress = DownloadedBytes > _lastProgressBytes;
        if (madeProgress)
        {
            _lastProgressTime = now;
            _lastProgressBytes = DownloadedBytes;
        }
        long knownTotalBytes = DownloadList.Sum(i => i.Size);
        if (knownTotalBytes > 0)
        {
            TotalBytes = knownTotalBytes;
        }
        Progress = TotalBytes > 0
            ? Math.Clamp((int)((double)DownloadedBytes / TotalBytes * 100), 0, 100)
            : 0;

        if (madeProgress && reportedSpeed > 0)
        {
            ApplySpeedSample(reportedSpeed.Value);
            _lastSpeedSampleTime = now;
            _lastSpeedSampleBytes = DownloadedBytes;
        }
        else
        {
            double sampleSeconds = (now - _lastSpeedSampleTime).TotalSeconds;
            if (sampleSeconds >= 0.5)
            {
                long sampledBytes = Math.Max(0, DownloadedBytes - _lastSpeedSampleBytes);
                if (sampledBytes > 0)
                {
                    ApplySpeedSample(sampledBytes / sampleSeconds);
                }
                _lastSpeedSampleTime = now;
                _lastSpeedSampleBytes = DownloadedBytes;
            }
        }
        UpdateEstimatedTimeRemaining();
    }

    [RelayCommand]
    public void PauseDownload()
    {
        foreach (DownloadItem item in DownloadList)
        {
            if (item.DownloadService == null) continue;
            item.DownloadService.Pause();
            item.Package = item.DownloadService.Package;
        }
        IsPaused = true;
        IsDownloading = false;
        ClearEstimatedTimeRemaining();
    }

    [RelayCommand]
    public async Task ResumeDownload()
    {
        IsPaused = false;
        IsDownloading = true;
        ResetSpeedTracking(false);

        foreach (DownloadItem downloadItem in DownloadList)
        {
            if (downloadItem.DownloadService == null) continue;
            if ((DateTime.Now - StartTime).TotalHours >= 1)
            {
                var urlResult = await Drive.Provider.GetDownloadUrlAsync(downloadItem.ItemId);
                if (urlResult.IsSuccess)
                {
                    DownloadPackage package = downloadItem.DownloadService.Package;
                    if (package != null)
                    {
                        await downloadItem.DownloadService.DownloadFileTaskAsync(package, urlResult.Data);
                    }
                }
            }
            else
            {
                downloadItem.DownloadService.Resume();
            }
        }
    }

    [RelayCommand]
    public async Task CancelTask()
    {
        if (!Completed)
        {
            if (!_isCancellationRequested)
            {
                _isCancellationRequested = true;
                IsDownloading = false;
                IsPaused = false;
                ClearEstimatedTimeRemaining();
                _downloadCts?.Cancel();

                foreach (DownloadItem item in DownloadList.Where(item => item.Started && !item.Completed))
                {
                    if (item.DownloadService == null)
                    {
                        continue;
                    }

                    try
                    {
                        await item.DownloadService.CancelTaskAsync();
                    }
                    catch (Exception)
                    {
                        // The active operation is awaited below so its output stream can
                        // still be released before cleanup.
                    }
                }

                try
                {
                    await _activeDownloadOperation;
                }
                catch (Exception)
                {
                }
            }

            if (!await DeleteIncompleteFilesAsync())
            {
                return;
            }
        }
        _manager.RemoveSelectedDownloadTasks(this);
    }

    private async Task TrackDownloadOperationAsync(Task downloadTask)
    {
        _activeDownloadOperation = downloadTask;
        try
        {
            await downloadTask;
        }
        catch (OperationCanceledException) when (_isCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_activeDownloadOperation, downloadTask))
            {
                _activeDownloadOperation = Task.CompletedTask;
            }
        }
    }

    private async Task<T> TrackDownloadOperationAsync<T>(Task<T> downloadTask)
    {
        _activeDownloadOperation = downloadTask;
        try
        {
            return await downloadTask;
        }
        finally
        {
            if (ReferenceEquals(_activeDownloadOperation, downloadTask))
            {
                _activeDownloadOperation = Task.CompletedTask;
            }
        }
    }

    private async Task<bool> DeleteIncompleteFilesAsync()
    {
        if (_target == null)
        {
            return true;
        }

        List<string> failedPaths = [];
        foreach (DownloadItem item in DownloadList.Where(item => item.Started && !item.Completed))
        {
            foreach (string path in GetIncompleteFilePaths(item))
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        File.Delete(path);
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        await Task.Delay(150);
                    }
                    catch (UnauthorizedAccessException) when (attempt < 2)
                    {
                        await Task.Delay(150);
                    }
                    catch (IOException)
                    {
                        failedPaths.Add(path);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        failedPaths.Add(path);
                    }
                }
            }
        }

        if (failedPaths.Count == 0)
        {
            return true;
        }

        string messageFormat = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_DeleteIncompleteFileFailed");
        ShowDownloadError(string.Format(messageFormat, string.Join(Environment.NewLine, failedPaths)));
        return false;
    }

    private static IEnumerable<string> GetIncompleteFilePaths(DownloadItem item)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        AddExistingPath(paths, item.Path);
        AddExistingPath(paths, $"{item.Path}{PartialDownloadExtension}");

        string downloaderPath = item.DownloadService?.Package?.DownloadingFileName;
        if (!string.IsNullOrWhiteSpace(downloaderPath))
        {
            if (!Path.IsPathFullyQualified(downloaderPath))
            {
                downloaderPath = Path.Combine(Path.GetDirectoryName(item.Path) ?? string.Empty, downloaderPath);
            }
            AddExistingPath(paths, downloaderPath);
        }

        return paths;
    }

    private static void AddExistingPath(HashSet<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            paths.Add(path);
        }
    }

    private void UpdateEstimatedTimeRemaining()
    {
        if (!IsDownloading || Completed)
        {
            ClearEstimatedTimeRemaining();
            return;
        }

        TimeSpan? remaining = _etaEstimator.EstimateRemaining(TotalBytes, DownloadedBytes);
        if (remaining == null)
        {
            ClearEstimatedTimeRemaining();
            return;
        }

        string duration = FormatEstimatedDuration(remaining.Value);
        EstimatedTimeRemainingText = string.Format(
            Helpers.ResourceHelper.GetLocalized("TaskManagerPage_EstimatedTimeRemaining"),
            duration);
        HasEstimatedTimeRemaining = true;
    }

    private void ApplySpeedSample(double bytesPerSecond)
    {
        if (!_etaEstimator.AddSpeedSample(bytesPerSecond))
        {
            return;
        }

        DownloadSpeed = (long)Math.Min(Math.Round(_etaEstimator.SmoothedBytesPerSecond), long.MaxValue);
    }

    private static string FormatEstimatedDuration(TimeSpan duration)
    {
        long totalSeconds = Math.Max(1, (long)Math.Ceiling(duration.TotalSeconds));
        if (totalSeconds >= 3_600)
        {
            long totalMinutes = (long)Math.Ceiling(totalSeconds / 60d);
            long hours = totalMinutes / 60;
            long minutes = totalMinutes % 60;
            return minutes == 0
                ? string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_DurationHours"), hours)
                : string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_DurationHoursMinutes"), hours, minutes);
        }

        if (totalSeconds >= 60)
        {
            return string.Format(
                Helpers.ResourceHelper.GetLocalized("TaskManagerPage_DurationMinutesSeconds"),
                totalSeconds / 60,
                totalSeconds % 60);
        }

        return string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_DurationSeconds"), totalSeconds);
    }

    private void EstimatedTimeTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (!IsDownloading || Completed || IsPaused || _isCancellationRequested)
        {
            sender.Stop();
            ClearEstimatedTimeRemaining();
            return;
        }

        DateTime now = DateTime.Now;
        if (now - _lastProgressTime >= ProgressStaleThreshold)
        {
            _etaEstimator.Reset();
            DownloadSpeed = 0;
            _lastSpeedSampleTime = now;
            _lastSpeedSampleBytes = DownloadedBytes;
            ClearEstimatedTimeRemaining();
            return;
        }

        UpdateEstimatedTimeRemaining();
    }

    private void ClearEstimatedTimeRemaining()
    {
        EstimatedTimeRemainingText = string.Empty;
        HasEstimatedTimeRemaining = false;
    }

    [RelayCommand]
    public void OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(_downloadRootPath))
        {
            return;
        }

        string folderPath = Directory.Exists(_downloadRootPath)
            ? _downloadRootPath
            : _target?.Path;
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        System.Diagnostics.ProcessStartInfo startInfo = new("explorer.exe")
        {
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(folderPath);
        System.Diagnostics.Process.Start(startInfo);
    }

    private void SetDownloadRootPath(FileItem item)
    {
        if (_target == null)
        {
            return;
        }

        _downloadRootPath = _target.IsOfType(StorageItemTypes.Folder)
            ? (item.IsFolder ? Path.Combine(_target.Path, item.Name) : _target.Path)
            : Path.GetDirectoryName(_target.Path);
        OnPropertyChanged(nameof(CanOpenFolder));
    }

    private void ResetSpeedTracking(bool resetStartTime = true)
    {
        DateTime now = DateTime.Now;
        if (resetStartTime)
        {
            StartTime = now;
        }
        _lastSpeedSampleTime = now;
        _lastSpeedSampleBytes = DownloadedBytes;
        _lastProgressTime = now;
        _lastProgressBytes = DownloadedBytes;
        _etaEstimator.Reset();
        DownloadSpeed = 0;
        ClearEstimatedTimeRemaining();
        _estimatedTimeTimer ??= _dispatcher.CreateTimer();
        _estimatedTimeTimer.Interval = TimeSpan.FromSeconds(1);
        _estimatedTimeTimer.Tick -= EstimatedTimeTimer_Tick;
        _estimatedTimeTimer.Tick += EstimatedTimeTimer_Tick;
        _estimatedTimeTimer.Start();
    }

    private static void ShowDownloadError(string message)
    {
        Growl.Error(new GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("Error"),
            Message = message,
            StaysOpen = false,
            Token = "DriveGrowl"
        });
    }

    public static readonly int chunkSize = 1024 * 1024;
    private DateTime _lastUpdate;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(1000);
    private readonly string _itemId;
    private readonly List<DownloadItem> DownloadList = [];
    private readonly IStorageItem _target;
    private readonly Stream _streamTarget;
    private DriveViewModel Drive { get; }
    private readonly TaskManagerViewModel _manager = App.GetService<TaskManagerViewModel>();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly EventHandler<AsyncCompletedEventArgs> _onCompleted;
    private CancellationTokenSource _downloadCts;
    private Task _activeDownloadOperation = Task.CompletedTask;
    private bool _isCancellationRequested;
    private DateTime _lastSpeedSampleTime;
    private long _lastSpeedSampleBytes;
    private DateTime _lastProgressTime;
    private long _lastProgressBytes;
    private readonly DownloadEtaEstimator _etaEstimator = new();
    private DispatcherQueueTimer _estimatedTimeTimer;
    private string _downloadRootPath;
    [ObservableProperty]
    public partial int Progress { get; set; }
    [ObservableProperty]
    public partial bool Completed { get; set; } = false;
    [ObservableProperty]
    public partial bool IsDownloading { get; set; } = true;
    [ObservableProperty]
    public partial bool IsPaused { get; set; } = false;
    [ObservableProperty]
    public partial long DownloadedBytes { get; set; } = 0;
    [ObservableProperty]
    public partial long TotalBytes { get; set; } = 0;
    [ObservableProperty]
    public partial long DownloadSpeed { get; set; } = 0;
    [ObservableProperty]
    public partial string EstimatedTimeRemainingText { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool HasEstimatedTimeRemaining { get; set; }
    public DateTime StartTime { get; private set; }
    public DateTime FinishTime { get; private set; }
    public string Name { get; private set; }
    public bool CanOpenFolder => !string.IsNullOrWhiteSpace(_downloadRootPath);
}
