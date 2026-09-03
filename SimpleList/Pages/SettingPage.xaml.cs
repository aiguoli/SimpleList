using Downloader;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using WinUICommunity;

namespace SimpleList.Pages
{
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
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
                IsUpdateAvailable = Utils.IsValidUrl(zipballUrl);
                StatusInfo.Description = ver.Changelog;
                NewVersion.Text = ver.TagName;
                StatusInfo.Visibility = Visibility.Visible;
                DownloadButton.Visibility = IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
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

            if (string.Equals(flavor, "SingleFile", StringComparison.OrdinalIgnoreCase))
            {
                return "SingleFile";
            }

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
                return "SingleFile";
            }

            return File.Exists(Path.Combine(AppContext.BaseDirectory, "SimpleList.dll")) ? "Slim" : "Portable";
        }
    }
}
