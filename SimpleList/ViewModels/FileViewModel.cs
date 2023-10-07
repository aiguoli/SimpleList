﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace SimpleList.ViewModels
{
    public partial class FileViewModel : ObservableObject
    {
        public FileViewModel(CloudViewModel cloud, DriveItem file, bool loadThumbnail=false)
        {
            Cloud = cloud;
            _file = file;
            ItemType = IsFile ? "File" : "Folder";
        }

        [RelayCommand]
        private async Task DownloadFile(string itemId)
        {
            Window _downloadPathSelectWindow = new();
            IntPtr hwnd = WindowNative.GetWindowHandle(_downloadPathSelectWindow);
            FileSavePicker savePicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            savePicker.FileTypeChoices.Add("All files", new List<string>() { Path.GetExtension(_file.Name) });
            savePicker.SuggestedFileName = _file.Name;
            InitializeWithWindow.Initialize(savePicker, hwnd);
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                TaskManagerViewModel manager = Ioc.Default.GetService<TaskManagerViewModel>();
                await manager.AddDownloadTask(itemId, file);
            }
        }

        public async Task LoadImage()
        {
            if (IsFile && _file.Image != null)
            {
                using Stream stream = await Cloud.Drive.GetItemContent(_file.Id);
                var randomAccessStream = new InMemoryRandomAccessStream();
                await RandomAccessStream.CopyAsync(stream.AsInputStream(), randomAccessStream);
                randomAccessStream.Seek(0);
                BitmapImage img = new();
                await img.SetSourceAsync(randomAccessStream);
                Image = img;
            }
        }

        [ObservableProperty] private BitmapImage _image;
        private readonly DriveItem _file;

        public string Id { get => _file.Id; }
        public string Name { get => _file.Name; }
        public long? Size { get => _file.Size; }
        public DateTimeOffset? Updated { get => _file.LastModifiedDateTime; }
        public bool IsFile { get => _file.Folder == null; }
        public bool IsFolder { get => !IsFile; }
        public int? ChildrenCount { get => _file.Folder?.ChildCount; }
        public CloudViewModel Cloud { get; }
        public string ItemType { get; }
    }
}