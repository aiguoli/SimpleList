using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SimpleList.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage.Streams;
using WinUICommunity;

namespace SimpleList.ViewModels;

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
        OneDriveResult<Stream> result = await _file.Drive.Provider.GetItemContent(_file.Id);
        if (result.IsSuccess)
        {
            Stream stram = result.Data;
            using StreamReader reader = new(stram);
            Text = await reader.ReadToEndAsync();
        }
        else
        {
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = result.ErrorMessage,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
        IsLoading = false;
    }

    [RelayCommand]
    public async Task LoadImageContent()
    {
        IsLoading = true;
        OneDriveResult<Stream> result = await _file.Drive.Provider.GetItemContent(_file.Id);
        if (result.IsSuccess)
        {
            Stream stream = result.Data;
            InMemoryRandomAccessStream randomAccessStream = new();
            await RandomAccessStream.CopyAsync(stream.AsInputStream(), randomAccessStream);
            randomAccessStream.Seek(0);
            BitmapImage img = new();
            await img.SetSourceAsync(randomAccessStream);
            Image = img;
        }
        else
        {
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = result.ErrorMessage,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
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
