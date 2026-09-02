using Microsoft.UI.Xaml.Controls;
using SimpleList.Services;
using SimpleList.ViewModels;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Views.Layout
{
    public sealed partial class ColumnCloudView : UserControl
    {
        public DriveViewModel ViewModel => DataContext as DriveViewModel;

        public ColumnCloudView()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                Bindings.Update();
                ObserveFiles();
            };
            Loaded += (s, e) => SelectPendingBookmarkItem();
        }

        private void ChangeSelectedFiles(object sender, SelectionChangedEventArgs e)
        {
            if ((DataContext as DriveViewModel).SelectedItems == null) return;
            (DataContext as DriveViewModel).SelectedItems.Clear();
            foreach (FileViewModel item in (sender as ListView).SelectedItems.Cast<FileViewModel>())
            {
                (DataContext as DriveViewModel).SelectedItems.Add(item);
            }
        }

        private void StartFileDrag(object sender, DragItemsStartingEventArgs e)
        {
            FileDragDropService.ConfigureDragItems(e, ViewModel);
        }

        private void SelectPendingBookmarkItem()
        {
            if (ViewModel?.PendingSelectedItemId == null)
            {
                return;
            }

            FileViewModel item = ViewModel.Files.FirstOrDefault(file => file.Id == ViewModel.PendingSelectedItemId);
            if (item == null)
            {
                return;
            }

            FileListView.SelectedItem = item;
            FileListView.ScrollIntoView(item);
            ViewModel.PendingSelectedItemId = null;
        }

        private void ObserveFiles()
        {
            if (ViewModel == null || ReferenceEquals(_observedViewModel, ViewModel))
            {
                return;
            }

            if (_observedViewModel != null)
            {
                _observedViewModel.Files.CollectionChanged -= Files_CollectionChanged;
            }

            _observedViewModel = ViewModel;
            _observedViewModel.Files.CollectionChanged += Files_CollectionChanged;
        }

        private void Files_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SelectPendingBookmarkItem();
        }

        private DriveViewModel _observedViewModel;
    }
}
