using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using SimpleList.Core.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public sealed class PersistentTaskStoreTests : IDisposable
{
    public PersistentTaskStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"SimpleList.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, "tasks.json");
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsRecoverableTaskData()
    {
        PersistentTaskStore store = new(_filePath);
        PersistentTaskListDTO expected = new();
        expected.Tasks.Add(new PersistentTaskDTO
        {
            Kind = PersistentTaskKind.Upload,
            Drive = new DriveReferenceDTO
            {
                ProviderType = ProviderType.OneDrive,
                AccountId = "account",
                DriveId = "drive",
                DisplayName = "Personal",
            },
            ItemId = "parent",
            Name = "archive.zip",
            Path = @"C:\uploads\archive.zip",
            Progress = 42,
            TransferredBytes = 420,
            TotalBytes = 1000,
            ResumeToken = "resume-token",
        });

        await store.SaveAsync(expected);
        PersistentTaskListDTO actual = await store.LoadAsync();

        PersistentTaskDTO task = Assert.Single(actual.Tasks);
        Assert.Equal(PersistentTaskKind.Upload, task.Kind);
        Assert.Equal(ProviderType.OneDrive, task.Drive.ProviderType);
        Assert.Equal("account", task.Drive.AccountId);
        Assert.Equal("archive.zip", task.Name);
        Assert.Equal(42, task.Progress);
        Assert.Equal("resume-token", task.ResumeToken);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsEmptyTaskList()
    {
        await File.WriteAllTextAsync(_filePath, "not-json");

        PersistentTaskListDTO actual = await new PersistentTaskStore(_filePath).LoadAsync();

        Assert.Empty(actual.Tasks);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private readonly string _directory;
    private readonly string _filePath;
}
