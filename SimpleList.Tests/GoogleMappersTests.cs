using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using GoogleFile = Google.Apis.Drive.v3.Data.File;
using GoogleFileList = Google.Apis.Drive.v3.Data.FileList;
using GoogleAbout = Google.Apis.Drive.v3.Data.About;

namespace SimpleList.Tests;

public class GoogleMappersTests
{
    // Helper to invoke the internal mapper from the Core assembly.
    private static FileItem InvokeToFileItem(GoogleFile file)
    {
        var type = typeof(StorageProviderBase).Assembly.GetType("SimpleList.Core.Services.GoogleMappers");
        var method = type.GetMethod("ToFileItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(GoogleFile) }, null);
        return (FileItem)method.Invoke(null, new object[] { file });
    }

    private static PageResult<FileItem> InvokeToPageResult(GoogleFileList list)
    {
        var type = typeof(StorageProviderBase).Assembly.GetType("SimpleList.Core.Services.GoogleMappers");
        var method = type.GetMethod("ToPageResult", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(GoogleFileList) }, null);
        return (PageResult<FileItem>)method.Invoke(null, new object[] { list });
    }

    private static StorageQuota InvokeToStorageQuota(GoogleAbout about)
    {
        var type = typeof(StorageProviderBase).Assembly.GetType("SimpleList.Core.Services.GoogleMappers");
        var method = type.GetMethod("ToStorageQuota", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(GoogleAbout) }, null);
        return (StorageQuota)method.Invoke(null, new object[] { about });
    }

    [Fact]
    public void ToFileItem_MapsFileFields()
    {
        var file = new GoogleFile
        {
            Id = "abc",
            Name = "report.pdf",
            MimeType = "application/pdf",
            Size = 1234,
            Parents = new List<string> { "parentId" },
            WebContentLink = "https://drive/dl/abc",
            WebViewLink = "https://drive/view/abc",
            Md5Checksum = "etag-md5",
            Shared = true,
        };

        var item = InvokeToFileItem(file);

        Assert.Equal("abc", item.Id);
        Assert.Equal("report.pdf", item.Name);
        Assert.Equal("application/pdf", item.MimeType);
        Assert.Equal(1234L, item.Size);
        Assert.False(item.IsFolder);
        Assert.Equal("parentId", item.ParentId);
        Assert.Equal(ProviderType.GoogleDrive, item.Provider);
        Assert.False(item.ProviderTokens.ContainsKey("downloadUrl"));
        Assert.Equal("https://drive/dl/abc", item.ProviderTokens["webContentLink"]);
        Assert.Equal("https://drive/view/abc", item.ProviderTokens["webViewLink"]);
        Assert.Equal("etag-md5", item.ETag);
        Assert.True(item.IsShared == true);
    }

    [Fact]
    public void ToFileItem_FolderMimeType_SetsIsFolder()
    {
        var file = new GoogleFile
        {
            Id = "folder1",
            Name = "Photos",
            MimeType = "application/vnd.google-apps.folder",
        };

        var item = InvokeToFileItem(file);

        Assert.True(item.IsFolder);
        Assert.Equal("application/vnd.google-apps.folder", item.MimeType);
        Assert.True(item.IsShared == false);
    }

    [Fact]
    public void ToFileItem_Null_ReturnsNull()
    {
        var item = InvokeToFileItem(null);
        Assert.Null(item);
    }

    [Fact]
    public void ToPageResult_EmptyList_ReturnsEmptyItems()
    {
        var page = InvokeToPageResult(new GoogleFileList { Files = null });
        Assert.NotNull(page);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void ToPageResult_PreservesNextPageToken()
    {
        var page = InvokeToPageResult(new GoogleFileList
        {
            Files = new List<GoogleFile> { new() { Id = "1", Name = "a" } },
            NextPageToken = "next-cursor",
        });
        Assert.Equal("next-cursor", page.NextPageToken);
        Assert.Single(page.Items);
    }

    [Fact]
    public void ToStorageQuota_MapsAllFields()
    {
        var about = new GoogleAbout
        {
            StorageQuota = new GoogleAbout.StorageQuotaData
            {
                Usage = 100,
                Limit = 1000,
                UsageInDriveTrash = 5,
            }
        };

        var quota = InvokeToStorageQuota(about);

        Assert.Equal(100L, quota.Used);
        Assert.Equal(1000L, quota.Total);
        Assert.Equal(900L, quota.Remaining);
        Assert.Equal(5L, quota.Deleted);
    }

    [Fact]
    public void ToStorageQuota_NullAbout_ReturnsEmpty()
    {
        var quota = InvokeToStorageQuota(null);
        Assert.NotNull(quota);
        Assert.Null(quota.Used);
        Assert.Null(quota.Total);
    }
}
