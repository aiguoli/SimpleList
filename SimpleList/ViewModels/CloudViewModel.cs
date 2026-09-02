using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using SimpleList.Helpers;
using SimpleList.Core.Models.DTO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class CloudViewModel : ObservableObject
    {
        public CloudViewModel()
        {
            Drives.CollectionChanged += async (sender, args) =>
            {
                OnPropertyChanged(nameof(DriveCountText));
                if (_suppressSave) return;
                await SaveDrivesToDisk();
            };
        }

        public void AddDrive(DriveViewModel drive)
        {
            Drives.Add(drive);
            _ = drive.GetCapacity();
        }

        public DriveViewModel GetDrive(string name)
        {
            return Drives.FirstOrDefault(d => d.DisplayName == name);
        }

        public DriveViewModel GetDrive(ProviderType providerType, string accountId, string driveId)
        {
            return Drives.FirstOrDefault(d =>
                d.Provider.ProviderType == providerType
                && string.Equals(d.Provider.AccountId, accountId, StringComparison.Ordinal)
                && string.Equals(d.Provider.DriveId, driveId, StringComparison.Ordinal));
        }

        [RelayCommand]
        public void RemoveDrive(DriveViewModel drive)
        {
            Drives.Remove(drive);
        }

        private async Task SaveDrivesToDisk()
        {
            List<DriveDTO> drives = [];
            foreach (DriveViewModel drive in Drives)
            {
                DriveDTO driveDTO = new()
                {
                    DisplayName = drive.DisplayName,
                    ProviderType = drive.Provider.ProviderType,
                    Provider = new()
                    {
                        HomeAccountId = drive.Provider.AccountId,
                        DriveId = drive.Provider.DriveId,
                        CredentialStoreKey = (drive.Provider as GoogleDriveStorageProvider)?.CredentialStoreKey,
                    }
                };
                drives.Add(driveDTO);
            }
            string jsonData = JsonSerializer.Serialize(drives, DriveDTOSourceGenerationContext.Default.ListDriveDTO);
            string cachePath = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            Directory.CreateDirectory(cachePath);
            await File.WriteAllTextAsync(cacheFilePath, jsonData);
        }

        public async Task LoadDrivesFromDisk()
        {
            if (File.Exists(cacheFilePath) && !isCacheLoaded)
            {
                string jsonData = await File.ReadAllTextAsync(cacheFilePath);
                List<DriveDTO> drives = JsonSerializer.Deserialize(jsonData, DriveDTOSourceGenerationContext.Default.ListDriveDTO);
                _suppressSave = true;
                try
                {
                    foreach (DriveDTO drive in drives)
                    {
                        Core.Contracts.IStorageProvider provider = drive.ProviderType switch
                        {
                            Core.Models.ProviderType.OneDrive => App.CreateOneDriveProvider(drive.Provider.DriveId, drive.Provider.HomeAccountId),
                            Core.Models.ProviderType.GoogleDrive => App.CreateGoogleDriveProvider(
                                drive.Provider.DriveId,
                                drive.Provider.HomeAccountId,
                                drive.Provider.CredentialStoreKey),
                            Core.Models.ProviderType.Local => App.CreateLocalProvider(drive.Provider?.DriveId ?? drive.Provider?.HomeAccountId),
                            Core.Models.ProviderType.PikPak => App.CreatePikPakProvider(drive.Provider?.DriveId ?? "Root", drive.Provider.HomeAccountId),
                            _ => App.CreateOneDriveProvider(drive.Provider.DriveId, drive.Provider.HomeAccountId),
                        };
                        Drives.Add(new DriveViewModel(provider, drive.DisplayName));
                    }
                }
                finally
                {
                    _suppressSave = false;
                }
            }
            isCacheLoaded = true;
        }

        public async Task RefreshDriveSummariesAsync()
        {
            if (Drives.Count == 0)
            {
                IsLoadingDriveInfo = false;
                OnPropertyChanged(nameof(DriveCountText));
                return;
            }

            IsLoadingDriveInfo = true;
            try
            {
                await Task.WhenAll(Drives.Select(drive => drive.GetCapacity()));
            }
            finally
            {
                IsLoadingDriveInfo = false;
                OnPropertyChanged(nameof(DriveCountText));
            }
        }

        private readonly string cacheFilePath = Path.Combine(Directory.GetCurrentDirectory(), "cache", "drives.json");
        private bool isCacheLoaded = false;
        private bool _suppressSave = false;
        public string DriveCountText => string.Format(ResourceHelper.GetLocalized("CloudPage_DriveCount"), Drives.Count);
        [ObservableProperty] public partial bool IsLoadingDriveInfo { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<DriveViewModel> Drives { get; set; } = [];
    }
}
