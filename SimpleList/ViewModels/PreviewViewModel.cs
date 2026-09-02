using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SimpleList.Core.Models;
using SimpleList.Models;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage.Streams;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class PreviewViewModel : ObservableObject
{
    public PreviewViewModel(FileViewModel file)
        : this(file, file?.Drive?.Images)
    {
    }

    public PreviewViewModel(FileViewModel file, IEnumerable<FileViewModel> imageFiles)
    {
        _fallbackFile = file;
        _imageFiles = CreateImageFileList(file, imageFiles);
        int selectedIndex = _imageFiles.FindIndex(item => item.Id == file?.Id);
        SelectedImageIndex = selectedIndex >= 0 ? selectedIndex : 0;
        UpdateCurrentImageMetadata();
    }

    [RelayCommand]
    public async Task LoadTextContent()
    {
        IsLoading = true;
        StorageResult<Stream> result = await _fallbackFile.Drive.Provider.GetItemContentAsync(_fallbackFile.Id);
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
        FileViewModel imageFile = CurrentImageFile;
        if (imageFile == null)
        {
            IsLoading = false;
            return;
        }

        int loadVersion = ++_imageLoadVersion;
        Image = null;
        IsLoading = true;
        bool waitForImageOpened = false;
        try
        {
            if (await TryLoadImageThumbnail(imageFile, loadVersion))
            {
                waitForImageOpened = true;
                return;
            }

            await LoadOriginalImageContent(imageFile, loadVersion);
        }
        finally
        {
            if (!waitForImageOpened && loadVersion == _imageLoadVersion)
            {
                IsLoading = false;
            }
        }
    }

    public void CompleteImageLoading()
    {
        IsLoading = false;
    }

    private async Task<bool> TryLoadImageThumbnail(FileViewModel file, int loadVersion)
    {
        StorageResult<ThumbnailSet> result = await file.Drive.Provider.GetThumbnailsAsync(file.Id);
        if (!result.IsSuccess)
        {
            return false;
        }

        string thumbnailUrl = GetPreferredThumbnailUrl(result.Data);
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || !Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out Uri thumbnailUri))
        {
            return false;
        }

        if (loadVersion != _imageLoadVersion)
        {
            return true;
        }

        Image = new BitmapImage
        {
            UriSource = thumbnailUri
        };
        return true;
    }

    private async Task LoadOriginalImageContent(FileViewModel file, int loadVersion)
    {
        StorageResult<Stream> result = await file.Drive.Provider.GetItemContentAsync(file.Id);
        if (result.IsSuccess)
        {
            using Stream stream = result.Data;
            InMemoryRandomAccessStream randomAccessStream = new();
            await RandomAccessStream.CopyAsync(stream.AsInputStream(), randomAccessStream);
            randomAccessStream.Seek(0);
            BitmapImage img = new();
            await img.SetSourceAsync(randomAccessStream);
            if (loadVersion == _imageLoadVersion)
            {
                Image = img;
            }
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
    }

    private static string GetPreferredThumbnailUrl(ThumbnailSet thumbnails)
    {
        return thumbnails?.LargeUrl ?? thumbnails?.MediumUrl ?? thumbnails?.SmallUrl;
    }

    private static List<FileViewModel> CreateImageFileList(FileViewModel file, IEnumerable<FileViewModel> imageFiles)
    {
        List<FileViewModel> images = imageFiles?
            .Where(item => item?.IsFile == true && Utils.GetFileType(Path.GetExtension(item.Name).ToLower()) == FileType.Image)
            .ToList() ?? [];

        bool currentFileIsImage = file?.IsFile == true && Utils.GetFileType(Path.GetExtension(file.Name).ToLower()) == FileType.Image;
        if (currentFileIsImage && !images.Any(item => item.Id == file.Id))
        {
            images.Insert(0, file);
        }

        return images;
    }

    private FileViewModel CurrentImageFile
    {
        get
        {
            if (_imageFiles.Count > 0 && SelectedImageIndex >= 0 && SelectedImageIndex < _imageFiles.Count)
            {
                return _imageFiles[SelectedImageIndex];
            }

            return _fallbackFile;
        }
    }

    private void UpdateCurrentImageMetadata()
    {
        FileViewModel file = CurrentImageFile;
        CurrentImageName = file?.Name;
        CurrentImagePosition = ImagePageCount > 0 ? $"{SelectedImageIndex + 1} / {ImagePageCount}" : "0 / 0";
    }

    partial void OnSelectedImageIndexChanged(int value)
    {
        UpdateCurrentImageMetadata();
        _ = LoadImageContent();
    }

    public async Task LoadMediaSource(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            string downloadUri = await _fallbackFile.GetDownloadUrlAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!string.IsNullOrEmpty(downloadUri) && Uri.TryCreate(downloadUri, UriKind.Absolute, out Uri mediaUri))
            {
                Media = MediaSource.CreateFromUri(mediaUri);
                return;
            }

            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = Helpers.ResourceHelper.GetLocalized("MediaDownloadUrlUnavailable"),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void CleanupMediaPreview()
    {
        Media = null;
    }

    public int ImagePageCount => _imageFiles.Count;
    public int ImagePagerPageCount => Math.Max(1, ImagePageCount);

    private readonly FileViewModel _fallbackFile;
    private readonly List<FileViewModel> _imageFiles;
    private int _imageLoadVersion;
    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial BitmapImage Image { get; set; }

    [ObservableProperty]
    public partial MediaSource Media { get; set; }

    [ObservableProperty]
    public partial int SelectedImageIndex { get; set; }

    [ObservableProperty]
    public partial string CurrentImageName { get; set; }

    [ObservableProperty]
    public partial string CurrentImagePosition { get; set; }
}
