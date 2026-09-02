using Microsoft.Extensions.Configuration;
using SimpleList.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Services;

public sealed class ShareCommunityApiException : Exception
{
    public ShareCommunityApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed partial class ShareCommunityApiClient
{
    private readonly HttpClient _client;
    private readonly ShareCommunityTokenStore _tokenStore;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public ShareCommunityApiClient(IConfigurationRoot configuration, ShareCommunityTokenStore tokenStore)
    {
        string baseUrl = Environment.GetEnvironmentVariable("SIMPLELIST_SHARE_COMMUNITY_URL");
#if DEBUG
        baseUrl ??= configuration.GetSection("Tools:ShareCommunity:DevelopmentUrl").Value;
#endif
        baseUrl ??= configuration.GetSection("Tools:ShareCommunity:Url").Value;
        baseUrl = baseUrl?.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri))
        {
            throw new InvalidOperationException("Tools:ShareCommunity:Url must be an absolute URL.");
        }
        _client = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("SimpleList/2.0");
        _tokenStore = tokenStore;
    }

    public bool IsAuthenticated => _tokenStore.Load() is not null;
    public bool HasSeenAuthPrompt { get => _tokenStore.HasSeenAuthPrompt; set => _tokenStore.HasSeenAuthPrompt = value; }
    public ShareCommunityUser CurrentUser => _tokenStore.Load()?.User;

    public async Task<LinksResponse> GetLinksAsync(CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v2/links", ct);
        return await ReadAsync(response, ShareCommunityJsonContext.Default.LinksResponse, ct);
    }

    public async Task<ProvidersResponse> GetProvidersAsync(CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v2/providers", ct);
        return await ReadAsync(response, ShareCommunityJsonContext.Default.ProvidersResponse, ct);
    }

    public async Task<CreateLinkResponse> CreateLinkAsync(CreateCommunityLinkRequest request, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(request, ShareCommunityJsonContext.Default.CreateCommunityLinkRequest);
        using HttpResponseMessage response = await SendAuthorizedAsync(
            token => CreateJsonRequest(HttpMethod.Post, "/api/v2/links", json, token), ct);
        return await ReadAsync(response, ShareCommunityJsonContext.Default.CreateLinkResponse, ct);
    }

    public Task<ShareCommunityAuthData> LoginAsync(string email, string password, CancellationToken ct = default) =>
        AuthenticateAsync("/api/v2/auth/login", new ShareCommunityAuthRequest { Email = email, Password = password }, ct);

    public Task<ShareCommunityAuthData> RegisterAsync(string email, string username, string password, CancellationToken ct = default) =>
        AuthenticateAsync("/api/v2/auth/register", new ShareCommunityAuthRequest { Email = email, Username = username, Password = password }, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        ShareCommunitySession session = _tokenStore.Load();
        try
        {
            if (session != null)
            {
                ShareCommunityRefreshRequest request = new() { RefreshToken = session.RefreshToken };
                string json = JsonSerializer.Serialize(request, ShareCommunityJsonContext.Default.ShareCommunityRefreshRequest);
                using HttpResponseMessage _ = await _client.SendAsync(CreateJsonRequest(HttpMethod.Post, "/api/v2/auth/logout", json), ct);
            }
        }
        finally
        {
            _tokenStore.Clear();
        }
    }

    private async Task<ShareCommunityAuthData> AuthenticateAsync(string path, ShareCommunityAuthRequest request, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(request, ShareCommunityJsonContext.Default.ShareCommunityAuthRequest);
        using HttpResponseMessage response = await _client.SendAsync(CreateJsonRequest(HttpMethod.Post, path, json), ct);
        ShareCommunityAuthResponse result = await ReadAsync(response, ShareCommunityJsonContext.Default.ShareCommunityAuthResponse, ct);
        _tokenStore.Save(result.Data);
        return result.Data;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<string, HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        ShareCommunitySession session = await EnsureFreshSessionAsync(ct);
        using HttpRequestMessage request = requestFactory(session.AccessToken);
        HttpResponseMessage response = await _client.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        session = await RefreshAsync(force: true, ct);
        using HttpRequestMessage retry = requestFactory(session.AccessToken);
        return await _client.SendAsync(retry, ct);
    }

    private async Task<ShareCommunitySession> EnsureFreshSessionAsync(CancellationToken ct)
    {
        ShareCommunitySession session = _tokenStore.Load() ?? throw new ShareCommunityApiException(
            HttpStatusCode.Unauthorized,
            Helpers.ResourceHelper.GetLocalized("ShareCommunity_SignInRequired"));
        return session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1) ? session : await RefreshAsync(force: false, ct);
    }

    private async Task<ShareCommunitySession> RefreshAsync(bool force, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            ShareCommunitySession current = _tokenStore.Load() ?? throw new ShareCommunityApiException(
                HttpStatusCode.Unauthorized,
                Helpers.ResourceHelper.GetLocalized("ShareCommunity_SignInRequired"));
            if (!force && current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return current;
            }
            ShareCommunityRefreshRequest request = new() { RefreshToken = current.RefreshToken };
            string json = JsonSerializer.Serialize(request, ShareCommunityJsonContext.Default.ShareCommunityRefreshRequest);
            using HttpResponseMessage response = await _client.SendAsync(CreateJsonRequest(HttpMethod.Post, "/api/v2/auth/refresh", json), ct);
            ShareCommunityAuthResponse result = await ReadAsync(response, ShareCommunityJsonContext.Default.ShareCommunityAuthResponse, ct);
            _tokenStore.Save(result.Data);
            return _tokenStore.Load();
        }
        catch
        {
            _tokenStore.Clear();
            throw;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, string json, string accessToken = null)
    {
        HttpRequestMessage request = new(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return request;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        string body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            ShareCommunityErrorResponse error = null;
            try
            {
                error = JsonSerializer.Deserialize(body, ShareCommunityJsonContext.Default.ShareCommunityErrorResponse);
            }
            catch (JsonException)
            {
            }
            throw new ShareCommunityApiException(response.StatusCode, error?.Message ?? body ?? response.ReasonPhrase);
        }
        return JsonSerializer.Deserialize(body, typeInfo) ?? throw new JsonException("The share community returned an empty response.");
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(LinksResponse))]
    [JsonSerializable(typeof(ProvidersResponse))]
    [JsonSerializable(typeof(CreateCommunityLinkRequest))]
    [JsonSerializable(typeof(CreateLinkResponse))]
    [JsonSerializable(typeof(ShareCommunityAuthRequest))]
    [JsonSerializable(typeof(ShareCommunityRefreshRequest))]
    [JsonSerializable(typeof(ShareCommunityAuthResponse))]
    [JsonSerializable(typeof(ShareCommunityErrorResponse))]
    internal partial class ShareCommunityJsonContext : JsonSerializerContext { }
}
