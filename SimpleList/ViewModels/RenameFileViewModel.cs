using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using SimpleList.Models;
using System;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class RenameFileViewModel : ObservableObject
{
    public RenameFileViewModel(DriveViewModel drive, FileViewModel file)
    {
        Drive = drive;
        _file = file;
        _fileName = file.Name;
    }

    [RelayCommand]
    private async Task RenameFile()
    {
        OneDriveResult<DriveItem> result = await Drive.Provider.RenameFile(_file.Id, FileName);
        if (result.IsSuccess)
        {
            Growl.Success(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Success"),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
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
            return;
        }
        await Drive.Refresh();
    }

    [ObservableProperty] private string _fileName;
    private readonly FileViewModel _file;
    public DriveViewModel Drive { get; }
}
