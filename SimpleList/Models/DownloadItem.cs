using Downloader;

namespace SimpleList.Models
{
    public class DownloadItem
    {
        public string ItemId { get; set; }
        public string Path { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
        public long ReceivedBytes { get; set; } = 0;
        public bool Started { get; set; }
        public bool Completed { get; set; }
        public IDownloadService DownloadService { get; set; }
        public DownloadPackage Package { get; set; }
    }
}
