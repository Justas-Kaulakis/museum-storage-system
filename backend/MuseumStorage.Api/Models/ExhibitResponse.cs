namespace MuseumStorage.Api.Models;

public sealed record ExhibitResponse(
    long Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    string Status,
    long? StorageLocationId,
    StorageLocationResponse? StorageLocation,
    double? MinTemperature,
    double? MaxTemperature,
    double? MinHumidity,
    double? MaxHumidity,
    double? MinLightLevel,
    double? MaxLightLevel);
