using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using Xunit;

namespace SimpleList.Tests;

public class GraphMappersTests
{
    [Fact]
    public void ToFileItem_File_MapsCoreFields()
    {
        var driveItem = new GraphDriveItem
        {
            Id = "1234",
            Name = "doc.txt",
            Size = 42,
            ETag = "etag-1",
            Shared = new GraphShared(),
            LastModifiedDateTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ParentReference = new GraphItemReference { Id = "parent-1" },
            DownloadUrl = "https://sharepoint/dl/1234",
        };

        FileItem item = GraphMappers.ToFileItem(driveItem);

        Assert.Equal("1234", item.Id);
        Assert.Equal("doc.txt", item.Name);
        Assert.Equal(42L, item.Size);
        Assert.False(item.IsFolder);
        Assert.Equal(ProviderType.OneDrive, item.Provider);
        Assert.Equal("https://sharepoint/dl/1234", item.ProviderTokens["downloadUrl"]);
        Assert.Equal("parent-1", item.ParentId);
        Assert.Equal("etag-1", item.ETag);
        Assert.True(item.IsShared == true);
    }

    [Fact]
    public void ToFileItem_Folder_IsFolderTrue()
    {
        var driveItem = new GraphDriveItem
        {
            Id = "f1",
            Name = "Photos",
            Folder = new GraphFolder { ChildCount = 12 },
        };

        FileItem item = GraphMappers.ToFileItem(driveItem);

        Assert.True(item.IsFolder);
        Assert.Equal(12, item.ChildCount);
        Assert.True(item.IsShared == false);
    }

    [Fact]
    public void ToFileItem_WithImage_PopulatesImageMetadata()
    {
        var driveItem = new GraphDriveItem
        {
            Id = "img1",
            Name = "photo.jpg",
            Image = new GraphImage { Width = 1920, Height = 1080 },
        };

        FileItem item = GraphMappers.ToFileItem(driveItem);

        Assert.NotNull(item.Image);
        Assert.Equal(1920, item.Image.Width);
        Assert.Equal(1080, item.Image.Height);
    }

    [Fact]
    public void ToFileItem_Null_ReturnsNull()
    {
        Assert.Null(GraphMappers.ToFileItem(null));
    }

    [Fact]
    public void ToStorageQuota_MapsAllFields()
    {
        var quota = new GraphQuota { Used = 1024, Total = 5120, Remaining = 4096, Deleted = 16 };

        StorageQuota result = GraphMappers.ToStorageQuota(quota);

        Assert.Equal(1024L, result.Used);
        Assert.Equal(5120L, result.Total);
        Assert.Equal(4096L, result.Remaining);
        Assert.Equal(16L, result.Deleted);
    }

    [Fact]
    public void ToStorageQuota_Null_ReturnsEmpty()
    {
        StorageQuota result = GraphMappers.ToStorageQuota(null);
        Assert.NotNull(result);
        Assert.Null(result.Used);
    }
}
