using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;

namespace SimpleList.Helpers;

public static class OperationLogger
{
    public static void Record(
        string categoryResourceKey,
        string actionResourceKey,
        string details = null,
        OperationLogLevel level = OperationLogLevel.Information)
    {
        try
        {
            App.GetService<OperationLogService>().Record(
                ResourceHelper.GetLocalized(categoryResourceKey),
                ResourceHelper.GetLocalized(actionResourceKey),
                details,
                level);
        }
        catch (Exception)
        {
            // Logging must never interrupt the operation being recorded.
        }
    }
}
