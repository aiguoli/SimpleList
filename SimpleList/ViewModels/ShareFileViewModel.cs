﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using SimpleList.Models;
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
    }

    [RelayCommand]
    private async Task<string> ShareFile()
    {
        PreventClose = true;
        OneDriveResult<string> result = await _file.Drive.Provider.CreateLink(_file.Id, ExpirationDateTime, Password, Type == 0 ? "view" : "edit");
        if (result.IsSuccess)
        {
            ShareLink = result.Data;
            await GenerateQRCode();
            Finished = true;
            return ShareLink;
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
            Finished = false;
        }
        return "";
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        DataPackage package = new();
        package.SetText(ShareLink);
        Clipboard.SetContent(package);
    }

    private async Task GenerateQRCode()
    {
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
    [ObservableProperty] private string _password;
    [ObservableProperty] private DateTimeOffset _expirationDateTime;
    [ObservableProperty] private int _type = 0;
    [ObservableProperty] private bool _finished = false;
    [ObservableProperty] private string _shareLink;
    [ObservableProperty] private BitmapImage _QRCodeImage;

    public static DateTime Today => DateTime.Today;
    public bool PreventClose { get; set; } = true;
}
