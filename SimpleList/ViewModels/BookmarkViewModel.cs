using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SimpleList.Core.Models.DTO;
using SimpleList.Core.Services;
using SimpleList.Models;
using SimpleList.Pages;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels
{
    public partial class BookmarkViewModel : ObservableObject
    {
        public async Task LoadBookmarksAsync()
        {
            if (_isLoaded)
            {
                return;
            }

            await ReloadBookmarksAsync();
            _isLoaded = true;
        }

        [RelayCommand]
        private async Task ReloadBookmarksAsync()
        {
            IReadOnlyList<BookmarkDTO> storedBookmarks = await _bookmarkStore.LoadAsync();
            _allBookmarks = [.. storedBookmarks];
            ApplyFilter();
        }

        [RelayCommand]
        private async Task RemoveBookmark(BookmarkItemViewModel bookmark)
        {
            await _bookmarkStore.RemoveAsync(bookmark.Source);
            _allBookmarks.RemoveAll(item => BookmarkStore.IsSameBookmark(item, bookmark.Source));
            ApplyFilter();
        }

        [RelayCommand]
        private async Task OpenBookmark(BookmarkItemViewModel bookmark)
        {
            CloudViewModel cloud = new();
            await cloud.LoadDrivesFromDisk();
            DriveViewModel drive = cloud.GetDrive(bookmark.Source.ProviderType, bookmark.Source.AccountId, bookmark.Source.DriveId);
            if (drive == null)
            {
                ShowOpenFailed(SimpleList.Helpers.ResourceHelper.GetLocalized("Bookmark_OpenDriveMissing"));
                return;
            }

            (App.StartupWindow as MainWindow).Navigate(typeof(DrivePage), new BookmarkNavigationRequest
            {
                Drive = drive,
                Bookmark = bookmark.Source
            });
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Bookmarks.Clear();

            IEnumerable<BookmarkDTO> bookmarks = _allBookmarks;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                bookmarks = bookmarks.Where(bookmark =>
                    (bookmark.Name?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false)
                    || (BookmarkItemViewModel.GetPath(bookmark)?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false)
                    || (bookmark.DriveDisplayName?.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ?? false));
            }

            foreach (BookmarkDTO bookmark in bookmarks)
            {
                Bookmarks.Add(new BookmarkItemViewModel(bookmark));
            }

            EmptyStateVisibility = Bookmarks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void ShowOpenFailed(string message)
        {
            Growl.Error(new GrowlInfo
            {
                Title = SimpleList.Helpers.ResourceHelper.GetLocalized("Bookmark_OpenFailedTitle"),
                Message = message,
                StaysOpen = false,
                Token = "BookmarkGrowl"
            });
        }

        private readonly BookmarkStore _bookmarkStore = new(Path.Combine(Directory.GetCurrentDirectory(), "cache", "bookmarks.json"));
        private List<BookmarkDTO> _allBookmarks = [];
        private bool _isLoaded;

        public ObservableCollection<BookmarkItemViewModel> Bookmarks { get; } = [];
        [ObservableProperty] public partial string SearchText { get; set; }

        [ObservableProperty] private partial Visibility EmptyStateVisibility { get; set; } = Visibility.Visible;
    }

    public class BookmarkItemViewModel
    {
        public BookmarkItemViewModel(BookmarkDTO source)
        {
            Source = source;
        }

        public BookmarkDTO Source { get; }
        public string Name => Source.Name;
        public string DriveDisplayName => Source.DriveDisplayName;
        public string Path => GetPath(Source);
        public string ItemType => Source.IsFolder ? Helpers.ResourceHelper.GetLocalized("ItemType_Folder") : Helpers.ResourceHelper.GetLocalized("ItemType_File");
        public string IconGlyph => Source.IsFolder ? "\uE8B7" : "\uE8A5";

        public static string GetPath(BookmarkDTO source)
        {
            return source.PathSegments == null || source.PathSegments.Count == 0
                ? source.Name ?? string.Empty
                : string.Join(" / ", source.PathSegments.Select(segment => segment.Name));
        }
    }
}
