namespace MuseumStorage.Domain.Entities;

public class StorageOperation
{
    public long Id { get; set; }
    public long ExhibitId { get; set; }
    public StorageOperationType Type { get; set; }
    public long? FromStorageLocationId { get; set; }
    public long? ToStorageLocationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public StorageLocation? FromStorageLocation { get; set; }
    public StorageLocation? ToStorageLocation { get; set; }
}
