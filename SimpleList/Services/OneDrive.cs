using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.Restore;
using Microsoft.Graph.Drives.Item.Items.Item.SearchWithQ;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Services
{
    public class OneDrive
    {
        public OneDrive()
        {
            PublicClientApp = Ioc.Default.GetService<IPublicClientApplication>();
        }

        public OneDrive(string driveId, string homeAccountId)
        {
            DriveId = driveId;
            HomeAccountId = homeAccountId;
            PublicClientApp = Ioc.Default.GetService<IPublicClientApplication>();
        }

        public async Task<DriveItemCollectionResponse> GetFiles(string parentId = "Root")
        {
            if (!IsAuthenticated) await Login();
            return await graphClient.Drives[DriveId].Items[parentId].Children.GetAsync();
        }

        public async Task<ThumbnailSetCollectionResponse> GetThumbNails(string itemId)
        {
            return await graphClient.Drives[DriveId].Items[itemId].Thumbnails.GetAsync();
        }

        public async Task<DriveItem> GetItem(string itemId)
        {
            return await graphClient.Drives[DriveId].Items[itemId].GetAsync();
        }

        public async Task<Stream> GetItemContent(string itemId)
        {
            return await graphClient.Drives[DriveId].Items[itemId].Content.GetAsync();
        }

        public async Task<DriveItem> CreateFolder(string parentItemId, string folderName)
        {
            var requestBody = new DriveItem
            {
                Name = folderName,
                Folder = new Folder { },
                AdditionalData = new Dictionary<string, object>
                {
                    {
                        "@microsoft.graph.conflictBehavior" , "rename"
                    },
                },
            };
            return await graphClient.Drives[DriveId].Items[parentItemId].Children.PostAsync(requestBody);
        }

        public async Task<DriveItem> RenameFile(string itemId, string newName)
        {
            DriveItem requestBody = new()
            {
                Name = newName,
            };
            return await graphClient.Drives[DriveId].Items[itemId].PatchAsync(requestBody);
        }

        public async Task UploadFileAsync(StorageFile file, string itemId, IProgress<long> progress = null)
        {
            Stream stream = await file.OpenStreamForReadAsync();
            if ((await file.GetBasicPropertiesAsync()).Size == 0)
            {
                // Upload an empty file
                await graphClient.Drives[DriveId].Items[itemId].ItemWithPath(file.Name).Content.PutAsync(new MemoryStream());
                return;
            }
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
            UploadSession uploadSession = await graphClient.Drives[DriveId].Items[itemId].ItemWithPath(file.Name).CreateUploadSession.PostAsync(uploadSessionRequestBody);
            int maxChunckSize = 320 * 1024;
            LargeFileUploadTask<DriveItem> fileUploadTask = new(uploadSession, stream, maxChunckSize, graphClient.RequestAdapter);

            await fileUploadTask.UploadAsync(progress);
        }

        public async Task UploadFolderAsync(StorageFolder folder, string itemId, IProgress<long> progress = null)
        {
            ulong totalSize = await Utils.GetFolderSize(folder);
            ulong uploadedSize = 0;
            var files = await folder.GetFilesAsync();
            DriveItem cloudFolder = await CreateFolder(itemId, folder.Name);

            IEnumerable<Task> uploadTasks = files.Select(async file =>
            {
                await UploadFileAsync(file, cloudFolder.Id, progress);
                ulong fileSize = (await file.GetBasicPropertiesAsync()).Size;
                Interlocked.Add(ref uploadedSize, fileSize);
                progress?.Report((long)(uploadedSize / totalSize));
            });
            await Task.WhenAll(uploadTasks);

            IReadOnlyList<StorageFolder> subfolders = await folder.GetFoldersAsync();
            IEnumerable<Task> subfolderTasks = subfolders.Select(subfolder => UploadFolderAsync(subfolder, cloudFolder.Id, progress));
            await Task.WhenAll(subfolderTasks);
        }

        public async Task<string> CreateLink(string itemId, DateTimeOffset? expirationDateTime = null, string password = null, string type = "view")
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
        }

        public async Task<string> GetDisplayName()
        {
            User user = await graphClient.Me.GetAsync();
            return user.DisplayName;
        }

        public async Task<Quota> GetStorageInfo()
        {
            if (!IsAuthenticated) await Login();
            Drive drive = await graphClient.Drives[DriveId].GetAsync();
            return drive.Quota;
        }

        public async Task ConvertFileFormat(string itemId, StorageFile file, string format = "pdf")
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
        }

        public async Task DeleteItem(string itemId)
        {
            await graphClient.Drives[DriveId].Items[itemId].DeleteAsync();
        }

        public async Task PermanentDeleteItem(string itemId)
        {
            await graphClient.Drives[DriveId].Items[itemId].PermanentDelete.PostAsync();
        }

        public async Task<DriveItem> RestoreItem(string itemId)
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
        }

        public async Task<SearchWithQGetResponse> SearchLocalItems(string query, string itemId)
        {
            // According to code, Microsoft.Graph.Drives.Item.Items.Item.SearchWithQ.SearchWithQResponse
            // is same as Microsoft.Graph.Drives.Item.SearchWithQ.SearchWithQResponse, so why Microsoft do this?
            return await graphClient.Drives[DriveId].Items[itemId].SearchWithQ(query).GetAsSearchWithQGetResponseAsync();
        }

        public async Task<Microsoft.Graph.Drives.Item.SearchWithQ.SearchWithQGetResponse> SearchGlobalItems(string query)
        {
            return await graphClient.Drives[DriveId].SearchWithQ(query).GetAsSearchWithQGetResponseAsync();
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

        public async Task Login()
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
            MsalCacheHelper cacheHelper = Ioc.Default.GetService<MsalCacheHelper>();
            cacheHelper.RegisterCache(PublicClientApp.UserTokenCache);
        }

        private static IPublicClientApplication PublicClientApp;
        private readonly string[] scopes = new string[] { "User.Read", "Files.ReadWrite.All" };
        private static AuthenticationResult authResult;
        private GraphServiceClient graphClient;

        public string DriveId;
        public bool IsAuthenticated = false;
        public string ClientId;
        // used for identify account for now
        public string HomeAccountId;
    }
}
