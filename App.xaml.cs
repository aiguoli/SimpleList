using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SimpleList.Helpers;
using SimpleList.Services;
using SimpleList.ViewModels;
using System;
using System.Reflection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow
            {
                Title = Assembly.GetEntryAssembly().GetName().Name,
            };
            m_window.Activate();

            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddSingleton<OneDrive>()
                    .AddSingleton<TaskManagerViewModel>()
                    .BuildServiceProvider()
            );
            LoadSettings();
        }

        private static void LoadSettings()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            Current.Resources["Configuration"] = configuration;
            string selectedTheme = configuration.GetSection("Theme").Value;
            ThemeHelper.RootTheme = Enum.TryParse(selectedTheme, out ElementTheme theme) ? theme : ElementTheme.Default;
        }


        private static Window m_window;
        public static Window StartupWindow => m_window;
        public IServiceProvider Services { get; }
    }
}
