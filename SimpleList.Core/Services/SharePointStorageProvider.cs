using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

public sealed class SharePointStorageProvider : OneDriveStorageProvider
{
    private static readonly string[] SharePointScopes =
        ["User.Read", "Files.ReadWrite.All", "Sites.Read.All"];

    public SharePointStorageProvider(
        IPublicClientApplication publicClientApp,
        string siteUrl,
        Task<MsalCacheHelper> cacheHelperTask = null,
        IStringLocalizer localizer = null,
        string driveId = null,
        string accountId = null)
        : base(publicClientApp, cacheHelperTask, localizer, driveId, accountId)
    {
        SiteUrl = NormalizeSiteUrl(siteUrl);
    }

    public override ProviderType ProviderType => ProviderType.SharePoint;
    public override bool SupportsTrash => false;
    public override ShareCapabilities ShareCapabilities { get; } = new(true, false, true);
    public string SiteUrl { get; }
    public string SiteDisplayName { get; private set; }

    protected override IReadOnlyList<string> RequiredScopes => SharePointScopes;
    protected override string ProviderErrorResourceKey => "SharePoint_ErrorFormat";
    protected override string ProviderErrorFallback => "SharePoint error: {0}";

    public override Task<StorageResult<string>> GetDisplayNameAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(SiteDisplayName))
            {
                await ResolveDriveAsync(ct).ConfigureAwait(false);
            }

            return SiteDisplayName;
        });
    }

    private protected override async Task<GraphDrive> ResolveDriveAsync(CancellationToken ct)
    {
        SharePointSiteAddress address = SharePointSiteAddress.Parse(SiteUrl);
        GraphSite site = await GraphClient.GetAsync(
            $"{address.GraphSitePath}?$select=id,displayName,webUrl",
            GraphJsonContext.Default.GraphSite,
            ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(site?.Id))
        {
            throw new InvalidOperationException("Microsoft Graph did not return a SharePoint site ID.");
        }

        SiteDisplayName = string.IsNullOrWhiteSpace(site.DisplayName)
            ? address.DefaultDisplayName
            : site.DisplayName;

        GraphDrive drive = await GraphClient.GetAsync(
            $"sites/{Uri.EscapeDataString(site.Id)}/drive?$select=id,quota",
            GraphJsonContext.Default.GraphDrive,
            ct).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(drive?.Id)
            ? throw new InvalidOperationException("The SharePoint site does not have a default document library.")
            : drive;
    }

    public static bool TryNormalizeSiteUrl(string siteUrl, out string normalizedSiteUrl)
    {
        try
        {
            normalizedSiteUrl = NormalizeSiteUrl(siteUrl);
            return true;
        }
        catch (ArgumentException)
        {
            normalizedSiteUrl = null;
            return false;
        }
    }

    public static string NormalizeSiteUrl(string siteUrl)
    {
        return SharePointSiteAddress.Parse(siteUrl).NormalizedUrl;
    }
}

internal sealed record SharePointSiteAddress(
    string NormalizedUrl,
    string GraphSitePath,
    string DefaultDisplayName)
{
    public static SharePointSiteAddress Parse(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || !uri.Host.EndsWith(".sharepoint.com", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "Enter an HTTPS SharePoint site URL such as https://contoso.sharepoint.com/sites/team.",
                nameof(value));
        }

        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        string escapedPath = string.Join('/', segments.Select(Uri.EscapeDataString));
        string normalizedPath = escapedPath.Length == 0 ? string.Empty : $"/{escapedPath}";
        string normalizedUrl = $"https://{uri.IdnHost.ToLowerInvariant()}{normalizedPath}";
        string graphSitePath = escapedPath.Length == 0
            ? $"sites/{uri.IdnHost.ToLowerInvariant()}"
            : $"sites/{uri.IdnHost.ToLowerInvariant()}:{normalizedPath}";
        string displayName = segments.LastOrDefault() ?? uri.IdnHost.Split('.')[0];

        return new SharePointSiteAddress(normalizedUrl, graphSitePath, displayName);
    }
}
