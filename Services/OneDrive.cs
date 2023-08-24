using CommunityToolkit.Authentication;
using CommunityToolkit.Graph.Extensions;
using Downloader;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Services
{
    public class OneDrive
    {
        public async Task<IDriveItemChildrenCollectionPage> GetFiles(string parentId = "Root")
        {
            GraphServiceClient graphClient = _provider.GetClient();
            return await graphClient.Me.Drive.Items[parentId].Children.Request().GetAsync();
        }

        public async Task<DriveItem> GetItem(string itemId)
        {
            GraphServiceClient graphClient = _provider.GetClient();
            return await graphClient.Me.Drive.Items[itemId].Request().GetAsync();
        }

        public async Task<DriveItem> CreateFolder(string parentItemId, string folderName)
        {
            GraphServiceClient graphClient = _provider.GetClient();
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
            return await graphClient.Me.Drive.Items[parentItemId].Children.Request().AddAsync(requestBody);
        }

        public async Task<DriveItem> RenameFile(string itemId, string newName)
        {
            GraphServiceClient graphClient = _provider.GetClient();
            DriveItem requestBody = new()
            {
                Name = newName,
            };
            return await graphClient.Me.Drive.Items[itemId].Request().UpdateAsync(requestBody);
        }

        public async Task UploadFileAsync(StorageFile file, string itemId, IProgress<long> progress = null)
        {
            GraphServiceClient graphClient = _provider.GetClient();
            Stream stream = await file.OpenStreamForReadAsync();
            if ((await file.GetBasicPropertiesAsync()).Size == 0)
            {
                // Upload an empty file
                await graphClient.Me.Drive.Items[itemId].ItemWithPath(file.Name).Content.Request().PutAsync<DriveItem>(new MemoryStream());
                return;
            }
            UploadSession uploadSession = await graphClient.Me.Drive.Items[itemId].ItemWithPath(file.Name).CreateUploadSession().Request().PostAsync();
            int maxChunckSize = 320 * 1024;
            LargeFileUploadTask<DriveItem> fileUploadTask = new(uploadSession, stream, maxChunckSize, graphClient);

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

        public async Task<string> CreateLink(string itemId, DateTimeOffset? expirationDateTime=null, string password=null, string type = "view")
        {
            GraphServiceClient graphClient = _provider.GetClient();
            Permission result = await graphClient.Me.Drive.Items[itemId].CreateLink(type, "anonymous", expirationDateTime, password).Request().PostAsync();
            return result.Link.WebUrl;
        }

        public async Task<string> GetDisplayName()
        {
            User user = await _provider.GetClient().Me.Request().GetAsync();
            return user.DisplayName;
        }

        public async Task<Quota> GetStorageInfo()
        {
            GraphServiceClient graphClient = _provider.GetClient();
            Drive drive = await graphClient.Me.Drive.Request().GetAsync();
            return drive.Quota;
        }

        public async Task ConvertFileFormat(string itemId, StorageFile file, string format = "pdf")
        {
            GraphServiceClient graphClient = _provider.GetClient();
            List<QueryOption> queryOptions = new()
            {
                new QueryOption("format", format)
            };
            Stream result = await graphClient.Me.Drive.Items[itemId].Content.Request(queryOptions).GetAsync();
            using var fileStream = System.IO.File.Create(file.Path);
            result.Seek(0, SeekOrigin.Begin);
            await result.CopyToAsync(fileStream);
        }

        public async void Login()
        {
            try
            {
                await _provider.TrySilentSignInAsync();
            }
            catch
            {
                await _provider.SignInAsync();
            }
        }

        private readonly IProvider _provider = ProviderManager.Instance.GlobalProvider;

        // https://github.com/CommunityToolkit/WindowsCommunityToolkit/issues/4910
        // public bool IsAuthenticated => _provider.State == ProviderState.SignedIn;
        public bool IsAuthenticated => _provider.CurrentAccountId != null;
    }
}
