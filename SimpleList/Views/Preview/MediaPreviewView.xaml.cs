using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SimpleList.Views.Preview
{
    public sealed partial class MediaPreviewView : ContentDialog
    {
        public MediaPreviewView()
        {
            InitializeComponent();
        }

        private void LoadDonwloadUrlAsync(object sender, RoutedEventArgs e)
        {
            PreviewViewModel vm = DataContext as PreviewViewModel;
            vm.LoadMediaSource();
        }
    }
}
