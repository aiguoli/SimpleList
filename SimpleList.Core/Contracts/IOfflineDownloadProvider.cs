using SimpleList.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Core.Contracts;

public interface IOfflineDownloadProvider
{
    Task<StorageResult<OfflineDownloadTask>> CreateOfflineDownloadAsync(
        string sourceUrl,
        CancellationToken ct = default);
}
