using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Core.Models;
using SimpleList.Models;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleList.ViewModels.Tools;

public partial class ShareCommunityViewModel : ObservableObject
{
    private readonly ShareCommunityApiClient _api;

    public ShareCommunityViewModel(ShareCommunityApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    public async Task Refresh()
    {
        try
        {
            HasError = false;
            LastError = string.Empty;
            Task<LinksResponse> linksTask = _api.GetLinksAsync();
            Task<ProvidersResponse> providersTask = _api.GetProvidersAsync();
            await Task.WhenAll(linksTask, providersTask);
            Links = linksTask.Result.Data ?? [];
            Providers = (providersTask.Result.Data ?? [])
                .Where(item => item.Capabilities?.CommunityPublish == true)
                .Select(ToOption)
                .Where(item => item is not null)
                .ToArray();
        }
        catch (Exception e)
        {
            LastError = e.Message;
            HasError = true;
            App.LogError("Error fetching share community data", this, e);
        }
    }

    [RelayCommand]
    public async Task Logout()
    {
        await _api.LogoutAsync();
        NotifySessionChanged();
    }

    public bool IsAuthenticated => _api.IsAuthenticated;
    public string CurrentUserName => _api.CurrentUser?.Username ?? string.Empty;
    public ShareCommunityApiClient Api => _api;

    public void NotifySessionChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(CurrentUserName));
    }

    [ObservableProperty]
    public partial IEnumerable<ShareCommunityLink> Links { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ShareProviderOption> Providers { get; set; } = [];

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string LastError { get; set; }

    private static ShareProviderOption ToOption(ShareProvider provider)
    {
        ProviderType? providerType = provider.Type switch
        {
            "onedrive" => ProviderType.OneDrive,
            "google_drive" => ProviderType.GoogleDrive,
            "local" => ProviderType.Local,
            "pikpak" => ProviderType.PikPak,
            _ => null,
        };
        return providerType is null
            ? null
            : new ShareProviderOption(
                providerType.Value,
                provider.Type,
                provider.DisplayName,
                provider.Capabilities.Password,
                provider.Capabilities.Expiration);
    }
}
