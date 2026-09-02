using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using System;

namespace SimpleList.Views
{
    public sealed partial class ShareFileView : ContentDialog
    {
        public ShareFileView()
        {
            InitializeComponent();
            ExpirationDateTime.MinDate = DateTime.Today;
        }

        private async void ContentDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (DataContext is ShareFileViewModel vm)
            {
                await vm.LoadCurrentShareStatusAsync();
            }
        }

        private void ShowQRCode(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            QRCodeTip.IsOpen = true;
        }
    }
}
