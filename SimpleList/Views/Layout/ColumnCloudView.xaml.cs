using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Views.Layout
{
    public sealed partial class ColumnCloudView : UserControl
    {
        public ColumnCloudView()
        {
            InitializeComponent();
        }

        private void ChangeSelectedFiles(object sender, SelectionChangedEventArgs e)
        {
            if ((DataContext as DriveViewModel).SelectedItems == null) return;
            (DataContext as DriveViewModel).SelectedItems.Clear();
            foreach (FileViewModel item in (sender as ListView).SelectedItems.Cast<FileViewModel>())
            {
                (DataContext as DriveViewModel).SelectedItems.Add(item);
            }
        }
    }
}
