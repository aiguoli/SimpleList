using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public sealed class OperationLogServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"SimpleList-log-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Record_PersistsAndPublishesEntry()
    {
        OperationLogService service = new(_directory);
        await service.InitializeAsync();
        OperationLogEntry? published = null;
        service.EntryAdded += (_, entry) => published = entry;

        OperationLogEntry recorded = service.Record("File", "Rename", "old.txt -> new.txt", OperationLogLevel.Success);

        Assert.Same(recorded, published);
        Assert.Single(service.GetRecentEntries());
        Assert.Single(Directory.GetFiles(_directory, "operations-*.jsonl"));
    }

    [Fact]
    public async Task Initialize_LoadsValidHistoryAndSkipsMalformedLines()
    {
        OperationLogService writer = new(_directory);
        await writer.InitializeAsync();
        OperationLogEntry expected = writer.Record("Task", "Queued", "photo.jpg");
        string path = Assert.Single(Directory.GetFiles(_directory, "operations-*.jsonl"));
        await File.AppendAllTextAsync(path, "not-json" + Environment.NewLine);

        OperationLogService reader = new(_directory);
        await reader.InitializeAsync();

        OperationLogEntry actual = Assert.Single(reader.GetRecentEntries());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Details, actual.Details);
    }

    [Fact]
    public async Task Clear_RemovesMemoryAndLogFiles()
    {
        OperationLogService service = new(_directory);
        await service.InitializeAsync();
        service.Record("Drive", "Added");

        service.Clear();

        Assert.Empty(service.GetRecentEntries());
        Assert.Empty(Directory.GetFiles(_directory, "operations-*.jsonl"));
    }

    [Fact]
    public async Task Record_KeepsOnlyConfiguredNumberOfRecentEntries()
    {
        OperationLogService service = new(_directory, maximumEntries: 2);
        await service.InitializeAsync();
        service.Record("Navigation", "First");
        OperationLogEntry second = service.Record("Navigation", "Second");
        OperationLogEntry third = service.Record("Navigation", "Third");

        IReadOnlyList<OperationLogEntry> entries = service.GetRecentEntries();

        Assert.Equal(2, entries.Count);
        Assert.Equal(second.Id, entries[0].Id);
        Assert.Equal(third.Id, entries[1].Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
