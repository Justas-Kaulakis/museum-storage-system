namespace MuseumStorage.Api.Entities;

public class StorageZone
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
}
