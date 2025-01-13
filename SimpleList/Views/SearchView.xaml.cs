using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SimpleList.Views;

public sealed partial class SearchView : ContentDialog
{
    public SearchView()
    {
        InitializeComponent();
    }

    private void LoadDefaultValue(object sender, RoutedEventArgs e)
    {
        (sender as ComboBox).SelectedIndex = 0;
    }
}
