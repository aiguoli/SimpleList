using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

public static class StorageProviderPagingExtensions
{
    public static Task<StorageResult<PageResult<FileItem>>> ListAllChildrenAsync(
        this IStorageProvider provider,
        string parentId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ReadAllPagesAsync(
            pageToken => provider.ListChildrenAsync(parentId, pageToken, ct),
            ct);
    }

    public static Task<StorageResult<PageResult<FileItem>>> ListAllTrashAsync(
        this IStorageProvider provider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ReadAllPagesAsync(
            pageToken => provider.ListTrashAsync(pageToken, ct),
            ct);
    }

    private static async Task<StorageResult<PageResult<FileItem>>> ReadAllPagesAsync(
        Func<string, Task<StorageResult<PageResult<FileItem>>>> loadPage,
        CancellationToken ct)
    {
        List<FileItem> items = [];
        HashSet<string> seenPageTokens = new(StringComparer.Ordinal);
        string pageToken = null;

        do
        {
            ct.ThrowIfCancellationRequested();
            StorageResult<PageResult<FileItem>> result = await loadPage(pageToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return StorageResult<PageResult<FileItem>>.Failure(
                    result.ErrorMessage,
                    result.ErrorType,
                    result.Exception);
            }

            PageResult<FileItem> page = result.Data ?? new PageResult<FileItem> { Items = [] };
            if (page.Items != null)
            {
                items.AddRange(page.Items);
            }

            pageToken = string.IsNullOrWhiteSpace(page.NextPageToken)
                ? null
                : page.NextPageToken;
            if (pageToken != null && !seenPageTokens.Add(pageToken))
            {
                return StorageResult<PageResult<FileItem>>.Failure(
                    "The storage provider returned a repeated page token.",
                    StorageErrorType.Unknown);
            }
        }
        while (pageToken != null);

        return StorageResult<PageResult<FileItem>>.Success(new PageResult<FileItem>
        {
            Items = items,
            NextPageToken = null,
        });
    }
}
