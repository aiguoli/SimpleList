using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Helpers;
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
            Loaded += OnSettingsPageLoaded;
        }

        private void OnSettingsPageLoaded(object sender, RoutedEventArgs e)
        {
            ElementTheme currentTheme = ThemeHelper.RootTheme;
            switch (currentTheme)
            {
                case ElementTheme.Default:
                    themeMode.SelectedIndex = 0;
                    break;
                case ElementTheme.Light:
                    themeMode.SelectedIndex = 1;
                    break;
                case ElementTheme.Dark:
                    themeMode.SelectedIndex = 2;
                    break;
            }
        }

        private void themeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedTheme = ((ComboBoxItem)themeMode.SelectedItem)?.Tag?.ToString();
            ThemeHelper.RootTheme = Enum.TryParse(selectedTheme, out ElementTheme theme) ? theme : ElementTheme.Default;
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
