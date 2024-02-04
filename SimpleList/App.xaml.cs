using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SimpleList.Helpers;
using SimpleList.Services;
using SimpleList.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace SimpleList
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            LoadSettings();
            string backdropType = Configuration.GetSection("Material").Value;
            m_window = new MainWindow
            {
                Title = Assembly.GetEntryAssembly().GetName().Name,
                SystemBackdrop = backdropType switch
                {
                    "Mica" => new MicaBackdrop(),
                    "MicaAlt" => new MicaBackdrop() { Kind = MicaKind.BaseAlt },
                    "Acrylic" => new DesktopAcrylicBackdrop(),
                    _ => new MicaBackdrop(),
                }
            };

            m_window.Activate();
            string selectedTheme = Configuration.GetSection("Theme").Value;
            ThemeHelper.RootTheme = Enum.TryParse(selectedTheme, out ElementTheme theme) ? theme : ElementTheme.Default;

            MsalCacheHelper CacheHelper = GetCacheHelper().GetAwaiter().GetResult();
            Ioc.Default.ConfigureServices(
                new ServiceCollection()
                    .AddSingleton<TaskManagerViewModel>()
                    .AddSingleton(CacheHelper)
                    .AddSingleton(BuildPublicApp())
                    .BuildServiceProvider()
            );
        }

        private void LoadSettings()
        {
            Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            Current.Resources["Configuration"] = Configuration;
        }

        private IPublicClientApplication BuildPublicApp()
        {
            IPublicClientApplication publicClientApp = PublicClientApplicationBuilder.Create(Configuration.GetSection("AzureAD:ClientId").Value)
                .WithClientName(Assembly.GetEntryAssembly().GetName().Name)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .Build();
            return publicClientApp;
        }

        private static async Task<MsalCacheHelper> GetCacheHelper()
        {
            string cacheFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            var storageProperties =
                    new StorageCreationPropertiesBuilder("OneDriveTokenCache.bin", cacheFolderPath)
                    .WithLinuxKeyring(
                        "SimpleList.TokenCache",
                        MsalCacheHelper.LinuxKeyRingDefaultCollection,
                        "MSAL token cache for SimpleList.",
                        new KeyValuePair<string, string>("Version", Utils.GetVersion()),
                        new KeyValuePair<string, string>("ProductGroup", "SimpleList"))
                    .Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
            cacheHelper.VerifyPersistence();
            return cacheHelper;
        }

        private static Window m_window;
        public static Window StartupWindow => m_window;
        public IServiceProvider Services { get; }
        public IConfigurationRoot Configuration { get; set; }
    }
}
