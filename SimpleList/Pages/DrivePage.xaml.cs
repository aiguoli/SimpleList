using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using Windows.System;

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

                TaskManagerViewModel manager = Ioc.Default.GetService<TaskManagerViewModel>();
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

        private async void FileListKeyBoardEvents(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    ListView listView = (ListView)sender;
                    FileViewModel folder = (FileViewModel)listView.SelectedItem;
                    if (folder != null && folder.IsFolder)
                    {
                        await (DataContext as DriveViewModel).OpenFolder(folder);
                    }
                    break;
                case VirtualKey.Back:
                    var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbItem>;
                    if (items.Count <= 1)
                    {
                        break;
                    }
                    items.RemoveAt(items.Count - 1);
                    await (DataContext as DriveViewModel).GetFiles(items.Last().ItemId);
                    break;
            }
        }

        private void ChangeLayout(object sender, RoutedEventArgs e)
        {
            string layout = ((MenuFlyoutItem)sender).Tag.ToString();
            CloudControl.ContentTemplate = Resources[$"{layout}ViewTemplate"] as DataTemplate;
        }
    }
}
