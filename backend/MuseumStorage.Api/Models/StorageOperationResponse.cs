using MuseumStorage.Domain.Entities;

namespace MuseumStorage.Api.Models;

public sealed record StorageOperationResponse(
    long Id,
    long ExhibitId,
    string Type,
    long? FromStorageLocationId,
    long? ToStorageLocationId,
    DateTime CreatedAt,
    StorageLocation? FromStorageLocation,
    StorageLocation? ToStorageLocation);
