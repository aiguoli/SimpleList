namespace SimpleList.Models;

public class FolderUploadProgressInfo
{
    public string FilePath { get; set; }
    public ulong UploadedBytes { get; set; }
    public ulong TotalBytes { get; set; }
    public bool Completed { get; set; }
}
