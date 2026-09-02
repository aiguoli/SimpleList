using SimpleList.Core.Models.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

public class BookmarkStore
{
    public BookmarkStore(string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
    }

    public async Task<IReadOnlyList<BookmarkDTO>> LoadAsync()
    {
        if (!File.Exists(_cacheFilePath))
        {
            return [];
        }

        string jsonData = await File.ReadAllTextAsync(_cacheFilePath);
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(jsonData, BookmarkDTOSourceGenerationContext.Default.ListBookmarkDTO) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<bool> AddAsync(BookmarkDTO bookmark)
    {
        List<BookmarkDTO> bookmarks = [.. await LoadAsync()];
        if (bookmarks.Any(existing => IsSameBookmark(existing, bookmark)))
        {
            return false;
        }

        bookmarks.Add(bookmark);
        await SaveAsync(bookmarks);
        return true;
    }

    public async Task RemoveAsync(BookmarkDTO bookmark)
    {
        List<BookmarkDTO> bookmarks = [.. await LoadAsync()];
        bookmarks.RemoveAll(existing => IsSameBookmark(existing, bookmark));
        await SaveAsync(bookmarks);
    }

    public async Task SaveAsync(IReadOnlyList<BookmarkDTO> bookmarks)
    {
        string cacheDirectory = Path.GetDirectoryName(_cacheFilePath);
        if (!string.IsNullOrEmpty(cacheDirectory))
        {
            Directory.CreateDirectory(cacheDirectory);
        }

        List<BookmarkDTO> bookmarkList = [.. bookmarks];
        string jsonData = JsonSerializer.Serialize(bookmarkList, BookmarkDTOSourceGenerationContext.Default.ListBookmarkDTO);
        await File.WriteAllTextAsync(_cacheFilePath, jsonData);
    }

    public static bool IsSameBookmark(BookmarkDTO left, BookmarkDTO right)
    {
        return left.ProviderType == right.ProviderType
            && string.Equals(left.AccountId, right.AccountId, StringComparison.Ordinal)
            && string.Equals(left.DriveId, right.DriveId, StringComparison.Ordinal)
            && string.Equals(left.Id, right.Id, StringComparison.Ordinal);
    }

    private readonly string _cacheFilePath;
}
