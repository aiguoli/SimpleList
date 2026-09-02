using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class DeleteFileViewModel : ObservableObject
{
    public DeleteFileViewModel(FileViewModel[] files)
    {
        Files = files;
    }

    [RelayCommand]
    public async Task DeleteFile()
    {
        FileViewModel[] files = Files?.ToArray() ?? [];
        if (files.Length == 0)
        {
            return;
        }

        List<StorageResult<bool>> results = new(files.Length);
        foreach (FileViewModel file in files)
        {
            try
            {
                StorageResult<bool> result = PermanentDelete
                    ? await file.Drive.Provider.PermanentDeleteAsync(file.Id)
                    : await file.Drive.Provider.DeleteAsync(file.Id);
                results.Add(result);
            }
            catch (System.Exception ex)
            {
                results.Add(StorageResult<bool>.Failure(ex.Message, StorageErrorType.Unknown, ex));
            }
        }

        // Refresh only after every item has been attempted so large batches do not
        // replace the collection while deletion is still in progress.
        await Task.WhenAll(files.Select(file => file.Drive).Distinct().Select(drive => drive.Refresh()));

        if (results.All(result => result.IsSuccess && result.Data))
        {
            Growl.Success(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("DeleteFileSuccess"),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
            return;
        }

        Growl.Error(new GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("DeleteFileFail"),
            StaysOpen = false,
            Message = string.Join(", ", results
                .Where(result => !result.IsSuccess || !result.Data)
                .Select(result => result.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))),
            Token = "DriveGrowl"
        });
    }

    public string ConfirmationMessage => Files?.Length == 1
        ? string.Format(Helpers.ResourceHelper.GetLocalized("DeleteFileView_ConfirmationSingle"), Files[0].Name)
        : string.Format(Helpers.ResourceHelper.GetLocalized("DeleteFileView_ConfirmationMultiple"), Files?.Length ?? 0);

    [ObservableProperty]
    public partial bool PermanentDelete { get; set; }
    [ObservableProperty]
    public partial FileViewModel[] Files { get; set; }
}
