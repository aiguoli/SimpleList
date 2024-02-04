using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SimpleList.Pages;
using System;

namespace SimpleList
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                contentFrame.Navigate(typeof(SettingPage));
            }
            else
            {
                var selectedItem = (NavigationViewItem)args.SelectedItem;
                string selectedItemTag = ((string)selectedItem.Tag);
                string pageName = "SimpleList.Pages." + selectedItemTag;
                Type pageType = Type.GetType(pageName);
                contentFrame.Navigate(pageType);
            }
        }

        public void Navigate(Type pageType, object targetPageArguments = null, NavigationTransitionInfo navigationTransitionInfo = null)
        {
            RootFrame.Navigate(pageType, targetPageArguments, navigationTransitionInfo);
        }
        public Frame RootFrame => contentFrame;
    }

}
