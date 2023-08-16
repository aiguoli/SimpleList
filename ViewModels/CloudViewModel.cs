using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph;
using Microsoft.UI.Xaml;
using SimpleList.Services;
using System.Collections.ObjectModel;

namespace SimpleList.ViewModels
{
    public class CloudViewModel : ObservableObject
    {
        public CloudViewModel()
        {
            Drive = Ioc.Default.GetService<OneDrive>();
            OpenFolderCommand = new RelayCommand<FileViewModel>(OpenFolder);
            RefreshCommand = new RelayCommand(Refresh);
            BreadcrumbItems.Add(new BreadcrumbItem { Name = "Home", ItemId = "Root" });
            // GetFiles();
        }

        public async void GetFiles(string itemId = "Root")
        {
            IsLoading = Visibility.Visible;
            _parentItemId = itemId;
            IDriveItemChildrenCollectionPage files = await Drive.GetFiles(_parentItemId);
            Files.Clear();
            foreach (DriveItem file in files)
            {
                Files.Add(new FileViewModel(this, file));
            }
            IsLoading = Visibility.Collapsed;
        }

        public void Refresh()
        {
            GetFiles(_parentItemId);
        }

        public void OpenFolder(FileViewModel file)
        {
            BreadcrumbItems.Add(new BreadcrumbItem { Name = file.Name, ItemId = file.Id });
            GetFiles(file.Id);
        }

        private string _parentItemId = "Root";
        private Visibility _isLoading = Visibility.Collapsed;
        public ObservableCollection<FileViewModel> Files { get; } = new();
        public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = new();
        public FileViewModel SelectedItem { get; set; }
        public string ParentItemId => _parentItemId;
        public RelayCommand<FileViewModel> OpenFolderCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public Visibility IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public OneDrive Drive { get; }
    }

    public class BreadcrumbItem
    {
        public string Name { get; set; }
        public string ItemId { get; set; }
    }
}
