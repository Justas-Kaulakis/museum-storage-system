namespace MuseumStorage.Api.Models;

public sealed record StorageSuitabilityResponse(
    string Status, DateTime? ReadingAt, StorageCondition Temperature, StorageCondition Humidity, StorageCondition LightLevel);

public sealed record StorageCondition(double? Value, double? Min, double? Max, string Status);
