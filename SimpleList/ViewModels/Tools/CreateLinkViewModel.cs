using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Models;
using SimpleList.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels.Tools;

public partial class CreateLinkViewModel : ObservableObject
{
    private readonly ShareCommunityViewModel _community;
    private readonly ShareCommunityApiClient _api;

    public CreateLinkViewModel(ShareCommunityViewModel community)
    {
        _community = community;
        _api = community.Api;
        SelectedProvider = Providers.FirstOrDefault();
    }

    [RelayCommand]
    public async Task CreateLink()
    {
        if (string.IsNullOrWhiteSpace(Title) ||
            !Uri.TryCreate(Url, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            SelectedProvider is null)
        {
            Growl.Warning(Helpers.ResourceHelper.GetLocalized("ShareCommunityCreateLink_Validation"));
            return;
        }

        try
        {
            CreateCommunityLinkRequest request = new()
            {
                Title = Title.Trim(),
                Url = uri.AbsoluteUri,
                Password = SupportsPassword ? Password : null,
                ExpiresAt = SupportsExpiration ? Expiration?.ToString("O") : null,
                ProviderType = SelectedProvider.ApiValue,
            };
            await _api.CreateLinkAsync(request);
            await _community.Refresh();
        }
        catch (Exception e)
        {
            Growl.Error(e.Message);
            App.LogError("Error creating a community link", this, e);
        }
    }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Url { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? Expiration { get; set; }

    [ObservableProperty]
    public partial ShareProviderOption SelectedProvider { get; set; }

    public IReadOnlyList<ShareProviderOption> Providers => _community.Providers;
    public bool SupportsPassword => SelectedProvider?.SupportsPassword == true;
    public bool SupportsExpiration => SelectedProvider?.SupportsExpiration == true;
    public static DateTimeOffset Today { get; } = DateTimeOffset.Now;

    partial void OnSelectedProviderChanged(ShareProviderOption value)
    {
        OnPropertyChanged(nameof(SupportsPassword));
        OnPropertyChanged(nameof(SupportsExpiration));
    }
}
