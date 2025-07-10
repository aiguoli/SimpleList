using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Models;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class DeleteFileViewModel : ObservableObject
{
    public DeleteFileViewModel(FileViewModel file) 
    {
        _drive = file.Drive;
        _file = file;
    }

    [RelayCommand]
    public async Task DeleteFile()
    {
        if (PermanentDelete)
        {
            OneDriveResult<bool> result = await _drive.Provider.PermanentDeleteItem(File.Id);
            if (result.IsSuccess && result.Data)
            {
                Growl.Success(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("DeleteFileSuccess"),
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            } else
            {
                Growl.Error(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("DeleteFileFail"),
                    StaysOpen = false,
                    Message = result.ErrorMessage,
                    Token = "DriveGrowl"
                });
                return;
            }
        } else
        {
            OneDriveResult<bool> result = await _drive.Provider.DeleteItem(File.Id);
            if (result.IsSuccess && result.Data)
            {
                Growl.Success(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("DeleteFileSuccess"),
                    StaysOpen = false,
                    Token = "DriveGrowl"
                });
            }
            else
            {
                Growl.Error(new GrowlInfo
                {
                    Title = Helpers.ResourceHelper.GetLocalized("DeleteFileFail"),
                    StaysOpen = false,
                    Message = result.ErrorMessage,
                    Token = "DriveGrowl"
                });
                return;
            }
        }
        await File.Drive.Refresh();
    }

    private readonly DriveViewModel _drive;
    [ObservableProperty] private bool _permanentDelete;
    [ObservableProperty] private FileViewModel _file;
}
