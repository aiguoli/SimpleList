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

        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            ShareFileViewModel vm = DataContext as ShareFileViewModel;
            if (vm.PreventClose)
            {
                args.Cancel = true;
                vm.PreventClose = false;
            }
        }
    }
}
