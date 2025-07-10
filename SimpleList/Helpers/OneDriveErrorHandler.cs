using Microsoft.Graph;
using Microsoft.Identity.Client;
using SimpleList.Models;
using System;
using System.Net;
using System.Threading.Tasks;

namespace SimpleList.Helpers;

/// <summary>
/// OneDrive错误处理帮助类
/// </summary>
public static class OneDriveErrorHandler
{
    /// <summary>
    /// 执行OneDrive操作并处理异常
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">要执行的操作</param>
    /// <returns>包含操作结果的OneDriveResult</returns>
    public static async Task<OneDriveResult<T>> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            var result = await operation();
            return OneDriveResult<T>.Success(result);
        }
        catch (Exception ex)
        {
            return HandleException<T>(ex);
        }
    }

    /// <summary>
    /// 执行无返回值的OneDrive操作并处理异常
    /// </summary>
    /// <param name="operation">要执行的操作</param>
    /// <returns>包含操作结果的OneDriveResult</returns>
    public static async Task<OneDriveResult<bool>> ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return OneDriveResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return HandleException<bool>(ex);
        }
    }

    /// <summary>
    /// 处理异常并返回相应的错误结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="exception">异常</param>
    /// <returns>包含错误信息的OneDriveResult</returns>
    private static OneDriveResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            ServiceException serviceEx => HandleServiceException<T>(serviceEx),
            MsalUiRequiredException => OneDriveResult<T>.Failure(
                "需要用户重新登录", OneDriveErrorType.Authentication, exception),
            MsalException msalEx => OneDriveResult<T>.Failure(
                $"身份验证错误: {msalEx.Message}", OneDriveErrorType.Authentication, exception),
            TimeoutException => OneDriveResult<T>.Failure(
                "操作超时，请检查网络连接", OneDriveErrorType.Network, exception),
            WebException webEx => OneDriveResult<T>.Failure(
                $"网络错误: {webEx.Message}", OneDriveErrorType.Network, exception),
            UnauthorizedAccessException => OneDriveResult<T>.Failure(
                "权限不足，无法执行此操作", OneDriveErrorType.Forbidden, exception),
            ArgumentException argEx => OneDriveResult<T>.Failure(
                $"参数错误: {argEx.Message}", OneDriveErrorType.InvalidRequest, exception),
            _ => OneDriveResult<T>.Failure(
                $"未知错误: {exception.Message}", OneDriveErrorType.Unknown, exception)
        };
    }

    /// <summary>
    /// 处理Microsoft Graph Service异常
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="serviceException">Service异常</param>
    /// <returns>包含错误信息的OneDriveResult</returns>
    private static OneDriveResult<T> HandleServiceException<T>(ServiceException serviceException)
    {
        var message = serviceException.Message.ToLower();
        var statusCode = (int)serviceException.ResponseStatusCode;

        return statusCode switch
        {
            404 => OneDriveResult<T>.Failure(
                "文件或文件夹不存在", OneDriveErrorType.NotFound, serviceException),
            401 => OneDriveResult<T>.Failure(
                "身份验证失败，请重新登录", OneDriveErrorType.Authentication, serviceException),
            403 => OneDriveResult<T>.Failure(
                "访问被拒绝，权限不足", OneDriveErrorType.Forbidden, serviceException),
            400 => OneDriveResult<T>.Failure(
                "请求无效", OneDriveErrorType.InvalidRequest, serviceException),
            409 => OneDriveResult<T>.Failure(
                "文件或文件夹名称已存在", OneDriveErrorType.Conflict, serviceException),
            507 => OneDriveResult<T>.Failure(
                "存储空间不足", OneDriveErrorType.QuotaExceeded, serviceException),
            503 => OneDriveResult<T>.Failure(
                "OneDrive服务暂时不可用", OneDriveErrorType.ServiceUnavailable, serviceException),
            _ when message.Contains("quota") => OneDriveResult<T>.Failure(
                "存储空间不足", OneDriveErrorType.QuotaExceeded, serviceException),
            _ when message.Contains("not found") => OneDriveResult<T>.Failure(
                "文件或文件夹不存在", OneDriveErrorType.NotFound, serviceException),
            _ when message.Contains("access denied") => OneDriveResult<T>.Failure(
                "访问被拒绝，权限不足", OneDriveErrorType.Forbidden, serviceException),
            _ => OneDriveResult<T>.Failure(
                $"OneDrive服务错误: {serviceException.Message}",
                OneDriveErrorType.Unknown, serviceException)
        };
    }

    /// <summary>
    /// 获取用户友好的错误消息
    /// </summary>
    /// <param name="errorType">错误类型</param>
    /// <returns>用户友好的错误消息</returns>
    public static string GetUserFriendlyMessage(OneDriveErrorType errorType)
    {
        return errorType switch
        {
            OneDriveErrorType.Authentication => "请重新登录您的账户",
            OneDriveErrorType.Network => "网络连接有问题，请检查网络设置",
            OneDriveErrorType.NotFound => "找不到指定的文件或文件夹",
            OneDriveErrorType.Forbidden => "您没有执行此操作的权限",
            OneDriveErrorType.QuotaExceeded => "OneDrive存储空间已满",
            OneDriveErrorType.Conflict => "操作冲突，可能是文件名重复",
            OneDriveErrorType.ServiceUnavailable => "OneDrive服务暂时不可用，请稍后重试",
            OneDriveErrorType.InvalidRequest => "请求参数有误",
            _ => "发生了未知错误，请稍后重试"
        };
    }
}
