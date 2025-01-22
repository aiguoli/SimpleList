using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Downloader;
using Microsoft.Graph.Models;
using Microsoft.UI.Dispatching;
using SimpleList.Helpers;
using SimpleList.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using WinUICommunity;

namespace SimpleList.ViewModels
{
    public partial class DownloadTaskViewModel : ObservableObject
    {
        public DownloadTaskViewModel(DriveViewModel drive, string itemId, IStorageItem target, EventHandler<AsyncCompletedEventArgs> onCompleted = null)
        {
            _itemId = itemId;
            _target = target;
            Drive = drive;
            _onCompleted = onCompleted;
        }

        private DownloadService CreateDownloadService(string itemId)
        {
            var downloadOpt = new DownloadConfiguration()
            {
                ChunkCount = 8,
                ParallelDownload = true
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

        public async Task StartDownload()
        {
            DriveItem item = await Drive.Provider.GetItem(_itemId);
            Name = item.Name;
            TotalBytes = item.Size ?? 0;
            StartTime = DateTime.Now;
            await WalkDownloadItem(item, _target);
            Growl.Info(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartDownload"),
                Message = string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartDownloadDesc"), DownloadList.Count),
                IsClosable = true,
                ShowDateTime = true,
                Token = "DriveGrowl"
            });
            foreach (DownloadItem downloadItem in DownloadList)
            {
                await downloadItem.DownloadService.DownloadFileTaskAsync(downloadItem.DownloadUrl, downloadItem.Path);
            }
        }

        private async Task WalkDownloadItem(DriveItem item, IStorageItem target)
        {
            // TODO: Add support for limiting count of parallel downloads
            if (item.File != null)
            {
                // item is a file
                string path = target.IsOfType(StorageItemTypes.Folder) ? Path.Combine(target.Path, item.Name) : target.Path;
                string downloadUrl = item.AdditionalData["@microsoft.graph.downloadUrl"].ToString();
                DownloadItem downloadItem = new()
                {
                    ItemId = item.Id,
                    DownloadUrl = downloadUrl,
                    Path = path,
                    Size = item.Size ?? 0,
                    DownloadService = CreateDownloadService(item.Id)
                };
                DownloadList.Add(downloadItem);
            }

            if (item.Folder != null)
            {
                // item is a folder
                if (target.IsOfType(StorageItemTypes.File))
                {
                    return;
                }
                if (target.IsOfType(StorageItemTypes.Folder))
                {
                    StorageFolder targetFolder = await (target as StorageFolder).CreateFolderAsync(item.Name, CreationCollisionOption.OpenIfExists);
                    var children = await Drive.Provider.GetFiles(item.Id);
                    foreach (DriveItem child in children.Value)
                    {
                        await WalkDownloadItem(child, targetFolder);
                    }
                }
            }
        }

        private EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted(string itemId)
        {
            return (object sender, AsyncCompletedEventArgs e) =>
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if ((sender as DownloadService).Status == DownloadStatus.Completed)
                    {
                        DownloadItem downloadItem = DownloadList.Find(i => i.ItemId == itemId);
                        if (downloadItem == null) return;
                        downloadItem.ReceivedBytes = downloadItem.Size;
                        UpdateProgress();
                        if (DownloadList.All(i => i.ReceivedBytes == i.Size))
                        {
                            Completed = true;
                            IsDownloading = false;
                            FinishTime = DateTime.Now;
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
                    // If double data type is used here, it will cause the progress control to handle additional data, resulting in the page being stuck.
                    _dispatcher.TryEnqueue(() =>
                    {
                        DownloadItem downloadItem = DownloadList.Find(i => i.ItemId == itemId);
                        if (downloadItem == null) return;
                        downloadItem.ReceivedBytes = e.ReceivedBytesSize;

                        UpdateProgress();
                    });
                }
            };
        }

        private void UpdateProgress()
        {
            _lastUpdate = DateTime.Now;
            DownloadedBytes = DownloadList.Sum(i => i.ReceivedBytes);
            Progress = (int)((double)DownloadedBytes / TotalBytes * 100);
            DownloadSpeed = (long)(DownloadedBytes / (DateTime.Now - StartTime).TotalSeconds);
        }

        [RelayCommand]
        public void PauseDownload()
        {
            foreach (DownloadItem item in DownloadList)
            {
                item.DownloadService.Pause();
                item.Package = item.DownloadService.Package;
            }
            IsPaused = true;
            IsDownloading = false;
        }

        [RelayCommand]
        public async Task ResumeDownload()
        {
            IsPaused = false;
            IsDownloading = true;

            foreach (DownloadItem downloadItem in DownloadList)
            {
                if ((DateTime.Now - StartTime).TotalHours >= 1)
                {
                    // Refresh the download URL
                    DriveItem item = await Drive.Provider.GetItem(downloadItem.ItemId);
                    string downloadUrl = item.AdditionalData["@microsoft.graph.downloadUrl"].ToString();
                    DownloadPackage package = downloadItem.DownloadService.Package;
                    if (package != null)
                    {
                        await downloadItem.DownloadService.DownloadFileTaskAsync(package, downloadUrl);
                    }
                }
                else
                {
                    downloadItem.DownloadService.Resume();
                }
            }
        }

        [RelayCommand]
        public void CancelTask()
        {
            if (!Completed)
            {
                foreach (DownloadItem item in DownloadList)
                {
                    item.DownloadService.CancelAsync();
                    File.Delete(item.Path);
                }
            }
            _manager.RemoveSelectedDownloadTasks(this);
        }

        [RelayCommand]
        public void OpenFolder()
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_target.Path}\"");
        }

        public static readonly int chunkSize = 1024 * 1024;  // 1MB chunks
        // The download speed is updated every second
        private DateTime _lastUpdate;
        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(1000);
        private readonly string _itemId;
        private readonly List<DownloadItem> DownloadList = [];
        private readonly IStorageItem _target;
        private DriveViewModel Drive { get; }
        private readonly TaskManagerViewModel _manager = App.GetService<TaskManagerViewModel>();
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        private EventHandler<AsyncCompletedEventArgs> _onCompleted;
        [ObservableProperty] private int _progress;
        [ObservableProperty] private bool _completed = false;
        [ObservableProperty] private bool _isDownloading = true;
        [ObservableProperty] private bool _isPaused = false;
        [ObservableProperty] private long _downloadedBytes = 0;
        [ObservableProperty] private long _totalBytes = 0;
        [ObservableProperty] private long _downloadSpeed = 0;

        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }
        public string Name { get; private set; }
    }
}
