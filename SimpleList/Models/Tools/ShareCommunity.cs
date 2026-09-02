using SimpleList.Core.Models;
using System;
using System.Text.Json.Serialization;

namespace SimpleList.Models;

public sealed class ShareCommunityLink
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("provider_type")]
    public string ProviderType { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("views")]
    public int Views { get; set; }
}

public class LinkResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public ShareCommunityLink Data { get; set; }
}

public sealed class LinksResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public ShareCommunityLink[] Data { get; set; }
}

public sealed class CreateLinkResponse : LinkResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public sealed class CreateCommunityLinkRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; }

    [JsonPropertyName("provider_type")]
    public string ProviderType { get; set; }
}

public sealed record ShareProviderOption(
    ProviderType ProviderType,
    string ApiValue,
    string DisplayName,
    bool SupportsPassword,
    bool SupportsExpiration);

public sealed class ShareProviderCapabilities
{
    [JsonPropertyName("public_share")]
    public bool PublicShare { get; set; }

    [JsonPropertyName("community_publish")]
    public bool CommunityPublish { get; set; }

    [JsonPropertyName("password")]
    public bool Password { get; set; }

    [JsonPropertyName("expiration")]
    public bool Expiration { get; set; }
}

public sealed class ShareProvider
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("capabilities")]
    public ShareProviderCapabilities Capabilities { get; set; }
}

public sealed class ProvidersResponse
{
    [JsonPropertyName("data")]
    public ShareProvider[] Data { get; set; }
}

public sealed class ShareCommunityAuthRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }
}

public sealed class ShareCommunityRefreshRequest
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }
}

public sealed class ShareCommunityUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }
}

public sealed class ShareCommunityAuthData
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public ShareCommunityUser User { get; set; }
}

public sealed class ShareCommunityAuthResponse
{
    [JsonPropertyName("data")]
    public ShareCommunityAuthData Data { get; set; }
}

public sealed class ShareCommunityErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
}
