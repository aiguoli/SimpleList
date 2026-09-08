using System;

namespace SimpleList.Core.Models;

public enum OperationLogLevel
{
    Information,
    Success,
    Warning,
    Error,
}

public sealed class OperationLogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public OperationLogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;

    public string TimestampText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
