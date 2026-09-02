using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleList.Core.Services;

internal sealed class GraphUser
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }
}

internal sealed class GraphDrive
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("quota")]
    public GraphQuota Quota { get; set; }
}

internal sealed class GraphQuota
{
    [JsonPropertyName("used")]
    public long? Used { get; set; }

    [JsonPropertyName("total")]
    public long? Total { get; set; }

    [JsonPropertyName("remaining")]
    public long? Remaining { get; set; }

    [JsonPropertyName("deleted")]
    public long? Deleted { get; set; }
}

internal sealed class GraphDriveItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("parentReference")]
    public GraphItemReference ParentReference { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public DateTimeOffset? LastModifiedDateTime { get; set; }

    [JsonPropertyName("createdDateTime")]
    public DateTimeOffset? CreatedDateTime { get; set; }

    [JsonPropertyName("folder")]
    public GraphFolder Folder { get; set; }

    [JsonPropertyName("file")]
    public GraphFile File { get; set; }

    [JsonPropertyName("image")]
    public GraphImage Image { get; set; }

    [JsonPropertyName("eTag")]
    public string ETag { get; set; }

    [JsonPropertyName("shared")]
    public GraphShared Shared { get; set; }

    [JsonPropertyName("deleted")]
    public GraphDeleted Deleted { get; set; }

    [JsonPropertyName("@microsoft.graph.downloadUrl")]
    public string DownloadUrl { get; set; }
}

internal sealed class GraphItemReference
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
}

internal sealed class GraphFolder
{
    [JsonPropertyName("childCount")]
    public int? ChildCount { get; set; }
}

internal sealed class GraphFile
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; }
}

internal sealed class GraphImage
{
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}

internal sealed class GraphShared;
internal sealed class GraphDeleted;
internal sealed class GraphEmptyObject;

internal sealed class GraphDriveItemCollection
{
    [JsonPropertyName("value")]
    public List<GraphDriveItem> Value { get; set; }

    [JsonPropertyName("@odata.nextLink")]
    public string OdataNextLink { get; set; }
}

internal sealed class GraphThumbnailSetCollection
{
    [JsonPropertyName("value")]
    public List<GraphThumbnailSet> Value { get; set; }
}

internal sealed class GraphThumbnailSet
{
    [JsonPropertyName("small")]
    public GraphThumbnail Small { get; set; }

    [JsonPropertyName("medium")]
    public GraphThumbnail Medium { get; set; }

    [JsonPropertyName("large")]
    public GraphThumbnail Large { get; set; }
}

internal sealed class GraphThumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

internal sealed class GraphPermissionCollection
{
    [JsonPropertyName("value")]
    public List<GraphPermission> Value { get; set; }
}

internal sealed class GraphPermission
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("expirationDateTime")]
    public DateTimeOffset? ExpirationDateTime { get; set; }

    [JsonPropertyName("hasPassword")]
    public bool? HasPassword { get; set; }

    [JsonPropertyName("link")]
    public GraphSharingLink Link { get; set; }
}

internal sealed class GraphSharingLink
{
    [JsonPropertyName("webUrl")]
    public string WebUrl { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}

internal sealed class GraphUploadSession
{
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; }

    [JsonPropertyName("expirationDateTime")]
    public DateTimeOffset? ExpirationDateTime { get; set; }

    [JsonPropertyName("nextExpectedRanges")]
    public List<string> NextExpectedRanges { get; set; }
}

internal sealed class GraphCreateFolderRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("folder")]
    public GraphEmptyObject Folder { get; set; } = new();

    [JsonPropertyName("@microsoft.graph.conflictBehavior")]
    public string ConflictBehavior { get; set; }
}

internal sealed class GraphRenameRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

internal sealed class GraphUploadSessionRequest
{
    [JsonPropertyName("item")]
    public GraphUploadItemProperties Item { get; set; } = new();
}

internal sealed class GraphUploadItemProperties
{
    [JsonPropertyName("@microsoft.graph.conflictBehavior")]
    public string ConflictBehavior { get; set; } = "replace";
}

internal sealed class GraphCreateLinkRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Password { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }

    [JsonPropertyName("retainInheritedPermissions")]
    public bool RetainInheritedPermissions { get; set; }

    [JsonPropertyName("expirationDateTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ExpirationDateTime { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GraphUser))]
[JsonSerializable(typeof(GraphDrive))]
[JsonSerializable(typeof(GraphDriveItem))]
[JsonSerializable(typeof(GraphDriveItemCollection))]
[JsonSerializable(typeof(GraphThumbnailSetCollection))]
[JsonSerializable(typeof(GraphPermission))]
[JsonSerializable(typeof(GraphPermissionCollection))]
[JsonSerializable(typeof(GraphUploadSession))]
[JsonSerializable(typeof(GraphCreateFolderRequest))]
[JsonSerializable(typeof(GraphRenameRequest))]
[JsonSerializable(typeof(GraphUploadSessionRequest))]
[JsonSerializable(typeof(GraphCreateLinkRequest))]
[JsonSerializable(typeof(GraphEmptyObject))]
internal partial class GraphJsonContext : JsonSerializerContext;
