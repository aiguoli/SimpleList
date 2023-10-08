using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using System.Linq;

namespace SimpleList.Views.Layout
{
    public sealed partial class ImageCloudView : UserControl
    {
        public ImageCloudView()
        {
            InitializeComponent();
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
            foreach (FileViewModel image in (DataContext as DriveViewModel).Images.ToList())
            {
                await image.LoadImage();
            }
        }
    }
}
