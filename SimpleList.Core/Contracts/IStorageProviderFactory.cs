using SimpleList.Core.Models;

namespace SimpleList.Core.Contracts;

public interface IStorageProviderFactory
{
    IStorageProvider Create(ProviderType providerType, string driveId = null, string accountId = null);
}
