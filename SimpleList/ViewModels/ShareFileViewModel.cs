using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using SimpleList.Core.Models;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class ShareFileViewModel : ObservableObject
{
    public ShareFileViewModel(FileViewModel file)
    {
        _file = file;
        ShareStatus = file.ShareStatusText;
    }

    public async Task LoadCurrentShareStatusAsync()
    {
        IsLoadingShareStatus = true;
        IsUpdatingShare = true;
        StorageResult<ShareLink> result;
        try
        {
            result = await _file.Drive.Provider.GetShareLinkAsync(_file.Id);
        }
        finally
        {
            IsLoadingShareStatus = false;
            IsUpdatingShare = false;
        }

        if (!result.IsSuccess)
        {
            ShareStatus = Helpers.ResourceHelper.GetLocalized("ShareFile_StatusUnavailable");
            return;
        }

        ShareLink existing = result.Data;
        bool isShared = existing?.IsShared == true;
        IsShared = isShared;
        _file.UpdateShareStatus(isShared);
        ShareStatus = isShared
            ? Helpers.ResourceHelper.GetLocalized("ShareFile_StatusShared")
            : Helpers.ResourceHelper.GetLocalized("ShareFile_StatusNotShared");

        if (isShared && existing != null)
        {
            ExpirationDateTime = existing.Expiration ?? ExpirationDateTime;
            ShareLink = existing.WebUrl;
            Finished = !string.IsNullOrWhiteSpace(ShareLink);
            if (Finished)
            {
                await GenerateQRCode();
            }
        }
    }

    [RelayCommand]
    private async Task<string> ShareFile()
    {
        if (IsUpdatingShare)
        {
            return "";
        }

        IsUpdatingShare = true;
        try
        {
            StorageResult<ShareLink> result = await _file.Drive.Provider.CreateLinkAsync(_file.Id, ExpirationDateTime, Password, Type == 0 ? "view" : "edit");
            if (result.IsSuccess)
            {
                ShareLink = result.Data?.WebUrl;
                IsShared = true;
                _file.UpdateShareStatus(true);
                ShareStatus = Helpers.ResourceHelper.GetLocalized("ShareFile_StatusShared");
                Finished = !string.IsNullOrWhiteSpace(ShareLink);
                if (Finished)
                {
                    await GenerateQRCode();
                }
                return ShareLink;
            }

            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = result.ErrorMessage,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
            Finished = false;
            return "";
        }
        finally
        {
            IsUpdatingShare = false;
        }
    }

    [RelayCommand]
    private async Task RevokeShare()
    {
        if (IsUpdatingShare)
        {
            return;
        }

        IsUpdatingShare = true;
        try
        {
            StorageResult<bool> result = await _file.Drive.Provider.RevokeShareLinkAsync(_file.Id);
            if (!result.IsSuccess)
            {
                Growl.Error(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("Error"),
                    Message = result.ErrorMessage,
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
                return;
            }

            IsShared = false;
            Finished = false;
            ShareLink = null;
            QRCodeImage = null;
            _file.UpdateShareStatus(false);
            ShareStatus = Helpers.ResourceHelper.GetLocalized("ShareFile_StatusNotShared");
        }
        finally
        {
            IsUpdatingShare = false;
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(ShareLink))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(ShareLink);
        Clipboard.SetContent(package);
    }

    private async Task GenerateQRCode()
    {
        if (string.IsNullOrWhiteSpace(ShareLink))
        {
            return;
        }

        QRCodeGenerator qrGenerator = new();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(ShareLink, QRCodeGenerator.ECCLevel.Q);
        BitmapByteQRCode qrCode = new(qrCodeData);
        byte[] qrCodeAsBitmapByteArr = qrCode.GetGraphic(20);

        using InMemoryRandomAccessStream stream = new();
        using (DataWriter writer = new(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(qrCodeAsBitmapByteArr);
            await writer.StoreAsync();
        }
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        QRCodeImage = image;
    }

    private readonly FileViewModel _file;
    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset ExpirationDateTime { get; set; }

    [ObservableProperty]
    public partial int Type { get; set; } = 0;

    [ObservableProperty]
    public partial bool Finished { get; set; } = false;

    [ObservableProperty]
    public partial bool IsLoadingShareStatus { get; set; } = false;

    [ObservableProperty]
    public partial bool IsShared { get; set; } = false;

    [ObservableProperty]
    public partial bool IsUpdatingShare { get; set; } = false;

    [ObservableProperty]
    public partial string ShareStatus { get; set; }

    [ObservableProperty]
    public partial string ShareLink { get; set; }

    [ObservableProperty]
    public partial BitmapImage QRCodeImage { get; set; }

    public static DateTime Today => DateTime.Today;
    public bool SupportsPassword => _file.Drive.Provider.ShareCapabilities.SupportsPassword;
    public bool SupportsExpiration => _file.Drive.Provider.ShareCapabilities.SupportsExpiration;
}
