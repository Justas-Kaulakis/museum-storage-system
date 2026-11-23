namespace MuseumStorage.Domain.Entities;

public class Exhibit
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public ExhibitStatus Status { get; set; }
    public long? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
}
