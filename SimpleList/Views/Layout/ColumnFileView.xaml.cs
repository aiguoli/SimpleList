using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SimpleList.Helpers;
using SimpleList.Models;
using SimpleList.Pages.Tools;
using SimpleList.Services;
using SimpleList.ViewModels;
using SimpleList.ViewModels.Tools;
using SimpleList.Views.Preview;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace SimpleList.Views.Layout
{
    public sealed partial class ColumnFileView : UserControl
    {
        public FileViewModel ViewModel => DataContext as FileViewModel;

        public ColumnFileView()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) => Bindings.Update();
        }

        private async void ShowRenameFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel?.CanRename != true)
            {
                return;
            }

            RenameFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new RenameFileViewModel(viewModel.Drive, viewModel)
            };
            await dialog.ShowAsync();
        }

        private async void ShowBatchRenameDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel == null || !viewModel.Drive.CanEditCurrentFolder || viewModel.Drive.SelectedItems.Count < 2)
            {
                return;
            }

            BatchRenameView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new BatchRenameViewModel(viewModel.Drive)
            };
            await dialog.ShowAsync();
        }

        private async void ShowPropertyDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            PropertyView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new PropertyViewModel([..viewModel.Drive.SelectedItems])
            };
            await dialog.ShowAsync();
        }

        private async void ShowShareFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel?.CanShare != true)
            {
                return;
            }

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
            if (viewModel?.CanUseNormalActions != true)
            {
                return;
            }

            if (viewModel.IsFolder)
            {
                await viewModel.Drive.OpenFolder(viewModel);
            } else
            {
                if (viewModel.Drive.Provider.ProviderType == SimpleList.Core.Models.ProviderType.Local)
                {
                    await viewModel.OpenExternallyAsync();
                }
                else
                {
                    await ShowPreviewDialogFromViewModel(viewModel);
                }
            }
        }

        private async void ShowConverFiletDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel.IsFile && viewModel.CanConvert)
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
                DataContext = new DeleteFileViewModel(viewModel.Drive.SelectedItems.ToArray())
            };
            await dialog.ShowAsync();
        }

        private async void ShowDeleteFileDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel?.CanDelete != true)
            {
                return;
            }

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
                            DataContext = new PreviewViewModel(viewModel, viewModel.Drive.Images)
                        };
                        await dialog.ShowAsync();
                        break;
                    }
                case FileType.Media:
                    {
                        MediaPreviewView previewWindow = new()
                        {
                            DataContext = new PreviewViewModel(viewModel)
                        };
                        previewWindow.Show();
                        break;
                    }
            }
        }

        private async void ShowPreviewDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel.Drive.Provider.ProviderType == SimpleList.Core.Models.ProviderType.Local)
            {
                await viewModel.OpenExternallyAsync();
                return;
            }
            await ShowPreviewDialogFromViewModel(viewModel);
        }

        private void CopyFilename(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            CopyText(viewModel.Name);
        }
        private void CopyFileId(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            CopyText(viewModel.Id);
        }

        private async void CopyDownloadUrl(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            string downloadUrl = await viewModel.GetDownloadUrlAsync();
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                CopyText(downloadUrl);
            }
        }

        private async void ShowExternalDownloaderDialogAsync(object sender, RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            if (viewModel?.CanDownloadWithExternalTool != true)
            {
                return;
            }

            var downloadUrls = await viewModel.Drive.GetSelectedDownloadUrlsAsync();
            if (downloadUrls.Count == 0)
            {
                return;
            }

            ExternalDownloader dialogContent = new(new ExternalDownloaderViewModel(downloadUrls))
            {
                MinWidth = 560
            };
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = ResourceHelper.GetLocalized("ExternalDownloaderDialog_Title"),
                Content = dialogContent,
                CloseButtonText = ResourceHelper.GetLocalized("TaskManagerPage_Close")
            };
            await dialog.ShowAsync();
        }

        private static void CopyText(string text)
        {
            DataPackage package = new();
            package.SetText(text);
            Clipboard.SetContent(package);
        }

        //private void OnStartDrag(UIElement sender, DragStartingEventArgs args)
        //{
        //    if ((sender as GridViewItem).DataContext is FileViewModel fileViewModel)
        //    {
        //        file = fileViewModel;
        //    }
        //}
    }

}
