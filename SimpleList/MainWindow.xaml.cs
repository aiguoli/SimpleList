using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SimpleList.Core.Services;
using SimpleList.Helpers;
using SimpleList.Pages;
using System;

namespace SimpleList
{
    public sealed partial class MainWindow : Window
    {
        private bool _isUpdatingNavigationSelection;

        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_isUpdatingNavigationSelection)
            {
                return;
            }

            if (args.IsSettingsSelected)
            {
                contentFrame.Navigate(typeof(SettingPage));
                RecordNavigation(ResourceHelper.GetLocalized("OperationLog_Page_Settings"));
            }
            else
            {
                if (args.SelectedItem is NavigationViewItem { Tag: string tag })
                {
                    contentFrame.Navigate(ResolveNavigationPage(tag));
                    RecordNavigation((args.SelectedItem as NavigationViewItem)?.Content?.ToString() ?? tag);
                }
            }
        }

        private static Type ResolveNavigationPage(string tag)
        {
            return tag switch
            {
                "HomePage" => typeof(HomePage),
                "CloudPage" => typeof(CloudPage),
                "TaskManagerPage" => typeof(TaskManagerPage),
                "ToolPage" => typeof(ToolPage),
                "BookmarkPage" => typeof(BookmarkPage),
                "OperationLogPage" => typeof(OperationLogPage),
                _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, "Unknown navigation page")
            };
        }

        public void Navigate(Type pageType, object targetPageArguments = null, NavigationTransitionInfo navigationTransitionInfo = null)
        {
            SelectNavigationItem(pageType);
            RootFrame.Navigate(pageType, targetPageArguments, navigationTransitionInfo);
        }

        private void SelectNavigationItem(Type pageType)
        {
            NavigationViewItem targetItem = pageType switch
            {
                not null when pageType == typeof(HomePage) => HomePageItem,
                not null when pageType == typeof(CloudPage) || pageType == typeof(DrivePage) => CloudPageItem,
                not null when pageType == typeof(TaskManagerPage) => TaskManagerItem,
                not null when pageType == typeof(ToolPage) => ToolPageItem,
                not null when pageType == typeof(BookmarkPage) => BookmarkItem,
                not null when pageType == typeof(OperationLogPage) => OperationLogItem,
                _ => null
            };

            if (targetItem == null || nvSample.SelectedItem is NavigationViewItem selectedItem && selectedItem == targetItem)
            {
                return;
            }

            _isUpdatingNavigationSelection = true;
            nvSample.SelectedItem = targetItem;
            _isUpdatingNavigationSelection = false;
        }
        public Frame RootFrame => contentFrame;

        private static void RecordNavigation(string destination)
        {
            App.GetService<OperationLogService>().Record(
                ResourceHelper.GetLocalized("OperationLog_Category_Navigation"),
                ResourceHelper.GetLocalized("OperationLog_Action_OpenPage"),
                destination);
        }
    }

}
