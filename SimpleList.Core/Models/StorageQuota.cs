namespace SimpleList.Core.Models;

public class StorageQuota
{
    public long? Used { get; set; }
    public long? Total { get; set; }
    public long? Remaining { get; set; }
    public long? Deleted { get; set; }
}
