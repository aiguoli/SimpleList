using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleList.Core.Models.DTO;

public enum PersistentTaskKind
{
    Download,
    Upload,
    Migration,
}

public sealed class PersistentTaskListDTO
{
    public int Version { get; set; } = 1;
    public List<PersistentTaskDTO> Tasks { get; set; } = [];
}

public sealed class PersistentTaskDTO
{
    public PersistentTaskKind Kind { get; set; }
    public DriveReferenceDTO Drive { get; set; }
    public DriveReferenceDTO SourceDrive { get; set; }
    public DriveReferenceDTO TargetDrive { get; set; }
    public string ItemId { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsFolder { get; set; }
    public bool Completed { get; set; }
    public bool IsPaused { get; set; }
    public int Progress { get; set; }
    public long TransferredBytes { get; set; }
    public long TotalBytes { get; set; }
    public string ResumeToken { get; set; }
    public string TargetParentId { get; set; }
    public string TargetPathText { get; set; }
    public string Status { get; set; }
    public string ErrorMessage { get; set; }
}

public sealed class DriveReferenceDTO
{
    public ProviderType ProviderType { get; set; }
    public string AccountId { get; set; }
    public string DriveId { get; set; }
    public string DisplayName { get; set; }
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(PersistentTaskListDTO))]
internal partial class PersistentTaskJsonContext : JsonSerializerContext
{
}
