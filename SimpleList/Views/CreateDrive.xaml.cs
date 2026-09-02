using Microsoft.UI.Xaml.Controls;
using SimpleList.Core.Models;
using SimpleList.ViewModels;
using System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SimpleList.Views
{
    public sealed partial class CreateDrive : ContentDialog
    {
        public CreateDrive()
        {
            InitializeComponent();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (DataContext is not CreateDriveViewModel vm)
            {
                return;
            }

            args.Cancel = true;
            bool created;
            if (vm.SelectedProviderType == ProviderType.Local)
            {
                created = await vm.CreateLocalDriveAsync();
            }
            else
            {
                created = await vm.CreateDrive();
            }

            if (created)
            {
                Hide();
            }
        }

        private async void BrowseLocalPath_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (DataContext is not CreateDriveViewModel vm)
            {
                return;
            }

            FolderPicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.StartupWindow));

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                vm.LocalPath = folder.Path;
            }
        }

        private void PikPakPassword_PasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (DataContext is CreateDriveViewModel vm && sender is PasswordBox passwordBox)
            {
                vm.PikPakPassword = passwordBox.Password;
            }
        }
    }
}
