using SimpleList.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Core.Contracts;

public interface IOfflineDownloadProvider
{
    Task<StorageResult<OfflineDownloadTask>> CreateOfflineDownloadAsync(
        string sourceUrl,
        string parentId,
        string fileName = null,
        CancellationToken ct = default);
}
