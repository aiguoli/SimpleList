using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System.IO;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels
{
    public partial class CreateDriveViewModel : ObservableObject
    {
        public CreateDriveViewModel(CloudViewModel cloud)
        {
            _cloud = cloud;
        }

        [RelayCommand]
        public async Task<bool> CreateDrive()
        {
            if (SelectedProviderType == ProviderType.PikPak
                && (string.IsNullOrWhiteSpace(PikPakUsername)
                    || string.IsNullOrWhiteSpace(PikPakPassword)
                    || (PikPakUsername.Length < 3)))
            {
                ShowCreateDriveError(Helpers.ResourceHelper.GetLocalized("CreateDrive_PikPakInvalidCredentials"));
                return false;
            }

            IStorageProvider drive = ProviderTypeIndex switch
            {
                1 => App.CreateGoogleDriveProvider(),
                3 => App.CreatePikPakProvider("Root", PikPakUsername, PikPakPassword, PikPakRememberPassword),
                _ => App.CreateOneDriveProvider(),
            };
            return await FinalizeCreateDriveAsync(drive);
        }

        public async Task<bool> CreateLocalDriveAsync(string path = null)
        {
            path ??= LocalPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return await FinalizeCreateDriveAsync(App.CreateLocalProvider(path));
        }

        private async Task<bool> FinalizeCreateDriveAsync(IStorageProvider drive)
        {
            if (drive == null)
            {
                return false;
            }

            var result = await drive.LoginAsync();
            if (result.IsSuccess && drive.IsAuthenticated)
            {
                string driveName = string.IsNullOrWhiteSpace(DisplayName)
                    ? GetDefaultDisplayName(drive)
                    : DisplayName;
                DriveViewModel driveViewModel = new(drive, driveName);
                _cloud.AddDrive(driveViewModel);
                return true;
            }
            else
            {
                ShowCreateDriveError(result.ErrorMessage ?? Helpers.ResourceHelper.GetLocalized("CreateDrive_LoginFailed"));
                return false;
            }
        }

        private static string GetDefaultDisplayName(IStorageProvider drive)
        {
            if (drive.ProviderType != ProviderType.Local)
            {
                return drive.DriveId;
            }

            string path = drive.DriveId?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(path) ? drive.DriveId : Path.GetFileName(path);
        }

        private static void ShowCreateDriveError(string message)
        {
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = message,
                StaysOpen = false,
                Token = "CloudGrowl"
            });
        }

        private readonly CloudViewModel _cloud;
        [ObservableProperty]
        public partial string DisplayName { get; set; }

        [ObservableProperty]
        public partial string LocalPath { get; set; }

        [ObservableProperty]
        public partial string PikPakUsername { get; set; }
        [ObservableProperty]
        public partial string PikPakPassword { get; set; }
        [ObservableProperty]
        public partial bool PikPakRememberPassword { get; set; } = true;
        [ObservableProperty]
        public partial int ProviderTypeIndex { get; set; } = 0;

        public ProviderType SelectedProviderType => (ProviderType)ProviderTypeIndex;
        public bool IsLocalProviderSelected => SelectedProviderType == ProviderType.Local;
        public bool IsPikPakProviderSelected => SelectedProviderType == ProviderType.PikPak;

        partial void OnProviderTypeIndexChanged(int value)
        {
            OnPropertyChanged(nameof(SelectedProviderType));
            OnPropertyChanged(nameof(IsLocalProviderSelected));
            OnPropertyChanged(nameof(IsPikPakProviderSelected));
        }
    }
}
