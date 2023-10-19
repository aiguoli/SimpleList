using CommunityToolkit.Mvvm.ComponentModel;
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

        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
        private async Task SaveDrivesToDisk()
        {
            string jsonData = JsonSerializer.Serialize(Drives);
            string cachePath = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            Directory.CreateDirectory(cachePath);
            await File.WriteAllTextAsync(Path.Combine(cachePath, "drives.json"), jsonData);
        }

        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
        public async Task LoadDrivesFromDisk()
        {
            if (File.Exists(cacheFilePath))
            {
                string jsonData = await File.ReadAllTextAsync(cacheFilePath);
                ObservableCollection<DriveViewModel> drives = JsonSerializer.Deserialize<ObservableCollection<DriveViewModel>>(jsonData);
                foreach (DriveViewModel drive in drives)
                {
                    Drives.Add(drive);
                }
            }
        }

        private string cacheFilePath = Path.Combine(Directory.GetCurrentDirectory(), "cache", "drives.json");
        [ObservableProperty] private ObservableCollection<DriveViewModel> drives = new();
    }
}
