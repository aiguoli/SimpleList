using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml;
using SimpleList.Helpers;
using SimpleList.Models;
using SimpleList.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class DriveViewModel : ObservableObject
    {
        public DriveViewModel(OneDrive provider, string displayName = null)
        {
            DisplayName = displayName ?? provider.DriveId;
            Provider = provider;
            BreadcrumbItems.Add(new BreadcrumbItem { Name = "RootFileName".GetLocalized(), ItemId = "Root" });
        }

        public async Task GetFiles(string itemId = "Root")
        {
            IsLoading = Visibility.Visible;
            _parentItemId = itemId;
            DriveItemCollectionResponse files = await Provider.GetFiles(_parentItemId);
            Files.Clear();
            Images.Clear();
            files.Value.ForEach(file =>
            {
                FileViewModel newFile = new(this, file);
                Files.Add(newFile);
                if (file.Image != null)
                    Images.Add(newFile);
            });
            IsLoading = Visibility.Collapsed;
        }


        [RelayCommand]
        private async Task GetCapacity()
        {
            Quota quota = await Provider.GetStorageInfo();
            StorageInfo = Utils.ReadableFileSize(quota.Used) + " / " + Utils.ReadableFileSize(quota.Total);
        }

        [RelayCommand]
        public async Task Refresh()
        {
            await GetFiles(_parentItemId);
        }

        [RelayCommand]
        public async Task OpenFolder(FileViewModel file)
        {
            BreadcrumbItems.Add(new BreadcrumbItem { Name = file.Name, ItemId = file.Id });
            await GetFiles(file.Id);
        }

        private string _parentItemId = "Root";
        [ObservableProperty] private Visibility _isLoading = Visibility.Collapsed;
        [ObservableProperty] private string _storageInfo;

        public ObservableCollection<FileViewModel> Files { get; } = new();
        public ObservableCollection<FileViewModel> Images { get; } = new();
        public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = new();
        public FileViewModel SelectedItem { get; set; }
        public string ParentItemId => _parentItemId;
        public OneDrive Provider { get; }
        public string DisplayName { get; }
    }
}
