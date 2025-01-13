using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using SimpleList.Views;
using System;
using System.Diagnostics.CodeAnalysis;

namespace SimpleList.Pages
{
    public sealed partial class CloudPage : Page
    {
        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
        public CloudPage()
        {
            InitializeComponent();
            DataContext = new CloudViewModel();
            Loaded += async (sender, args) =>
            {
                await (DataContext as CloudViewModel).LoadDrivesFromDisk();
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
            string displayName = (sender as Grid).Tag.ToString();
            DriveViewModel drive = (DataContext as CloudViewModel).GetDrive(displayName);
            Type pageType = Type.GetType("SimpleList.Pages.DrivePage");
            (App.StartupWindow as MainWindow).Navigate(pageType, drive);
            DirvePanel.IsItemClickEnabled = true;
        }
    }
}
