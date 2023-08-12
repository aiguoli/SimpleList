using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
            //themeMode.SelectedIndex = App.Current.RequestedTheme switch
            //{
            //    ApplicationTheme.Light => 1,
            //    ApplicationTheme.Dark => 2,
            //    _ => 0,
            //};
        }

        private void themeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Window startUpWindow = App.StartupWindow;
            string selectedTheme = ((ComboBoxItem)themeMode.SelectedItem)?.Tag?.ToString();
            if (Enum.TryParse(selectedTheme, out ElementTheme theme) is true)
            {
                if (startUpWindow.Content is FrameworkElement frameworkElement)
                {
                    frameworkElement.RequestedTheme = theme;
                }
            }
        }

        public string Version
        {
            get
            {
                var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
                return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            }
        }
    }
}
