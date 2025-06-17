using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                CanPush = true;
            }
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
        if (string.IsNullOrWhiteSpace(DirectLink) || !CanPush)
        {
            return;
        }
        switch (SelectedDownloaderType)
        {
            case DownloaderType.Aria2:
                Aria2RpcRequest payload = new()
                {
                    jsonrpc = "2.0",
                    method = "aria2.addUri",
                    id = Guid.NewGuid().ToString(),
                    @params = string.IsNullOrEmpty(RpcSecret) ? [new[] { DirectLink }] : [$"token:{RpcSecret}", new[] { DirectLink }],
                };
                string jsonRequest = JsonSerializer.Serialize(payload, Aria2JsonContext.Default.Aria2RpcRequest);
                StringContent content = new(jsonRequest, Encoding.UTF8, "application/json");
                var resp = await client.PostAsync(RpcUrl, content);
                Result = await resp.Content.ReadAsStringAsync();
                break;
            case DownloaderType.IDM:
                string idmPath = GetIDMPath();
                ProcessStartInfo startInfo = new()
                {
                    FileName = idmPath,
                    Arguments = $"/d \"{DirectLink}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
                break;
            case DownloaderType.Motrix:
                Aria2RpcRequest motrixPayload = new()
                {
                    jsonrpc = "2.0",
                    method = "aria2.addUri",
                    id = Guid.NewGuid().ToString(),
                    @params = string.IsNullOrEmpty(RpcSecret) ? [new[] { DirectLink }] : [$"token:{MotrixRpcSecret}", new[] { DirectLink }],
                };
                string motrixRequest = JsonSerializer.Serialize(motrixPayload, Aria2JsonContext.Default.Aria2RpcRequest);
                StringContent motrixContent = new(motrixRequest, Encoding.UTF8, "application/json");
                var motrixResp = await client.PostAsync(MotrixRpcUrl, motrixContent);
                Result = await motrixResp.Content.ReadAsStringAsync();
                break;
        }
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
    [ObservableProperty] private string _RpcUrl = "http://localhost:6800/jsonrpc";
    [ObservableProperty] private string _RpcSecret = "";

    // motrix config
    [ObservableProperty] private string _motrixRpcUrl = "http://localhost:16800/jsonrpc";
    [ObservableProperty] private string _motrixRpcSecret = "";

    // idm config
    [ObservableProperty] private string _idmPath = GetIDMPath();

    [ObservableProperty] private string[] _downloaderTypes = Enum.GetNames<DownloaderType>();
    [ObservableProperty] private string _shareUrl;
    [ObservableProperty] private bool _isConverting = false;
    [ObservableProperty] private string _directLink;
    [ObservableProperty] private DownloaderType _selectedDownloaderType;
    [ObservableProperty] private bool _canPush = false;
    [ObservableProperty] private string _result;
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
