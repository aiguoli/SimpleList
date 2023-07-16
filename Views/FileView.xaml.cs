using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using System;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Views
{
    public sealed partial class FileView : UserControl
    {
        public FileView()
        {
            InitializeComponent();
        }

        private async void ShowRenameFileDialogAsync(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FileViewModel viewModel = DataContext as FileViewModel;
            RenameFileView dialog = new()
            {
                XamlRoot = XamlRoot,
                DataContext = new RenameFileViewModel(viewModel.Cloud, viewModel)
            };
            await dialog.ShowAsync();
        }
    }
}
