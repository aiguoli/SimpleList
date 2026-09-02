using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimpleList.Core.Models;
using Windows.Storage;

namespace SimpleList.Core.Contracts;

public interface IStorageProvider
{
    ProviderType ProviderType { get; }
    string AccountId { get; }
    string DriveId { get; }
    bool IsAuthenticated { get; }
    bool SupportsTrash { get; }
    ShareCapabilities ShareCapabilities => global::SimpleList.Core.Models.ShareCapabilities.Unsupported;

    Task<StorageResult<bool>> LoginAsync(CancellationToken ct = default);
    Task<StorageResult<string>> GetDisplayNameAsync(CancellationToken ct = default);
    Task<StorageResult<StorageQuota>> GetQuotaAsync(CancellationToken ct = default);

    Task<StorageResult<PageResult<FileItem>>> ListChildrenAsync(string parentId, string pageToken = null, CancellationToken ct = default);
    Task<StorageResult<FileItem>> GetItemAsync(string itemId, CancellationToken ct = default);
    Task<StorageResult<Stream>> GetItemContentAsync(string itemId, CancellationToken ct = default);
    Task<StorageResult<bool>> DownloadFileAsync(string itemId, Stream destination, IProgress<long> progress = null, CancellationToken ct = default);
    Task<StorageResult<ThumbnailSet>> GetThumbnailsAsync(string itemId, CancellationToken ct = default);
    Task<StorageResult<string>> GetDownloadUrlAsync(string itemId, CancellationToken ct = default);

    Task<StorageResult<FileItem>> CreateFolderAsync(string parentId, string name, string conflictBehavior = "rename", CancellationToken ct = default);
    Task<StorageResult<FileItem>> RenameAsync(string itemId, string newName, CancellationToken ct = default);
    Task<StorageResult<bool>> DeleteAsync(string itemId, CancellationToken ct = default);
    Task<StorageResult<bool>> PermanentDeleteAsync(string itemId, CancellationToken ct = default);
    Task<StorageResult<FileItem>> RestoreAsync(string itemId, CancellationToken ct = default);
    Task<StorageResult<PageResult<FileItem>>> ListTrashAsync(string pageToken = null, CancellationToken ct = default);
    Task<StorageResult<bool>> EmptyTrashAsync(CancellationToken ct = default);

    Task<StorageResult<FileItem>> UploadFileAsync(
        StorageFile file,
        string parentId,
        IProgress<long> progress = null,
        string resumeToken = null,
        Action<string> resumeTokenCallback = null,
        CancellationToken ct = default);

    Task<StorageResult<FileItem>> UploadFileContentAsync(
        Stream content,
        string fileName,
        string parentId,
        long? size = null,
        IProgress<long> progress = null,
        CancellationToken ct = default);

    Task<StorageResult<FileItem>> UploadFolderAsync(
        StorageFolder folder,
        string parentId,
        IProgress<long> overallProgress = null,
        IProgress<FolderUploadProgressInfo> detailProgress = null,
        CancellationToken ct = default);

    Task<StorageResult<ShareLink>> CreateLinkAsync(
        string itemId,
        DateTimeOffset? expiration = null,
        string password = null,
        string type = "view",
        CancellationToken ct = default);

    Task<StorageResult<ShareLink>> GetShareLinkAsync(string itemId, CancellationToken ct = default);

    Task<StorageResult<bool>> RevokeShareLinkAsync(string itemId, CancellationToken ct = default);

    Task<StorageResult<bool>> ConvertToPdfAsync(string itemId, StorageFile destination, CancellationToken ct = default);

    Task<StorageResult<PageResult<FileItem>>> SearchAsync(string query, string scopeParentId = null, CancellationToken ct = default);
}
