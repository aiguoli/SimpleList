using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Downloader;
using Microsoft.Graph.Models;
using Microsoft.UI.Dispatching;
using SimpleList.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
    public partial class DownloadTaskViewModel : ObservableObject
    {
        public DownloadTaskViewModel(string itemId, StorageFile file)
        {
            _itemId = itemId;
            _file = file;
            Drive = Ioc.Default.GetService<OneDrive>();
        }

        public async Task StartDownload()
        {
            DriveItem item = await Drive.GetItem(_itemId);
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
            // If double data type is used here, it will cause the progress control to handle additional data, resulting in the page being stuck.
            _dispatcher.TryEnqueue(() => Progress = (int)e.ProgressPercentage);
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
                DriveItem item = await Drive.GetItem(_itemId);
                string downloadUrl = item.AdditionalData["@microsoft.graph.downloadUrl"].ToString();
                _pack.Address = downloadUrl;
            }
            if (_pack != null)
            {
                await _downloader.DownloadFileTaskAsync(_pack);
            } else
            {
                _downloader.Resume();
            }
        }

        [RelayCommand]
        public void CancelTask()
        {
            if (!Completed)
            {
                _downloader.CancelAsync();
            }
            _manager.RemoveSelectedDownloadTasks(this);
        }

        public static readonly int chunkSize = 1024 * 1024;  // 1MB chunks
        private readonly string _itemId;
        private readonly StorageFile _file;
        private OneDrive Drive { get; }
        private readonly TaskManagerViewModel _manager = Ioc.Default.GetService<TaskManagerViewModel>();
        private DownloadService _downloader;
        private DownloadPackage _pack;
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        [ObservableProperty] private int _progress;
        [ObservableProperty] private bool _completed = false;
        [ObservableProperty] private bool _isDownloading = true;
        [ObservableProperty] private bool _isPaused = false;

        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }
        public string Name { get => _file.Name; }
    }
}
