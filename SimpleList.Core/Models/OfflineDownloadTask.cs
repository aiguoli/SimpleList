namespace SimpleList.Core.Models;

public class OfflineDownloadTask
{
    public string Id { get; set; }
    public string FileId { get; set; }
    public string FileName { get; set; }
    public string Phase { get; set; }
    public string Message { get; set; }
    public long Progress { get; set; }
}
