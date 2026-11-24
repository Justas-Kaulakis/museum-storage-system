namespace MuseumStorage.Api.Models;

public sealed record StorageOperationResponse(
    long Id,
    long ExhibitId,
    string Type,
    long? FromStorageLocationId,
    long? ToStorageLocationId,
    DateTime CreatedAt,
    StorageLocationResponse? FromStorageLocation,
    StorageLocationResponse? ToStorageLocation);
