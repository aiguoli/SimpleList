using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using System;
using System.Text.Json;
using Windows.Security.Credentials;

namespace SimpleList.Services;

public class PikPakCredentialStore : IPikPakCredentialStore
{
    private const string ResourcePrefix = "SimpleList.PikPak";

    public PikPakCredentials Get(string serverUrl, string username)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        try
        {
            PasswordCredential credential = new PasswordVault().Retrieve(GetResource(serverUrl), username);
            credential.RetrievePassword();
            PikPakCredentials cached = TryDeserialize(credential.Password);
            if (cached != null)
            {
                cached.ServerUrl ??= serverUrl;
                cached.Username ??= username;
                return cached;
            }

            return new PikPakCredentials
            {
                ServerUrl = serverUrl,
                Username = username,
                Password = credential.Password,
            };
        }
        catch
        {
            return null;
        }
    }

    public void Save(PikPakCredentials credentials)
    {
        if (credentials == null
            || string.IsNullOrWhiteSpace(credentials.ServerUrl)
            || string.IsNullOrWhiteSpace(credentials.Username)
            || string.IsNullOrEmpty(credentials.Password))
        {
            return;
        }

        PasswordVault vault = new();
        string resource = GetResource(credentials.ServerUrl);
        try
        {
            PasswordCredential existing = vault.Retrieve(resource, credentials.Username);
            vault.Remove(existing);
        }
        catch
        {
        }

        vault.Add(new PasswordCredential(resource, credentials.Username, JsonSerializer.Serialize(credentials, DriveDTOSourceGenerationContext.Default.PikPakCredentials)));
    }

    private static string GetResource(string serverUrl)
    {
        return $"{ResourcePrefix}:{NormalizeServerUrl(serverUrl)}";
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        return Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri)
            ? uri.ToString().TrimEnd('/')
            : serverUrl.Trim();
    }

    private static PikPakCredentials TryDeserialize(string value)
    {
        try
        {
            return JsonSerializer.Deserialize(value, DriveDTOSourceGenerationContext.Default.PikPakCredentials);
        }
        catch
        {
            return null;
        }
    }
}
