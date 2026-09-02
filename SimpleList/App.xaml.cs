using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.UI.Xaml;
using SimpleList.Core.Contracts;
using SimpleList.Core.Services;
using SimpleList.Helpers;
using SimpleList.Services;
using SimpleList.ViewModels;
using SimpleList.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using WinUICommunity;

namespace SimpleList;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleList",
        "appsettings.json");

    public static T GetService<T>() where T : class
    {
        if (App.Current!.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            LogError("Unhandled Exception", sender, exception);
        };
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            LogError("Unobserved Task Exception", sender, args.Exception);
            args.SetObserved();
        };

        Application.Current.UnhandledException += (sender, args) =>
        {
            LogError("UI Thread Exception", sender, args.Exception);
            args.Handled = true;
        };
    }

    public static void LogError(string title, object sender, Exception exception)
    {
        var logFilePath = Path.Combine(Environment.CurrentDirectory, "error.log");
        var logMessage = $"{DateTime.Now}: {title}\n{sender}\n{exception}\n\n";

        File.AppendAllText(logFilePath, logMessage);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        //Current.Resources["Configuration"] = Configuration;
        //string backdropType = Configuration.GetSection("Material").Value;
        m_window = new MainWindow
        {
            Title = Assembly.GetEntryAssembly().GetName().Name,
        };

        m_window.Activate();
        m_window.DispatcherQueue.TryEnqueue(() =>
        {
            var themeService = GetThemeService;
            if (themeService != null)
            {
                themeService.Initialize(m_window, true, "theme.json");

                string cfgMaterial = Configuration.GetSection("Material").Value;
                string cfgTheme = Configuration.GetSection("Theme").Value;

                var backdropType = Enum.TryParse(cfgMaterial, out BackdropType parsedBackdrop) ? parsedBackdrop : BackdropType.Mica;
                var elementTheme = Enum.TryParse(cfgTheme, out ElementTheme parsedTheme) ? parsedTheme : ElementTheme.Default;

                themeService.ConfigBackdrop(backdropType);
                themeService.ConfigElementTheme(elementTheme);
            }
        });
        //string selectedTheme = Configuration.GetSection("Theme").Value;
        //ThemeHelper.RootTheme = Enum.TryParse(selectedTheme, out ElementTheme theme) ? theme : ElementTheme.Default;

        //MsalCacheHelper CacheHelper = GetCacheHelper().GetAwaiter().GetResult();
        //Ioc.Default.ConfigureServices(
        //    new ServiceCollection()
        //        .AddSingleton<TaskManagerViewModel>()
        //        .AddSingleton(CacheHelper)
        //        .AddSingleton(BuildPublicApp())
        //        .BuildServiceProvider()
        //);
    }

    private ServiceProvider ConfigureServices()
    {
        LoadSettings();
        var services = new ServiceCollection();
        services.AddSingleton<IConfigurationRoot>(Configuration);
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<TaskManagerViewModel>();
        services.AddSingleton<CloudViewModel>();
        services.AddSingleton<Task<MsalCacheHelper>>(sp => GetCacheHelper());
        services.AddSingleton<IPublicClientApplication>(sp => BuildPublicApp());
        services.AddSingleton<IStringLocalizer, WinUiStringLocalizer>();
        services.AddSingleton<IPikPakCredentialStore, PikPakCredentialStore>();
        services.AddSingleton<ClientSecrets>(sp => BuildGoogleSecrets());
        services.AddSingleton<GoogleTokenDataStore>(sp => new GoogleTokenDataStore(Path.Combine(Directory.GetCurrentDirectory(), "cache", "GoogleDriveTokenCache")));
        services.AddSingleton<ShareCommunityTokenStore>();
        services.AddSingleton<ShareCommunityApiClient>();
        services.AddTransient<ShareCommunityViewModel>();
        return services.BuildServiceProvider();
    }

    public static OneDriveStorageProvider CreateOneDriveProvider(string driveId = null, string accountId = null)
    {
        return new OneDriveStorageProvider(
            Current.BuildPublicApp(),
            GetService<Task<MsalCacheHelper>>(),
            GetService<IStringLocalizer>(),
            driveId,
            accountId);
    }

    public static GoogleDriveStorageProvider CreateGoogleDriveProvider(
        string driveId = null,
        string accountId = null,
        string credentialStoreKey = null)
    {
        return new GoogleDriveStorageProvider(
            Current.BuildGoogleSecrets(),
            GetService<GoogleTokenDataStore>(),
            GetService<IStringLocalizer>(),
            driveId,
            accountId,
            credentialStoreKey);
    }

    public static LocalStorageProvider CreateLocalProvider(string rootPath)
    {
        return new LocalStorageProvider(rootPath, GetService<IStringLocalizer>());
    }

    public static PikPakStorageProvider CreatePikPakProvider(string driveId, string username, string password = null, bool rememberPassword = true)
    {
        return new PikPakStorageProvider(
            driveId,
            username,
            password,
            GetService<IPikPakCredentialStore>(),
            GetService<IStringLocalizer>(),
            rememberPassword: rememberPassword,
            captchaChallengeHandler: OpenPikPakCaptchaAsync);
    }

    private static Task OpenPikPakCaptchaAsync(string verificationUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(verificationUrl))
        {
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        if (ct.CanBeCanceled)
        {
            registration = ct.Register(() => completion.TrySetCanceled(ct));
        }

        bool enqueued = StartupWindow?.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                bool launched = await Launcher.LaunchUriAsync(new Uri(verificationUrl));
                if (!launched)
                {
                    completion.TrySetException(new InvalidOperationException("Unable to open PikPak captcha verification page."));
                    return;
                }

                completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                registration.Dispose();
            }
        }) == true;

        if (!enqueued)
        {
            registration.Dispose();
            completion.TrySetException(new InvalidOperationException("Unable to open PikPak captcha verification page."));
        }

        return completion.Task;
    }

    private ClientSecrets BuildGoogleSecrets()
    {
        return new ClientSecrets
        {
            ClientId = Configuration.GetSection("GoogleOAuth:ClientId").Value ?? string.Empty,
            ClientSecret = Configuration.GetSection("GoogleOAuth:ClientSecret").Value ?? string.Empty,
        };
    }

    private void LoadSettings()
    {
        Dictionary<string, string> builtInDefaults = new()
        {
            ["AzureAD:ClientId"] = "f3416197-df13-4fd9-a57d-9fb052ba2cdf",
            ["GoogleOAuth:ClientId"] = string.Empty,
            ["GoogleOAuth:ClientSecret"] = string.Empty,
            ["Theme"] = "Default",
            ["Material"] = "MicaAlt",
            ["TintColor"] = string.Empty,
            ["Tools:ShareCommunity:Url"] = "https://share.qqsign.cn",
            ["Tools:ShareCommunity:DevelopmentUrl"] = "http://127.0.0.1:3000",
        };

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(builtInDefaults)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.defaults.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(SettingsPath, optional: true, reloadOnChange: false)
            .Build();
    }

    public void ReloadSettings()
    {
        LoadSettings();
    }

    private IPublicClientApplication BuildPublicApp()
    {
        IPublicClientApplication publicClientApp = PublicClientApplicationBuilder.Create(Configuration.GetSection("AzureAD:ClientId").Value)
            .WithClientName(Assembly.GetEntryAssembly().GetName().Name)
            .WithRedirectUri("http://localhost")
            .WithLogging((level, message, containsPii) =>
            {
                Debug.WriteLine($"MSAL: {level} {message}");
            }, LogLevel.Verbose, enablePiiLogging: true, enableDefaultPlatformLogging: true)
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
    public new static App Current => (App)Application.Current;
    public static Window StartupWindow => m_window;
    //public IServiceProvider Services { get; }
    public IConfigurationRoot Configuration { get; set; }
    //public IThemeService ThemeService { get; set; }
    public IThemeService GetThemeService => GetService<IThemeService>();
    public IServiceProvider Services { get; }
}

