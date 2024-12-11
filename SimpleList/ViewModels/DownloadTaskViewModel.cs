using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using Microsoft.Graph.Models;
using Microsoft.UI.Dispatching;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
    public partial class DownloadTaskViewModel : ObservableObject
    {
        public DownloadTaskViewModel(DriveViewModel drive, string itemId, StorageFile file)
        {
            _itemId = itemId;
            _file = file;
            Drive = drive;
        }

        public async Task StartDownload()
        {
            DriveItem item = await Drive.Provider.GetItem(_itemId);
            string downloadUrl = item.AdditionalData["@microsoft.graph.downloadUrl"].ToString();

            StartTime = DateTime.Now;
            _downloader = new();
            _downloader.DownloadFileCompleted += DownloadFileCompleted;
            _downloader.DownloadProgressChanged += DownloadProgressChanged;
            await _downloader.DownloadFileTaskAsync(downloadUrl, _file.Path);
        }

        private void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            _dispatcher.TryEnqueue(() =>
            {
                if (_downloader.Status == DownloadStatus.Completed)
                {
                    Completed = true;
                    IsDownloading = false;
                }
            });
        }

        private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (DateTime.Now - _lastUpdate >= _updateInterval)
            {
                _lastUpdate = DateTime.Now;
                // If double data type is used here, it will cause the progress control to handle additional data, resulting in the page being stuck.
                _dispatcher.TryEnqueue(() =>
                {
                    Progress = (int)e.ProgressPercentage;
                    DownloadedBytes = e.ReceivedBytesSize;
                    TotalBytes = e.TotalBytesToReceive;
                    DownloadSpeed = (long)e.BytesPerSecondSpeed;
                });
            }
        }

        [RelayCommand]
        public void PauseDownload()
        {
            _downloader.Pause();
            _pack = _downloader.Package;
            IsPaused = true;
            IsDownloading = false;
        }

        [RelayCommand]
        public async Task ResumeDownload()
        {
            IsPaused = false;
            IsDownloading = true;

            // If the download has been paused for more than an hour, refresh the download URL
            if ((DateTime.Now - StartTime).TotalHours >= 1)
            {
                // Refresh the download URL
                DriveItem item = await Drive.Provider.GetItem(_itemId);
                string downloadUrl = item.AdditionalData["@microsoft.graph.downloadUrl"].ToString();
                if (_pack != null)
                {
                    await _downloader.DownloadFileTaskAsync(_pack, downloadUrl);
                }
            }
            else
            {
                _downloader.Resume();
            }
        }

        [RelayCommand]
        public async Task CancelTaskAsync()
        {
            if (!Completed)
            {
                _downloader.CancelAsync();
                await _file.DeleteAsync();
            }
            _manager.RemoveSelectedDownloadTasks(this);
        }

        [RelayCommand]
        public void OpenFolder()
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_file.Path}\"");
        }

        public static readonly int chunkSize = 1024 * 1024;  // 1MB chunks
        // The download speed is updated every second
        private DateTime _lastUpdate;
        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(1000);
        private readonly string _itemId;
        private readonly StorageFile _file;
        private DriveViewModel Drive { get; }
        private readonly TaskManagerViewModel _manager = App.GetService<TaskManagerViewModel>();
        private DownloadService _downloader;
        private DownloadPackage _pack;
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        [ObservableProperty] private int _progress;
        [ObservableProperty] private bool _completed = false;
        [ObservableProperty] private bool _isDownloading = true;
        [ObservableProperty] private bool _isPaused = false;
        [ObservableProperty] private long _downloadedBytes = 0;
        [ObservableProperty] private long _totalBytes = 0;
        [ObservableProperty] private long _downloadSpeed = 0;

        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }
        public string Name { get => _file.Name; }
    }
}
