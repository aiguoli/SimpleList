using SimpleList.Core.Models;

namespace SimpleList.Core.Contracts;

public interface IPikPakCredentialStore
{
    PikPakCredentials Get(string serverUrl, string username);
    void Save(PikPakCredentials credentials);
}
