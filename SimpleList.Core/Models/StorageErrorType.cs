namespace SimpleList.Core.Models;

public enum StorageErrorType
{
    None,
    Authentication,
    Network,
    NotFound,
    Forbidden,
    QuotaExceeded,
    InvalidRequest,
    Conflict,
    ServiceUnavailable,
    Unknown,
}
