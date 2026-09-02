using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;

namespace SimpleList.Core.Services;

public abstract class StorageProviderBase
{
    protected IStringLocalizer Localizer { get; }

    protected StorageProviderBase(IStringLocalizer localizer = null)
    {
        Localizer = localizer;
    }

    protected string L(string key, string fallback = null)
    {
        if (Localizer == null)
        {
            return fallback ?? key;
        }
        var value = Localizer[key];
        return string.IsNullOrEmpty(value) ? (fallback ?? key) : value;
    }

    protected string LFormat(string key, params object[] args)
    {
        return Localizer == null ? string.Format(key, args) : Localizer.Format(key, args);
    }

    protected string LF(string key, string fallback, params object[] args)
    {
        return string.Format(L(key, fallback), args);
    }

    protected async Task<StorageResult<T>> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Action validateParams = null)
    {
        try
        {
            validateParams?.Invoke();
            await EnsureAuthenticatedAsync();
            var result = await operation();
            return StorageResult<T>.Success(result);
        }
        catch (ValidationException validationEx)
        {
            return StorageResult<T>.Failure(validationEx.Message, StorageErrorType.InvalidRequest, validationEx);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HandleException<T>(ex);
        }
    }

    protected Task<StorageResult<bool>> ExecuteAsync(
        Func<Task> operation,
        Action validateParams = null)
    {
        return ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, validateParams);
    }

    protected abstract Task EnsureAuthenticatedAsync();

    protected virtual StorageResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            TimeoutException => StorageResult<T>.Failure(L("OperationTimeout", "Operation timed out"), StorageErrorType.Network, exception),
            System.Net.WebException webEx => StorageResult<T>.Failure($"{L("NetworkError", "Network error")}: {webEx.Message}", StorageErrorType.Network, exception),
            UnauthorizedAccessException => StorageResult<T>.Failure(L("InsufficientPermission", "Insufficient permission"), StorageErrorType.Forbidden, exception),
            ArgumentException argEx => StorageResult<T>.Failure($"{L("ParameterError", "Parameter error")}: {argEx.Message}", StorageErrorType.InvalidRequest, exception),
            IOException ioEx => StorageResult<T>.Failure($"{L("NetworkError", "Network error")}: {ioEx.Message}", StorageErrorType.Network, exception),
            _ => StorageResult<T>.Failure($"{L("UnknownError", "Unknown error")}: {exception.Message}", StorageErrorType.Unknown, exception),
        };
    }

    protected void ValidateNotEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(LF("Validation_CannotBeEmpty", "{0} cannot be empty", paramName));
        }
    }

    protected void ValidateNotNull(object value, string paramName)
    {
        if (value == null)
        {
            throw new ValidationException(LF("Validation_CannotBeNull", "{0} cannot be null", paramName));
        }
    }

    protected void ValidateFileName(string fileName, string paramName)
    {
        ValidateNotEmpty(fileName, paramName);
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
        {
            throw new ValidationException(LF("Validation_InvalidFileName", "Filename '{0}' contains invalid characters", fileName));
        }
    }
}
