using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using SimpleList.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class MigrateFileViewModel : ObservableObject
{
    public MigrateFileViewModel(DriveViewModel sourceDrive, IReadOnlyList<FileViewModel> sourceItems)
    {
        SourceDrive = sourceDrive;
        SourceItems = sourceItems;
        Cloud = App.GetService<CloudViewModel>();

        TargetDrive = Cloud.Drives.FirstOrDefault(d => d != sourceDrive) ?? sourceDrive;
        BreadcrumbItems.Add(new BreadcrumbItem { Name = Helpers.ResourceHelper.GetLocalized("RootFileName"), ItemId = "Root" });
        TargetParentId = "Root";
        _ = LoadRootAsync();
    }

    public DriveViewModel SourceDrive { get; }
    public IReadOnlyList<FileViewModel> SourceItems { get; }
    public CloudViewModel Cloud { get; }
    public ObservableCollection<FileViewModel> Folders { get; } = [];
    public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = [];

    [ObservableProperty]
    public partial DriveViewModel TargetDrive { get; set; }

    [ObservableProperty]
    public partial string TargetParentId { get; set; }

    [ObservableProperty]
    public partial string TargetPathText { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    partial void OnTargetDriveChanged(DriveViewModel value)
    {
        if (value != null && BreadcrumbItems.Count > 0)
        {
            _ = LoadRootAsync();
        }
    }

    [RelayCommand]
    public async Task LoadRootAsync()
    {
        if (TargetDrive == null)
        {
            return;
        }

        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new BreadcrumbItem { Name = Helpers.ResourceHelper.GetLocalized("RootFileName"), ItemId = "Root" });
        await LoadFoldersAsync("Root");
    }

    [RelayCommand]
    public async Task OpenFolder(FileViewModel folder)
    {
        if (folder == null || !folder.IsFolder)
        {
            return;
        }

        BreadcrumbItems.Add(new BreadcrumbItem { Name = folder.Name, ItemId = folder.Id });
        await LoadFoldersAsync(folder.Id);
    }

    public async Task NavigateToBreadcrumbAsync(int index)
    {
        if (index < 0 || index >= BreadcrumbItems.Count)
        {
            return;
        }

        while (BreadcrumbItems.Count > index + 1)
        {
            BreadcrumbItems.RemoveAt(BreadcrumbItems.Count - 1);
        }

        await LoadFoldersAsync(BreadcrumbItems[index].ItemId);
    }

    private async Task LoadFoldersAsync(string parentId)
    {
        IsLoading = true;
        TargetParentId = parentId;
        TargetPathText = string.Join("/", BreadcrumbItems.Skip(1).Select(i => i.Name));
        Folders.Clear();

        StorageResult<PageResult<FileItem>> result = await TargetDrive.Provider.ListAllChildrenAsync(parentId);
        if (result.IsSuccess)
        {
            foreach (FileItem folder in result.Data.Items.Where(i => i.IsFolder))
            {
                Folders.Add(new FileViewModel(TargetDrive, folder));
            }
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
        }

        IsLoading = false;
    }
}
