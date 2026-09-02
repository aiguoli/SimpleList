using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Windows.ApplicationModel.DataTransfer;

namespace SimpleList.ViewModels.Tools;

public partial class ExternalDownloaderViewModel : ObservableObject
{
    public ExternalDownloaderViewModel()
    {
    }

    public ExternalDownloaderViewModel(IEnumerable<string> downloadUrls)
    {
        SetDownloadUrls(downloadUrls);
    }

    [RelayCommand]
    private void ParseShareUrlAsync()
    {
        if (IsConverting) return;
        IsConverting = true;
        if (string.IsNullOrEmpty(ShareUrl))
        {
            IsConverting = false;
            return;
        };
        if (Uri.TryCreate(ShareUrl, UriKind.Absolute, out Uri shareUrl))
        {
            if (shareUrl.Host.EndsWith("sharepoint.com"))
            {
                var match = Regex.Match(ShareUrl, _sharepointPattern);

                if (!match.Success)
                {
                    IsConverting = false;
                    return;
                }

                string domain = match.Groups[1].Value;
                string type = match.Groups[2].Value;
                string user = match.Groups[3].Value;
                string shareId = match.Groups[4].Value;

                if (shareId.Contains('?'))
                {
                    shareId = shareId.Split('?')[0];
                }

                if (type == "f")
                {
                    IsConverting = false;
                    return;
                }
                DirectLink = $"{domain}/personal/{user}/_layouts/52/download.aspx?share={shareId}";
                IsConverting = false;
                UpdateCanPush();
            }
            else
            {
                DirectLink = ShareUrl;
                IsConverting = false;
                UpdateCanPush();
            }
        }
        else
        {
            IsConverting = false;
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        DataPackage package = new();
        package.SetText(DirectLink);
        Clipboard.SetContent(package);
    }

    [RelayCommand]
    private async Task PushToDownloader()
    {
        string[] downloadUrls = GetDownloadUrls();
        if (downloadUrls.Length == 0 || !CanPush)
        {
            return;
        }

        List<string> responses = [];
        switch (SelectedDownloaderType)
        {
            case DownloaderType.Aria2:
                foreach (string downloadUrl in downloadUrls)
                {
                    Aria2RpcRequest payload = new()
                    {
                        jsonrpc = "2.0",
                        method = "aria2.addUri",
                        id = Guid.NewGuid().ToString(),
                        @params = string.IsNullOrEmpty(RpcSecret) ? [new[] { downloadUrl }] : [$"token:{RpcSecret}", new[] { downloadUrl }],
                    };
                    string jsonRequest = JsonSerializer.Serialize(payload, Aria2JsonContext.Default.Aria2RpcRequest);
                    StringContent content = new(jsonRequest, Encoding.UTF8, "application/json");
                    var resp = await client.PostAsync(RpcUrl, content);
                    responses.Add(await resp.Content.ReadAsStringAsync());
                }
                Result = string.Join(Environment.NewLine, responses);
                break;
            case DownloaderType.IDM:
                string idmPath = GetIDMPath();
                foreach (string downloadUrl in downloadUrls)
                {
                    ProcessStartInfo startInfo = new()
                    {
                        FileName = idmPath,
                        Arguments = $"/d \"{downloadUrl}\" /n",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(startInfo);
                }
                Result = string.Format(Helpers.ResourceHelper.GetLocalized("ExternalDownloader_IdmSent"), downloadUrls.Length);
                break;
            case DownloaderType.Motrix:
                foreach (string downloadUrl in downloadUrls)
                {
                    Aria2RpcRequest motrixPayload = new()
                    {
                        jsonrpc = "2.0",
                        method = "aria2.addUri",
                        id = Guid.NewGuid().ToString(),
                        @params = string.IsNullOrEmpty(MotrixRpcSecret) ? [new[] { downloadUrl }] : [$"token:{MotrixRpcSecret}", new[] { downloadUrl }],
                    };
                    string motrixRequest = JsonSerializer.Serialize(motrixPayload, Aria2JsonContext.Default.Aria2RpcRequest);
                    StringContent motrixContent = new(motrixRequest, Encoding.UTF8, "application/json");
                    var motrixResp = await client.PostAsync(MotrixRpcUrl, motrixContent);
                    responses.Add(await motrixResp.Content.ReadAsStringAsync());
                }
                Result = string.Join(Environment.NewLine, responses);
                break;
        }
    }

    private void SetDownloadUrls(IEnumerable<string> downloadUrls)
    {
        DirectLink = string.Join(Environment.NewLine, downloadUrls.Where(url => !string.IsNullOrWhiteSpace(url)));
        UpdateCanPush();
    }

    private string[] GetDownloadUrls()
    {
        return (DirectLink ?? string.Empty)
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToArray();
    }

    private void UpdateCanPush()
    {
        CanPush = GetDownloadUrls().Length > 0;
    }

    partial void OnDirectLinkChanged(string value)
    {
        UpdateCanPush();
    }

    private static string GetIDMPath()
    {
        string registryPath = @"SOFTWARE\Wow6432Node\Internet Download Manager";
        using RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath);
        if (key?.GetValue("InstallLocation") != null)
        {
            string installLocation = key.GetValue("InstallLocation").ToString();
            return System.IO.Path.Combine(installLocation, "IDMan.exe");
        }
        Process[] processes = Process.GetProcessesByName("IDMan");
        if (processes.Length > 0)
        {
            return processes[0].MainModule.FileName;
        }
        return string.Empty;
    }

    public enum DownloaderType
    {
        Aria2,
        Motrix,
        IDM
    }

    private static readonly string _sharepointPattern = @"(https://[^/]+sharepoint\.com)/:([a-z]):/g/personal/([^/]+)/([^/?]+)";
    private readonly HttpClient client = new();

    // aria2 config
    [ObservableProperty]
    public partial string RpcUrl { get; set; } = "http://localhost:6800/jsonrpc";

    [ObservableProperty]
    public partial string RpcSecret { get; set; } = "";

    // motrix config
    [ObservableProperty]
    public partial string MotrixRpcUrl { get; set; } = "http://localhost:16800/jsonrpc";

    [ObservableProperty]
    public partial string MotrixRpcSecret { get; set; } = "";

    // idm config
    [ObservableProperty]
    public partial string IdmPath { get; set; } = GetIDMPath();

    [ObservableProperty]
    public partial string[] DownloaderTypes { get; set; } = Enum.GetNames<DownloaderType>();

    [ObservableProperty]
    public partial string ShareUrl { get; set; }

    [ObservableProperty]
    public partial bool IsConverting { get; set; } = false;

    [ObservableProperty]
    public partial string DirectLink { get; set; }

    [ObservableProperty]
    public partial DownloaderType SelectedDownloaderType { get; set; }

    [ObservableProperty]
    public partial bool CanPush { get; set; } = false;

    [ObservableProperty]
    public partial string Result { get; set; }
}

public class Aria2RpcRequest
{
    public string jsonrpc { get; set; } = "2.0";
    public string method { get; set; }
    public string id { get; set; }
    public IList<object> @params { get; set; }
}


[JsonSerializable(typeof(Aria2RpcRequest))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(object))]
public partial class Aria2JsonContext : JsonSerializerContext { }
