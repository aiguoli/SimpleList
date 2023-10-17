using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Models;
using SimpleList.Services;
using SimpleList.ViewModels;
using SimpleList.Views.Preview;
using System;
using System.IO;

namespace SimpleList.Views.Layout
{
    public sealed partial class ColumnFileView : UserControl
    {
        public ColumnFileView()
        {
            InitializeComponent();
        }

        private async void ShowRenameFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            RenameFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new RenameFileViewModel(viewModel.Drive, viewModel)
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

        private async void OpenFolder(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel.IsFolder)
            {
                await viewModel.Drive.OpenFolder(viewModel);
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

        private async void ShowDeleteFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            DeleteFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new DeleteFileViewModel(viewModel)
            };
            await dialog.ShowAsync();
        }

        private async void ShowPreviewDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            switch (Utils.GetFileType(Path.GetExtension(viewModel.Name).ToLower()))
            {
                case FileType.Markdown:
                    {
                        MarkdownPreviewView dialog = new()
                        {
                            XamlRoot = XamlRoot,
                            DataContext = new PreviewViewModel(viewModel)
                        };
                        await dialog.ShowAsync();
                        break;
                    }
                case FileType.Image:
                    {
                        ImagePreviewView dialog = new()
                        {
                            XamlRoot = XamlRoot,
                            DataContext = new PreviewViewModel(viewModel)
                        };
                        await dialog.ShowAsync();
                        break;
                    }
                case FileType.Media:
                    {
                        MediaPreviewView dialog = new()
                        {
                            XamlRoot = XamlRoot,
                            DataContext = new PreviewViewModel(viewModel)
                        };
                        await dialog.ShowAsync();
                        break;
                    }
            }
            
        }
    }

}
