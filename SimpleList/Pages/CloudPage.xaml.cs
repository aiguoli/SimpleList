using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using SimpleList.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using SimpleList.Models;

namespace SimpleList.Pages
{
    public sealed partial class CloudPage : Page
    {
        public CloudPage()
        {
            InitializeComponent();
            DataContext = new CloudViewModel();
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
            string displayName = (sender as Grid).Tag.ToString();
            DriveViewModel drive = (DataContext as CloudViewModel).GetDrive(displayName);
            Type pageType = Type.GetType("SimpleList.Pages.DrivePage");
            (App.StartupWindow as MainWindow).Navigate(pageType, drive);
        }
    }
}
