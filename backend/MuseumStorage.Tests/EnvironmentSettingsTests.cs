using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuseumStorage.Api.Controllers;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;

namespace MuseumStorage.Tests;

public sealed class EnvironmentSettingsTests : IAsyncLifetime
{
    private static readonly EnvironmentSettingsValues DefaultSettings = new()
    {
        FanOnTemperature = 26.0,
        FanOffTemperature = 25.0,
        FanOnHumidity = 70.0,
        FanOffHumidity = 65.0,
        TemperatureAlarm = 30.0,
        HumidityAlarm = 80.0
    };

    private static readonly EnvironmentSettingsValues UpdatedSettings = new()
    {
        FanOnTemperature = 27.0,
        FanOffTemperature = 24.5,
        FanOnHumidity = 72.0,
        FanOffHumidity = 64.0,
        TemperatureAlarm = 27.0,
        HumidityAlarm = 72.0
    };

    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Fact]
    public async Task GetReturnsSeededDefaults()
    {
        await using var dbContext = CreateDbContext();
        var controller = new EnvironmentSettingsController(dbContext);

        var result = await controller.Get(1, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(DefaultSettings, Assert.IsType<EnvironmentSettingsValues>(response.Value));
    }

    [Fact]
    public async Task PutReturnsSavedSettingsAndSubsequentGetReadsThem()
    {
        await using var writeContext = CreateDbContext();
        var writeController = new EnvironmentSettingsController(writeContext);
        var putResult = await writeController.Put(1, UpdatedSettings, CancellationToken.None);

        var putResponse = Assert.IsType<OkObjectResult>(putResult.Result);
        Assert.Equal(UpdatedSettings, Assert.IsType<EnvironmentSettingsValues>(putResponse.Value));

        await using var readContext = CreateDbContext();
        var readController = new EnvironmentSettingsController(readContext);
        var getResult = await readController.Get(1, CancellationToken.None);

        var getResponse = Assert.IsType<OkObjectResult>(getResult.Result);
        Assert.Equal(UpdatedSettings, Assert.IsType<EnvironmentSettingsValues>(getResponse.Value));
        Assert.Equal(1, await readContext.EnvironmentSettings.CountAsync());
    }

    [Fact]
    public async Task CreatingAndDeletingZoneAlsoCreatesAndDeletesItsSettings()
    {
        var zone = await CreateZoneAsync("ZONE-A");
        await using var dbContext = CreateDbContext();
        var settings = new EnvironmentSettingsController(dbContext);
        Assert.Equal(DefaultSettings, GetOk(await settings.Get(zone.Id, CancellationToken.None)));
        Assert.Equal(2, await dbContext.EnvironmentSettings.CountAsync());

        var zones = new StorageZonesController(dbContext);
        Assert.IsType<NoContentResult>(await zones.Delete(zone.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>((await settings.Get(zone.Id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await settings.Put(zone.Id, UpdatedSettings, CancellationToken.None)).Result);
        Assert.Equal(1, await dbContext.EnvironmentSettings.CountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MigrationCopiesLegacySettingsToEveryZone(bool existingZones)
    {
        const string PreviousMigration = "20261124203502_AddStorageZones";
        await using var legacyConnection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await legacyConnection.OpenAsync();
        await using var dbContext = new MuseumStorageDbContext(
            new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(legacyConnection).Options);
        await dbContext.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "EnvironmentSettings"
            SET "FanOnTemperature" = 27, "FanOffTemperature" = 24.5, "FanOnHumidity" = 72,
                "FanOffHumidity" = 64, "TemperatureAlarm" = 27, "HumidityAlarm" = 72;
            """);
        if (existingZones)
            await dbContext.Database.ExecuteSqlRawAsync("""INSERT INTO "StorageZones" ("Code", "Name") VALUES ('ZONE-LEGACY', 'Legacy zone');""");
        else
            await dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "StorageZones";""");

        await dbContext.Database.MigrateAsync();

        var zones = await dbContext.StorageZones.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(existingZones ? 2 : 1, zones.Count);
        Assert.Contains(zones, x => x.Code == "DEFAULT");
        Assert.Equal(zones.Count, await dbContext.EnvironmentSettings.CountAsync());
        var controller = new EnvironmentSettingsController(dbContext);
        foreach (var zone in zones)
        {
            Assert.Null(zone.DeviceId);
            Assert.Equal(UpdatedSettings, GetOk(await controller.Get(zone.Id, CancellationToken.None)));
        }
    }

    [Fact]
    public async Task FirmwareReadsOnlyAssignedZoneAndOtherUpdatesDoNotAffectIt()
    {
        const string deviceId = "museum-storage-01";
        var zoneA = await CreateZoneAsync("ZONE-A");
        var zoneB = await CreateZoneAsync("ZONE-B");

        await using var dbContext = CreateDbContext();
        var zones = new StorageZonesController(dbContext);
        var mapped = GetOk(await zones.Update(zoneA.Id,
            new StorageZoneRequest { Code = zoneA.Code, Name = zoneA.Name, DeviceId = $" {deviceId} " }, CancellationToken.None));
        Assert.Equal(deviceId, mapped.DeviceId);
        Assert.Equal(deviceId, GetOk(await zones.GetById(zoneA.Id, CancellationToken.None)).DeviceId);
        var settings = new EnvironmentSettingsController(dbContext);
        var coolerZone = DefaultSettings with { FanOnTemperature = 18, FanOffTemperature = 17 };
        Assert.Equal(coolerZone, GetOk(await settings.Put(zoneA.Id, coolerZone, CancellationToken.None)));
        Assert.Equal(DefaultSettings, GetOk(await settings.Get(zoneB.Id, CancellationToken.None)));
        Assert.Equal(coolerZone, GetOk(await settings.GetByDeviceId(deviceId, CancellationToken.None)));
        Assert.Equal(UpdatedSettings, GetOk(await settings.Put(zoneB.Id, UpdatedSettings, CancellationToken.None)));
        Assert.Equal(coolerZone, GetOk(await settings.GetByDeviceId(deviceId, CancellationToken.None)));
        Assert.Equal(coolerZone, GetOk(await settings.Get(zoneA.Id, CancellationToken.None)));

        var unmapped = GetOk(await zones.Update(zoneA.Id,
            new StorageZoneRequest { Code = zoneA.Code, Name = zoneA.Name, DeviceId = " " }, CancellationToken.None));
        Assert.Null(unmapped.DeviceId);
        Assert.IsType<NotFoundResult>((await settings.GetByDeviceId(deviceId, CancellationToken.None)).Result);
        GetOk(await zones.Update(zoneB.Id,
            new StorageZoneRequest { Code = zoneB.Code, Name = zoneB.Name, DeviceId = deviceId }, CancellationToken.None));
        Assert.Equal(UpdatedSettings, GetOk(await settings.GetByDeviceId(deviceId, CancellationToken.None)));
    }

    [Fact]
    public async Task ZoneCreateAndUpdateRejectDuplicateDeviceId()
    {
        const string deviceId = "museum-storage-01";
        var assigned = await CreateZoneAsync("ZONE-A", deviceId);
        var unassigned = await CreateZoneAsync("ZONE-B");
        Assert.Equal(deviceId, assigned.DeviceId);

        await using var createContext = CreateDbContext();
        var create = await new StorageZonesController(createContext).Create(
            new StorageZoneRequest { Code = "ZONE-C", Name = "ZONE-C", DeviceId = $" {deviceId} " }, CancellationToken.None);
        AssertDeviceError(create);
        Assert.False(await createContext.StorageZones.AnyAsync(x => x.Code == "ZONE-C"));
        Assert.Equal(3, await createContext.EnvironmentSettings.CountAsync());

        await using var updateContext = CreateDbContext();
        var update = await new StorageZonesController(updateContext).Update(unassigned.Id,
            new StorageZoneRequest { Code = unassigned.Code, Name = unassigned.Name, DeviceId = deviceId }, CancellationToken.None);
        AssertDeviceError(update);
        Assert.Null((await updateContext.StorageZones.AsNoTracking().SingleAsync(x => x.Id == unassigned.Id)).DeviceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-device")]
    public async Task FirmwareReturnsNotFoundWhenDeviceIsUnassigned(string? deviceId)
    {
        await using var dbContext = CreateDbContext();
        var result = await new EnvironmentSettingsController(dbContext).GetByDeviceId(deviceId, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData(27.0)]
    [InlineData(28.0)]
    public Task PutRejectsFanOffTemperatureAtOrAboveFanOn(double fanOffTemperature) =>
        AssertRejectedAsync(UpdatedSettings with { FanOffTemperature = fanOffTemperature },
            nameof(EnvironmentSettingsValues.FanOffTemperature));

    [Theory]
    [InlineData(72.0)]
    [InlineData(73.0)]
    public Task PutRejectsFanOffHumidityAtOrAboveFanOn(double fanOffHumidity) =>
        AssertRejectedAsync(UpdatedSettings with { FanOffHumidity = fanOffHumidity },
            nameof(EnvironmentSettingsValues.FanOffHumidity));

    [Fact]
    public Task PutRejectsTemperatureAlarmBelowFanOn() =>
        AssertRejectedAsync(UpdatedSettings with { TemperatureAlarm = 26.9 },
            nameof(EnvironmentSettingsValues.TemperatureAlarm));

    [Fact]
    public Task PutRejectsHumidityAlarmBelowFanOn() =>
        AssertRejectedAsync(UpdatedSettings with { HumidityAlarm = 71.9 },
            nameof(EnvironmentSettingsValues.HumidityAlarm));

    [Fact]
    public Task PutRejectsTemperatureBelowRange() =>
        AssertRejectedAsync(UpdatedSettings with { FanOffTemperature = -40.1 }, "Temperature");

    [Theory]
    [InlineData(80.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public Task PutRejectsInvalidAlarmTemperature(double temperatureAlarm) =>
        AssertRejectedAsync(UpdatedSettings with { TemperatureAlarm = temperatureAlarm }, "Temperature");

    [Fact]
    public Task PutRejectsHumidityBelowRange() =>
        AssertRejectedAsync(UpdatedSettings with { FanOffHumidity = -0.1 }, "Humidity");

    [Fact]
    public Task PutRejectsHumidityAboveRange() =>
        AssertRejectedAsync(UpdatedSettings with { HumidityAlarm = 100.1 }, "Humidity");

    [Fact]
    public async Task SensorReadingsCanStillBeCreatedAndRetrieved()
    {
        await using var dbContext = CreateDbContext();
        var controller = new SensorReadingsController(dbContext);
        var request = new CreateSensorReadingRequest
        {
            DeviceId = "storage-controller",
            Temperature = 26.5,
            Humidity = 71.0,
            LightLevel = 13.0,
            FanEnabled = true,
            AlarmEnabled = false
        };

        var postResult = await controller.Create(request);
        var created = Assert.IsType<CreatedAtActionResult>(postResult.Result);
        var saved = Assert.IsType<SensorReading>(created.Value);

        await using var readContext = CreateDbContext();
        var getResult = await new SensorReadingsController(readContext).GetAll();
        var response = Assert.IsType<OkObjectResult>(getResult.Result);
        var reading = Assert.Single(Assert.IsAssignableFrom<IEnumerable<SensorReading>>(response.Value));
        Assert.Equal(saved.Id, reading.Id);
        Assert.Equal(request.DeviceId, reading.DeviceId);
        Assert.Equal(request.Temperature, reading.Temperature);
        Assert.Equal(request.Humidity, reading.Humidity);
        Assert.Equal(request.LightLevel, reading.LightLevel);
        Assert.Equal(request.FanEnabled, reading.FanEnabled);
        Assert.Equal(request.AlarmEnabled, reading.AlarmEnabled);
    }

    [Fact]
    public async Task SensorReadingsFilterAppliesBeforeTheExistingLimit()
    {
        await using var dbContext = CreateDbContext();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        dbContext.SensorReadings.AddRange(Enumerable.Range(0, 102).Select(i => new SensorReading
        {
            DeviceId = i < 2 ? "museum-storage-01" : "other-device",
            CreatedAt = start.AddMinutes(i)
        }));
        await dbContext.SaveChangesAsync();
        var controller = new SensorReadingsController(dbContext);

        var all = GetOk(await controller.GetAll()).ToArray();
        Assert.Equal(100, all.Length);
        Assert.All(all, reading => Assert.Equal("other-device", reading.DeviceId));
        Assert.True(all[0].CreatedAt > all[^1].CreatedAt);
        var filtered = GetOk(await controller.GetAll("museum-storage-01")).ToArray();
        Assert.Equal(2, filtered.Length);
        Assert.All(filtered, reading => Assert.Equal("museum-storage-01", reading.DeviceId));
        Assert.True(filtered[0].CreatedAt > filtered[1].CreatedAt);
        Assert.Empty(GetOk(await controller.GetAll("unknown-device")));
    }

    private MuseumStorageDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(_connection).Options);

    private async Task<StorageZone> CreateZoneAsync(string code, string? deviceId = null)
    {
        await using var dbContext = CreateDbContext();
        var result = await new StorageZonesController(dbContext).Create(
            new StorageZoneRequest { Code = code, Name = code, DeviceId = deviceId }, CancellationToken.None);
        return Assert.IsType<StorageZone>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static T GetOk<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void AssertDeviceError(ActionResult<StorageZone> result)
    {
        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(nameof(StorageZoneRequest.DeviceId), Assert.IsType<ValidationProblemDetails>(response.Value).Errors.Keys);
    }

    private async Task AssertRejectedAsync(EnvironmentSettingsValues settings, string errorKey)
    {
        await using var dbContext = CreateDbContext();
        var controller = new EnvironmentSettingsController(dbContext);

        var result = await controller.Put(1, settings, CancellationToken.None);

        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, response.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(response.Value);
        Assert.Contains(errorKey, problem.Errors.Keys);

        var getResult = await controller.Get(1, CancellationToken.None);
        var getResponse = Assert.IsType<OkObjectResult>(getResult.Result);
        Assert.Equal(DefaultSettings, Assert.IsType<EnvironmentSettingsValues>(getResponse.Value));
    }
}
