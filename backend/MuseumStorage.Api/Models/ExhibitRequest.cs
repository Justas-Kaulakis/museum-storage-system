namespace MuseumStorage.Api.Models;

public sealed record ExhibitRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public required string Status { get; init; }
    public long? StorageLocationId { get; init; }
    public double? MinTemperature { get; init; }
    public double? MaxTemperature { get; init; }
    public double? MinHumidity { get; init; }
    public double? MaxHumidity { get; init; }
    public double? MinLightLevel { get; init; }
    public double? MaxLightLevel { get; init; }
}
