using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Models;
using System.Linq;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class DeleteFileViewModel : ObservableObject
{
    public DeleteFileViewModel(FileViewModel[] files)
    {
        _files = files;
    }

    [RelayCommand]
    public async Task DeleteFile()
    {
        if (_files.Count() == 0) return;
        if (PermanentDelete)
        {
            OneDriveResult<bool>[] results = await Task.WhenAll(_files.Select(file => file.Drive.Provider.PermanentDeleteItem(file.Id)));
            if (results.All(result => result.IsSuccess && result.Data))
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
                    Message = string.Join(", ", results.Where(result => !result.IsSuccess).Select(result => result.ErrorMessage)),
                    Token = "DriveGrowl"
                });
                return;
            }
        }
        else
        {
            OneDriveResult<bool>[] results = await Task.WhenAll(_files.Select(file => file.Drive.Provider.DeleteItem(file.Id)));
            if (results.All(result => result.IsSuccess && result.Data))
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
                    Message = string.Join(", ", results.Where(result => !result.IsSuccess).Select(result => result.ErrorMessage)),
                    Token = "DriveGrowl"
                });
                return;
            }
        }
        await _files[0].Drive.Refresh();
    }

    [ObservableProperty] private bool _permanentDelete;
    [ObservableProperty] private FileViewModel[] _files;
}
