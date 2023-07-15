using CommunityToolkit.Authentication;
using CommunityToolkit.Graph.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Services;
using SimpleList.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SimpleList.ViewModels
{
    public class CloudViewModel : ObservableObject
    {
        public CloudViewModel()
        {
            Drive = new();
            OpenFolderCommand = new RelayCommand<FileViewModel>(OpenFolder);
            RefreshCommand = new RelayCommand(Refresh);
            BreadcrumbItems.Add(new BreadcrumbItem { Name = "Home", ItemId = "Root" });
            GetFiles();
        }


        public async void GetFiles(string itemId = "Root")
        {
            _isLoading = Visibility.Visible;
            _parentItemId = itemId;
            IProvider provider = ProviderManager.Instance.GlobalProvider;
            GraphServiceClient graphClient = provider.GetClient();
            try
            {
                var files = await graphClient.Me.Drive.Items[_parentItemId].Children.Request().GetAsync();
                Files.Clear();
                foreach (var file in files)
                {
                    Files.Add(new FileViewModel(this, file));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await provider.SignInAsync();
            }
            _isLoading = Visibility.Collapsed;
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


        private string _parentItemId;
        private Visibility _isLoading;
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
