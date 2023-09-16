using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml;
using SimpleList.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class CloudViewModel : ObservableObject
    {
        public CloudViewModel()
        {
            Drive = Ioc.Default.GetService<OneDrive>();
            BreadcrumbItems.Add(new BreadcrumbItem { Name = "Home", ItemId = "Root" });
        }

        public async Task GetFiles(string itemId = "Root")
        {
            IsLoading = Visibility.Visible;
            _parentItemId = itemId;
            DriveItemCollectionResponse files = await Drive.GetFiles(_parentItemId);
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

        public ObservableCollection<FileViewModel> Files { get; } = new();
        public ObservableCollection<FileViewModel> Images { get; } = new();
        public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = new();
        public FileViewModel SelectedItem { get; set; }
        public string ParentItemId => _parentItemId;
        public OneDrive Drive { get; }
    }

    public class BreadcrumbItem
    {
        public string Name { get; set; }
        public string ItemId { get; set; }
    }
}
