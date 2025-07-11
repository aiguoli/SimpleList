﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.UI.Xaml;
using SimpleList.Services;
using SimpleList.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
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
        LogError("UI Thread Exception", new object { }, new Exception());
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

        GetThemeService?.Initialize(m_window, true, "theme.json");
        m_window.Activate();
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
        MsalCacheHelper CacheHelper = GetCacheHelper().GetAwaiter().GetResult();
        var services = new ServiceCollection();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<TaskManagerViewModel>();
        services.AddSingleton(CacheHelper);
        services.AddSingleton(BuildPublicApp());
        return services.BuildServiceProvider();
    }

    private void LoadSettings()
    {
        Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
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

