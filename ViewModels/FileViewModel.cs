using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SimpleList.ViewModels
{
    public class FileViewModel : ObservableObject
    {
        public FileViewModel(CloudViewModel cloud, DriveItem file)
        {
            Cloud = cloud;
            _file = file;
            ItemType = IsFile ? "File" : "Folder";
            DownloadFileCommand = new RelayCommand<string>(DownloadFile);
        }

        private async void DownloadFile(string itemId)
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
            //if (file != null)
            //{
            //    var filePath = file.Path;
            //    var contentStream = await graphClient.Me.Drive.Items[itemId].Content.Request().GetAsync();
            //    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            //    await contentStream.CopyToAsync(fileStream);
            //}
            if (file != null)
            {
                TaskManagerViewModel manager = Ioc.Default.GetService<TaskManagerViewModel>();
                await manager.AddDownloadTask(itemId, file);
            }
        }

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
        public RelayCommand<string> DownloadFileCommand { get; }
    }
}
