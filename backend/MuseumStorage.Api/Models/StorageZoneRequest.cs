namespace MuseumStorage.Api.Models;

public sealed record StorageZoneRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? DeviceId { get; init; }
}
