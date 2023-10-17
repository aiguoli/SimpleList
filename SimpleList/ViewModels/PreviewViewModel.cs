using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace SimpleList.ViewModels
{
    public partial class PreviewViewModel : ObservableObject
    {
        public PreviewViewModel(FileViewModel file)
        {
            _file = file;
        }

        [RelayCommand]
        public async Task LoadTextContent()
        {
            IsLoading = true;
            Stream stram = await _file.Drive.Provider.GetItemContent(_file.Id);
            using StreamReader reader = new(stram);
            Text = await reader.ReadToEndAsync();
            IsLoading = false;
        }

        [RelayCommand]
        public async Task LoadImageContent()
        {
            IsLoading = true;
            Stream stream = await _file.Drive.Provider.GetItemContent(_file.Id);
            InMemoryRandomAccessStream randomAccessStream = new();
            await RandomAccessStream.CopyAsync(stream.AsInputStream(), randomAccessStream);
            randomAccessStream.Seek(0);
            BitmapImage img = new();
            await img.SetSourceAsync(randomAccessStream);
            Image = img;
            IsLoading = false;
        }

        public void LoadMediaSource()
        {
            IsLoading = true;
            string downloadUri = _file.DownloadUrl;
            MediaSource mediaSource = MediaSource.CreateFromUri(new Uri(downloadUri));
            Media = mediaSource;
            IsLoading = false;
        }

        private readonly FileViewModel _file;
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string _text;
        [ObservableProperty] private BitmapImage _image;
        [ObservableProperty] private MediaSource _media;
    }
}
