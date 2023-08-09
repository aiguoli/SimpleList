using CommunityToolkit.Authentication;
using CommunityToolkit.Graph.Extensions;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Services
{
    public class OneDrive
    {
        public async Task<IDriveItemChildrenCollectionPage> GetFiles(string itemId = "Root")
        {
            GraphServiceClient graphClient = _provider.GetClient();
            return await graphClient.Me.Drive.Items[itemId].Children.Request().GetAsync();
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

        public async Task UploadFileAsync(StorageFile file, string itemId)
        {
            GraphServiceClient graphClient = _provider.GetClient();
            using Stream stream = await file.OpenStreamForReadAsync();
            UploadSession uploadSession = await graphClient.Me.Drive.Items[itemId].ItemWithPath(file.Name).CreateUploadSession().Request().PostAsync();
            int maxChunckSize = 320 * 1024;
            LargeFileUploadTask<DriveItem> fileUploadTask = new(uploadSession, stream, maxChunckSize, graphClient);

            long fileSize = stream.Length;
            await fileUploadTask.UploadAsync();
        }

        public async Task UploadFolderAsync(StorageFolder folder, string itemId)
        {
            var files = await folder.GetFilesAsync();
            DriveItem cloudFolder = await CreateFolder(itemId, folder.Name);
            foreach (var file in files)
            {
                await UploadFileAsync(file, cloudFolder.Id);
            }

            var subfolders = await folder.GetFoldersAsync();
            foreach(var subfolder in subfolders)
            {
                await UploadFolderAsync(subfolder, cloudFolder.Id);
            }
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
