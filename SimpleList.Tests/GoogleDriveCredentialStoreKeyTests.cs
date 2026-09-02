using Google.Apis.Auth.OAuth2;
using SimpleList.Core.Services;
using Xunit;

namespace SimpleList.Tests;

public class GoogleDriveCredentialStoreKeyTests
{
    [Fact]
    public void NewProviders_ReceiveDistinctCredentialStoreKeys()
    {
        GoogleDriveStorageProvider first = new(new ClientSecrets(), null);
        GoogleDriveStorageProvider second = new(new ClientSecrets(), null);

        Assert.NotEqual(first.CredentialStoreKey, second.CredentialStoreKey);
    }

    [Fact]
    public void RestoredLegacyProvider_UsesLegacyCredentialStoreKey()
    {
        GoogleDriveStorageProvider provider = new(
            new ClientSecrets(),
            null,
            driveId: "root",
            accountId: "user@example.com");

        Assert.Equal("user", provider.CredentialStoreKey);
    }

    [Fact]
    public void RestoredProvider_PreservesExplicitCredentialStoreKey()
    {
        GoogleDriveStorageProvider provider = new(
            new ClientSecrets(),
            null,
            driveId: "root",
            accountId: "user@example.com",
            credentialStoreKey: "google-account-1");

        Assert.Equal("google-account-1", provider.CredentialStoreKey);
    }
}
