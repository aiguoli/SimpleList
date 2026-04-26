using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using SimpleList.Helpers;
using SimpleList.ViewModels;
using System;

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

        private async void ShowUploadDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: UploadTaskViewModel uploadTask })
            {
                return;
            }

            DataTemplate detailTemplate;
            try
            {
                detailTemplate = (Content as FrameworkElement)?.Resources["UploadDetailTemplate"] as DataTemplate;
            }
            catch
            {
                return;
            }

            if (detailTemplate == null)
            {
                return;
            }

            var listView = new ListView
            {
                ItemsSource = uploadTask.FolderUploadItems,
                ItemTemplate = detailTemplate,
                MinWidth = 560,
                MaxHeight = 420
            };
            ScrollViewer.SetVerticalScrollMode(listView, ScrollMode.Enabled);
            ScrollViewer.SetVerticalScrollBarVisibility(listView, ScrollBarVisibility.Visible);
            ScrollViewer.SetHorizontalScrollBarVisibility(listView, ScrollBarVisibility.Disabled);

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = string.Format(ResourceHelper.GetLocalized("TaskManagerPage_UploadDetailsTitle"), uploadTask.Name),
                CloseButtonText = ResourceHelper.GetLocalized("TaskManagerPage_Close"),
                DefaultButton = ContentDialogButton.Close,
                Content = listView
            };

            await dialog.ShowAsync();
        }
    }
}
