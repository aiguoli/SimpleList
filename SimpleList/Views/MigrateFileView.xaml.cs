using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SimpleList.ViewModels;

namespace SimpleList.Views;

public sealed partial class MigrateFileView : ContentDialog
{
    public MigrateFileView()
    {
        InitializeComponent();
    }

    private async void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (DataContext is MigrateFileViewModel vm)
        {
            await vm.NavigateToBreadcrumbAsync(args.Index);
        }
    }

    private async void FolderList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DataContext is not MigrateFileViewModel vm || sender is not ListView listView)
        {
            return;
        }

        if (listView.SelectedItem is FileViewModel folder)
        {
            await vm.OpenFolder(folder);
        }
    }
}
