using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SimpleList.ViewModels;

namespace SimpleList.Pages
{
    public sealed partial class BookmarkPage : Page
    {
        public BookmarkViewModel ViewModel => DataContext as BookmarkViewModel;

        public BookmarkPage()
        {
            InitializeComponent();
            DataContext = new BookmarkViewModel();
            Loaded += async (sender, args) =>
            {
                await ViewModel.LoadBookmarksAsync();
            };
        }

        private async void OpenBookmark(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((sender as Grid)?.DataContext is BookmarkItemViewModel bookmark)
            {
                await ViewModel.OpenBookmarkCommand.ExecuteAsync(bookmark);
            }
        }

        private async void OpenBookmarkMenu(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if ((sender as MenuFlyoutItem)?.DataContext is BookmarkItemViewModel bookmark)
            {
                await ViewModel.OpenBookmarkCommand.ExecuteAsync(bookmark);
            }
        }

        private async void RemoveBookmarkMenu(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if ((sender as MenuFlyoutItem)?.DataContext is BookmarkItemViewModel bookmark)
            {
                await ViewModel.RemoveBookmarkCommand.ExecuteAsync(bookmark);
            }
        }
    }
}
