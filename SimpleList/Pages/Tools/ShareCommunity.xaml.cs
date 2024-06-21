using Microsoft.UI.Xaml.Controls;
using SimpleList.Models;
using SimpleList.ViewModels.Tools;
using SimpleList.Views.Tools;
using System;

namespace SimpleList.Pages.Tools
{
    public sealed partial class ShareCommunity : Page
    {
        public ShareCommunity()
        {
            InitializeComponent();
            DataContext = new ShareCommunityViewModel();
        }

        private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await (DataContext as ShareCommunityViewModel).Refresh();
        }

        private async void ShowLinkDetailsDialogAsync(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            LinkDetails dialog = new() 
            {
                XamlRoot = XamlRoot,
                DataContext = new LinkDetailsViewModel((sender as Button).DataContext as Link)
            };
            await dialog.ShowAsync();
        }

        private async void ShowCreateLinkDialogAsync(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            CreateLink dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new CreateLinkViewModel(DataContext as ShareCommunityViewModel)
            };
            await dialog.ShowAsync();
        }
    }
}
