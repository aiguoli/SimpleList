using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SimpleList.Models;
using SimpleList.Services;
using SimpleList.ViewModels;
using SimpleList.Views.Preview;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SimpleList.Views.Layout
{
    public sealed partial class GridFileView : UserControl
    {
        public GridFileView()
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

        private async void OpenFile(object sender, DoubleTappedRoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel.IsFolder)
            {
                await viewModel.Drive.OpenFolder(viewModel);
            }
            else
            {
                await ShowPreviewDialogFromViewModel(viewModel);
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

        private async Task ShowDeleteDialogFromViewModel(FileViewModel viewModel)
        {
            DeleteFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new DeleteFileViewModel(viewModel)
            };
            await dialog.ShowAsync();
        }

        private async void ShowDeleteFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            await ShowDeleteDialogFromViewModel(viewModel);
        }

        private async Task ShowPreviewDialogFromViewModel(FileViewModel viewModel)
        {
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

        private async void ShowPreviewDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            await ShowPreviewDialogFromViewModel(viewModel);
        }
    }

}
