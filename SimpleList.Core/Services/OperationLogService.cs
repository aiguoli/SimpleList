using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

/// <summary>
/// Stores a bounded in-memory operation history and persists every entry as JSON Lines.
/// Malformed historical lines and persistence failures never interrupt the user operation.
/// </summary>
public sealed class OperationLogService
{
    private const string FilePrefix = "operations-";
    private const string FileExtension = ".jsonl";
    private readonly object _gate = new();
    private readonly List<OperationLogEntry> _entries = [];
    private readonly int _maximumEntries;
    private readonly int _retentionDays;
    private bool _isInitialized;

    public OperationLogService(string directoryPath, int maximumEntries = 500, int retentionDays = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (maximumEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }
        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        }

        DirectoryPath = directoryPath;
        _maximumEntries = maximumEntries;
        _retentionDays = retentionDays;
    }

    public string DirectoryPath { get; }
    public string? LastPersistenceError { get; private set; }

    public event EventHandler<OperationLogEntry>? EntryAdded;
    public event EventHandler? EntriesCleared;

    public async Task InitializeAsync()
    {
        lock (_gate)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;
        }

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            DeleteExpiredFiles();

            List<OperationLogEntry> loaded = [];
            foreach (string path in Directory.EnumerateFiles(DirectoryPath, $"{FilePrefix}*{FileExtension}"))
            {
                string[] lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        OperationLogEntry? entry = DeserializeEntry(line);
                        if (entry != null)
                        {
                            loaded.Add(entry);
                        }
                    }
                    catch (JsonException)
                    {
                        // A partial or externally edited line should not hide valid history.
                    }
                }
            }

            lock (_gate)
            {
                HashSet<Guid> currentIds = [.. _entries.Select(entry => entry.Id)];
                IEnumerable<OperationLogEntry> merged = loaded
                    .Where(entry => !currentIds.Contains(entry.Id))
                    .Concat(_entries)
                    .OrderBy(entry => entry.Timestamp)
                    .TakeLast(_maximumEntries);
                _entries.Clear();
                _entries.AddRange(merged);
            }
            LastPersistenceError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastPersistenceError = ex.Message;
        }
    }

    public OperationLogEntry Record(
        string category,
        string action,
        string? details = null,
        OperationLogLevel level = OperationLogLevel.Information)
    {
        OperationLogEntry entry = new()
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Category = category?.Trim() ?? string.Empty,
            Action = action?.Trim() ?? string.Empty,
            Details = details?.Trim() ?? string.Empty,
        };

        lock (_gate)
        {
            _entries.Add(entry);
            TrimEntries();
            TryPersist(entry);
        }

        try
        {
            EntryAdded?.Invoke(this, entry);
        }
        catch (Exception)
        {
            // A presentation subscriber must not interrupt the recorded operation.
        }
        return entry;
    }

    public IReadOnlyList<OperationLogEntry> GetRecentEntries()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    foreach (string path in Directory.EnumerateFiles(DirectoryPath, $"{FilePrefix}*{FileExtension}"))
                    {
                        File.Delete(path);
                    }
                }
                LastPersistenceError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LastPersistenceError = ex.Message;
            }
        }

        try
        {
            EntriesCleared?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Clearing persisted data succeeded even if a presentation subscriber failed.
        }
    }

    private void TryPersist(OperationLogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(
                DirectoryPath,
                $"{FilePrefix}{entry.Timestamp.ToLocalTime():yyyy-MM-dd}{FileExtension}");
            string json = SerializeEntry(entry);
            File.AppendAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
            LastPersistenceError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastPersistenceError = ex.Message;
        }
    }

    private void TrimEntries()
    {
        int overflow = _entries.Count - _maximumEntries;
        if (overflow > 0)
        {
            _entries.RemoveRange(0, overflow);
        }
    }

    private void DeleteExpiredFiles()
    {
        DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-_retentionDays);
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, $"{FilePrefix}*{FileExtension}"))
        {
            if (File.GetLastWriteTimeUtc(path) < cutoff.UtcDateTime)
            {
                File.Delete(path);
            }
        }
    }

    private static string SerializeEntry(OperationLogEntry entry)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("id", entry.Id);
            writer.WriteString("timestamp", entry.Timestamp);
            writer.WriteNumber("level", (int)entry.Level);
            writer.WriteString("category", entry.Category);
            writer.WriteString("action", entry.Action);
            writer.WriteString("details", entry.Details);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static OperationLogEntry? DeserializeEntry(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("id", out JsonElement idElement)
            || idElement.ValueKind != JsonValueKind.String
            || !idElement.TryGetGuid(out Guid id)
            || !root.TryGetProperty("timestamp", out JsonElement timestampElement)
            || timestampElement.ValueKind != JsonValueKind.String
            || !timestampElement.TryGetDateTimeOffset(out DateTimeOffset timestamp))
        {
            return null;
        }

        int levelValue = root.TryGetProperty("level", out JsonElement levelElement)
            && levelElement.ValueKind == JsonValueKind.Number
            && levelElement.TryGetInt32(out int parsedLevel)
                ? parsedLevel
                : (int)OperationLogLevel.Information;

        return new OperationLogEntry
        {
            Id = id,
            Timestamp = timestamp,
            Level = Enum.IsDefined((OperationLogLevel)levelValue)
                ? (OperationLogLevel)levelValue
                : OperationLogLevel.Information,
            Category = GetString(root, "category"),
            Action = GetString(root, "action"),
            Details = GetString(root, "details"),
        };
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;
    }
}
