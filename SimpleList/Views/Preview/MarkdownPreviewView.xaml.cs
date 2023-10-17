using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;

namespace SimpleList.Views.Preview
{
    public sealed partial class MarkdownPreviewView : ContentDialog
    {
        public MarkdownPreviewView()
        {
            InitializeComponent();
        }

        private void LoadTextContentAsync(object sender, RoutedEventArgs e)
        {
            PreviewViewModel vm = DataContext as PreviewViewModel;
            _ = vm.LoadTextContent();
        }
    }
}
