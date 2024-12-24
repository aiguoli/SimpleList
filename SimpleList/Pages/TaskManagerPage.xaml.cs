using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TaskManagerPage : Page
    {
        public TaskManagerPage()
        {
            InitializeComponent();
            DataContext = App.GetService<TaskManagerViewModel>();
        }
    }
}
