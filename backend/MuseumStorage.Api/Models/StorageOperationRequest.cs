namespace MuseumStorage.Api.Models;

public sealed record StorageOperationRequest
{
    public required long StorageLocationId { get; init; }
}
