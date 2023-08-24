using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Views
{
    public sealed partial class FileView : UserControl
    {
        public FileView()
        {
            InitializeComponent();
        }

        private async void ShowRenameFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            RenameFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new RenameFileViewModel(viewModel.Cloud, viewModel)
            };
            await dialog.ShowAsync();
        }

        private async void ShowPropertyDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            PropertyView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new PropertyViewModel(viewModel)
            };
            await dialog.ShowAsync();
        }

        private async void ShowShareFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            ShareFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new ShareFileViewModel(viewModel)
            };
            await dialog.ShowAsync();
        }

        private void OpenFolder(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel.IsFolder)
            {
                viewModel.Cloud.OpenFolder(viewModel);
            }
        }

        private async void ShowConverFiletDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel.IsFile)
            {
                ConvertFileFormatView dialog = new()
                {
                    XamlRoot = XamlRoot,
                    DataContext = new ConvertFileFormatViewModel(viewModel)
                };
                await dialog.ShowAsync();
            }
        }
    }
}
