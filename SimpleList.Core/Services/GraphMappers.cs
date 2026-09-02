using System.Collections.Generic;
using System.Linq;

namespace SimpleList.Core.Services;

internal static class GraphMappers
{
    public static Models.FileItem ToFileItem(GraphDriveItem item)
    {
        if (item == null) return null;
        var tokens = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(item.DownloadUrl)) tokens["downloadUrl"] = item.DownloadUrl;

        Models.ImageMetadata image = item.Image == null
            ? null
            : new Models.ImageMetadata { Width = item.Image.Width, Height = item.Image.Height };

        return new Models.FileItem
        {
            Id = item.Id,
            Name = item.Name,
            ParentId = item.ParentReference?.Id,
            Size = item.Size,
            Updated = item.LastModifiedDateTime,
            Created = item.CreatedDateTime,
            IsFolder = item.Folder != null,
            ChildCount = item.Folder?.ChildCount,
            MimeType = item.File?.MimeType,
            ETag = item.ETag,
            Image = image,
            Provider = Models.ProviderType.OneDrive,
            IsShared = item.Shared != null,
            ProviderTokens = tokens,
        };
    }

    public static Models.PageResult<Models.FileItem> ToPageResult(GraphDriveItemCollection response)
    {
        return new Models.PageResult<Models.FileItem>
        {
            Items = response?.Value?.Select(ToFileItem).ToList() ?? new List<Models.FileItem>(),
            NextPageToken = response?.OdataNextLink,
        };
    }

    public static Models.StorageQuota ToStorageQuota(GraphQuota quota)
    {
        if (quota == null) return new Models.StorageQuota();
        return new Models.StorageQuota
        {
            Used = quota.Used,
            Total = quota.Total,
            Remaining = quota.Remaining,
            Deleted = quota.Deleted,
        };
    }

    public static Models.ThumbnailSet ToThumbnailSet(GraphThumbnailSetCollection response)
    {
        GraphThumbnailSet set = response?.Value?.FirstOrDefault();
        if (set == null) return new Models.ThumbnailSet();
        return new Models.ThumbnailSet
        {
            SmallUrl = set.Small?.Url,
            MediumUrl = set.Medium?.Url,
            LargeUrl = set.Large?.Url,
        };
    }
}
