using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using SimpleList.Views;
using System;
using System.Collections.ObjectModel;

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

        private async void CreateFolderDialogAsync(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            CreateFolderView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new CreateFolderViewModel(DataContext as CloudViewModel)
            };
            await dialog.ShowAsync();
        }

        private void DragFileToDownload(ListViewBase sender, DragItemsCompletedEventArgs args)
        {

        }
    }
}
