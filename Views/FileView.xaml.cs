using Microsoft.Graph;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

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

        private async void ShowRenameFileDialogAsync(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            RenameFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new RenameFileViewModel(viewModel.Cloud, viewModel)
            };
            await dialog.ShowAsync();
        }

        private async void Grid_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            // It is impossible to drag to download...
            // In WinUI, due to security and privacy restrictions, we cannot obtain the path
            // of the file being dragged to after the drag is completed. Therefore, we cannot
            // asynchronously download the file to a specific location after the drag is
            // completed.
            FileViewModel file = DataContext as FileViewModel;
            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile tempFile = await tempFolder.CreateFileAsync(file.Name, CreationCollisionOption.GenerateUniqueName);

            args.Data.RequestedOperation = DataPackageOperation.Move;
            args.Data.SetStorageItems(new[] { tempFile });
        }
    }
}
