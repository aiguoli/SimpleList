using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using SimpleList.Models;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class CreateFolderViewModel : ObservableObject
{
    public CreateFolderViewModel(DriveViewModel drive)
    {
        Drive = drive;
    }

    [RelayCommand]
    private async Task CreateFolder()
    {
        OneDriveResult<DriveItem> result = await Drive.Provider.CreateFolder(Drive.ParentItemId, FolderName);
        if (result.IsSuccess)
        {
            await Drive.Refresh();
            Growl.Success(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("CreateFolderSuccess"),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        } else
        {
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("CreateFolderFail"),
                Message = result.ErrorMessage,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
    }

    [ObservableProperty] private string _folderName;
    public DriveViewModel Drive;
}
