using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;

namespace SimpleList.Views.Preview
{
    public sealed partial class ImagePreviewView : ContentDialog
    {
        public ImagePreviewView()
        {
            InitializeComponent();
        }

        private void LoadImageContentAsync(object sender, RoutedEventArgs e)
        {
            PreviewViewModel vm = DataContext as PreviewViewModel;
            _ = vm.LoadImageContent();
        }

        private void PreviewImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            (DataContext as PreviewViewModel)?.CompleteImageLoading();
        }

        private void PreviewImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            (DataContext as PreviewViewModel)?.CompleteImageLoading();
        }
    }
}
