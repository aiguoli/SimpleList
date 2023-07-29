using CommunityToolkit.Authentication;
using CommunityToolkit.Graph.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI;
using Downloader;
using Microsoft.Graph;
using Microsoft.UI.Dispatching;
using SimpleList.Services;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels
{
    public class DownloadTaskViewModel : ObservableObject
    {
        public DownloadTaskViewModel(string itemId, StorageFile file)
        {
            _itemId = itemId;
            _file = file;
            Drive = Ioc.Default.GetService<OneDrive>();
        }

        public async Task StartDownload()
        {
            IProvider provider = ProviderManager.Instance.GlobalProvider;
            GraphServiceClient graphClient = provider.GetClient();
            DriveItem item = await graphClient.Me.Drive.Items[_itemId].Request().GetAsync();
            string downloadUrl = item.AdditionalData["@microsoft.graph.downloadUrl"].ToString();

            // https://github.com/microsoft/microsoft-ui-xaml/issues/6952
            // BackgroundDownloader is not supported yet in WinUI3. Fuck WinUI3!
            // BackgroundDownloader downloader = new();
            // _downloadOperation = downloader.CreateDownload(new Uri(downloadUrl), _file);
            // await _downloadOperation.StartAsync();
            
            StartTime = DateTime.Now;
            _downloader = new();
            _downloader.DownloadFileCompleted += (sender, e) => Completed = true;
            _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
            await _downloader.DownloadFileTaskAsync(downloadUrl, _file.Path);
        }

        private void Downloader_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _dispatcher.TryEnqueue(() => Progress = e.ProgressPercentage);
        }

        public void PauseDownload()
        {
            _downloader.Pause();
            _pack = _downloader.Package;
        }

        public async Task ResumeDownload()
        {
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

        public static readonly int chunkSize = 1024 * 1024;  // 1MB chunks
        private readonly string _itemId;
        private readonly StorageFile _file;
        private OneDrive Drive { get; }
        // private DownloadOperation _downloadOperation;
        private DownloadService _downloader;
        private DownloadPackage _pack;
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        private double _progress;

        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }
        public bool Completed { get; private set; } = false;
        public string Name { get => _file.Name; }
        public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    }
}
