using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;

namespace SimpleList.Core.Services;

public class StorageProviderFactory : IStorageProviderFactory
{
    private readonly Func<string, string, IStorageProvider> _oneDriveFactory;
    private readonly Func<string, string, IStorageProvider> _googleDriveFactory;
    private readonly Func<string, string, IStorageProvider> _localFactory;
    private readonly Func<string, string, IStorageProvider> _pikPakFactory;
    private readonly Func<string, string, string, IStorageProvider> _sharePointFactory;

    public StorageProviderFactory(
        Func<string, string, IStorageProvider> oneDriveFactory,
        Func<string, string, IStorageProvider> googleDriveFactory,
        Func<string, string, IStorageProvider> localFactory = null,
        Func<string, string, IStorageProvider> pikPakFactory = null,
        Func<string, string, string, IStorageProvider> sharePointFactory = null)
    {
        _oneDriveFactory = oneDriveFactory;
        _googleDriveFactory = googleDriveFactory;
        _localFactory = localFactory;
        _pikPakFactory = pikPakFactory;
        _sharePointFactory = sharePointFactory;
    }

    public IStorageProvider Create(ProviderType providerType, string driveId = null, string accountId = null, string siteUrl = null)
    {
        return providerType switch
        {
            ProviderType.OneDrive => _oneDriveFactory(driveId, accountId),
            ProviderType.GoogleDrive => _googleDriveFactory?.Invoke(driveId, accountId)
                ?? throw new NotSupportedException("GoogleDrive factory not registered"),
            ProviderType.Local => _localFactory?.Invoke(driveId, accountId)
                ?? new LocalStorageProvider(driveId),
            ProviderType.PikPak => _pikPakFactory?.Invoke(driveId, accountId)
                ?? throw new NotSupportedException("PikPak factory not registered"),
            ProviderType.SharePoint => _sharePointFactory?.Invoke(siteUrl, driveId, accountId)
                ?? throw new NotSupportedException("SharePoint factory not registered"),
            _ => throw new NotSupportedException($"Provider {providerType} is not supported"),
        };
    }
}
