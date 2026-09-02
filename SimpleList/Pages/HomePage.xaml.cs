using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace SimpleList.Pages
{
    public sealed partial class HomePage : Page
    {
        public string ConfigurationPath { get; } = App.SettingsPath;

        public string GoogleConfigurationExample { get; } =
            "{\r\n" +
            "  \"GoogleOAuth\": {\r\n" +
            "    \"ClientId\": \"YOUR_CLIENT_ID.apps.googleusercontent.com\",\r\n" +
            "    \"ClientSecret\": \"YOUR_CLIENT_SECRET\"\r\n" +
            "  }\r\n" +
            "}";

        public HomePage()
        {
            InitializeComponent();
        }

        private void OpenDrives_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            (App.StartupWindow as MainWindow)?.Navigate(typeof(CloudPage));
        }
    }
}
