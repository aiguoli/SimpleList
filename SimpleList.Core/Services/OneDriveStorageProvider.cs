using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Core.Services;

public class OneDriveStorageProvider : StorageProviderBase, IStorageProvider
{
    private const int MaxConcurrentFileUploadCount = 3;
    private const long SimpleUploadMaxBytes = 4 * 1024 * 1024;
    private const string ItemSelect = "id,name,parentReference,size,lastModifiedDateTime,createdDateTime,folder,file,image,eTag,shared,@microsoft.graph.downloadUrl";
    private static readonly string[] Scopes = ["User.Read", "Files.ReadWrite.All"];

    private readonly IPublicClientApplication _publicClientApp;
    private readonly Task<MsalCacheHelper> _cacheHelperTask;
    private GraphRestClient _graphClient;
    private AuthenticationResult _authResult;

    public OneDriveStorageProvider(
        IPublicClientApplication publicClientApp,
        Task<MsalCacheHelper> cacheHelperTask = null,
        IStringLocalizer localizer = null,
        string driveId = null,
        string accountId = null)
        : base(localizer)
    {
        _publicClientApp = publicClientApp;
        _cacheHelperTask = cacheHelperTask;
        DriveId = driveId;
        AccountId = accountId;
    }

    public ProviderType ProviderType => ProviderType.OneDrive;
    public string AccountId { get; private set; }
    public string DriveId { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool SupportsTrash => true;
    public ShareCapabilities ShareCapabilities { get; } = new(true, true, true);

    public Task<StorageResult<bool>> LoginAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                await LoginInternalAsync().ConfigureAwait(false);
                return StorageResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return StorageResult<bool>.Failure($"{L("LoginFail", "Login failed")}: {ex.Message}", StorageErrorType.Authentication, ex);
            }
        }, ct);
    }

    public Task<StorageResult<string>> GetDisplayNameAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphUser user = await _graphClient.GetAsync("me?$select=displayName", GraphJsonContext.Default.GraphUser, ct);
            return user.DisplayName;
        });
    }

    public Task<StorageResult<StorageQuota>> GetQuotaAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphDrive drive = await _graphClient.GetAsync($"{DrivePath}?$select=quota", GraphJsonContext.Default.GraphDrive, ct);
            return GraphMappers.ToStorageQuota(drive.Quota);
        });
    }

    public Task<StorageResult<PageResult<FileItem>>> ListChildrenAsync(string parentId, string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            parentId ??= "root";
            string url = string.IsNullOrWhiteSpace(pageToken)
                ? $"{DrivePath}/items/{Escape(parentId)}/children?$select={ItemSelect}"
                : pageToken;
            GraphDriveItemCollection response = await _graphClient.GetAsync(url, GraphJsonContext.Default.GraphDriveItemCollection, ct);
            return GraphMappers.ToPageResult(response);
        }, () => ValidateNotEmpty(parentId, nameof(parentId)));
    }

    public Task<StorageResult<FileItem>> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphDriveItem item = await _graphClient.GetAsync(
                $"{ItemPath(itemId)}?$select={ItemSelect}", GraphJsonContext.Default.GraphDriveItem, ct);
            return GraphMappers.ToFileItem(item);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<Stream>> GetItemContentAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            return await _graphClient.GetStreamAsync($"{ItemPath(itemId)}/content", null, ct);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> DownloadFileAsync(string itemId, Stream destination, IProgress<long> progress = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(destination, nameof(destination));
            using Stream source = await _graphClient.GetStreamAsync($"{ItemPath(itemId)}/content", null, ct);
            await CopyToAsync(source, destination, progress, ct);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<Models.ThumbnailSet>> GetThumbnailsAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphThumbnailSetCollection response = await _graphClient.GetAsync(
                $"{ItemPath(itemId)}/thumbnails", GraphJsonContext.Default.GraphThumbnailSetCollection, ct);
            return GraphMappers.ToThumbnailSet(response);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<string>> GetDownloadUrlAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphDriveItem item = await _graphClient.GetAsync(
                $"{ItemPath(itemId)}?$select=id,name,@microsoft.graph.downloadUrl", GraphJsonContext.Default.GraphDriveItem, ct);
            if (!string.IsNullOrWhiteSpace(item.DownloadUrl)) return item.DownloadUrl;

            return await GetContentRedirectUrlAsync(itemId, ct);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<FileItem>> CreateFolderAsync(string parentId, string name, string conflictBehavior = "rename", CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            var requestBody = new GraphCreateFolderRequest
            {
                Name = name,
                ConflictBehavior = conflictBehavior,
            };
            GraphDriveItem item = await _graphClient.PostAsync(
                $"{ItemPath(parentId)}/children", requestBody,
                GraphJsonContext.Default.GraphCreateFolderRequest,
                GraphJsonContext.Default.GraphDriveItem, ct);
            return GraphMappers.ToFileItem(item);
        }, () =>
        {
            ValidateNotEmpty(parentId, nameof(parentId));
            ValidateFileName(name, nameof(name));
        });
    }

    public Task<StorageResult<FileItem>> RenameAsync(string itemId, string newName, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphRenameRequest requestBody = new() { Name = newName };
            GraphDriveItem item = await _graphClient.PatchAsync(
                ItemPath(itemId), requestBody, GraphJsonContext.Default.GraphRenameRequest,
                GraphJsonContext.Default.GraphDriveItem, ct);
            return GraphMappers.ToFileItem(item);
        }, () =>
        {
            ValidateNotEmpty(itemId, nameof(itemId));
            ValidateFileName(newName, nameof(newName));
        });
    }

    public Task<StorageResult<bool>> DeleteAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            await _graphClient.DeleteAsync(ItemPath(itemId), ct);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> PermanentDeleteAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            await _graphClient.PostAsync($"{ItemPath(itemId)}/permanentDelete", new GraphEmptyObject(), GraphJsonContext.Default.GraphEmptyObject, ct);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<FileItem>> RestoreAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphDriveItem item = await _graphClient.PostAsync(
                $"{ItemPath(itemId)}/restore", new GraphEmptyObject(), GraphJsonContext.Default.GraphEmptyObject,
                GraphJsonContext.Default.GraphDriveItem, ct);
            return GraphMappers.ToFileItem(item);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<PageResult<FileItem>>> ListTrashAsync(string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string url = string.IsNullOrWhiteSpace(pageToken)
                ? $"https://graph.microsoft.com/v1.0/drives/{Uri.EscapeDataString(DriveId)}/root/children?includeDeletedItems=true&$select=id,name,parentReference,size,lastModifiedDateTime,createdDateTime,folder,file,deleted,image,eTag,shared,@microsoft.graph.downloadUrl&$top=200"
                : pageToken;

            GraphDriveItemCollection response = await _graphClient.GetAsync(url, GraphJsonContext.Default.GraphDriveItemCollection, ct);
            response.Value = response.Value?.Where(item => item.Deleted != null).ToList() ?? [];
            return GraphMappers.ToPageResult(response);
        });
    }

    public Task<StorageResult<bool>> EmptyTrashAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string pageToken = null;
            do
            {
                StorageResult<PageResult<FileItem>> result = await ListTrashAsync(pageToken, ct).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);
                }

                foreach (FileItem item in result.Data?.Items ?? [])
                {
                    await _graphClient.PostAsync($"{ItemPath(item.Id)}/permanentDelete", new GraphEmptyObject(), GraphJsonContext.Default.GraphEmptyObject, ct);
                }

                pageToken = result.Data?.NextPageToken;
            }
            while (!string.IsNullOrWhiteSpace(pageToken));

            return true;
        });
    }

    public Task<StorageResult<FileItem>> UploadFileAsync(
        StorageFile file,
        string parentId,
        IProgress<long> progress = null,
        string resumeToken = null,
        Action<string> resumeTokenCallback = null,
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(file, nameof(file));
            ValidateNotEmpty(parentId, nameof(parentId));

            await using Stream stream = await file.OpenStreamForReadAsync();
            ulong size = (await file.GetBasicPropertiesAsync()).Size;
            if (size == 0)
            {
                using var empty = new MemoryStream();
                GraphDriveItem emptyItem = await _graphClient.PutContentAsync(ItemContentPath(parentId, file.Name), empty, ct);
                return GraphMappers.ToFileItem(emptyItem);
            }

            bool isResume = !string.IsNullOrWhiteSpace(resumeToken);
            string uploadUrl = resumeToken;
            if (!isResume)
            {
                GraphUploadSession uploadSession = await CreateUploadSessionAsync(parentId, file.Name, ct);
                uploadUrl = uploadSession.UploadUrl;
                resumeTokenCallback?.Invoke(uploadUrl);
            }

            GraphDriveItem uploaded = await _graphClient.UploadLargeFileAsync(
                uploadUrl, stream, checked((long)size), progress, isResume, ct);
            return GraphMappers.ToFileItem(uploaded);
        });
    }

    public Task<StorageResult<FileItem>> UploadFileContentAsync(
        Stream content,
        string fileName,
        string parentId,
        long? size = null,
        IProgress<long> progress = null,
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(content, nameof(content));
            ValidateFileName(fileName, nameof(fileName));
            ValidateNotEmpty(parentId, nameof(parentId));

            if (size == 0)
            {
                using var empty = new MemoryStream();
                GraphDriveItem emptyItem = await _graphClient.PutContentAsync(ItemContentPath(parentId, fileName), empty, ct);
                progress?.Report(0);
                return GraphMappers.ToFileItem(emptyItem);
            }

            if (size.HasValue && size.Value <= SimpleUploadMaxBytes)
            {
                GraphDriveItem item = await _graphClient.PutContentAsync(ItemContentPath(parentId, fileName), content, ct);
                progress?.Report(size.Value);
                return GraphMappers.ToFileItem(item);
            }

            string tempPath = null;
            Stream uploadStream = content;
            try
            {
                if (!content.CanSeek)
                {
                    tempPath = Path.GetTempFileName();
                    await using (FileStream tempWriteStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await content.CopyToAsync(tempWriteStream, ct);
                    }
                    uploadStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                else if (content.Position != 0)
                {
                    content.Seek(0, SeekOrigin.Begin);
                }

                long uploadLength = size ?? uploadStream.Length;
                GraphUploadSession uploadSession = await CreateUploadSessionAsync(parentId, fileName, ct);
                GraphDriveItem uploaded = await _graphClient.UploadLargeFileAsync(
                    uploadSession.UploadUrl, uploadStream, uploadLength, progress, false, ct);
                return GraphMappers.ToFileItem(uploaded);
            }
            finally
            {
                if (uploadStream != content)
                {
                    uploadStream.Dispose();
                }
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        });
    }

    public Task<StorageResult<FileItem>> UploadFolderAsync(
        StorageFolder folder,
        string parentId,
        IProgress<long> overallProgress = null,
        IProgress<FolderUploadProgressInfo> detailProgress = null,
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(folder, nameof(folder));
            ValidateNotEmpty(parentId, nameof(parentId));

            ulong totalSize = await GetFolderSize(folder);
            UploadProgressTracker tracker = new();
            using SemaphoreSlim uploadSemaphore = new(MaxConcurrentFileUploadCount, MaxConcurrentFileUploadCount);
            GraphDriveItem cloudFolder = await UploadFolderInternalAsync(folder, parentId, folder.Name, totalSize, tracker, overallProgress, detailProgress, uploadSemaphore, ct);
            return GraphMappers.ToFileItem(cloudFolder);
        });
    }

    private async Task<GraphDriveItem> UploadFolderInternalAsync(
        StorageFolder folder,
        string parentId,
        string relativeFolderPath,
        ulong totalSize,
        UploadProgressTracker tracker,
        IProgress<long> progress,
        IProgress<FolderUploadProgressInfo> detailProgress,
        SemaphoreSlim uploadSemaphore,
        CancellationToken ct)
    {
        var createFolderRequestBody = new GraphCreateFolderRequest
        {
            Name = folder.Name,
            ConflictBehavior = "replace",
        };
        GraphDriveItem cloudFolder = await _graphClient.PostAsync(
            $"{ItemPath(parentId)}/children", createFolderRequestBody,
            GraphJsonContext.Default.GraphCreateFolderRequest,
            GraphJsonContext.Default.GraphDriveItem, ct);
        if (cloudFolder == null)
        {
            throw new Exception("Failed to create or retrieve cloud folder.");
        }

        var files = await folder.GetFilesAsync();
        GraphDriveItemCollection existingItemsResult = await _graphClient.GetAsync(
            $"{ItemPath(cloudFolder.Id)}/children?$select={ItemSelect}",
            GraphJsonContext.Default.GraphDriveItemCollection, ct);
        var existingItems = existingItemsResult?.Value ?? new List<GraphDriveItem>();

        IEnumerable<Task> uploadTasks = files.Select(async file =>
        {
            await uploadSemaphore.WaitAsync(ct);
            try
            {
                ulong fileSize = (await file.GetBasicPropertiesAsync()).Size;
                string relativePath = $"{relativeFolderPath}/{file.Name}";

                var existing = existingItems.FirstOrDefault(i => i.Name == file.Name);
                if (existing != null && (ulong?)existing.Size == fileSize)
                {
                    long totalUploaded = Interlocked.Add(ref tracker.UploadedSize, (long)fileSize);
                    ReportOverallProgress(progress, totalSize, totalUploaded);
                    detailProgress?.Report(new FolderUploadProgressInfo
                    {
                        FilePath = relativePath,
                        UploadedBytes = fileSize,
                        TotalBytes = fileSize,
                        Completed = true,
                    });
                    return;
                }

                long currentUploaded = 0;
                object progressLock = new();
                IProgress<long> fileProgress = new Progress<long>(bytes =>
                {
                    long delta;
                    lock (progressLock)
                    {
                        delta = bytes - currentUploaded;
                        if (delta < 0) delta = 0;
                        currentUploaded = bytes;
                    }
                    long totalUploaded = Interlocked.Add(ref tracker.UploadedSize, delta);
                    ReportOverallProgress(progress, totalSize, totalUploaded);
                    detailProgress?.Report(new FolderUploadProgressInfo
                    {
                        FilePath = relativePath,
                        UploadedBytes = (ulong)Math.Max(0, bytes),
                        TotalBytes = fileSize,
                        Completed = bytes >= (long)fileSize,
                    });
                });

                var uploadResult = await UploadFileAsync(file, cloudFolder.Id, fileProgress, null, null, ct);
                if (!uploadResult.IsSuccess)
                {
                    throw new Exception($"Upload failed: {uploadResult.ErrorMessage}");
                }

                long remain;
                lock (progressLock)
                {
                    remain = (long)fileSize - currentUploaded;
                    if (remain < 0) remain = 0;
                    currentUploaded += remain;
                }
                if (remain > 0)
                {
                    long totalUploaded = Interlocked.Add(ref tracker.UploadedSize, remain);
                    ReportOverallProgress(progress, totalSize, totalUploaded);
                }
                detailProgress?.Report(new FolderUploadProgressInfo
                {
                    FilePath = relativePath,
                    UploadedBytes = fileSize,
                    TotalBytes = fileSize,
                    Completed = true,
                });
            }
            finally
            {
                uploadSemaphore.Release();
            }
        });
        await Task.WhenAll(uploadTasks);

        IReadOnlyList<StorageFolder> subfolders = await folder.GetFoldersAsync();
        foreach (StorageFolder subfolder in subfolders)
        {
            string subfolderPath = $"{relativeFolderPath}/{subfolder.Name}";
            await UploadFolderInternalAsync(subfolder, cloudFolder.Id, subfolderPath, totalSize, tracker, progress, detailProgress, uploadSemaphore, ct);
        }

        return cloudFolder;
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

    private class UploadProgressTracker
    {
        public long UploadedSize;
    }

    private static void ReportOverallProgress(IProgress<long> progress, ulong totalSize, long uploadedSize)
    {
        if (totalSize == 0) return;
        long percent = (long)(Math.Min(uploadedSize, (long)totalSize) * 100.0 / totalSize);
        progress?.Report(percent);
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

    private async Task<string> GetContentRedirectUrlAsync(string itemId, CancellationToken ct)
    {
        return await _graphClient.GetRedirectLocationAsync($"{ItemPath(itemId)}/content", ct);
    }

    public Task<StorageResult<ShareLink>> CreateLinkAsync(
        string itemId,
        DateTimeOffset? expiration = null,
        string password = null,
        string type = "view",
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            var requestBody = new GraphCreateLinkRequest
            {
                Type = type,
                Password = password,
                Scope = "anonymous",
                RetainInheritedPermissions = false,
                ExpirationDateTime = expiration,
            };
            GraphPermission result = await _graphClient.PostAsync(
                $"{ItemPath(itemId)}/createLink", requestBody, GraphJsonContext.Default.GraphCreateLinkRequest,
                GraphJsonContext.Default.GraphPermission, ct);
            return new ShareLink
            {
                WebUrl = result.Link?.WebUrl,
                Token = result.Id,
                Expiration = result.ExpirationDateTime,
                HasPassword = result.HasPassword == true || !string.IsNullOrEmpty(password),
                IsShared = true,
            };
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<ShareLink>> GetShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphPermissionCollection permissions = await _graphClient.GetAsync(
                $"{ItemPath(itemId)}/permissions", GraphJsonContext.Default.GraphPermissionCollection, ct);
            GraphPermission permission = permissions?.Value?
                .FirstOrDefault(item => string.Equals(item.Link?.Scope, "anonymous", StringComparison.OrdinalIgnoreCase));

            if (permission == null)
            {
                return new ShareLink { IsShared = false };
            }

            return new ShareLink
            {
                WebUrl = permission.Link?.WebUrl,
                Token = permission.Id,
                Expiration = permission.ExpirationDateTime,
                HasPassword = permission.HasPassword == true,
                IsShared = true,
            };
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> RevokeShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            GraphPermissionCollection permissions = await _graphClient.GetAsync(
                $"{ItemPath(itemId)}/permissions", GraphJsonContext.Default.GraphPermissionCollection, ct);
            IEnumerable<GraphPermission> publicLinks = permissions?.Value?
                .Where(item => !string.IsNullOrWhiteSpace(item.Id)
                    && string.Equals(item.Link?.Scope, "anonymous", StringComparison.OrdinalIgnoreCase))
                ?? [];

            foreach (GraphPermission permission in publicLinks)
            {
                await _graphClient.DeleteAsync(
                    $"{ItemPath(itemId)}/permissions/{Uri.EscapeDataString(permission.Id)}", ct);
            }

            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> ConvertToPdfAsync(string itemId, StorageFile destination, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            using Stream result = await _graphClient.GetStreamAsync($"{ItemPath(itemId)}/content?format=pdf", null, ct);
            using Stream fileStream = await destination.OpenStreamForWriteAsync();
            if (result.CanSeek) result.Seek(0, SeekOrigin.Begin);
            await result.CopyToAsync(fileStream, ct);
            return true;
        }, () =>
        {
            ValidateNotEmpty(itemId, nameof(itemId));
            ValidateNotNull(destination, nameof(destination));
        });
    }

    public Task<StorageResult<PageResult<FileItem>>> SearchAsync(string query, string scopeParentId = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string escapedQuery = Escape(query.Replace("'", "''"));
            string url = string.IsNullOrEmpty(scopeParentId)
                ? $"{DrivePath}/root/search(q='{escapedQuery}')?$select={ItemSelect}"
                : $"{ItemPath(scopeParentId)}/search(q='{escapedQuery}')?$select={ItemSelect}";
            GraphDriveItemCollection response = await _graphClient.GetAsync(
                url, GraphJsonContext.Default.GraphDriveItemCollection, ct);
            return GraphMappers.ToPageResult(response);
        }, () => ValidateNotEmpty(query, nameof(query)));
    }

    protected override Task EnsureAuthenticatedAsync()
    {
        if (IsAuthenticated) return Task.CompletedTask;
        return LoginInternalAsync();
    }

    private async Task LoginInternalAsync()
    {
        if (_cacheHelperTask != null)
        {
            MsalCacheHelper cacheHelper = await _cacheHelperTask.ConfigureAwait(false);
            cacheHelper.RegisterCache(_publicClientApp.UserTokenCache);
        }

        await AcquireAccessTokenAsync(CancellationToken.None).ConfigureAwait(false);
        _graphClient ??= new GraphRestClient(AcquireAccessTokenAsync);

        try
        {
            GraphDrive driveItem = await _graphClient.GetAsync("me/drive?$select=id", GraphJsonContext.Default.GraphDrive, CancellationToken.None);
            DriveId = driveItem.Id;
        }
        catch
        {
            // DriveId may already be populated from saved cache; ignore.
        }
    }

    private async Task<string> AcquireAccessTokenAsync(CancellationToken ct)
    {
        IAccount account = null;
        if (!string.IsNullOrEmpty(AccountId))
        {
            account = await _publicClientApp.GetAccountAsync(AccountId).ConfigureAwait(false);
        }

        if (account == null)
        {
            IEnumerable<IAccount> accounts = await _publicClientApp.GetAccountsAsync().ConfigureAwait(false);
            account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier == AccountId) ?? accounts.FirstOrDefault();
        }

        try
        {
            _authResult = await _publicClientApp.AcquireTokenSilent(Scopes, account).ExecuteAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is MsalUiRequiredException || exception is InvalidOperationException)
        {
            try
            {
                _authResult = await _publicClientApp.AcquireTokenInteractive(Scopes).ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (MsalException msalex)
            {
                Debug.WriteLine($"MSAL error: {msalex}");
                throw;
            }
        }

        if (_authResult == null)
        {
            throw new MsalUiRequiredException("token_unavailable", "Microsoft access token is unavailable");
        }

        IsAuthenticated = true;
        AccountId = _authResult.Account?.HomeAccountId?.Identifier;
        return _authResult.AccessToken;
    }

    protected override StorageResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            GraphHttpException graphEx => HandleGraphException<T>(graphEx),
            MsalUiRequiredException => StorageResult<T>.Failure(
                L("LoginRequired", "Login required"), StorageErrorType.Authentication, exception),
            MsalException msalEx => StorageResult<T>.Failure(
                $"{L("AuthenticationError", "Authentication error")}: {msalEx.Message}", StorageErrorType.Authentication, exception),
            _ => base.HandleException<T>(exception),
        };
    }

    private StorageResult<T> HandleGraphException<T>(GraphHttpException graphException)
    {
        int statusCode = (int)graphException.ResponseStatusCode;
        return statusCode switch
        {
            400 => StorageResult<T>.Failure(L("Http_BadRequest", "Bad Request"), StorageErrorType.InvalidRequest, graphException),
            401 => StorageResult<T>.Failure(L("Http_Unauthorized", "Unauthorized"), StorageErrorType.Authentication, graphException),
            403 => StorageResult<T>.Failure(L("Http_Forbidden", "Forbidden"), StorageErrorType.Forbidden, graphException),
            404 => StorageResult<T>.Failure(L("Http_NotFound", "Not Found"), StorageErrorType.NotFound, graphException),
            409 => StorageResult<T>.Failure(L("Http_Conflict", "Conflict"), StorageErrorType.Conflict, graphException),
            429 or 500 or 502 or 503 or 504 => StorageResult<T>.Failure(L("Http_ServiceUnavailable", "Service Unavailable"), StorageErrorType.ServiceUnavailable, graphException),
            507 => StorageResult<T>.Failure(L("Http_InsufficientStorage", "Insufficient Storage"), StorageErrorType.QuotaExceeded, graphException),
            _ => StorageResult<T>.Failure(LF("OneDrive_ErrorFormat", "OneDrive error: {0}", graphException.Message), StorageErrorType.Unknown, graphException),
        };
    }

    private string DrivePath => $"drives/{Escape(DriveId)}";

    private string ItemPath(string itemId) => $"{DrivePath}/items/{Escape(itemId)}";

    private string ItemContentPath(string parentId, string fileName)
        => $"{ItemPath(parentId)}:/{Escape(fileName)}:/content";

    private async Task<GraphUploadSession> CreateUploadSessionAsync(string parentId, string fileName, CancellationToken ct)
    {
        GraphUploadSession session = await _graphClient.PostAsync(
            $"{ItemPath(parentId)}:/{Escape(fileName)}:/createUploadSession",
            new GraphUploadSessionRequest(), GraphJsonContext.Default.GraphUploadSessionRequest,
            GraphJsonContext.Default.GraphUploadSession, ct);
        if (string.IsNullOrWhiteSpace(session?.UploadUrl))
            throw new InvalidOperationException("Failed to create upload session");
        return session;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value ?? string.Empty);
}
