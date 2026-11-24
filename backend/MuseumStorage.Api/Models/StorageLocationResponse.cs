using System.Diagnostics.CodeAnalysis;
using MuseumStorage.Api.Entities;

namespace MuseumStorage.Api.Models;

public sealed record StorageLocationResponse(
    long Id,
    string Code,
    string Name,
    long StorageZoneId,
    string StorageZoneCode,
    string StorageZoneName)
{
    [return: NotNullIfNotNull(nameof(location))]
    public static StorageLocationResponse? From(StorageLocation? location) =>
        location is null ? null : new(location.Id, location.Code, location.Name,
            location.StorageZoneId, location.StorageZone!.Code, location.StorageZone.Name);
}
