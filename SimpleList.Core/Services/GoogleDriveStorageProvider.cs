using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.Apis.Upload;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace SimpleList.Core.Services;

public class GoogleDriveStorageProvider : StorageProviderBase, IStorageProvider
{
    private const int MaxConcurrentFileUploadCount = 3;
    private const int UploadChunkSize = 256 * 1024;
    private static readonly string[] Scopes = [DriveService.Scope.Drive];
    private const string ApplicationName = "SimpleList";
    private const string LegacyCredentialStoreKey = "user";

    private readonly ClientSecrets _clientSecrets;
    private readonly GoogleTokenDataStore _dataStore;
    private DriveService _driveService;
    private UserCredential _credential;

    public GoogleDriveStorageProvider(
        ClientSecrets clientSecrets,
        GoogleTokenDataStore dataStore,
        IStringLocalizer localizer = null,
        string driveId = null,
        string accountId = null,
        string credentialStoreKey = null)
        : base(localizer)
    {
        _clientSecrets = clientSecrets;
        _dataStore = dataStore;
        DriveId = driveId;
        AccountId = accountId;
        CredentialStoreKey = !string.IsNullOrWhiteSpace(credentialStoreKey)
            ? credentialStoreKey
            : !string.IsNullOrWhiteSpace(driveId) || !string.IsNullOrWhiteSpace(accountId)
                ? LegacyCredentialStoreKey
                : Guid.NewGuid().ToString("N");
    }

    public ProviderType ProviderType => ProviderType.GoogleDrive;
    public string AccountId { get; private set; }
    public string DriveId { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool SupportsTrash => true;
    public ShareCapabilities ShareCapabilities { get; } = new(true, false, false);
    public string CredentialStoreKey { get; }

    public Task<StorageResult<bool>> LoginAsync(CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                await LoginInternalAsync(ct).ConfigureAwait(false);
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
            var request = _driveService.About.Get();
            request.Fields = "user(displayName,emailAddress)";
            var about = await request.ExecuteAsync(ct);
            return about.User?.DisplayName ?? about.User?.EmailAddress;
        });
    }

    public Task<StorageResult<StorageQuota>> GetQuotaAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            var request = _driveService.About.Get();
            request.Fields = "storageQuota";
            var about = await request.ExecuteAsync(ct);
            return GoogleMappers.ToStorageQuota(about);
        });
    }

    public Task<StorageResult<PageResult<FileItem>>> ListChildrenAsync(string parentId, string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string parent = string.IsNullOrEmpty(parentId) || parentId == "Root" ? "root" : parentId;
            var request = _driveService.Files.List();
            request.Q = $"'{parent}' in parents and trashed = false";
            request.Fields = "nextPageToken, files(id,name,mimeType,size,modifiedTime,createdTime,parents,thumbnailLink,imageMediaMetadata,webContentLink,webViewLink,md5Checksum,shared)";
            request.PageToken = pageToken;
            request.PageSize = 200;
            var response = await request.ExecuteAsync(ct);
            return GoogleMappers.ToPageResult(response);
        }, () => ValidateNotEmpty(parentId, nameof(parentId)));
    }

    public Task<StorageResult<FileItem>> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            var request = _driveService.Files.Get(id);
            request.Fields = "id,name,mimeType,size,modifiedTime,createdTime,parents,thumbnailLink,imageMediaMetadata,webContentLink,webViewLink,md5Checksum,shared";
            var file = await request.ExecuteAsync(ct);
            return GoogleMappers.ToFileItem(file);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<Stream>> GetItemContentAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            Uri contentUri = new($"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(id)}?alt=media");
            using HttpRequestMessage request = new(HttpMethod.Get, contentUri);
            HttpResponseMessage response = await _driveService.HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);
            try
            {
                response.EnsureSuccessStatusCode();
                Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                return (Stream)new HttpResponseOwnedStream(stream, response);
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> DownloadFileAsync(string itemId, Stream destination, IProgress<long> progress = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(destination, nameof(destination));
            string id = NormalizeId(itemId);
            var request = _driveService.Files.Get(id);
            request.MediaDownloader.ProgressChanged += downloadProgress =>
            {
                progress?.Report(downloadProgress.BytesDownloaded);
            };
            IDownloadProgress result = await request.DownloadAsync(destination, ct);
            if (result.Status == DownloadStatus.Failed)
            {
                throw new Exception(result.Exception?.Message ?? "Download failed");
            }
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<Models.ThumbnailSet>> GetThumbnailsAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            var request = _driveService.Files.Get(id);
            request.Fields = "thumbnailLink";
            var file = await request.ExecuteAsync(ct);
            return new Models.ThumbnailSet
            {
                SmallUrl = file.ThumbnailLink,
                MediumUrl = file.ThumbnailLink,
                LargeUrl = file.ThumbnailLink,
            };
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<string>> GetDownloadUrlAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            string token = await _credential.GetAccessTokenForRequestAsync(cancellationToken: ct);
            return $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(id)}?alt=media&access_token={Uri.EscapeDataString(token)}";
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<FileItem>> CreateFolderAsync(string parentId, string name, string conflictBehavior = "rename", CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string parent = string.IsNullOrEmpty(parentId) || parentId == "Root" ? "root" : parentId;
            var metadata = new GoogleFile
            {
                Name = name,
                MimeType = GoogleMappers.FolderMimeType,
                Parents = new List<string> { parent },
            };
            var request = _driveService.Files.Create(metadata);
            request.Fields = "id,name,mimeType,parents,modifiedTime,createdTime";
            var created = await request.ExecuteAsync(ct);
            return GoogleMappers.ToFileItem(created);
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
            string id = NormalizeId(itemId);
            var metadata = new GoogleFile { Name = newName };
            var request = _driveService.Files.Update(metadata, id);
            request.Fields = "id,name,mimeType,parents,modifiedTime";
            var updated = await request.ExecuteAsync(ct);
            return GoogleMappers.ToFileItem(updated);
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
            string id = NormalizeId(itemId);
            var metadata = new GoogleFile { Trashed = true };
            var request = _driveService.Files.Update(metadata, id);
            await request.ExecuteAsync(ct);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> PermanentDeleteAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            await _driveService.Files.Delete(id).ExecuteAsync(ct);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<FileItem>> RestoreAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            var metadata = new GoogleFile { Trashed = false };
            var request = _driveService.Files.Update(metadata, id);
            request.Fields = "id,name,mimeType,parents,modifiedTime";
            var updated = await request.ExecuteAsync(ct);
            return GoogleMappers.ToFileItem(updated);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<PageResult<FileItem>>> ListTrashAsync(string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            var request = _driveService.Files.List();
            request.Q = "trashed = true";
            request.Fields = "nextPageToken, files(id,name,mimeType,size,modifiedTime,createdTime,parents,thumbnailLink,imageMediaMetadata,webContentLink,webViewLink,md5Checksum,shared)";
            request.PageToken = pageToken;
            request.PageSize = 200;
            var response = await request.ExecuteAsync(ct);
            return GoogleMappers.ToPageResult(response);
        });
    }

    public Task<StorageResult<bool>> EmptyTrashAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            await _driveService.Files.EmptyTrash().ExecuteAsync(ct);
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

            string parent = string.IsNullOrEmpty(parentId) || parentId == "Root" ? "root" : parentId;
            using Stream stream = await file.OpenStreamForReadAsync();
            ulong size = (await file.GetBasicPropertiesAsync()).Size;

            var metadata = new GoogleFile
            {
                Name = file.Name,
                Parents = new List<string> { parent },
            };

            var upload = _driveService.Files.Create(metadata, stream, "application/octet-stream");
            upload.ChunkSize = UploadChunkSize;
            upload.Fields = "id,name,mimeType,size,parents,modifiedTime,createdTime";

            upload.ProgressChanged += p =>
            {
                progress?.Report(p.BytesSent);
                // Note: Google Drive SDK's resumable upload URI is managed internally;
                // resume after pause re-initiates the upload session.
            };

            IUploadProgress result;
            if (!string.IsNullOrEmpty(resumeToken) && Uri.TryCreate(resumeToken, UriKind.Absolute, out var uri))
            {
                result = await upload.ResumeAsync(uri, ct);
            }
            else
            {
                result = await upload.UploadAsync(ct);
            }

            if (result.Status == UploadStatus.Failed)
            {
                throw new Exception(result.Exception?.Message ?? "Upload failed");
            }

            return GoogleMappers.ToFileItem(upload.ResponseBody);
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

            string tempPath = null;
            Stream uploadStream = content;
            try
            {
                if (!content.CanSeek)
                {
                    tempPath = Path.GetTempFileName();
                    await using (FileStream tempWriteStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await content.CopyToAsync(tempWriteStream, ct).ConfigureAwait(false);
                    }
                    uploadStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                else if (content.Position != 0)
                {
                    content.Seek(0, SeekOrigin.Begin);
                }

                string parent = string.IsNullOrEmpty(parentId) || parentId == "Root" ? "root" : parentId;
                var metadata = new GoogleFile
                {
                    Name = fileName,
                    Parents = new List<string> { parent },
                };

                var upload = _driveService.Files.Create(metadata, uploadStream, "application/octet-stream");
                upload.ChunkSize = UploadChunkSize;
                upload.Fields = "id,name,mimeType,size,parents,modifiedTime,createdTime";
                upload.ProgressChanged += p => progress?.Report(p.BytesSent);

                IUploadProgress result = await upload.UploadAsync(ct);
                if (result.Status == UploadStatus.Failed)
                {
                    throw new Exception(result.Exception?.Message ?? "Upload failed");
                }

                return GoogleMappers.ToFileItem(upload.ResponseBody);
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
            using SemaphoreSlim semaphore = new(MaxConcurrentFileUploadCount, MaxConcurrentFileUploadCount);
            FileItem cloudFolder = await UploadFolderInternalAsync(folder, parentId, folder.Name, totalSize, tracker, overallProgress, detailProgress, semaphore, ct);
            return cloudFolder;
        });
    }

    private async Task<FileItem> UploadFolderInternalAsync(
        StorageFolder folder,
        string parentId,
        string relativeFolderPath,
        ulong totalSize,
        UploadProgressTracker tracker,
        IProgress<long> progress,
        IProgress<FolderUploadProgressInfo> detailProgress,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        var createResult = await CreateFolderAsync(parentId, folder.Name, "rename", ct);
        if (!createResult.IsSuccess) throw new Exception($"CreateFolder failed: {createResult.ErrorMessage}");
        var cloudFolder = createResult.Data;

        var files = await folder.GetFilesAsync();
        IEnumerable<Task> uploadTasks = files.Select(async f =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ulong fileSize = (await f.GetBasicPropertiesAsync()).Size;
                string relativePath = $"{relativeFolderPath}/{f.Name}";
                long currentUploaded = 0;
                object progressLock = new();
                IProgress<long> fileProgress = new Progress<long>(uploadedBytes =>
                {
                    long bytes = Math.Clamp(uploadedBytes, 0, (long)fileSize);
                    long delta;
                    lock (progressLock)
                    {
                        delta = bytes - currentUploaded;
                        if (delta < 0) delta = 0;
                        currentUploaded = bytes;
                    }
                    long totalUploaded = Interlocked.Add(ref tracker.UploadedSize, delta);
                    if (totalSize > 0)
                    {
                        progress?.Report((long)(totalUploaded * 100.0 / totalSize));
                    }
                    detailProgress?.Report(new FolderUploadProgressInfo
                    {
                        FilePath = relativePath,
                        UploadedBytes = (ulong)Math.Max(0, bytes),
                        TotalBytes = fileSize,
                        Completed = bytes >= (long)fileSize,
                    });
                });
                var uploadResult = await UploadFileAsync(f, cloudFolder.Id, fileProgress, null, null, ct);
                if (!uploadResult.IsSuccess) throw new Exception(uploadResult.ErrorMessage);
                detailProgress?.Report(new FolderUploadProgressInfo
                {
                    FilePath = relativePath,
                    UploadedBytes = fileSize,
                    TotalBytes = fileSize,
                    Completed = true,
                });
            }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(uploadTasks);

        IReadOnlyList<StorageFolder> subfolders = await folder.GetFoldersAsync();
        foreach (StorageFolder sub in subfolders)
        {
            string subPath = $"{relativeFolderPath}/{sub.Name}";
            await UploadFolderInternalAsync(sub, cloudFolder.Id, subPath, totalSize, tracker, progress, detailProgress, semaphore, ct);
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

    public Task<StorageResult<ShareLink>> CreateLinkAsync(
        string itemId,
        DateTimeOffset? expiration = null,
        string password = null,
        string type = "view",
        CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            string role = (type == "edit") ? "writer" : "reader";
            var permission = new Google.Apis.Drive.v3.Data.Permission
            {
                Role = role,
                Type = "anyone",
            };
            var request = _driveService.Permissions.Create(permission, id);
            await request.ExecuteAsync(ct);

            var fileRequest = _driveService.Files.Get(id);
            fileRequest.Fields = "webViewLink,webContentLink";
            var file = await fileRequest.ExecuteAsync(ct);

            return new ShareLink
            {
                WebUrl = file.WebViewLink ?? file.WebContentLink,
                HasPassword = false,
                Expiration = null,
                IsShared = true,
            };
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<ShareLink>> GetShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            var fileRequest = _driveService.Files.Get(id);
            fileRequest.Fields = "shared,webViewLink,webContentLink";
            GoogleFile file = await fileRequest.ExecuteAsync(ct);

            var permissionsRequest = _driveService.Permissions.List(id);
            permissionsRequest.Fields = "permissions(id,type,role,expirationTime)";
            var permissions = await permissionsRequest.ExecuteAsync(ct);
            var anyonePermission = permissions?.Permissions?
                .FirstOrDefault(permission => string.Equals(permission.Type, "anyone", StringComparison.OrdinalIgnoreCase));

            bool isShared = anyonePermission != null;
            if (!isShared)
            {
                return new ShareLink { IsShared = false };
            }

            return new ShareLink
            {
                WebUrl = anyonePermission != null ? file.WebViewLink ?? file.WebContentLink : null,
                Token = anyonePermission?.Id,
                Expiration = anyonePermission?.ExpirationTimeDateTimeOffset,
                HasPassword = false,
                IsShared = true,
            };
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> RevokeShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            var permissionsRequest = _driveService.Permissions.List(id);
            permissionsRequest.Fields = "permissions(id,type)";
            var permissions = await permissionsRequest.ExecuteAsync(ct);
            var publicPermissions = permissions?.Permissions?
                .Where(permission => !string.IsNullOrWhiteSpace(permission.Id)
                    && string.Equals(permission.Type, "anyone", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? [];

            foreach (var permission in publicPermissions)
            {
                await _driveService.Permissions.Delete(id, permission.Id).ExecuteAsync(ct);
            }

            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> ConvertToPdfAsync(string itemId, StorageFile destination, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string id = NormalizeId(itemId);
            var fileRequest = _driveService.Files.Get(id);
            fileRequest.Fields = "mimeType";
            var meta = await fileRequest.ExecuteAsync(ct);
            if (!GoogleMappers.IsGoogleNativeDoc(meta.MimeType))
            {
                throw new InvalidOperationException(L("GoogleDrive_PdfConvertOnlyDocs", "PDF conversion is only supported for native Google Docs files."));
            }
            var export = _driveService.Files.Export(id, "application/pdf");
            using Stream fileStream = await destination.OpenStreamForWriteAsync();
            await export.DownloadAsync(fileStream, ct);
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
            string escaped = query.Replace("'", "\\'");
            string q = $"name contains '{escaped}' and trashed = false";
            if (!string.IsNullOrEmpty(scopeParentId) && scopeParentId != "Root")
            {
                q += $" and '{scopeParentId}' in parents";
            }
            var request = _driveService.Files.List();
            request.Q = q;
            request.Fields = "nextPageToken, files(id,name,mimeType,size,modifiedTime,createdTime,parents,thumbnailLink,imageMediaMetadata,webContentLink,webViewLink,md5Checksum,shared)";
            request.PageSize = 200;
            var response = await request.ExecuteAsync(ct);
            return GoogleMappers.ToPageResult(response);
        }, () => ValidateNotEmpty(query, nameof(query)));
    }

    private static string NormalizeId(string id) => (id == "Root" || string.IsNullOrEmpty(id)) ? "root" : id;

    protected override Task EnsureAuthenticatedAsync()
    {
        if (IsAuthenticated && _driveService != null) return Task.CompletedTask;
        return LoginInternalAsync(CancellationToken.None);
    }

    private async Task LoginInternalAsync(CancellationToken ct)
    {
        _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            _clientSecrets,
            Scopes,
            CredentialStoreKey,
            ct,
            _dataStore);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential,
            ApplicationName = ApplicationName,
        });

        var aboutRequest = _driveService.About.Get();
        aboutRequest.Fields = "user(emailAddress,permissionId)";
        var about = await aboutRequest.ExecuteAsync(ct);
        AccountId = about.User?.EmailAddress ?? AccountId;
        if (string.IsNullOrEmpty(DriveId)) DriveId = "root";
        IsAuthenticated = true;
    }

    protected override StorageResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            Google.GoogleApiException gex => HandleGoogleException<T>(gex),
            _ => base.HandleException<T>(exception),
        };
    }

    private StorageResult<T> HandleGoogleException<T>(Google.GoogleApiException ex)
    {
        int statusCode = (int)ex.HttpStatusCode;
        return statusCode switch
        {
            400 => StorageResult<T>.Failure(L("Http_BadRequest", "Bad Request"), StorageErrorType.InvalidRequest, ex),
            401 => StorageResult<T>.Failure(L("Http_Unauthorized", "Unauthorized"), StorageErrorType.Authentication, ex),
            403 => StorageResult<T>.Failure(L("Http_Forbidden", "Forbidden"), StorageErrorType.Forbidden, ex),
            404 => StorageResult<T>.Failure(L("Http_NotFound", "Not Found"), StorageErrorType.NotFound, ex),
            409 => StorageResult<T>.Failure(L("Http_Conflict", "Conflict"), StorageErrorType.Conflict, ex),
            429 => StorageResult<T>.Failure(L("Http_TooManyRequests", "Too Many Requests"), StorageErrorType.ServiceUnavailable, ex),
            507 => StorageResult<T>.Failure(L("Http_InsufficientStorage", "Insufficient Storage"), StorageErrorType.QuotaExceeded, ex),
            503 => StorageResult<T>.Failure(L("Http_ServiceUnavailable", "Service Unavailable"), StorageErrorType.ServiceUnavailable, ex),
            _ => StorageResult<T>.Failure(LF("GoogleDrive_ErrorFormat", "Google Drive error: {0}", ex.Message), StorageErrorType.Unknown, ex),
        };
    }
}
