using CommunityToolkit.Authentication;
using CommunityToolkit.Graph.Extensions;
using Microsoft.Graph;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimpleList.Services
{
    public class OneDrive
    {
        public async Task<IDriveItemChildrenCollectionPage> GetFiles(string itemId = "Root")
        {
            GraphServiceClient graphClient = _provider.GetClient();
            return await graphClient.Me.Drive.Items[itemId].Children.Request().GetAsync();
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

        private IProvider _provider = ProviderManager.Instance.GlobalProvider;
    }
}
