using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using Xunit;

namespace SimpleList.Tests;

public class StorageProviderFactoryTests
{
    private class FakeProvider : IStorageProvider
    {
        public ProviderType ProviderType { get; init; }
        public string AccountId { get; init; }
        public string DriveId { get; init; }
        public bool IsAuthenticated => true;
        public bool SupportsTrash => false;

        public System.Threading.Tasks.Task<StorageResult<bool>> LoginAsync(System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<string>> GetDisplayNameAsync(System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<StorageQuota>> GetQuotaAsync(System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<PageResult<FileItem>>> ListChildrenAsync(string parentId, string pageToken = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> GetItemAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<System.IO.Stream>> GetItemContentAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<bool>> DownloadFileAsync(string itemId, System.IO.Stream destination, IProgress<long> progress = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<ThumbnailSet>> GetThumbnailsAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<string>> GetDownloadUrlAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> CreateFolderAsync(string parentId, string name, string conflictBehavior = "rename", System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> RenameAsync(string itemId, string newName, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<bool>> DeleteAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<bool>> PermanentDeleteAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> RestoreAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<PageResult<FileItem>>> ListTrashAsync(string pageToken = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<bool>> EmptyTrashAsync(System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> UploadFileAsync(Windows.Storage.StorageFile file, string parentId, IProgress<long> progress = null, string resumeToken = null, Action<string> resumeTokenCallback = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> UploadFileContentAsync(System.IO.Stream content, string fileName, string parentId, long? size = null, IProgress<long> progress = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<FileItem>> UploadFolderAsync(Windows.Storage.StorageFolder folder, string parentId, IProgress<long> overallProgress = null, IProgress<FolderUploadProgressInfo> detailProgress = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<ShareLink>> CreateLinkAsync(string itemId, DateTimeOffset? expiration = null, string password = null, string type = "view", System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<ShareLink>> GetShareLinkAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<bool>> RevokeShareLinkAsync(string itemId, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<bool>> ConvertToPdfAsync(string itemId, Windows.Storage.StorageFile destination, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<StorageResult<PageResult<FileItem>>> SearchAsync(string query, string scopeParentId = null, System.Threading.CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public void Create_OneDrive_InvokesOneDriveFactory()
    {
        var factory = new StorageProviderFactory(
            (driveId, accountId) => new FakeProvider { ProviderType = ProviderType.OneDrive, DriveId = driveId, AccountId = accountId },
            null);

        var provider = factory.Create(ProviderType.OneDrive, "drive-1", "acct-a");

        Assert.Equal(ProviderType.OneDrive, provider.ProviderType);
        Assert.Equal("drive-1", provider.DriveId);
        Assert.Equal("acct-a", provider.AccountId);
    }

    [Fact]
    public void Create_GoogleDrive_InvokesGoogleFactory()
    {
        var factory = new StorageProviderFactory(
            null,
            (driveId, accountId) => new FakeProvider { ProviderType = ProviderType.GoogleDrive, DriveId = driveId, AccountId = accountId });

        var provider = factory.Create(ProviderType.GoogleDrive, "root", "me@gmail.com");

        Assert.Equal(ProviderType.GoogleDrive, provider.ProviderType);
        Assert.Equal("root", provider.DriveId);
        Assert.Equal("me@gmail.com", provider.AccountId);
    }

    [Fact]
    public void Create_GoogleDrive_WithoutFactory_Throws()
    {
        var factory = new StorageProviderFactory(
            (driveId, accountId) => new FakeProvider { ProviderType = ProviderType.OneDrive },
            null);

        Assert.Throws<NotSupportedException>(() => factory.Create(ProviderType.GoogleDrive));
    }

    [Fact]
    public void Create_PikPak_InvokesPikPakFactory()
    {
        var factory = new StorageProviderFactory(
            null,
            null,
            null,
            (driveId, accountId) => new FakeProvider { ProviderType = ProviderType.PikPak, DriveId = driveId, AccountId = accountId });

        var provider = factory.Create(ProviderType.PikPak, "https://dav.example.com", "user@example.com");

        Assert.Equal(ProviderType.PikPak, provider.ProviderType);
        Assert.Equal("https://dav.example.com", provider.DriveId);
        Assert.Equal("user@example.com", provider.AccountId);
    }
}
