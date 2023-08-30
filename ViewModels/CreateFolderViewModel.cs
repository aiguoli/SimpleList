using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleList.ViewModels
{
    public class CreateFolderViewModel : ObservableObject
    {
        public CreateFolderViewModel(CloudViewModel cloud)
        {
            Cloud = cloud;
            CreateFolderCommand = new RelayCommand(CreateFolder);
        }

        private async void CreateFolder()
        {
            await Cloud.Drive.CreateFolder(Cloud.ParentItemId, _folderName);
            await Cloud.Refresh();
        }

        private string _folderName;
        public CloudViewModel Cloud;
        public string FolderName
        {
            get => _folderName; 
            set => SetProperty(ref _folderName, value);
        }
        public RelayCommand CreateFolderCommand { get; }
    }
}
