using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class CreateFolderViewModel : ObservableObject
    {
        public CreateFolderViewModel(CloudViewModel cloud)
        {
            Cloud = cloud;
        }

        [RelayCommand]
        private async Task CreateFolder()
        {
            await Cloud.Drive.CreateFolder(Cloud.ParentItemId, FolderName);
            await Cloud.Refresh();
        }

        [ObservableProperty] private string _folderName;
        public CloudViewModel Cloud;
    }
}
