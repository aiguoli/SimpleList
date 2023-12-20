using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Identity.Client.Extensions.Msal;
using SimpleList.Models.DTO;
using SimpleList.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class CloudViewModel : ObservableObject
    {
        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
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

        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
        private async Task SaveDrivesToDisk()
        {
            List<DriveDTO> drives = new();
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
            string jsonData = JsonSerializer.Serialize(drives);
            string cachePath = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            Directory.CreateDirectory(cachePath);
            await File.WriteAllTextAsync(cacheFilePath, jsonData);
        }

        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
        public async Task LoadDrivesFromDisk()
        {
            if (File.Exists(cacheFilePath) && !isCacheLoaded)
            {
                string jsonData = await File.ReadAllTextAsync(cacheFilePath);
                ObservableCollection<DriveDTO> drives = JsonSerializer.Deserialize<ObservableCollection<DriveDTO>>(jsonData);
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
        [ObservableProperty] private ObservableCollection<DriveViewModel> drives = new();
    }
}
