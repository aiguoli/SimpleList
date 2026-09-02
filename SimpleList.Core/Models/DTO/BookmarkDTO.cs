using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleList.Core.Models.DTO;

public class BookmarkDTO
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsFolder { get; set; }
    public string ParentId { get; set; }
    public string DriveId { get; set; }
    public ProviderType ProviderType { get; set; } = ProviderType.OneDrive;
    public string AccountId { get; set; }
    public string DriveDisplayName { get; set; }
    public List<BookmarkPathSegmentDTO> PathSegments { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public class BookmarkPathSegmentDTO
{
    public string Name { get; set; }
    public string ItemId { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(List<BookmarkDTO>))]
[JsonSerializable(typeof(BookmarkDTO))]
[JsonSerializable(typeof(BookmarkPathSegmentDTO))]
public partial class BookmarkDTOSourceGenerationContext : JsonSerializerContext
{
}
