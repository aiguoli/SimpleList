using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.Security;
using Microsoft.UI.Xaml;
using SimpleList.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleList.ViewModels
{
    public class CloudViewModel : ObservableObject
    {
        public CloudViewModel()
        {
            Drive = Ioc.Default.GetService<OneDrive>();
            OpenFolderCommand = new AsyncRelayCommand<FileViewModel>(OpenFolder);
            RefreshCommand = new AsyncRelayCommand(Refresh);
            BreadcrumbItems.Add(new BreadcrumbItem { Name = "Home", ItemId = "Root" });
        }

        public async Task GetFiles(string itemId = "Root")
        {
            IsLoading = Visibility.Visible;
            _parentItemId = itemId;
            DriveItemCollectionResponse files = await Drive.GetFiles(_parentItemId);
            Files.Clear();
            files.Value.ForEach(files => Files.Add(new FileViewModel(this, files)));
            IsLoading = Visibility.Collapsed;
        }

        public async Task Refresh()
        {
            await GetFiles(_parentItemId);
        }

        public async Task OpenFolder(FileViewModel file)
        {
            BreadcrumbItems.Add(new BreadcrumbItem { Name = file.Name, ItemId = file.Id });
            await GetFiles(file.Id);
        }

        private string _parentItemId = "Root";
        private Visibility _isLoading = Visibility.Collapsed;

        public ObservableCollection<FileViewModel> Files { get; } = new();
        public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = new();
        public FileViewModel SelectedItem { get; set; }
        public string ParentItemId => _parentItemId;
        public AsyncRelayCommand<FileViewModel> OpenFolderCommand { get; }
        public AsyncRelayCommand RefreshCommand { get; }
        public Visibility IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public OneDrive Drive { get; }
    }

    public class BreadcrumbItem
    {
        public string Name { get; set; }
        public string ItemId { get; set; }
    }
}
