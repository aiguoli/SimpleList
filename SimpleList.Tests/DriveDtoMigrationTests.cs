using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace SimpleList.Tests;

public class DriveDtoMigrationTests
{
    [Fact]
    public void LegacyJson_WithoutProviderType_DeserializesAsOneDrive()
    {
        const string legacy = """
            [
              {
                "DisplayName": "Personal",
                "Provider": {
                  "HomeAccountId": "abc-account",
                  "DriveId": "drive-1"
                }
              }
            ]
            """;

        var drives = JsonSerializer.Deserialize(legacy, DriveDTOSourceGenerationContext.Default.ListDriveDTO);

        Assert.NotNull(drives);
        Assert.Single(drives);
        Assert.Equal(ProviderType.OneDrive, drives[0].ProviderType);
        Assert.Equal("Personal", drives[0].DisplayName);
        Assert.Equal("drive-1", drives[0].Provider.DriveId);
        Assert.Equal("abc-account", drives[0].Provider.HomeAccountId);
    }

    [Fact]
    public void GoogleDrive_Roundtrip_PreservesProviderType()
    {
        var input = new List<DriveDTO>
        {
            new()
            {
                DisplayName = "My Google",
                ProviderType = ProviderType.GoogleDrive,
                Provider = new ProviderDTO
                {
                    DriveId = "root",
                    HomeAccountId = "me@gmail.com",
                    CredentialStoreKey = "google-account-1",
                },
            }
        };

        string json = JsonSerializer.Serialize(input, DriveDTOSourceGenerationContext.Default.ListDriveDTO);
        var roundtripped = JsonSerializer.Deserialize(json, DriveDTOSourceGenerationContext.Default.ListDriveDTO);

        Assert.NotNull(roundtripped);
        Assert.Single(roundtripped);
        Assert.Equal(ProviderType.GoogleDrive, roundtripped[0].ProviderType);
        Assert.Equal("My Google", roundtripped[0].DisplayName);
        Assert.Equal("me@gmail.com", roundtripped[0].Provider.HomeAccountId);
        Assert.Equal("google-account-1", roundtripped[0].Provider.CredentialStoreKey);
    }

    [Fact]
    public void Serialize_AlwaysIncludesProviderType()
    {
        var input = new List<DriveDTO>
        {
            new()
            {
                DisplayName = "OneDrive",
                Provider = new ProviderDTO { DriveId = "drive-1", HomeAccountId = "acc" },
            }
        };

        string json = JsonSerializer.Serialize(input, DriveDTOSourceGenerationContext.Default.ListDriveDTO);
        Assert.Contains("ProviderType", json);
    }

    [Fact]
    public void MixedList_DeserializesBothProviderTypes()
    {
        const string json = """
            [
              { "DisplayName": "OneDrive", "Provider": { "HomeAccountId": "a", "DriveId": "1" } },
              { "DisplayName": "Google", "ProviderType": 1, "Provider": { "HomeAccountId": "b", "DriveId": "root" } }
            ]
            """;

        var drives = JsonSerializer.Deserialize(json, DriveDTOSourceGenerationContext.Default.ListDriveDTO);

        Assert.Equal(2, drives.Count);
        Assert.Equal(ProviderType.OneDrive, drives[0].ProviderType);
        Assert.Equal(ProviderType.GoogleDrive, drives[1].ProviderType);
    }

    [Fact]
    public void PikPak_Roundtrip_PreservesProviderType()
    {
        var input = new List<DriveDTO>
        {
            new()
            {
                DisplayName = "PikPak",
                ProviderType = ProviderType.PikPak,
                Provider = new ProviderDTO { DriveId = "https://dav.example.com", HomeAccountId = "me@example.com" },
            }
        };

        string json = JsonSerializer.Serialize(input, DriveDTOSourceGenerationContext.Default.ListDriveDTO);
        var roundtripped = JsonSerializer.Deserialize(json, DriveDTOSourceGenerationContext.Default.ListDriveDTO);

        Assert.NotNull(roundtripped);
        Assert.Single(roundtripped);
        Assert.Equal(ProviderType.PikPak, roundtripped[0].ProviderType);
        Assert.Equal("https://dav.example.com", roundtripped[0].Provider.DriveId);
        Assert.Equal("me@example.com", roundtripped[0].Provider.HomeAccountId);
    }
}
