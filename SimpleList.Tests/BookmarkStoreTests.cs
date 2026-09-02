using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using SimpleList.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public class BookmarkStoreTests
{
    [Fact]
    public void BookmarkJson_Roundtrip_PreservesFields()
    {
        List<BookmarkDTO> input =
        [
            new()
            {
                Id = "item-1",
                Name = "Documents",
                IsFolder = true,
                ParentId = "parent-1",
                DriveId = "drive-1",
                ProviderType = ProviderType.GoogleDrive,
                AccountId = "account-1",
                DriveDisplayName = "Work",
                CreatedAt = DateTimeOffset.Parse("2026-05-16T10:00:00+08:00"),
                PathSegments =
                [
                    new BookmarkPathSegmentDTO { Name = "Home", ItemId = "Root" },
                    new BookmarkPathSegmentDTO { Name = "Documents", ItemId = "item-1" },
                ]
            }
        ];

        string json = JsonSerializer.Serialize(input, BookmarkDTOSourceGenerationContext.Default.ListBookmarkDTO);
        List<BookmarkDTO> roundtripped = JsonSerializer.Deserialize(json, BookmarkDTOSourceGenerationContext.Default.ListBookmarkDTO);

        Assert.NotNull(roundtripped);
        Assert.Single(roundtripped);
        Assert.Equal("item-1", roundtripped[0].Id);
        Assert.Equal("Documents", roundtripped[0].Name);
        Assert.True(roundtripped[0].IsFolder);
        Assert.Equal("parent-1", roundtripped[0].ParentId);
        Assert.Equal("drive-1", roundtripped[0].DriveId);
        Assert.Equal(ProviderType.GoogleDrive, roundtripped[0].ProviderType);
        Assert.Equal("account-1", roundtripped[0].AccountId);
        Assert.Equal("Work", roundtripped[0].DriveDisplayName);
        Assert.Equal(2, roundtripped[0].PathSegments.Count);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmpty()
    {
        BookmarkStore store = new(CreateTempFilePath());

        IReadOnlyList<BookmarkDTO> bookmarks = await store.LoadAsync();

        Assert.Empty(bookmarks);
    }

    [Fact]
    public async Task LoadAsync_EmptyFile_ReturnsEmpty()
    {
        string path = CreateTempFilePath();
        await File.WriteAllTextAsync(path, string.Empty);
        BookmarkStore store = new(path);

        IReadOnlyList<BookmarkDTO> bookmarks = await store.LoadAsync();

        Assert.Empty(bookmarks);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsEmpty()
    {
        string path = CreateTempFilePath();
        await File.WriteAllTextAsync(path, "not json");
        BookmarkStore store = new(path);

        IReadOnlyList<BookmarkDTO> bookmarks = await store.LoadAsync();

        Assert.Empty(bookmarks);
    }

    [Fact]
    public async Task AddAsync_DuplicateBookmark_ReturnsFalseAndDoesNotDuplicate()
    {
        string path = CreateTempFilePath();
        BookmarkStore store = new(path);
        BookmarkDTO bookmark = CreateBookmark();

        bool firstAdd = await store.AddAsync(bookmark);
        bool secondAdd = await store.AddAsync(CreateBookmark());
        IReadOnlyList<BookmarkDTO> bookmarks = await store.LoadAsync();

        Assert.True(firstAdd);
        Assert.False(secondAdd);
        Assert.Single(bookmarks);
    }

    private static BookmarkDTO CreateBookmark()
    {
        return new BookmarkDTO
        {
            Id = "item-1",
            Name = "Budget.xlsx",
            IsFolder = false,
            ParentId = "parent-1",
            DriveId = "drive-1",
            ProviderType = ProviderType.OneDrive,
            AccountId = "account-1",
            DriveDisplayName = "Personal",
            PathSegments =
            [
                new BookmarkPathSegmentDTO { Name = "Home", ItemId = "Root" },
            ],
            CreatedAt = DateTimeOffset.Now
        };
    }

    private static string CreateTempFilePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SimpleList.Tests");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.json");
    }
}
