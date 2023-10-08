using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Services;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class CreateDriveViewModel : ObservableObject
    {
        public CreateDriveViewModel(CloudViewModel cloud)
        {
            _cloud = cloud;
        }

        [RelayCommand]
        public async Task CreateDrive()
        {
            OneDrive drive = new();
            await drive.Login();
            DriveViewModel driveViewModel = new(drive, DisplayName);
            _cloud.AddDrive(driveViewModel);
        }

        private readonly CloudViewModel _cloud;
        [ObservableProperty] private string _displayName;
    }
}
