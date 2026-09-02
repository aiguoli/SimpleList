using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class RenameFileViewModel : ObservableObject
{
    public RenameFileViewModel(DriveViewModel drive, FileViewModel file)
    {
        Drive = drive;
        _file = file;
        FileName = file.Name;
    }

    [RelayCommand]
    private async Task RenameFile()
    {
        StorageResult<FileItem> result = await Drive.Provider.RenameAsync(_file.Id, FileName);
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

    [ObservableProperty]
    public partial string FileName { get; set; }

    private readonly FileViewModel _file;
    public DriveViewModel Drive { get; }
}
