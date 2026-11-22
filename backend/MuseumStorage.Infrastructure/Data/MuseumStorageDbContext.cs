using Microsoft.EntityFrameworkCore;
using MuseumStorage.Domain.Entities;

namespace MuseumStorage.Infrastructure.Data;

public class MuseumStorageDbContext : DbContext
{
    public MuseumStorageDbContext(DbContextOptions<MuseumStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<EnvironmentSettings> EnvironmentSettings => Set<EnvironmentSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var settings = modelBuilder.Entity<EnvironmentSettings>();
        settings.ToTable(table => table.HasCheckConstraint("CK_EnvironmentSettings_GlobalId", "Id = 1"));
        settings.Property(x => x.Id).ValueGeneratedNever();
        settings.HasData(new EnvironmentSettings
        {
            Id = Domain.Entities.EnvironmentSettings.GlobalId,
            FanOnTemperature = 26.0,
            FanOffTemperature = 25.0,
            FanOnHumidity = 70.0,
            FanOffHumidity = 65.0,
            TemperatureAlarm = 30.0,
            HumidityAlarm = 80.0
        });
    }
}
