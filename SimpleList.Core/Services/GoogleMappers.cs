using System.Collections.Generic;
using System.Linq;
using GoogleFile = Google.Apis.Drive.v3.Data.File;
using GoogleFileList = Google.Apis.Drive.v3.Data.FileList;
using GoogleAbout = Google.Apis.Drive.v3.Data.About;

namespace SimpleList.Core.Services;

internal static class GoogleMappers
{
    public const string FolderMimeType = "application/vnd.google-apps.folder";
    public const string GoogleDocMimeType = "application/vnd.google-apps.document";
    public const string GoogleSheetMimeType = "application/vnd.google-apps.spreadsheet";
    public const string GoogleSlidesMimeType = "application/vnd.google-apps.presentation";

    public static bool IsGoogleNativeDoc(string mimeType)
    {
        return mimeType == GoogleDocMimeType
            || mimeType == GoogleSheetMimeType
            || mimeType == GoogleSlidesMimeType;
    }

    public static Models.FileItem ToFileItem(GoogleFile file)
    {
        if (file == null) return null;

        var tokens = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(file.WebContentLink))
        {
            tokens["webContentLink"] = file.WebContentLink;
        }
        if (!string.IsNullOrEmpty(file.WebViewLink))
        {
            tokens["webViewLink"] = file.WebViewLink;
        }

        Models.ImageMetadata image = null;
        if (file.ImageMediaMetadata != null)
        {
            image = new Models.ImageMetadata
            {
                Width = file.ImageMediaMetadata.Width,
                Height = file.ImageMediaMetadata.Height,
            };
        }

        bool isFolder = file.MimeType == FolderMimeType;

        return new Models.FileItem
        {
            Id = file.Id,
            Name = file.Name,
            ParentId = file.Parents?.FirstOrDefault(),
            Size = file.Size,
            Updated = file.ModifiedTimeDateTimeOffset,
            Created = file.CreatedTimeDateTimeOffset,
            IsFolder = isFolder,
            ChildCount = null,
            MimeType = file.MimeType,
            ETag = file.Md5Checksum,
            Image = image,
            Provider = Models.ProviderType.GoogleDrive,
            IsShared = file.Shared ?? false,
            ProviderTokens = tokens,
        };
    }

    public static Models.PageResult<Models.FileItem> ToPageResult(GoogleFileList response)
    {
        if (response?.Files == null)
        {
            return new Models.PageResult<Models.FileItem> { Items = new List<Models.FileItem>(), NextPageToken = null };
        }
        return new Models.PageResult<Models.FileItem>
        {
            Items = response.Files.Select(ToFileItem).ToList(),
            NextPageToken = response.NextPageToken,
        };
    }

    public static Models.StorageQuota ToStorageQuota(GoogleAbout about)
    {
        if (about?.StorageQuota == null) return new Models.StorageQuota();
        return new Models.StorageQuota
        {
            Used = about.StorageQuota.Usage,
            Total = about.StorageQuota.Limit,
            Remaining = (about.StorageQuota.Limit ?? 0) - (about.StorageQuota.Usage ?? 0),
            Deleted = about.StorageQuota.UsageInDriveTrash,
        };
    }
}
