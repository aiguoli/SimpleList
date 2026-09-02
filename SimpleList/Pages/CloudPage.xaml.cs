using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Helpers;
using SimpleList.ViewModels;
using SimpleList.Views;
using System;

namespace SimpleList.Pages
{
    public sealed partial class CloudPage : Page
    {
        public CloudPage()
        {
            InitializeComponent();
            DataContext = App.GetService<CloudViewModel>();
            Loaded += async (sender, args) =>
            {
                CloudViewModel viewModel = DataContext as CloudViewModel;
                await viewModel.LoadDrivesFromDisk();
                await viewModel.RefreshDriveSummariesAsync();
            };
        }

        private async void ShowCreateDriveDialogAsync(object sender, RoutedEventArgs e)
        {
            CreateDrive dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new CreateDriveViewModel(DataContext as CloudViewModel)
            };
            await dialog.ShowAsync();
        }

        private void NavigateToDrive(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            DirvePanel.IsItemClickEnabled = false;
            DriveViewModel drive = (sender as Grid)?.Tag as DriveViewModel;
            if (drive == null)
            {
                DirvePanel.IsItemClickEnabled = true;
                return;
            }
            drive.ExitTrashMode();
            (App.StartupWindow as MainWindow)?.Navigate(typeof(DrivePage), drive);
            DirvePanel.IsItemClickEnabled = true;
        }

        private void OpenTrash_Click(object sender, RoutedEventArgs e)
        {
            DriveViewModel drive = sender switch
            {
                Button button => button.CommandParameter as DriveViewModel,
                MenuFlyoutItem item => item.CommandParameter as DriveViewModel,
                _ => null,
            };
            if (drive == null || !drive.CanManageTrash)
            {
                return;
            }

            drive.EnterTrashMode();
            (App.StartupWindow as MainWindow)?.Navigate(typeof(DrivePage), drive);
        }

        private async void RefreshDriveCapacity_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuFlyoutItem)?.CommandParameter is DriveViewModel drive)
            {
                await drive.GetCapacity();
            }
        }

        private async void RemoveDrive_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuFlyoutItem)?.CommandParameter is not DriveViewModel drive
                || DataContext is not CloudViewModel cloud)
            {
                return;
            }

            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = ResourceHelper.GetLocalized("CloudPage_RemoveDriveDialog_Title"),
                Content = string.Format(ResourceHelper.GetLocalized("CloudPage_RemoveDriveDialog_Content"), drive.DisplayName),
                PrimaryButtonText = ResourceHelper.GetLocalized("CloudPage_RemoveDriveDialog_Remove"),
                CloseButtonText = ResourceHelper.GetLocalized("CloudPage_RemoveDriveDialog_Cancel"),
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                cloud.RemoveDrive(drive);
            }
        }
    }
}
