namespace MuseumStorage.Api.Entities;

public class StorageLocation
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long StorageZoneId { get; set; }
    public StorageZone? StorageZone { get; set; }
}
