using MuseumStorage.Api.Entities;

namespace MuseumStorage.Api.Models;

public sealed record ExhibitResponse(
    long Id,
    string Code,
    string Name,
    string? Description,
    string? Category,
    string Status,
    long? StorageLocationId,
    StorageLocation? StorageLocation);
