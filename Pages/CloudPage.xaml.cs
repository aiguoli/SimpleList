using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using SimpleList.Views;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CloudPage : Page
    {
        public CloudPage()
        {
            InitializeComponent();
            DataContext = new CloudViewModel();
        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbItem>;
            for (int i = items.Count - 1; i >= args.Index + 1; i--)
            {
                items.RemoveAt(i);
            }
            string itemId = (args.Item as BreadcrumbItem).ItemId;
            (DataContext as CloudViewModel).GetFiles(itemId);
        }

        private async void CreateFolderDialogAsync(object sender, RoutedEventArgs e)
        {
            CreateFolderView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new CreateFolderViewModel(DataContext as CloudViewModel)
            };
            await dialog.ShowAsync();
        }

        private async void DropToUpload(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
                CloudViewModel cloudViewModel = (CloudViewModel)DataContext;

                TaskManagerViewModel manager = Ioc.Default.GetService<TaskManagerViewModel>();
                var tasks = items.Select(item => manager.AddUploadTask(cloudViewModel.ParentItemId, item));
                await Task.WhenAll(tasks);
                cloudViewModel.Refresh();
            }
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private void FileListKeyBoardEvents(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    ListView listView = (ListView)sender;
                    FileViewModel folder = (FileViewModel)listView.SelectedItem;
                    if (folder != null && folder.IsFolder)
                    {
                        (DataContext as CloudViewModel).OpenFolder(folder);
                    }
                    break;
                case VirtualKey.Back:
                    var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbItem>;
                    if (items.Count <= 1)
                    {
                        break;
                    }
                    items.RemoveAt(items.Count - 1);
                    (DataContext as CloudViewModel).GetFiles(items.Last().ItemId);
                    break;
            }
        }
    }
}
