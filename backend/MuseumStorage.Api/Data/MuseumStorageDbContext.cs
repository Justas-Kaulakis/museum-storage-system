using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Entities;

namespace MuseumStorage.Api.Data;

public class MuseumStorageDbContext : DbContext
{
    public MuseumStorageDbContext(DbContextOptions<MuseumStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<EnvironmentSettings> EnvironmentSettings => Set<EnvironmentSettings>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<Exhibit> Exhibits => Set<Exhibit>();
    public DbSet<StorageOperation> StorageOperations => Set<StorageOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var settings = modelBuilder.Entity<EnvironmentSettings>();
        settings.ToTable(table => table.HasCheckConstraint("CK_EnvironmentSettings_GlobalId", "Id = 1"));
        settings.Property(x => x.Id).ValueGeneratedNever();
        settings.HasData(new EnvironmentSettings
        {
            Id = Entities.EnvironmentSettings.GlobalId,
            FanOnTemperature = 26.0,
            FanOffTemperature = 25.0,
            FanOnHumidity = 70.0,
            FanOffHumidity = 65.0,
            TemperatureAlarm = 30.0,
            HumidityAlarm = 80.0
        });

        modelBuilder.Entity<StorageLocation>().HasIndex(x => x.Code).IsUnique();

        var exhibits = modelBuilder.Entity<Exhibit>();
        exhibits.HasIndex(x => x.Code).IsUnique();
        exhibits.Property(x => x.Status).HasConversion<string>().IsConcurrencyToken();
        exhibits.Property(x => x.StorageLocationId).IsConcurrencyToken();
        exhibits.HasOne(x => x.StorageLocation)
            .WithMany()
            .HasForeignKey(x => x.StorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        exhibits.ToTable(table => table.HasCheckConstraint(
            "CK_Exhibits_StatusAndStorageLocation",
            $"(Status = '{nameof(ExhibitStatus.Stored)}' AND StorageLocationId IS NOT NULL) OR " +
            $"(Status = '{nameof(ExhibitStatus.CheckedOut)}' AND StorageLocationId IS NULL)"));

        var operations = modelBuilder.Entity<StorageOperation>();
        operations.Property(x => x.Type).HasConversion<string>();
        operations.Property(x => x.CreatedAt)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        operations.HasOne<Exhibit>()
            .WithMany()
            .HasForeignKey(x => x.ExhibitId)
            .OnDelete(DeleteBehavior.Restrict);
        operations.HasOne(x => x.FromStorageLocation)
            .WithMany()
            .HasForeignKey(x => x.FromStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        operations.HasOne(x => x.ToStorageLocation)
            .WithMany()
            .HasForeignKey(x => x.ToStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
