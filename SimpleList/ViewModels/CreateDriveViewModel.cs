using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using SimpleList.Helpers;
using System;
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
            string normalizedSiteUrl = null;
            if (SelectedProviderType == ProviderType.PikPak
                && (string.IsNullOrWhiteSpace(PikPakUsername)
                    || string.IsNullOrWhiteSpace(PikPakPassword)
                    || (PikPakUsername.Length < 3)))
            {
                ShowCreateDriveError(Helpers.ResourceHelper.GetLocalized("CreateDrive_PikPakInvalidCredentials"));
                return false;
            }

            if (SelectedProviderType == ProviderType.SharePoint
                && !SharePointStorageProvider.TryNormalizeSiteUrl(SharePointSiteUrl, out normalizedSiteUrl))
            {
                ShowCreateDriveError(Helpers.ResourceHelper.GetLocalized("CreateDrive_SharePointInvalidSiteUrl"));
                return false;
            }

            IStorageProvider drive = SelectedProviderType switch
            {
                ProviderType.GoogleDrive => App.CreateGoogleDriveProvider(),
                ProviderType.PikPak => App.CreatePikPakProvider("Root", PikPakUsername, PikPakPassword, PikPakRememberPassword),
                ProviderType.SharePoint => App.CreateSharePointProvider(normalizedSiteUrl),
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
                OperationLogger.Record(
                    "OperationLog_Category_Drive",
                    "OperationLog_Action_DriveAdded",
                    $"{driveName} · {drive.ProviderType}",
                    OperationLogLevel.Success);
                return true;
            }
            else
            {
                OperationLogger.Record(
                    "OperationLog_Category_Drive",
                    "OperationLog_Action_DriveAdded",
                    result.ErrorMessage,
                    OperationLogLevel.Error);
                ShowCreateDriveError(result.ErrorMessage ?? Helpers.ResourceHelper.GetLocalized("CreateDrive_LoginFailed"));
                return false;
            }
        }

        private static string GetDefaultDisplayName(IStorageProvider drive)
        {
            if (drive.ProviderType != ProviderType.Local)
            {
                if (drive is SharePointStorageProvider sharePoint)
                {
                    return string.IsNullOrWhiteSpace(sharePoint.SiteDisplayName)
                        ? new Uri(sharePoint.SiteUrl).Segments[^1].Trim('/')
                        : sharePoint.SiteDisplayName;
                }

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
        public partial string SharePointSiteUrl { get; set; }
        [ObservableProperty]
        public partial int ProviderTypeIndex { get; set; } = 0;

        public ProviderType SelectedProviderType => (ProviderType)ProviderTypeIndex;
        public bool IsLocalProviderSelected => SelectedProviderType == ProviderType.Local;
        public bool IsPikPakProviderSelected => SelectedProviderType == ProviderType.PikPak;
        public bool IsSharePointProviderSelected => SelectedProviderType == ProviderType.SharePoint;

        partial void OnProviderTypeIndexChanged(int value)
        {
            OnPropertyChanged(nameof(SelectedProviderType));
            OnPropertyChanged(nameof(IsLocalProviderSelected));
            OnPropertyChanged(nameof(IsPikPakProviderSelected));
            OnPropertyChanged(nameof(IsSharePointProviderSelected));
        }
    }
}
