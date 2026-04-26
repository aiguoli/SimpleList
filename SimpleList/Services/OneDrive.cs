using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.Restore;
using Microsoft.Graph.Drives.Item.Items.Item.SearchWithQ;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;
using SimpleList.Helpers;
using SimpleList.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Services;

public class OneDrive : OneDriveServiceBase
{
    private const int MaxConcurrentFileUploadCount = 3;

    public OneDrive()
    {
        PublicClientApp = App.GetService<IPublicClientApplication>();
    }

    public OneDrive(string driveId, string homeAccountId)
    {
        DriveId = driveId;
        HomeAccountId = homeAccountId;
        PublicClientApp = App.GetService<IPublicClientApplication>();
    }

    public async Task<OneDriveResult<DriveItemCollectionResponse>> GetFiles(string parentId = "Root")
    {
        return await ExecuteAsync(async () =>
        {
            return await graphClient.Drives[DriveId].Items[parentId].Children.GetAsync();
        }, () => ValidateNotEmpty(parentId, nameof(parentId)));
    }

    public async Task<OneDriveResult<ThumbnailSetCollectionResponse>> GetThumbNails(string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            return await graphClient.Drives[DriveId].Items[itemId].Thumbnails.GetAsync();
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<DriveItem>> GetItem(string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            return await graphClient.Drives[DriveId].Items[itemId].GetAsync();
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<Stream>> GetItemContent(string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            return await graphClient.Drives[DriveId].Items[itemId].Content.GetAsync();
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<DriveItem>> CreateFolder(string parentItemId, string folderName, string conflictBehavior = "rename")
    {
        return await ExecuteAsync(async () =>
        {
            var requestBody = new DriveItem
            {
                Name = folderName,
                Folder = new Folder { },
                AdditionalData = new Dictionary<string, object>
                {
                    {
                        "@microsoft.graph.conflictBehavior" , conflictBehavior
                    },
                },
            };
            return await graphClient.Drives[DriveId].Items[parentItemId].Children.PostAsync(requestBody);
        }, () =>
        {
            ValidateNotEmpty(parentItemId, nameof(parentItemId));
            ValidateFileName(folderName, nameof(folderName));
        });
    }

    public async Task<OneDriveResult<DriveItem>> RenameFile(string itemId, string newName)
    {
        return await ExecuteAsync(async () =>
        {
            DriveItem requestBody = new()
            {
                Name = newName,
            };
            return await graphClient.Drives[DriveId].Items[itemId].PatchAsync(requestBody);
        }, () =>
        {
            ValidateNotEmpty(itemId, nameof(itemId));
            ValidateFileName(newName, nameof(newName));
        });
    }

    public async Task<OneDriveResult<DriveItem>> UploadFileAsync(StorageFile file, string itemId, IProgress<long> progress = null, string uploadUrl = null, Action<string> uploadUrlCallback = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            ValidateNotNull(file, nameof(file));
            ValidateNotEmpty(itemId, nameof(itemId));

            Stream stream = await file.OpenStreamForReadAsync();
            if ((await file.GetBasicPropertiesAsync()).Size == 0)
            {
                // Upload an empty file
                await graphClient.Drives[DriveId].Items[itemId].ItemWithPath(file.Name).Content.PutAsync(new MemoryStream(), null, cancellationToken);
                return await graphClient.Drives[DriveId].Items[itemId].ItemWithPath(file.Name).GetAsync(null, cancellationToken);
            }

            LargeFileUploadTask<DriveItem> fileUploadTask;
            int maxChunckSize = 320 * 1024;

            if (!string.IsNullOrEmpty(uploadUrl))
            {
                var uploadSession = new UploadSession { UploadUrl = uploadUrl };
                fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunckSize);
            }
            else
            {
                var uploadSessionRequestBody = new Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession.CreateUploadSessionPostRequestBody
                {
                    Item = new DriveItemUploadableProperties
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "replace" },
                        },
                    },
                };
                
                UploadSession uploadSession = await graphClient.Drives[DriveId].Items[itemId].ItemWithPath(file.Name).CreateUploadSession.PostAsync(uploadSessionRequestBody, null, cancellationToken);
                if (uploadSession != null)
                {
                    uploadUrlCallback?.Invoke(uploadSession.UploadUrl);
                    fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxChunckSize);
                }
                else
                {
                    throw new Exception("Failed to create upload session.");
                }
            }

            var uploadResult = await fileUploadTask.UploadAsync(progress, 3, cancellationToken);
            if (uploadResult == null)
            {
                throw new Exception("Upload failed.");
            }
            return uploadResult.ItemResponse;
        });

        // Handle exceptions from UploadAsync which can happen if cancelled or network error
        // ExecuteAsync wrapper handles exceptions, but for UploadAsync it might return null result if cancelled?
        // LargeFileUploadTask.UploadAsync returns UploadResult<T>.
        // If it fails, it might throw.
    }

    public async Task<OneDriveResult<DriveItem>> UploadFolderAsync(StorageFolder folder, string itemId, IProgress<long> progress = null, IProgress<FolderUploadProgressInfo> detailProgress = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            ValidateNotNull(folder, nameof(folder));
            ValidateNotEmpty(itemId, nameof(itemId));

            ulong totalSize = await Utils.GetFolderSize(folder);
            UploadProgressTracker tracker = new();
            using SemaphoreSlim uploadSemaphore = new(MaxConcurrentFileUploadCount, MaxConcurrentFileUploadCount);
            return await UploadFolderInternalAsync(folder, itemId, folder.Name, totalSize, tracker, progress, detailProgress, uploadSemaphore, cancellationToken);
        });
    }

    private async Task<DriveItem> UploadFolderInternalAsync(
        StorageFolder folder,
        string itemId,
        string relativeFolderPath,
        ulong totalSize,
        UploadProgressTracker tracker,
        IProgress<long> progress,
        IProgress<FolderUploadProgressInfo> detailProgress,
        SemaphoreSlim uploadSemaphore,
        CancellationToken cancellationToken)
    {
        var createFolderResult = await CreateFolder(itemId, folder.Name, "replace");
        if (!createFolderResult.IsSuccess)
        {
            throw new Exception($"{"CreateFolderFail".GetLocalized()}: {createFolderResult.ErrorMessage}");
        }

        DriveItem cloudFolder = createFolderResult.Data;
        if (cloudFolder == null)
        {
            throw new Exception("Failed to create or retrieve cloud folder.");
        }

        var files = await folder.GetFilesAsync();
        var existingItemsResult = await graphClient.Drives[DriveId].Items[cloudFolder.Id].Children.GetAsync();
        var existingItems = existingItemsResult?.Value ?? new List<DriveItem>();

        IEnumerable<Task> uploadTasks = files.Select(async file =>
        {
            await uploadSemaphore.WaitAsync(cancellationToken);
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
                        Completed = true
                    });
                    return;
                }

                long currentUploaded = 0;
                object progressLock = new();
                System.IProgress<long> fileProgress = new System.Progress<long>(bytes =>
                {
                    long delta;
                    lock (progressLock)
                    {
                        delta = bytes - currentUploaded;
                        if (delta < 0)
                        {
                            delta = 0;
                        }
                        currentUploaded = bytes;
                    }

                    long totalUploaded = Interlocked.Add(ref tracker.UploadedSize, delta);
                    ReportOverallProgress(progress, totalSize, totalUploaded);

                    detailProgress?.Report(new FolderUploadProgressInfo
                    {
                        FilePath = relativePath,
                        UploadedBytes = (ulong)Math.Max(0, bytes),
                        TotalBytes = fileSize,
                        Completed = bytes >= (long)fileSize
                    });
                });

                var uploadResult = await UploadFileAsync(file, cloudFolder.Id, fileProgress, null, null, cancellationToken);
                if (!uploadResult.IsSuccess)
                {
                    throw new Exception($"{"UploadFileFail".GetLocalized()}: {uploadResult.ErrorMessage}");
                }

                long remain;
                lock (progressLock)
                {
                    remain = (long)fileSize - currentUploaded;
                    if (remain < 0)
                    {
                        remain = 0;
                    }
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
                    Completed = true
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
            await UploadFolderInternalAsync(subfolder, cloudFolder.Id, subfolderPath, totalSize, tracker, progress, detailProgress, uploadSemaphore, cancellationToken);
        }

        return cloudFolder;
    }

    private class UploadProgressTracker
    {
        public long UploadedSize;
    }

    private static void ReportOverallProgress(IProgress<long> progress, ulong totalSize, long uploadedSize)
    {
        if (totalSize == 0)
        {
            return;
        }

        long percent = (long)(Math.Min(uploadedSize, (long)totalSize) * 100.0 / totalSize);
        progress?.Report(percent);
    }

    public async Task<OneDriveResult<string>> CreateLink(string itemId, DateTimeOffset? expirationDateTime = null, string password = null, string type = "view")
    {
        return await ExecuteAsync(async () =>
        {
            Microsoft.Graph.Drives.Item.Items.Item.CreateLink.CreateLinkPostRequestBody requestBody = new()
            {
                Type = type,
                Password = password,
                Scope = "anonymous",
                RetainInheritedPermissions = false,
                ExpirationDateTime = expirationDateTime,
            };
            Permission result = await graphClient.Drives[DriveId].Items[itemId].CreateLink.PostAsync(requestBody);
            return result.Link.WebUrl;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<string>> GetDisplayName()
    {
        return await ExecuteAsync(async () =>
        {
            User user = await graphClient.Me.GetAsync();
            return user.DisplayName;
        });
    }

    public async Task<OneDriveResult<Quota>> GetStorageInfo()
    {
        return await ExecuteAsync(async () =>
        {
            Drive drive = await graphClient.Drives[DriveId].GetAsync();
            return drive.Quota;
        });
    }

    public async Task<OneDriveResult<bool>> ConvertFileFormat(string itemId, StorageFile file, string format = "pdf")
    {
        return await ExecuteAsync(async () =>
        {
            Stream result = await graphClient.Drives[DriveId].Items[itemId].Content.GetAsync(requestConfiguration =>
            {
                // The document is written like this, but there is an error. Upon reviewing the source code, I found that there is no definition for "QueryParameters."
                // requestConfiguration.QueryParameters.Format = format;
                requestConfiguration.Headers.Add("Format", format);
            });
            using Stream fileStream = await file.OpenStreamForWriteAsync();
            if (result.CanSeek)
            {
                result.Seek(0, SeekOrigin.Begin);
            }
            await result.CopyToAsync(fileStream);
            return true;
        }, () =>
        {
            ValidateNotEmpty(itemId, nameof(itemId));
            ValidateNotNull(file, nameof(file));
            ValidateNotEmpty(format, nameof(format));
        });
    }

    public async Task<OneDriveResult<bool>> DeleteItem(string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            await graphClient.Drives[DriveId].Items[itemId].DeleteAsync();
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<bool>> PermanentDeleteItem(string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            await graphClient.Drives[DriveId].Items[itemId].PermanentDelete.PostAsync();
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<DriveItem>> RestoreItem(string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            // Restore the original name by default.
            RestorePostRequestBody requestBody = new()
            {
                ParentReference = new ItemReference
                {
                    Id = itemId,
                },
            };
            return await graphClient.Drives[DriveId].Items[itemId].Restore.PostAsync(requestBody);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public async Task<OneDriveResult<SearchWithQGetResponse>> SearchLocalItems(string query, string itemId)
    {
        return await ExecuteAsync(async () =>
        {
            // According to code, Microsoft.Graph.Drives.Item.Items.Item.SearchWithQ.SearchWithQResponse
            // is same as Microsoft.Graph.Drives.Item.SearchWithQ.SearchWithQResponse, so why Microsoft do this?
            return await graphClient.Drives[DriveId].Items[itemId].SearchWithQ(query).GetAsSearchWithQGetResponseAsync();
        }, () =>
        {
            ValidateNotEmpty(query, nameof(query));
            ValidateNotEmpty(itemId, nameof(itemId));
        });
    }

    public async Task<OneDriveResult<Microsoft.Graph.Drives.Item.SearchWithQ.SearchWithQGetResponse>> SearchGlobalItems(string query)
    {
        return await ExecuteAsync(async () =>
        {
            return await graphClient.Drives[DriveId].SearchWithQ(query).GetAsSearchWithQGetResponseAsync();
        }, () => ValidateNotEmpty(query, nameof(query)));
    }

    private class TokenProvider : IAccessTokenProvider
    {
        private readonly Func<string[], Task<string>> getTokenDelegate;
        private readonly string[] scopes;

        public TokenProvider(Func<string[], Task<string>> getTokenDelegate, string[] scopes)
        {
            this.getTokenDelegate = getTokenDelegate;
            this.scopes = scopes;
        }

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = default,
            CancellationToken cancellationToken = default)
        {
            return getTokenDelegate(scopes);
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }
    }

    public async Task<OneDriveResult<bool>> Login()
    {
        try
        {
            await LoginInternal();
            return OneDriveResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return OneDriveResult<bool>.Failure($"{"LoginFail".GetLocalized()}: {ex.Message}", OneDriveErrorType.Authentication, ex);
        }
    }

    private async Task LoginInternal()
    {
        TokenProvider tokenProvider = new(async Task<string> (string[] scopes) =>
        {
            IEnumerable<IAccount> accounts = await PublicClientApp.GetAccountsAsync().ConfigureAwait(false);

            try
            {
                authResult = await PublicClientApp
                                .AcquireTokenSilent(scopes, accounts.First(account => account.HomeAccountId.Identifier == HomeAccountId))
                                .ExecuteAsync();
            }
            catch (Exception exception) when (exception is MsalUiRequiredException || exception is InvalidOperationException)
            {
                try
                {
                    authResult = await PublicClientApp.AcquireTokenInteractive(scopes).ExecuteAsync();
                }
                catch (MsalException msalex)
                {
                    Console.WriteLine(msalex);
                }
                catch (Exception odataEx)
                {
                    Debug.WriteLine($"OData Error: {odataEx}");
                }
            }
            if (authResult != null)
            {
                IsAuthenticated = true;
            }
            HomeAccountId = authResult?.Account.HomeAccountId.Identifier;
            return authResult?.AccessToken;
        }, scopes);
        BaseBearerTokenAuthenticationProvider authProvider = new(tokenProvider);
        graphClient = new(authProvider);
        await Task.FromResult(graphClient);
        SaveTokenCache();
        try
        {
            Drive driveItem = await graphClient.Me.Drive.GetAsync();
            DriveId = driveItem.Id;
        }
        catch
        {
                
        }
    }

    public static void SaveTokenCache()
    {
        MsalCacheHelper cacheHelper = App.GetService<MsalCacheHelper>();
        cacheHelper.RegisterCache(PublicClientApp.UserTokenCache);
    }

    private static IPublicClientApplication PublicClientApp;
    private readonly string[] scopes = ["User.Read", "Files.ReadWrite.All"];
    private static AuthenticationResult authResult;
    private GraphServiceClient graphClient;

    public string DriveId;
    public bool IsAuthenticated = false;
    public string ClientId;
    // used for identify account for now
    public string HomeAccountId;

    protected override async Task EnsureAuthenticatedAsync()
    {
        if (!IsAuthenticated)
        {
            await LoginInternal();
        }
    }
}
