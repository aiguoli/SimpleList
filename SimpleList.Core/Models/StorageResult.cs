using System;

namespace SimpleList.Core.Models;

public class StorageResult<T>
{
    public bool IsSuccess { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
    public StorageErrorType ErrorType { get; set; }
    public Exception Exception { get; set; }

    public static StorageResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data,
        ErrorMessage = null,
        ErrorType = StorageErrorType.None,
    };

    public static StorageResult<T> Failure(string errorMessage, StorageErrorType errorType, Exception exception = null) => new()
    {
        IsSuccess = false,
        Data = default,
        ErrorMessage = errorMessage,
        ErrorType = errorType,
        Exception = exception,
    };
}
