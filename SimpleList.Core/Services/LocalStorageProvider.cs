using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Core.Services;

public class LocalStorageProvider : StorageProviderBase, IStorageProvider
{
    public LocalStorageProvider(string rootPath, IStringLocalizer localizer = null)
        : base(localizer)
    {
        RootPath = NormalizeRootPath(rootPath);
        DriveId = RootPath;
        AccountId = RootPath;
    }

    public ProviderType ProviderType => ProviderType.Local;
    public string AccountId { get; private set; }
    public string DriveId { get; private set; }
    public bool IsAuthenticated => Directory.Exists(RootPath);
    public bool SupportsTrash => false;
    public ShareCapabilities ShareCapabilities => global::SimpleList.Core.Models.ShareCapabilities.Unsupported;
    public string RootPath { get; }

    public Task<StorageResult<bool>> LoginAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Directory.Exists(RootPath)
            ? StorageResult<bool>.Success(true)
            : StorageResult<bool>.Failure(LF("Local_FolderNotFoundFormat", "Local folder does not exist: {0}", RootPath), StorageErrorType.NotFound));
    }

    public Task<StorageResult<string>> GetDisplayNameAsync(CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<string>.Success(new DirectoryInfo(RootPath).Name));
    }

    public Task<StorageResult<StorageQuota>> GetQuotaAsync(CancellationToken ct = default)
    {
        DriveInfo drive = new(Path.GetPathRoot(RootPath));
        return Task.FromResult(StorageResult<StorageQuota>.Success(new StorageQuota
        {
            Total = drive.TotalSize,
            Remaining = drive.AvailableFreeSpace,
            Used = drive.TotalSize - drive.AvailableFreeSpace,
        }));
    }

    public Task<StorageResult<PageResult<FileItem>>> ListChildrenAsync(string parentId, string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string directoryPath = ResolveDirectoryPath(parentId);
            IEnumerable<FileItem> items = EnumerateDirectory(directoryPath);
            return Task.FromResult(new PageResult<FileItem>
            {
                Items = items.ToList(),
                NextPageToken = null,
            });
        }, () => ValidateParent(parentId));
    }

    public Task<StorageResult<FileItem>> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string path = ResolveItemPath(itemId);
            return Task.FromResult(ToFileItem(path));
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<Stream>> GetItemContentAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string path = ResolveItemPath(itemId);
            return (Stream)new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }, () => ValidateFile(itemId));
    }

    public Task<StorageResult<bool>> DownloadFileAsync(string itemId, Stream destination, IProgress<long> progress = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(destination, nameof(destination));
            string path = ResolveItemPath(itemId);
            await using FileStream source = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            await CopyToAsync(source, destination, progress, ct);
            return true;
        }, () => ValidateFile(itemId));
    }

    public Task<StorageResult<ThumbnailSet>> GetThumbnailsAsync(string itemId, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<ThumbnailSet>.Failure(L("Local_ThumbnailsUnavailable", "Thumbnails are not available for local items"), StorageErrorType.NotFound));
    }

    public Task<StorageResult<string>> GetDownloadUrlAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string path = ResolveItemPath(itemId);
            return Task.FromResult(path);
        }, () => ValidateFile(itemId));
    }

    public Task<StorageResult<FileItem>> CreateFolderAsync(string parentId, string name, string conflictBehavior = "rename", CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string parentPath = ResolveDirectoryPath(parentId);
            ValidateFileName(name, nameof(name));
            string folderPath = GetUniquePath(Path.Combine(parentPath, name), Directory.Exists, conflictBehavior);
            Directory.CreateDirectory(folderPath);
            return Task.FromResult(ToFileItem(folderPath));
        }, () => ValidateParent(parentId));
    }

    public Task<StorageResult<FileItem>> RenameAsync(string itemId, string newName, CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string path = ResolveItemPath(itemId);
            ValidateFileName(newName, nameof(newName));
            string parent = Path.GetDirectoryName(path);
            string targetPath = Path.Combine(parent ?? RootPath, newName);
            targetPath = GetUniquePath(targetPath, File.Exists, "rename", path);
            if (Directory.Exists(path))
            {
                Directory.Move(path, targetPath);
            }
            else
            {
                File.Move(path, targetPath);
            }
            return Task.FromResult(ToFileItem(targetPath));
        }, () => ValidateFile(itemId));
    }

    public Task<StorageResult<bool>> DeleteAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string path = ResolveItemPath(itemId);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }
            return Task.FromResult(true);
        }, () => ValidateFile(itemId));
    }

    public Task<StorageResult<bool>> PermanentDeleteAsync(string itemId, CancellationToken ct = default)
    {
        return DeleteAsync(itemId, ct);
    }

    public Task<StorageResult<FileItem>> RestoreAsync(string itemId, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<FileItem>.Failure(L("Local_RestoreUnsupported", "Local items do not support restore"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<PageResult<FileItem>>> ListTrashAsync(string pageToken = null, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<PageResult<FileItem>>.Failure(L("Local_TrashUnsupported", "Local items do not support trash"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<bool>> EmptyTrashAsync(CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<bool>.Failure(L("Local_TrashUnsupported", "Local items do not support trash"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<FileItem>> UploadFileAsync(StorageFile file, string parentId, IProgress<long> progress = null, string resumeToken = null, Action<string> resumeTokenCallback = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(file, nameof(file));
            string parentPath = ResolveDirectoryPath(parentId);
            string targetPath = GetUniquePath(Path.Combine(parentPath, file.Name), File.Exists, "rename");
            await using Stream source = await file.OpenStreamForReadAsync();
            await using FileStream destination = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await CopyToAsync(source, destination, progress, ct);
            return ToFileItem(targetPath);
        }, () => ValidateParent(parentId));
    }

    public Task<StorageResult<FileItem>> UploadFileContentAsync(Stream content, string fileName, string parentId, long? size = null, IProgress<long> progress = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(content, nameof(content));
            ValidateNotEmpty(fileName, nameof(fileName));
            string parentPath = ResolveDirectoryPath(parentId);
            string targetPath = GetUniquePath(Path.Combine(parentPath, fileName), File.Exists, "rename");
            await using FileStream destination = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await CopyToAsync(content, destination, progress, ct);
            return ToFileItem(targetPath);
        }, () => ValidateParent(parentId));
    }

    public async Task<StorageResult<FileItem>> UploadFolderAsync(StorageFolder folder, string parentId, IProgress<long> overallProgress = null, IProgress<FolderUploadProgressInfo> detailProgress = null, CancellationToken ct = default)
    {
        return await ExecuteAsync(async () =>
        {
            ValidateNotNull(folder, nameof(folder));
            string parentPath = ResolveDirectoryPath(parentId);
            string targetRoot = GetUniquePath(Path.Combine(parentPath, folder.Name), Directory.Exists, "rename");
            Directory.CreateDirectory(targetRoot);
            ulong totalSize = await GetFolderSize(folder);
            UploadProgressTracker tracker = new();
            await CopyFolderAsync(folder, targetRoot, folder.Name, totalSize, tracker, overallProgress, detailProgress, ct);
            return ToFileItem(targetRoot);
        }, () => ValidateParent(parentId));
    }

    public Task<StorageResult<ShareLink>> CreateLinkAsync(string itemId, DateTimeOffset? expiration = null, string password = null, string type = "view", CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<ShareLink>.Failure(L("Local_ShareUnsupported", "Local items do not support share links"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<ShareLink>> GetShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<ShareLink>.Failure(L("Local_ShareUnsupported", "Local items do not support share links"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<bool>> RevokeShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<bool>.Failure(L("Local_ShareUnsupported", "Local items do not support share links"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<bool>> ConvertToPdfAsync(string itemId, StorageFile destination, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<bool>.Failure(L("Local_PdfConversionUnsupported", "Local items do not support PDF conversion"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<PageResult<FileItem>>> SearchAsync(string query, string scopeParentId = null, CancellationToken ct = default)
    {
        return ExecuteAsync(() =>
        {
            string basePath = string.IsNullOrEmpty(scopeParentId) || scopeParentId == "Root"
                ? RootPath
                : ResolveDirectoryPath(scopeParentId);
            var items = EnumerateDirectoryRecursive(basePath)
                .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Task.FromResult(new PageResult<FileItem> { Items = items, NextPageToken = null });
        }, () => ValidateNotEmpty(query, nameof(query)));
    }

    protected override Task EnsureAuthenticatedAsync()
    {
        if (Directory.Exists(RootPath))
        {
            return Task.CompletedTask;
        }
        throw new DirectoryNotFoundException(RootPath);
    }

    private IEnumerable<FileItem> EnumerateDirectory(string directoryPath)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            yield return ToFileItem(entry);
        }
    }

    private IEnumerable<FileItem> EnumerateDirectoryRecursive(string directoryPath)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            FileItem item = ToFileItem(entry);
            yield return item;
            if (item.IsFolder)
            {
                foreach (FileItem nested in EnumerateDirectoryRecursive(entry))
                {
                    yield return nested;
                }
            }
        }
    }

    private FileItem ToFileItem(string path)
    {
        bool isFolder = Directory.Exists(path);
        string name;
        DateTimeOffset updated;
        DateTimeOffset created;
        long? size = null;
        if (isFolder)
        {
            DirectoryInfo dirInfo = new(path);
            name = dirInfo.Name;
            updated = dirInfo.LastWriteTimeUtc;
            created = dirInfo.CreationTimeUtc;
        }
        else
        {
            FileInfo fileInfo = new(path);
            name = fileInfo.Name;
            updated = fileInfo.LastWriteTimeUtc;
            created = fileInfo.CreationTimeUtc;
            size = fileInfo.Length;
        }
        return new FileItem
        {
            Id = path,
            Name = name,
            ParentId = Path.GetDirectoryName(path),
            Size = size,
            Updated = updated,
            Created = created,
            IsFolder = isFolder,
            ChildCount = isFolder ? Directory.EnumerateFileSystemEntries(path).Count() : (int?)null,
            MimeType = isFolder ? "inode/directory" : GetMimeType(path),
            Image = IsImage(path) ? new ImageMetadata() : null,
            Provider = ProviderType.Local,
            ProviderTokens = new Dictionary<string, string>
            {
                ["localPath"] = path,
            },
        };
    }

    private string ResolveItemPath(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || itemId == "Root")
        {
            return RootPath;
        }

        string fullPath = Path.GetFullPath(itemId);
        EnsureWithinRoot(fullPath);
        EnsureReparsePointsStayWithinRoot(fullPath);
        return fullPath;
    }

    private string ResolveDirectoryPath(string parentId)
    {
        string path = ResolveItemPath(parentId);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }
        return path;
    }

    private void ValidateParent(string parentId)
    {
        ValidateNotEmpty(parentId, nameof(parentId));
    }

    private void ValidateFile(string itemId)
    {
        ValidateNotEmpty(itemId, nameof(itemId));
        string path = ResolveItemPath(itemId);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException(path);
        }
    }

    private string NormalizeRootPath(string rootPath)
    {
        ValidateNotEmpty(rootPath, nameof(rootPath));
        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        if (Directory.Exists(fullPath)
            && File.GetAttributes(fullPath).HasFlag(System.IO.FileAttributes.ReparsePoint))
        {
            DirectoryInfo root = new(fullPath);
            FileSystemInfo target = root.ResolveLinkTarget(returnFinalTarget: true);
            if (target != null)
            {
                fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
            }
        }

        return fullPath;
    }

    private void EnsureWithinRoot(string path)
    {
        string relativePath = Path.GetRelativePath(RootPath, path);
        if (Path.IsPathRooted(relativePath)
            || string.Equals(relativePath, "..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Path is outside the local drive root: {path}");
        }
    }

    private void EnsureReparsePointsStayWithinRoot(string path)
    {
        string relativePath = Path.GetRelativePath(RootPath, path);
        if (relativePath == ".")
        {
            return;
        }

        string currentPath = RootPath;
        foreach (string segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            bool isDirectory = Directory.Exists(currentPath);
            if (!isDirectory && !File.Exists(currentPath))
            {
                break;
            }

            if (!File.GetAttributes(currentPath).HasFlag(System.IO.FileAttributes.ReparsePoint))
            {
                continue;
            }

            FileSystemInfo item = isDirectory
                ? new DirectoryInfo(currentPath)
                : new FileInfo(currentPath);
            FileSystemInfo target = item.ResolveLinkTarget(returnFinalTarget: true);
            if (target == null)
            {
                throw new UnauthorizedAccessException($"Unable to resolve reparse point: {currentPath}");
            }

            currentPath = Path.GetFullPath(target.FullName);
            EnsureWithinRoot(currentPath);
        }
    }

    private static string GetUniquePath(string targetPath, Func<string, bool> exists, string conflictBehavior, string sourcePath = null)
    {
        if (conflictBehavior != "rename" || !exists(targetPath) || string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return targetPath;
        }

        string directory = Path.GetDirectoryName(targetPath);
        string fileName = Path.GetFileNameWithoutExtension(targetPath);
        string extension = Path.GetExtension(targetPath);
        int index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory ?? string.Empty, $"{fileName} ({index++}){extension}");
        }
        while (exists(candidate));
        return candidate;
    }

    private static string GetMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".csv" => "text/csv",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream",
        };
    }

    private static bool IsImage(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }

    private static async Task CopyToAsync(Stream source, Stream destination, IProgress<long> progress, CancellationToken ct)
    {
        byte[] buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, ct);
            totalBytes += bytesRead;
            progress?.Report(totalBytes);
        }
    }

    private static async Task<ulong> GetFolderSize(StorageFolder folder)
    {
        ulong total = 0;
        foreach (StorageFile file in await folder.GetFilesAsync())
        {
            total += (await file.GetBasicPropertiesAsync()).Size;
        }

        foreach (StorageFolder sub in await folder.GetFoldersAsync())
        {
            total += await GetFolderSize(sub);
        }

        return total;
    }

    private async Task CopyFolderAsync(StorageFolder sourceFolder, string targetRoot, string relativeRoot, ulong totalSize, UploadProgressTracker tracker, IProgress<long> overallProgress, IProgress<FolderUploadProgressInfo> detailProgress, CancellationToken ct)
    {
        foreach (StorageFile file in await sourceFolder.GetFilesAsync())
        {
            ct.ThrowIfCancellationRequested();
            string targetPath = Path.Combine(targetRoot, file.Name);
            ulong fileSize = (await file.GetBasicPropertiesAsync()).Size;
            await using Stream source = await file.OpenStreamForReadAsync();
            await using FileStream destination = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            long previousBytes = 0;
            await CopyToAsync(source, destination, new Progress<long>(bytes =>
            {
                long delta = bytes - previousBytes;
                if (delta < 0) delta = 0;
                previousBytes = bytes;
                long currentTotal = Interlocked.Add(ref tracker.UploadedSize, delta);
                overallProgress?.Report(totalSize == 0 ? 100 : (long)(Math.Min(currentTotal, (long)totalSize) * 100.0 / totalSize));
                detailProgress?.Report(new FolderUploadProgressInfo
                {
                    FilePath = $"{relativeRoot}/{file.Name}",
                    UploadedBytes = (ulong)bytes,
                    TotalBytes = fileSize,
                    Completed = false,
                });
            }), ct);
            long remain = (long)fileSize - previousBytes;
            if (remain > 0)
            {
                Interlocked.Add(ref tracker.UploadedSize, remain);
            }
            detailProgress?.Report(new FolderUploadProgressInfo
            {
                FilePath = $"{relativeRoot}/{file.Name}",
                UploadedBytes = fileSize,
                TotalBytes = fileSize,
                Completed = true,
            });
        }

        foreach (StorageFolder subfolder in await sourceFolder.GetFoldersAsync())
        {
            string childTarget = Path.Combine(targetRoot, subfolder.Name);
            Directory.CreateDirectory(childTarget);
            await CopyFolderAsync(subfolder, childTarget, $"{relativeRoot}/{subfolder.Name}", totalSize, tracker, overallProgress, detailProgress, ct);
        }
    }

    private class UploadProgressTracker
    {
        public long UploadedSize;
    }
}
