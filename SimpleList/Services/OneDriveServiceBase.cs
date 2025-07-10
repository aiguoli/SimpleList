using SimpleList.Helpers;
using SimpleList.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SimpleList.Services;

public abstract class OneDriveServiceBase
{
    protected async Task<OneDriveResult<T>> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Action validateParams = null)
    {
        try
        {
            validateParams?.Invoke();
            
            await EnsureAuthenticatedAsync();
            
            var result = await operation();
            return OneDriveResult<T>.Success(result);
        }
        catch (ValidationException validationEx)
        {
            return OneDriveResult<T>.Failure(
                validationEx.Message, 
                OneDriveErrorType.InvalidRequest, 
                validationEx);
        }
        catch (Exception ex)
        {
            return HandleException<T>(ex);
        }
    }

    protected async Task<OneDriveResult<bool>> ExecuteAsync(
        Func<Task> operation,
        Action validateParams = null)
    {
        return await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, validateParams);
    }

    protected abstract Task EnsureAuthenticatedAsync();

    private OneDriveResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            Microsoft.Graph.ServiceException serviceEx => HandleServiceException<T>(serviceEx),
            Microsoft.Identity.Client.MsalUiRequiredException => OneDriveResult<T>.Failure(
                "LoginRequired".GetLocalized(), OneDriveErrorType.Authentication, exception),
            Microsoft.Identity.Client.MsalException msalEx => OneDriveResult<T>.Failure(
                $"{"AuthenticationError".GetLocalized()}: {msalEx.Message}", OneDriveErrorType.Authentication, exception),
            TimeoutException => OneDriveResult<T>.Failure(
                "OperationTimeout".GetLocalized(), OneDriveErrorType.Network, exception),
            System.Net.WebException webEx => OneDriveResult<T>.Failure(
                $"{"NetworkError".GetLocalized()}: {webEx.Message}", OneDriveErrorType.Network, exception),
            UnauthorizedAccessException => OneDriveResult<T>.Failure(
                "InsufficientPermission".GetLocalized(), OneDriveErrorType.Forbidden, exception),
            ArgumentException argEx => OneDriveResult<T>.Failure(
                $"{"ParameterError".GetLocalized()}: {argEx.Message}", OneDriveErrorType.InvalidRequest, exception),
            _ => OneDriveResult<T>.Failure(
                $"{"UnknownError".GetLocalized()}: {exception.Message}", OneDriveErrorType.Unknown, exception)
        };
    }

    private OneDriveResult<T> HandleServiceException<T>(Microsoft.Graph.ServiceException serviceException)
    {
        int statusCode = (int)serviceException.ResponseStatusCode;

        return statusCode switch
        {
            // https://learn.microsoft.com/en-us/graph/errors
            400 => OneDriveResult<T>.Failure(
                "Bad Request", OneDriveErrorType.InvalidRequest, serviceException),
            401 => OneDriveResult<T>.Failure(
                "Unauthorized", OneDriveErrorType.Authentication, serviceException),
            403 => OneDriveResult<T>.Failure(
                "Forbidden", OneDriveErrorType.Forbidden, serviceException),
            404 => OneDriveResult<T>.Failure(
                "Not Found", OneDriveErrorType.NotFound, serviceException),
            409 => OneDriveResult<T>.Failure(
                "Conflict", OneDriveErrorType.Conflict, serviceException),
            507 => OneDriveResult<T>.Failure(
                "Insufficient Storage", OneDriveErrorType.QuotaExceeded, serviceException),
            503 => OneDriveResult<T>.Failure(
                "Service Unavailable", OneDriveErrorType.ServiceUnavailable, serviceException),
            _ => OneDriveResult<T>.Failure(
                $"OneDrive Error: {serviceException.Message}",
                OneDriveErrorType.Unknown, serviceException)
        };
    }

    protected static void ValidateNotEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{paramName}{"CanNotNull".GetLocalized()}");
        }
    }

    protected static void ValidateNotNull(object value, string paramName)
    {
        if (value == null)
        {
            throw new ValidationException($"{paramName}{"CanNotNull".GetLocalized()}");
        }
    }

    protected static void ValidateFileName(string fileName, string paramName)
    {
        ValidateNotEmpty(fileName, paramName);
        
        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
        {
            throw new ValidationException(string.Format("FilenameIllegal".GetLocalized(), fileName));
        }
    }
}
