using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using Xunit;

namespace SimpleList.Tests;

public class DirectoryListingCacheTests
{
    [Fact]
    public void TryGet_ClassifiesSnapshotByAge()
    {
        DateTimeOffset now = new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);
        DirectoryListingCache cache = new(
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMinutes(3),
            clock: () => now);

        cache.Set("folder", Page("item"));

        Assert.True(cache.TryGet("folder", out DirectoryCacheSnapshot? fresh));
        Assert.Equal(DirectoryCacheFreshness.Fresh, fresh!.Freshness);

        now += TimeSpan.FromSeconds(16);
        Assert.True(cache.TryGet("folder", out DirectoryCacheSnapshot? stale));
        Assert.Equal(DirectoryCacheFreshness.Stale, stale!.Freshness);

        now += TimeSpan.FromMinutes(3);
        Assert.True(cache.TryGet("folder", out DirectoryCacheSnapshot? expired));
        Assert.Equal(DirectoryCacheFreshness.Expired, expired!.Freshness);
    }

    [Fact]
    public void Set_CopiesItemsCollection()
    {
        FileItem[] items = [new FileItem { Id = "first" }];
        DirectoryListingCache cache = new(TimeSpan.Zero, TimeSpan.Zero);

        cache.Set("folder", new PageResult<FileItem> { Items = items });
        items[0] = new FileItem { Id = "changed" };

        Assert.True(cache.TryGet("folder", out DirectoryCacheSnapshot? snapshot));
        Assert.Equal("first", snapshot!.Page.Items[0].Id);
    }

    [Fact]
    public void Set_EvictsLeastRecentlyUsedSnapshot()
    {
        DirectoryListingCache cache = new(TimeSpan.Zero, TimeSpan.Zero, capacity: 2);
        cache.Set("first", Page("1"));
        cache.Set("second", Page("2"));

        Assert.True(cache.TryGet("first", out _));
        cache.Set("third", Page("3"));

        Assert.True(cache.TryGet("first", out _));
        Assert.False(cache.TryGet("second", out _));
        Assert.True(cache.TryGet("third", out _));
    }

    [Fact]
    public void Invalidate_RemovesSnapshot()
    {
        DirectoryListingCache cache = new(TimeSpan.Zero, TimeSpan.Zero);
        cache.Set("folder", Page("item"));

        cache.Invalidate("folder");

        Assert.False(cache.TryGet("folder", out _));
    }

    [Fact]
    public void MarkStale_PreservesSnapshotAsFallback()
    {
        DirectoryListingCache cache = new(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(3));
        cache.Set("folder", Page("item"));

        cache.MarkStale("folder");

        Assert.True(cache.TryGet("folder", out DirectoryCacheSnapshot? snapshot));
        Assert.Equal(DirectoryCacheFreshness.Stale, snapshot!.Freshness);
        Assert.Equal("item", snapshot.Page.Items[0].Id);
    }

    private static PageResult<FileItem> Page(string itemId) => new()
    {
        Items = [new FileItem { Id = itemId }],
    };
}
