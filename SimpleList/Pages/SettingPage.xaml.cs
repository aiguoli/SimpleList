using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Helpers;
using SimpleList.Services;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

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

        [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
        private async Task checkForUpdate()
        {
            string url = "https://api.github.com/repos/aiguoli/simplelist/releases/latest";
            HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(url);
            dynamic latestVersion = JsonSerializer.Deserialize<dynamic>(await response.Content.ReadAsStringAsync());
            if (latestVersion != Version)
            {
                IsUpdateAvailable = true;
            }
        }

        public string Version
        {
            get => Utils.GetVersion();
        }

        public bool IsUpdateAvailable { get; set; } = false;
    }
}
