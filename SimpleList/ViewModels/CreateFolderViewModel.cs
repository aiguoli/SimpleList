using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class CreateFolderViewModel : ObservableObject
    {
        public CreateFolderViewModel(DriveViewModel drive)
        {
            Drive = drive;
        }

        [RelayCommand]
        private async Task CreateFolder()
        {
            await Drive.Provider.CreateFolder(Drive.ParentItemId, FolderName);
            await Drive.Refresh();
        }

        [ObservableProperty] private string _folderName;
        public DriveViewModel Drive;
    }
}
