using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace SimpleList.Tests;

public class PikPakMappersTests
{
    [Fact]
    public void ToPageResult_MapsFilesAndFolders()
    {
        var response = new PikPakFilesResponse
        {
            Files =
            [
                new PikPakFile
                {
                    Id = "folder-1",
                    Kind = "drive#folder",
                    Name = "Photos",
                    ModifiedTime = new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero),
                },
                new PikPakFile
                {
                    Id = "file-1",
                    Kind = "drive#file",
                    Name = "cat one.jpg",
                    ParentId = "folder-1",
                    Size = "42",
                    MimeType = "image/jpeg",
                    Hash = "ABC",
                    ThumbnailLink = "https://thumb.example/cat.jpg",
                    WebContentLink = "https://download.example/cat.jpg",
                },
            ],
            NextPageToken = "next",
        };

        PageResult<FileItem> result = PikPakMappers.ToPageResult(response);

        Assert.Equal("next", result.NextPageToken);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.Items[0].IsFolder);
        Assert.False(result.Items[1].IsFolder);
        Assert.Equal("file-1", result.Items[1].Id);
        Assert.Equal("folder-1", result.Items[1].ParentId);
        Assert.Equal(42, result.Items[1].Size);
        Assert.Equal(ProviderType.PikPak, result.Items[1].Provider);
        Assert.NotNull(result.Items[1].Image);
        Assert.Equal("https://download.example/cat.jpg", result.Items[1].ProviderTokens["downloadUrl"]);
    }
}
