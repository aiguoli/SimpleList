using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Views
{
    public sealed partial class SearchView : ContentDialog
    {
        public SearchView()
        {
            InitializeComponent();
        }

        private void LoadDefaultValue(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            (sender as ComboBox).SelectedIndex = 0;
        }
    }
}
