using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using SimpleList.Helpers;
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
            OperationLogger.Record("OperationLog_Category_File", "OperationLog_Action_Renamed", $"{_file.Name} → {FileName}", OperationLogLevel.Success);
            Growl.Success(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Success"),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
        else
        {
            OperationLogger.Record("OperationLog_Category_File", "OperationLog_Action_Renamed", $"{_file.Name}: {result.ErrorMessage}", OperationLogLevel.Error);
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
