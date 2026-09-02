using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimpleList.Core.Models.DTO;

public class DriveDTO
{
    public string DisplayName { get; set; }
    public ProviderDTO Provider { get; set; }
    public ProviderType ProviderType { get; set; } = ProviderType.OneDrive;
}

public class ProviderDTO
{
    public string HomeAccountId { get; set; }
    public string DriveId { get; set; }
    public string CredentialStoreKey { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(List<DriveDTO>))]
[JsonSerializable(typeof(ProviderType))]
[JsonSerializable(typeof(PikPakCredentials))]
public partial class DriveDTOSourceGenerationContext : JsonSerializerContext
{
}
