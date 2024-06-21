using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Models.DTO;
using SimpleList.Services;
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
                await SaveDrivesToDisk();
            };
        }

        public void AddDrive(DriveViewModel drive)
        {
            Drives.Add(drive);
        }

        public DriveViewModel GetDrive(string name)
        {
            return Drives.FirstOrDefault(d => d.DisplayName == name);
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
                    Provider = new()
                    {
                        HomeAccountId = drive.Provider.HomeAccountId,
                        DriveId = drive.Provider.DriveId
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
                foreach (DriveDTO drive in drives)
                {
                    OneDrive provider = new(drive.Provider.DriveId, drive.Provider.HomeAccountId);
                    Drives.Add(new DriveViewModel(provider, drive.DisplayName));
                }
            }
            isCacheLoaded = true;
        }

        private readonly string cacheFilePath = Path.Combine(Directory.GetCurrentDirectory(), "cache", "drives.json");
        private bool isCacheLoaded = false;
        [ObservableProperty] private ObservableCollection<DriveViewModel> drives = [];
    }
}
