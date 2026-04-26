using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SimpleList.Models;
using SimpleList.ViewModels;
using SimpleList.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SimpleList.Pages
{
    public sealed partial class DrivePage : Page
    {
        public DrivePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is DriveViewModel drive)
            {
                DataContext = drive;
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await drive.GetFiles();
                });
            }
        }

        private async void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbItem>;
            for (int i = items.Count - 1; i >= args.Index + 1; i--)
            {
                items.RemoveAt(i);
            }
            string itemId = (args.Item as BreadcrumbItem).ItemId;
            await (DataContext as DriveViewModel).GetFiles(itemId);
        }

        private async void CreateFolderDialogAsync(object sender, RoutedEventArgs e)
        {
            CreateFolderView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new CreateFolderViewModel(DataContext as DriveViewModel)
            };
            await dialog.ShowAsync();
        }

        private async void DropToUpload(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
                DriveViewModel driveViewModel = (DriveViewModel)DataContext;

                TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
                var tasks = items.Select(item => manager.AddUploadTask(driveViewModel, driveViewModel.ParentItemId, item));
                await Task.WhenAll(tasks);
                await driveViewModel.Refresh();
            }
        }

        private void DisplayCopyIcon(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private void ChangeLayout(object sender, RoutedEventArgs e)
        {
            string layout = ((MenuFlyoutItem)sender).Tag.ToString();
            CloudControl.ContentTemplate = Resources[$"{layout}ViewTemplate"] as DataTemplate;
        }

        private async void BackToLastFolder(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbItem>;
            if (items.Count <= 1)
            {
                return;
            }
            items.RemoveAt(items.Count - 1);
            await (DataContext as DriveViewModel).GetFiles(items.Last().ItemId);
        }

        private async void ShowSearchDialogAsync(object sender, RoutedEventArgs e)
        {
            SearchView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new SearchViewModel(DataContext as DriveViewModel)
            };
            await dialog.ShowAsync();
        }

        private async void ShowUploadFileDialogAsync(object sender, RoutedEventArgs e)
        {
            var fileTypeFilter = new List<string> { "*" };
            Windows.Storage.Pickers.FileOpenPicker picker = new()
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            foreach (var filter in fileTypeFilter)
            {
                picker.FileTypeFilter.Add(filter);
            }

            // Initialize the picker with the current window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.StartupWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                DriveViewModel driveViewModel = (DriveViewModel)DataContext;
                TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
                var tasks = files.Select(file => manager.AddUploadTask(driveViewModel, driveViewModel.ParentItemId, file));
                await Task.WhenAll(tasks);
                await driveViewModel.Refresh();
            }
        }

        private async void ShowUploadFolderDialogAsync(object sender, RoutedEventArgs e)
        {
            Windows.Storage.Pickers.FolderPicker picker = new()
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");

            // Initialize the picker with the current window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.StartupWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                DriveViewModel driveViewModel = (DriveViewModel)DataContext;
                TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
                await manager.AddUploadTask(driveViewModel, driveViewModel.ParentItemId, folder);
                await driveViewModel.Refresh();
            }
        }

        private void SplitButton_Click(object sender, Microsoft.UI.Xaml.Controls.SplitButtonClickEventArgs e)
        {
            // Default action - upload files
            ShowUploadFileDialogAsync(sender, new RoutedEventArgs());
        }
    }
}
