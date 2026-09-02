using SimpleList.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Security.Credentials;

namespace SimpleList.Services;

public sealed record ShareCommunitySession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    ShareCommunityUser User);

internal sealed class ShareCommunityLocalState
{
    public bool HasSeenAuthPrompt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public ShareCommunityUser User { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ShareCommunityLocalState))]
internal partial class ShareCommunityLocalStateJsonContext : JsonSerializerContext { }

public sealed class ShareCommunityTokenStore
{
    private const string ResourceName = "SimpleList.ShareCommunity";
    private const string AccessUserName = "access-token";
    private const string RefreshUserName = "refresh-token";

    private readonly PasswordVault _vault = new();
    private readonly object _sync = new();
    private readonly string _statePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleList",
        "ShareCommunity",
        "state.json");

    public bool HasSeenAuthPrompt
    {
        get
        {
            lock (_sync)
            {
                return ReadState().HasSeenAuthPrompt;
            }
        }
        set
        {
            lock (_sync)
            {
                ShareCommunityLocalState state = ReadState();
                state.HasSeenAuthPrompt = value;
                WriteState(state);
            }
        }
    }

    public ShareCommunitySession Load()
    {
        lock (_sync)
        {
            try
            {
                PasswordCredential access = Find(AccessUserName);
                PasswordCredential refresh = Find(RefreshUserName);
                ShareCommunityLocalState state = ReadState();
                if (access is null || refresh is null || state.ExpiresAt is null || state.User is null)
                {
                    return null;
                }
                access.RetrievePassword();
                refresh.RetrievePassword();
                return new ShareCommunitySession(access.Password, refresh.Password, state.ExpiresAt.Value, state.User);
            }
            catch
            {
                return null;
            }
        }
    }

    public void Save(ShareCommunityAuthData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        lock (_sync)
        {
            RemoveCredential(AccessUserName);
            RemoveCredential(RefreshUserName);
            try
            {
                _vault.Add(new PasswordCredential(ResourceName, AccessUserName, data.AccessToken));
                _vault.Add(new PasswordCredential(ResourceName, RefreshUserName, data.RefreshToken));
                ShareCommunityLocalState state = ReadState();
                state.ExpiresAt = data.ExpiresAt;
                state.User = data.User;
                WriteState(state);
            }
            catch
            {
                RemoveCredential(AccessUserName);
                RemoveCredential(RefreshUserName);
                throw;
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            RemoveCredential(AccessUserName);
            RemoveCredential(RefreshUserName);
            ShareCommunityLocalState state = ReadState();
            state.ExpiresAt = null;
            state.User = null;
            WriteState(state);
        }
    }

    private ShareCommunityLocalState ReadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new ShareCommunityLocalState();
            }
            string json = File.ReadAllText(_statePath);
            return JsonSerializer.Deserialize(json, ShareCommunityLocalStateJsonContext.Default.ShareCommunityLocalState)
                ?? new ShareCommunityLocalState();
        }
        catch
        {
            return new ShareCommunityLocalState();
        }
    }

    private void WriteState(ShareCommunityLocalState state)
    {
        string directory = Path.GetDirectoryName(_statePath);
        Directory.CreateDirectory(directory);
        string temporaryPath = _statePath + ".tmp";
        string json = JsonSerializer.Serialize(state, ShareCommunityLocalStateJsonContext.Default.ShareCommunityLocalState);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _statePath, true);
    }

    private PasswordCredential Find(string userName)
    {
        try
        {
            return _vault.FindAllByResource(ResourceName).FirstOrDefault(item => item.UserName == userName);
        }
        catch
        {
            return null;
        }
    }

    private void RemoveCredential(string userName)
    {
        PasswordCredential credential = Find(userName);
        if (credential != null)
        {
            _vault.Remove(credential);
        }
    }
}
