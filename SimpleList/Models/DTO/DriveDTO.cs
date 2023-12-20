namespace SimpleList.Models.DTO
{
    public class DriveDTO
    {
        public string DisplayName { get; set; }
        public ProviderDTO Provider { get; set; }
    }

    public class ProviderDTO
    {
        public string HomeAccountId { get; set; }
        public string DriveId { get; set; }
    }
}
