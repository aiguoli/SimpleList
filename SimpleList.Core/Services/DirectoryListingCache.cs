using SimpleList.Core.Models;
using System;
using System.Collections.Generic;

namespace SimpleList.Core.Services;

public enum DirectoryCacheFreshness
{
    Fresh,
    Stale,
    Expired,
}

public sealed class DirectoryCacheSnapshot
{
    internal DirectoryCacheSnapshot(
        PageResult<FileItem> page,
        DateTimeOffset cachedAt,
        DirectoryCacheFreshness freshness)
    {
        Page = page;
        CachedAt = cachedAt;
        Freshness = freshness;
    }

    public PageResult<FileItem> Page { get; }
    public DateTimeOffset CachedAt { get; }
    public DirectoryCacheFreshness Freshness { get; }
}

/// <summary>
/// Keeps a bounded set of directory snapshots in memory. Entries older than
/// <see cref="StaleDuration"/> remain available as an offline fallback until
/// they are replaced or evicted by the LRU policy.
/// </summary>
public sealed class DirectoryListingCache
{
    public DirectoryListingCache(
        TimeSpan freshDuration,
        TimeSpan staleDuration,
        int capacity = 50,
        Func<DateTimeOffset>? clock = null)
    {
        if (freshDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(freshDuration));
        }

        if (staleDuration < freshDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(staleDuration));
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        FreshDuration = freshDuration;
        StaleDuration = staleDuration;
        Capacity = capacity;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public TimeSpan FreshDuration { get; }
    public TimeSpan StaleDuration { get; }
    public int Capacity { get; }

    public bool TryGet(string directoryId, out DirectoryCacheSnapshot? snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId);

        lock (_gate)
        {
            if (!_entries.TryGetValue(directoryId, out CacheEntry? entry))
            {
                snapshot = null;
                return false;
            }

            Touch(entry);
            TimeSpan age = _clock() - entry.CachedAt;
            DirectoryCacheFreshness freshness = age <= FreshDuration
                ? DirectoryCacheFreshness.Fresh
                : age <= StaleDuration
                    ? DirectoryCacheFreshness.Stale
                    : DirectoryCacheFreshness.Expired;

            snapshot = new DirectoryCacheSnapshot(entry.Page, entry.CachedAt, freshness);
            return true;
        }
    }

    public void Set(string directoryId, PageResult<FileItem> page)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId);
        ArgumentNullException.ThrowIfNull(page);

        PageResult<FileItem> snapshot = new()
        {
            Items = page.Items is null ? [] : [.. page.Items],
            NextPageToken = page.NextPageToken,
        };

        lock (_gate)
        {
            if (_entries.TryGetValue(directoryId, out CacheEntry? existing))
            {
                existing.Page = snapshot;
                existing.CachedAt = _clock();
                Touch(existing);
                return;
            }

            LinkedListNode<string> node = _lru.AddFirst(directoryId);
            _entries[directoryId] = new CacheEntry(snapshot, _clock(), node);

            while (_entries.Count > Capacity)
            {
                LinkedListNode<string>? last = _lru.Last;
                if (last is null)
                {
                    break;
                }

                _lru.RemoveLast();
                _entries.Remove(last.Value);
            }
        }
    }

    public void Invalidate(string directoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId);

        lock (_gate)
        {
            if (_entries.Remove(directoryId, out CacheEntry? entry))
            {
                _lru.Remove(entry.Node);
            }
        }
    }

    public void MarkStale(string directoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryId);

        lock (_gate)
        {
            if (_entries.TryGetValue(directoryId, out CacheEntry? entry))
            {
                entry.CachedAt = _clock() - FreshDuration - TimeSpan.FromTicks(1);
                Touch(entry);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _lru.Clear();
        }
    }

    private void Touch(CacheEntry entry)
    {
        _lru.Remove(entry.Node);
        _lru.AddFirst(entry.Node);
    }

    private sealed class CacheEntry(
        PageResult<FileItem> page,
        DateTimeOffset cachedAt,
        LinkedListNode<string> node)
    {
        public PageResult<FileItem> Page { get; set; } = page;
        public DateTimeOffset CachedAt { get; set; } = cachedAt;
        public LinkedListNode<string> Node { get; } = node;
    }

    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = [];
}
