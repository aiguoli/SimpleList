using System;

namespace SimpleList.Models;

public class OneDriveResult<T>
{
    public bool IsSuccess { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
    public OneDriveErrorType ErrorType { get; set; }
    public Exception Exception { get; set; }

    public static OneDriveResult<T> Success(T data)
    {
        return new OneDriveResult<T>
        {
            IsSuccess = true,
            Data = data,
            ErrorMessage = null,
            ErrorType = OneDriveErrorType.None
        };
    }

    public static OneDriveResult<T> Failure(string errorMessage, OneDriveErrorType errorType, Exception exception = null)
    {
        return new OneDriveResult<T>
        {
            IsSuccess = false,
            Data = default(T),
            ErrorMessage = errorMessage,
            ErrorType = errorType,
            Exception = exception
        };
    }
}

public enum OneDriveErrorType
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
    Unknown
}
