using Downloader;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using WinUICommunity;
using AppResourceHelper = SimpleList.Helpers.ResourceHelper;

namespace SimpleList.Pages
{
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
            LoadProviderSettings();
        }

        private void LoadProviderSettings()
        {
            OneDriveClientIdBox.Text = App.Current.Configuration.GetSection("AzureAD:ClientId").Value ?? string.Empty;
            GoogleClientIdBox.Text = App.Current.Configuration.GetSection("GoogleOAuth:ClientId").Value ?? string.Empty;
            GoogleClientSecretBox.Password = App.Current.Configuration.GetSection("GoogleOAuth:ClientSecret").Value ?? string.Empty;
        }

        private void SaveProviderSettings(object sender, RoutedEventArgs e)
        {
            SaveProviderSettingsButton.IsEnabled = false;
            try
            {
                string settingsPath = App.SettingsPath;
                JsonObject settings = LoadSettingsFile(settingsPath);

                JsonObject azureAd = GetOrCreateObject(settings, "AzureAD");
                azureAd["ClientId"] = OneDriveClientIdBox.Text?.Trim() ?? string.Empty;

                JsonObject googleOAuth = GetOrCreateObject(settings, "GoogleOAuth");
                googleOAuth["ClientId"] = GoogleClientIdBox.Text?.Trim() ?? string.Empty;
                googleOAuth["ClientSecret"] = GoogleClientSecretBox.Password?.Trim() ?? string.Empty;

                JsonSerializerOptions options = new() { WriteIndented = true };
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                File.WriteAllText(settingsPath, settings.ToJsonString(options));
                App.Current.ReloadSettings();

                Growl.Success(new GrowlInfo
                {
                    Title = AppResourceHelper.GetLocalized("SettingPage_SaveProviderSettingsSuccessTitle"),
                    Message = AppResourceHelper.GetLocalized("SettingPage_SaveProviderSettingsSuccessMessage"),
                    StaysOpen = false,
                    Token = "SettingGrowl"
                });
            }
            catch (Exception ex)
            {
                Growl.Error(new GrowlInfo
                {
                    Title = AppResourceHelper.GetLocalized("SettingPage_SaveProviderSettingsFailedTitle"),
                    Message = ex.Message,
                    StaysOpen = false,
                    Token = "SettingGrowl"
                });
            }
            finally
            {
                SaveProviderSettingsButton.IsEnabled = true;
            }
        }

        private static JsonObject LoadSettingsFile(string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                return [];
            }

            string json = File.ReadAllText(settingsPath);
            return JsonNode.Parse(json)?.AsObject() ?? [];
        }

        private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
        {
            if (parent[propertyName] is JsonObject existing)
            {
                return existing;
            }

            JsonObject created = [];
            parent[propertyName] = created;
            return created;
        }

        private async void CheckUpdate(object sender, RoutedEventArgs e)
        {
            CheckUpdateButton.IsEnabled = false;
            zipballUrl = null;
            IsUpdateAvailable = false;
            var ver = await UpdateHelper.CheckUpdateAsync("aiguoli", "SimpleList");
            if (ver.IsExistNewVersion)
            {
                // Update App
                IsUpdateAvailable = true;
                var arch = RuntimeInformation.ProcessArchitecture;
                var archMap = new Dictionary<Architecture, string>
                {
                    { Architecture.X64, "x64" },
                    { Architecture.X86, "x86" },
                    { Architecture.Arm64, "arm64" },
                };
                string archName = archMap.TryGetValue(arch, out string mappedArch) ? mappedArch : arch.ToString().ToLowerInvariant();
                string flavor = GetCurrentPublishFlavor();
                string assetName = $@"SimpleList-{ver.TagName}-{archName}-{flavor}.zip";
                string legacyAssetName = $@"SimpleList-{ver.TagName}-{archName}.zip";
                var asset = Array.Find(ver.Assets, asset => string.Equals(asset.Name, assetName, StringComparison.OrdinalIgnoreCase))
                    ?? Array.Find(ver.Assets, asset => string.Equals(asset.Name, legacyAssetName, StringComparison.OrdinalIgnoreCase));
                zipballUrl = asset?.Url;
                ChangelogText.Text = ver.Changelog;
                NewVersion.Text = ver.TagName;
                StatusInfo.Visibility = Visibility.Visible;
                DownloadButton.Visibility = Utils.IsValidUrl(zipballUrl) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ChangelogText.Text = string.Empty;
                StatusInfo.Visibility = Visibility.Visible;
                DownloadButton.Visibility = Visibility.Collapsed;
            }
            Bindings.Update();
            CheckUpdateButton.IsEnabled = true;
        }

        private async void DownloadLatestZip(object sender, RoutedEventArgs e)
        {
            //(sender as HyperlinkButton).IsEnabled = false;
            DownloadButton.IsEnabled = false;
            if (!Utils.IsValidUrl(zipballUrl) || !IsUpdateAvailable)
            {
                return;
            }
            var downloadOpt = new DownloadConfiguration()
            {
                ChunkCount = 8,
                ParallelDownload = true,
                RequestConfiguration =
                {
                    Proxy = WebRequest.DefaultWebProxy,
                }
            };
            var downloader = new DownloadService(downloadOpt);
            var file = Path.Combine(Path.GetTempPath(), $@"{Path.GetRandomFileName()}.zip");
            downloader.DownloadFileCompleted += (s, e) =>
            {
                if ((s as IDownloadService).Status == DownloadStatus.Completed)
                {
                    ExtractZip(file);
                }
            };
            await downloader.DownloadFileTaskAsync(zipballUrl, file);
        }

        private static void ExtractZip(string zipFile)
        {
            if (!Path.Exists(zipFile))
            {
                return;
            }
            // Extract Zip using powershell
            var destinationDirectory = Environment.CurrentDirectory;
            string script = $@"Expand-Archive -Path '{zipFile}' -DestinationPath '{destinationDirectory}' -Force";
            Process.Start("PowerShell", script);
        }

        private void UpdateByPowershell(object sender, RoutedEventArgs e)
        {
            if (zipballUrl == null)
            {
                return;
            }
            var zipFile = Path.Combine(Path.GetTempPath(), $@"{Path.GetRandomFileName()}.zip");
            string psScript = $@"
                Stop-Process -Name '{Process.GetCurrentProcess().ProcessName}' -Force
                Start-BitsTransfer -Source '{zipballUrl}' -Destination '{zipFile}' -DisplayName 'SimpleList Update'
                Expand-Archive -Path '{zipFile}' -DestinationPath '{Environment.CurrentDirectory}' -Force
                Remove-Item -Path {zipFile}
                Start-Process '{Path.Combine(Environment.CurrentDirectory, "SimpleList.exe")}'
                Pause
            ";
            Process.Start("PowerShell", psScript);
        }

        private string zipballUrl;
        public string Version => Utils.GetVersion();
        public bool IsUpdateAvailable { get; set; } = false;

        private static string GetCurrentPublishFlavor()
        {
            string flavor = Assembly.GetEntryAssembly()?
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "PublishFlavor")
                ?.Value;

            if (string.Equals(flavor, "Slim", StringComparison.OrdinalIgnoreCase))
            {
                return "Slim";
            }

            if (string.Equals(flavor, "Portable", StringComparison.OrdinalIgnoreCase))
            {
                return "Portable";
            }

            if (AppContext.GetData("IsSingleFile") is bool isSingleFile && isSingleFile)
            {
                return "Portable";
            }

            return File.Exists(Path.Combine(AppContext.BaseDirectory, "SimpleList.dll")) ? "Slim" : "Portable";
        }
    }
}
