﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml;
using SimpleList.Helpers;
using SimpleList.Models;
using SimpleList.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Microsoft.UI.Dispatching;

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

        [RelayCommand]
        public async Task GetFiles(string itemId = null)
        {
            itemId ??= _parentItemId;
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

        [RelayCommand]
        public async Task SearchFile(string fileName)
        {
            IsLoading = Visibility.Visible;
            var files = await Provider.SearchGlobalItems(fileName);
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
        private async Task DownloadFiles()
        {
            Window _downloadPathSelectWindow = new();
            IntPtr hwnd = WindowNative.GetWindowHandle(_downloadPathSelectWindow);
            FolderPicker savePicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
            };
            InitializeWithWindow.Initialize(savePicker, hwnd);
            StorageFolder folder = await savePicker.PickSingleFolderAsync();
            if (folder != null)
            {
                TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
                var tasks = SelectedItems.Select(i => manager.AddDownloadTask(this, i.Id, folder));
                await Task.WhenAll(tasks);
            }
        }

        public void FilterByName(string name)
        {
            var filesToRemove = Files.Where(file => !file.Name.Contains(name)).ToList();
            foreach (var file in filesToRemove)
            {
                Files.Remove(file);
                Images.Remove(file);
            }
        }

        private string _parentItemId = "Root";
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        [ObservableProperty] private Visibility _isLoading = Visibility.Collapsed;
        [ObservableProperty] private string _storageInfo;

        public ObservableCollection<FileViewModel> Files { get; } = [];
        public ObservableCollection<FileViewModel> Images { get; } = [];
        public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = [];
        public ObservableCollection<FileViewModel> SelectedItems { get; set; } = [];
        public string ParentItemId => _parentItemId;
        public OneDrive Provider { get; }
        public string DisplayName { get; }
    }
}
