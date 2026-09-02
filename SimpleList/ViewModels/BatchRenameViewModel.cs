using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class BatchRenameViewModel : ObservableObject
{
    public BatchRenameViewModel(DriveViewModel drive)
    {
        Drive = drive;
        foreach (FileViewModel item in drive.SelectedItems)
        {
            _items.Add(item);
        }
        RefreshPreview();
    }

    partial void OnModeIndexChanged(int value) => RefreshPreview();
    partial void OnFindTextChanged(string value) => RefreshPreview();
    partial void OnReplaceTextChanged(string value) => RefreshPreview();

    [RelayCommand]
    private async Task RenameFiles()
    {
        RefreshPreview();
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ShowError(ErrorMessage);
            return;
        }

        int successCount = 0;
        List<string> errors = [];
        foreach (BatchRenamePreviewItem preview in PreviewItems)
        {
            if (!preview.WillRename)
            {
                continue;
            }

            StorageResult<FileItem> result = await Drive.Provider.RenameAsync(preview.File.Id, preview.NewName);
            if (result.IsSuccess)
            {
                successCount++;
            }
            else
            {
                errors.Add($"{preview.OriginalName}: {result.ErrorMessage}");
            }
        }

        if (errors.Count == 0)
        {
            Growl.Success(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Success"),
                Message = string.Format(Helpers.ResourceHelper.GetLocalized("BatchRenameSuccess"), successCount),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
        else
        {
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = string.Join(Environment.NewLine, errors),
                StaysOpen = true,
                Token = "DriveGrowl"
            });
        }

        await Drive.Refresh();
    }

    private void RefreshPreview()
    {
        PreviewItems.Clear();
        ErrorMessage = string.Empty;

        Regex regex = null;
        if (ModeIndex == 1 && !string.IsNullOrEmpty(FindText))
        {
            try
            {
                regex = new Regex(FindText ?? string.Empty);
            }
            catch (ArgumentException ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        foreach (FileViewModel item in _items)
        {
            string newName = item.Name;
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                newName = ModeIndex == 0
                    ? RenameByString(item.Name)
                    : RenameByRegex(item.Name, regex);
            }

            PreviewItems.Add(new BatchRenamePreviewItem(item, newName));
        }
    }

    private static void ShowError(string message)
    {
        Growl.Error(new GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("Error"),
            Message = message,
            StaysOpen = false,
            Token = "DriveGrowl"
        });
    }

    private string RenameByString(string name)
    {
        return string.IsNullOrEmpty(FindText)
            ? name
            : name.Replace(FindText, ReplaceText ?? string.Empty, StringComparison.Ordinal);
    }

    private string RenameByRegex(string name, Regex regex)
    {
        return string.IsNullOrEmpty(FindText)
            ? name
            : regex.Replace(name, ReplaceText ?? string.Empty);
    }

    private readonly List<FileViewModel> _items = [];
    public DriveViewModel Drive { get; }
    public ObservableCollection<BatchRenamePreviewItem> PreviewItems { get; } = [];
    [ObservableProperty] public partial int ModeIndex { get; set; }

    [ObservableProperty] public partial string FindText { get; set; } = string.Empty;

    [ObservableProperty] public partial string ReplaceText { get; set; } = string.Empty;

    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
}

public class BatchRenamePreviewItem(FileViewModel file, string newName)
{
    public FileViewModel File { get; } = file;
    public string OriginalName => File.Name;
    public string NewName { get; } = newName;
    public bool WillRename => !string.Equals(OriginalName, NewName, StringComparison.Ordinal);
}
