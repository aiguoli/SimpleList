using System;
using System.Collections.Generic;

namespace SimpleList.Core.Models;

public class FileItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ParentId { get; set; }
    public long? Size { get; set; }
    public DateTimeOffset? Updated { get; set; }
    public DateTimeOffset? Created { get; set; }
    public bool IsFolder { get; set; }
    public int? ChildCount { get; set; }
    public string MimeType { get; set; }
    public string ETag { get; set; }
    public ImageMetadata Image { get; set; }
    public ProviderType Provider { get; set; }
    public bool? IsShared { get; set; }

    public IReadOnlyDictionary<string, string> ProviderTokens { get; set; }
}

public class ImageMetadata
{
    public int? Width { get; set; }
    public int? Height { get; set; }
}
