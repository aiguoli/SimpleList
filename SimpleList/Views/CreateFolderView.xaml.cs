using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Views
{
    public sealed partial class CreateFolderView : ContentDialog
    {
        public CreateFolderView()
        {
            InitializeComponent();
        }

        private void NewFolderName_TextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrEmpty(NewFolderName.Text);
        }
    }
}
