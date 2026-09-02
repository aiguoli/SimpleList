using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SimpleList.Core.Models;
using SimpleList.Services;
using SimpleList.ViewModels;
using SimpleList.Views.Preview;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace SimpleList.Views.Layout
{
    public sealed partial class ImageCloudView : UserControl
    {
        public DriveViewModel ViewModel => DataContext as DriveViewModel;

        public ImageCloudView()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) =>
            {
                ObserveFiles();
            };
            Loaded += (s, e) => SelectPendingBookmarkItem();
        }

        private async void LoadIamgeAsync(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
            {
                FileViewModel file = (FileViewModel)args.Item;
                await file.LoadImage();
            }
        }

        private async void LoadAllImages(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            foreach (FileViewModel image in (ViewModel?.Images?.ToList() ?? new List<FileViewModel>()))
            {
                await image.LoadImage();
            }
        }

        private void ChangeSelectedFiles(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel?.SelectedItems == null) return;
            ViewModel.SelectedItems.Clear();
            foreach (FileViewModel item in (sender as GridView).SelectedItems.Cast<FileViewModel>())
            {
                ViewModel.SelectedItems.Add(item);
            }
        }

        private void StartFileDrag(object sender, DragItemsStartingEventArgs e)
        {
            FileDragDropService.ConfigureDragItems(e, ViewModel);
        }

        private async void OpenImagePreview(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ViewModel?.IsTrashMode == true)
            {
                return;
            }

            FileViewModel file = (e.OriginalSource as FrameworkElement)?.DataContext as FileViewModel;
            if (file == null && FileGridView.SelectedItem is FileViewModel selectedFile)
            {
                file = selectedFile;
            }

            if (file == null)
            {
                return;
            }

            if (file.Drive.Provider.ProviderType == ProviderType.Local)
            {
                await file.OpenExternallyAsync();
                return;
            }

            ImagePreviewView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new PreviewViewModel(file, ViewModel.Images)
            };
            await dialog.ShowAsync();
        }

        private void SelectPendingBookmarkItem()
        {
            if (ViewModel?.PendingSelectedItemId == null)
            {
                return;
            }

            FileViewModel item = ViewModel.Images.FirstOrDefault(file => file.Id == ViewModel.PendingSelectedItemId);
            if (item == null)
            {
                return;
            }

            FileGridView.SelectedItem = item;
            FileGridView.ScrollIntoView(item);
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
                _observedViewModel.Images.CollectionChanged -= Files_CollectionChanged;
            }

            _observedViewModel = ViewModel;
            _observedViewModel.Images.CollectionChanged += Files_CollectionChanged;
        }

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SelectPendingBookmarkItem();
        }

        private DriveViewModel _observedViewModel;
    }
}
