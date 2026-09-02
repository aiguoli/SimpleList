using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SimpleList.Core.Services;

public static class PikPakMappers
{
    public static PageResult<FileItem> ToPageResult(PikPakFilesResponse response)
    {
        List<FileItem> items = [];
        foreach (PikPakFile file in response?.Files ?? [])
        {
            items.Add(ToFileItem(file));
        }

        return new PageResult<FileItem>
        {
            Items = items,
            NextPageToken = response?.NextPageToken,
        };
    }

    public static FileItem ToFileItem(PikPakFile file)
    {
        bool isFolder = string.Equals(file.Kind, "drive#folder", StringComparison.OrdinalIgnoreCase);
        string mimeType = isFolder ? "inode/directory" : file.MimeType ?? GetMimeType(file.Name);
        return new FileItem
        {
            Id = string.IsNullOrWhiteSpace(file.Id) ? "Root" : file.Id,
            Name = file.Name,
            ParentId = string.IsNullOrWhiteSpace(file.ParentId) ? "Root" : file.ParentId,
            Size = isFolder ? null : TryParseLong(file.Size),
            Updated = file.ModifiedTime,
            Created = file.CreatedTime,
            IsFolder = isFolder,
            MimeType = mimeType,
            ETag = file.Hash,
            Image = IsImageMimeType(mimeType) || IsImageExtension(file.Name) ? new ImageMetadata() : null,
            Provider = ProviderType.PikPak,
            IsShared = null,
            ProviderTokens = new Dictionary<string, string>
            {
                ["hash"] = file.Hash,
                ["thumbnailLink"] = file.ThumbnailLink,
                ["downloadUrl"] = file.WebContentLink,
            },
        };
    }

    private static long? TryParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;
    }

    private static string GetMimeType(string name)
    {
        string ext = Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".csv" => "text/csv",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream",
        };
    }

    private static bool IsImageMimeType(string mimeType)
    {
        return !string.IsNullOrWhiteSpace(mimeType)
            && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageExtension(string name)
    {
        string ext = Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }
}
