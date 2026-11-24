using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Controllers;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;

namespace MuseumStorage.Tests;

public sealed class StorageRegistryTests : IAsyncLifetime
{
    private static readonly StorageLocationRequest ShelfA = new() { Code = "SHELF-A1", Name = "Shelf A1", StorageZoneId = 1 };
    private static readonly StorageLocationRequest ShelfB = new() { Code = "SHELF-B1", Name = "Shelf B1", StorageZoneId = 1 };
    private static readonly ExhibitRequest SampleExhibit = new()
    {
        Code = "EXHIBIT-0001",
        Name = "Ceramic Bowl",
        Description = "Glazed bowl",
        Category = "Ceramics",
        Status = "CheckedOut"
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
    public async Task StorageLocationCanBeCreatedAndRetrieved()
    {
        var created = await CreateLocationAsync(ShelfA with { Code = " SHELF-A1 " });

        await using var dbContext = CreateDbContext();
        var fetched = GetOk(await new StorageLocationsController(dbContext).GetById(created.Id, CancellationToken.None));
        Assert.True(created.Id > 0);
        Assert.Equal(ShelfA.Code, fetched.Code);
        Assert.Equal(ShelfA.Name, fetched.Name);
    }

    [Fact]
    public async Task StorageLocationCreateAndUpdateRejectDuplicateCodes()
    {
        await CreateLocationAsync(ShelfA);
        var second = await CreateLocationAsync(ShelfB);

        await using var createContext = CreateDbContext();
        var createResult = await new StorageLocationsController(createContext).Create(
            ShelfA with { Name = "Different name" }, CancellationToken.None);
        AssertValidationError(createResult, nameof(StorageLocationRequest.Code));

        await using var updateContext = CreateDbContext();
        var updateResult = await new StorageLocationsController(updateContext).Update(second.Id, ShelfA, CancellationToken.None);
        AssertValidationError(updateResult, nameof(StorageLocationRequest.Code));
    }

    [Fact]
    public async Task LocationUpdatePersistsAndListIsOrderedByCode()
    {
        var location = await CreateLocationAsync(ShelfB);
        await CreateLocationAsync(ShelfA);

        await using var writeContext = CreateDbContext();
        var update = await new StorageLocationsController(writeContext).Update(
            location.Id, ShelfB with { Name = "Renamed shelf" }, CancellationToken.None);
        Assert.Equal("Renamed shelf", GetOk(update).Name);

        await using var readContext = CreateDbContext();
        var locations = GetOk(await new StorageLocationsController(readContext).GetAll(CancellationToken.None)).ToList();
        Assert.Equal([ShelfA.Code, ShelfB.Code], locations.Select(x => x.Code));
        Assert.Equal("Renamed shelf", locations[1].Name);
    }

    [Theory]
    [InlineData("", "Shelf A1", nameof(StorageLocationRequest.Code))]
    [InlineData("SHELF-A1", " ", nameof(StorageLocationRequest.Name))]
    public async Task LocationRequiresNonblankCodeAndName(string code, string name, string field)
    {
        await using var dbContext = CreateDbContext();
        var result = await new StorageLocationsController(dbContext).Create(
            new StorageLocationRequest { Code = code, Name = name, StorageZoneId = 1 }, CancellationToken.None);
        AssertValidationError(result, field);
    }

    [Fact]
    public async Task ExhibitCanBeCreatedAndAssignedToExistingStorage()
    {
        var location = await CreateLocationAsync(ShelfA);
        var created = await CreateExhibitAsync(SampleExhibit with { Status = "Stored", StorageLocationId = location.Id });

        await using var dbContext = CreateDbContext();
        var fetched = GetOk(await new ExhibitsController(dbContext).GetById(created.Id, CancellationToken.None));
        Assert.True(created.Id > 0);
        Assert.Equal(SampleExhibit.Code, fetched.Code);
        Assert.Equal(SampleExhibit.Description, fetched.Description);
        Assert.Equal("Stored", fetched.Status);
        Assert.Equal(location.Id, fetched.StorageLocationId);
        Assert.Equal(ShelfA.Code, Assert.IsType<StorageLocationResponse>(fetched.StorageLocation).Code);
        Assert.Equal(ShelfA.Name, Assert.IsType<StorageLocationResponse>(created.StorageLocation).Name);
    }

    [Fact]
    public async Task ExhibitCreateAndUpdateRejectDuplicateCodes()
    {
        await CreateExhibitAsync(SampleExhibit);
        var second = await CreateExhibitAsync(SampleExhibit with { Code = "EXHIBIT-0002" });

        await using var createContext = CreateDbContext();
        var createResult = await new ExhibitsController(createContext).Create(SampleExhibit, CancellationToken.None);
        AssertValidationError(createResult, nameof(ExhibitRequest.Code));

        await using var updateContext = CreateDbContext();
        var updateResult = await new ExhibitsController(updateContext).Update(second.Id, SampleExhibit, CancellationToken.None);
        AssertValidationError(updateResult, nameof(ExhibitRequest.Code));
    }

    [Theory]
    [InlineData("Stored", 100L)]
    [InlineData("Stored", null)]
    [InlineData("CheckedOut", 1L)]
    public async Task ExhibitRejectsInconsistentStatusOrUnknownLocation(string status, long? locationId)
    {
        await CreateLocationAsync(ShelfA);

        await using var dbContext = CreateDbContext();
        var request = SampleExhibit with { Status = status, StorageLocationId = locationId };
        var result = await new ExhibitsController(dbContext).Create(request, CancellationToken.None);
        AssertValidationError(result, nameof(ExhibitRequest.StorageLocationId));
        Assert.Equal(0, await dbContext.Exhibits.CountAsync());
    }

    [Theory]
    [InlineData(" ", "Ceramic Bowl", "CheckedOut", nameof(ExhibitRequest.Code))]
    [InlineData("EXHIBIT-0001", "", "CheckedOut", nameof(ExhibitRequest.Name))]
    [InlineData("EXHIBIT-0001", "Ceramic Bowl", "Missing", nameof(ExhibitRequest.Status))]
    public async Task ExhibitRequiresNonblankCodeAndNameAndSupportedStatus(string code, string name, string status, string field)
    {
        await using var dbContext = CreateDbContext();
        var request = SampleExhibit with { Code = code, Name = name, Status = status };
        var result = await new ExhibitsController(dbContext).Create(request, CancellationToken.None);
        AssertValidationError(result, field);
    }

    [Fact]
    public async Task ExhibitCanBeFoundByCodeAndListIsOrderedByCode()
    {
        var second = await CreateExhibitAsync(SampleExhibit with { Code = "EXHIBIT-0002" });
        var first = await CreateExhibitAsync(SampleExhibit);

        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var fetched = GetOk(await controller.GetByCode(second.Code, CancellationToken.None));
        Assert.Equal(second.Id, fetched.Id);
        Assert.Equal("CheckedOut", fetched.Status);
        Assert.Null(fetched.StorageLocationId);
        Assert.Null(fetched.StorageLocation);

        var exhibits = GetOk(await controller.GetAll(CancellationToken.None));
        Assert.Equal([first.Code, second.Code], exhibits.Select(x => x.Code));
    }

    [Fact]
    public async Task ExhibitUpdatePersistsChangesAndRemovesLocationWhenCheckedOut()
    {
        var location = await CreateLocationAsync(ShelfA);
        var created = await CreateExhibitAsync(SampleExhibit with { Status = "Stored", StorageLocationId = location.Id });
        var request = SampleExhibit with { Name = "Restored bowl", Description = null, Category = "Pottery" };

        await using var writeContext = CreateDbContext();
        var updated = GetOk(await new ExhibitsController(writeContext).Update(created.Id, request, CancellationToken.None));

        await using var readContext = CreateDbContext();
        var fetched = GetOk(await new ExhibitsController(readContext).GetById(created.Id, CancellationToken.None));
        Assert.Equal(updated, fetched);
        Assert.Equal(request.Name, fetched.Name);
        Assert.Equal(request.Description, fetched.Description);
        Assert.Equal(request.Category, fetched.Category);
        Assert.Equal("CheckedOut", fetched.Status);
        Assert.Null(fetched.StorageLocationId);
        Assert.Null(fetched.StorageLocation);
    }

    [Fact]
    public async Task InvalidExhibitUpdatePreservesPreviousAssignment()
    {
        var location = await CreateLocationAsync(ShelfA);
        var created = await CreateExhibitAsync(SampleExhibit with { Status = "Stored", StorageLocationId = location.Id });

        await using var writeContext = CreateDbContext();
        var request = SampleExhibit with { StorageLocationId = location.Id };
        var update = await new ExhibitsController(writeContext).Update(created.Id, request, CancellationToken.None);
        AssertValidationError(update, nameof(ExhibitRequest.StorageLocationId));

        await using var readContext = CreateDbContext();
        var exhibit = await readContext.Exhibits.SingleAsync();
        Assert.Equal(ExhibitStatus.Stored, exhibit.Status);
        Assert.Equal(location.Id, exhibit.StorageLocationId);
    }

    [Fact]
    public async Task LookupAndUpdateReturnNotFoundForMissingRecords()
    {
        await using var dbContext = CreateDbContext();
        var locations = new StorageLocationsController(dbContext);
        var exhibits = new ExhibitsController(dbContext);
        Assert.IsType<NotFoundResult>((await locations.GetById(1, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await locations.Update(1, ShelfA, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await exhibits.GetById(1, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await exhibits.GetByCode(SampleExhibit.Code, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await exhibits.Update(1, SampleExhibit, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task DatabaseRestrictsDeletionOfLocationReferencedByExhibit()
    {
        var location = await CreateLocationAsync(ShelfA);
        await CreateExhibitAsync(SampleExhibit with { Status = "Stored", StorageLocationId = location.Id });

        await using var dbContext = CreateDbContext();
        await Assert.ThrowsAsync<SqliteException>(() => dbContext.StorageLocations.ExecuteDeleteAsync());
        Assert.Equal(location.Id, (await dbContext.Exhibits.SingleAsync()).StorageLocationId);
    }

    [Fact]
    public async Task ZoneCanBeCreatedUpdatedAndRetrieved()
    {
        var zone = await CreateZoneAsync(" ZONE-A ", " Pagrindinė saugykla ");
        Assert.Equal("ZONE-A", zone.Code);
        Assert.Equal("Pagrindinė saugykla", zone.Name);

        await using var dbContext = CreateDbContext();
        var controller = new StorageZonesController(dbContext);
        var request = new StorageZoneRequest { Code = "ZONE-B", Name = "Antroji saugykla" };
        Assert.Equal(request.Code, GetOk(await controller.Update(zone.Id, request, CancellationToken.None)).Code);
        var stored = GetOk(await controller.GetById(zone.Id, CancellationToken.None));
        Assert.Equal(request.Name, stored.Name);
        var zones = GetOk(await controller.GetAll(CancellationToken.None));
        Assert.Equal(["DEFAULT", "ZONE-B"], zones.Select(x => x.Code));
    }

    [Fact]
    public async Task ZoneCreateAndUpdateRejectDuplicateCode()
    {
        var zone = await CreateZoneAsync("ZONE-A");
        var second = await CreateZoneAsync("ZONE-B");
        var request = new StorageZoneRequest { Code = zone.Code, Name = "Kita zona" };

        await using var createContext = CreateDbContext();
        AssertValidationError(await new StorageZonesController(createContext).Create(request, CancellationToken.None), nameof(request.Code));

        await using var updateContext = CreateDbContext();
        AssertValidationError(await new StorageZonesController(updateContext).Update(second.Id, request, CancellationToken.None), nameof(request.Code));
    }

    [Theory]
    [InlineData(" ", "Saugykla", nameof(StorageZoneRequest.Code))]
    [InlineData("ZONE-A", "", nameof(StorageZoneRequest.Name))]
    public async Task ZoneRequiresNonblankCodeAndName(string code, string name, string field)
    {
        await using var dbContext = CreateDbContext();
        var request = new StorageZoneRequest { Code = code, Name = name };
        AssertValidationError(await new StorageZonesController(dbContext).Create(request, CancellationToken.None), field);
    }

    [Fact]
    public async Task OnlyAnEmptyZoneCanBeDeleted()
    {
        var occupied = await CreateZoneAsync("ZONE-A");
        var empty = await CreateZoneAsync("ZONE-B");
        var location = await CreateLocationAsync(ShelfA with { StorageZoneId = occupied.Id });
        await using var dbContext = CreateDbContext();
        var controller = new StorageZonesController(dbContext);

        Assert.IsType<ConflictObjectResult>(await controller.Delete(occupied.Id, CancellationToken.None));
        Assert.Equal(occupied.Id, (await dbContext.StorageLocations.SingleAsync(x => x.Id == location.Id)).StorageZoneId);
        Assert.IsType<NoContentResult>(await controller.Delete(empty.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Delete(empty.Id, CancellationToken.None));
    }

    [Fact]
    public async Task LocationCanBeCreatedInAZoneAndReassigned()
    {
        var first = await CreateZoneAsync("ZONE-A", "Pagrindinė saugykla");
        var second = await CreateZoneAsync("ZONE-B", "Antroji saugykla");
        var location = await CreateLocationAsync(ShelfA with { StorageZoneId = first.Id });
        Assert.Equal(first.Id, location.StorageZoneId);
        Assert.Equal(first.Code, location.StorageZoneCode);
        Assert.Equal(first.Name, location.StorageZoneName);

        await using var dbContext = CreateDbContext();
        var controller = new StorageLocationsController(dbContext);
        var request = ShelfA with { Code = "B-SHELF-01", Name = "Lentyna B1", StorageZoneId = second.Id };
        var updated = GetOk(await controller.Update(location.Id, request, CancellationToken.None));
        Assert.Equal(second.Id, updated.StorageZoneId);
        Assert.Equal(second.Name, updated.StorageZoneName);
        Assert.Equal(request.Code, updated.Code);
        Assert.Equal(request.Name, updated.Name);
        Assert.Equal(updated, GetOk(await controller.GetById(location.Id, CancellationToken.None)));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(999L)]
    public async Task LocationCannotBeCreatedOrUpdatedWithAnUnknownZone(long zoneId)
    {
        var location = await CreateLocationAsync(ShelfA);
        var request = ShelfB with { StorageZoneId = zoneId };
        await using var context = CreateDbContext();
        var controller = new StorageLocationsController(context);
        AssertValidationError(await controller.Create(request, CancellationToken.None), nameof(request.StorageZoneId));
        controller.ModelState.Clear();
        AssertValidationError(await controller.Update(location.Id, request, CancellationToken.None), nameof(request.StorageZoneId));
        Assert.Equal(location, GetOk(await controller.GetById(location.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task UnusedLocationCanBeDeletedAndOccupiedLocationCannot()
    {
        var used = await CreateLocationAsync(ShelfA);
        var unused = await CreateLocationAsync(ShelfB);
        await CreateExhibitAsync(SampleExhibit with { Status = "Stored", StorageLocationId = used.Id });
        await using var dbContext = CreateDbContext();
        var controller = new StorageLocationsController(dbContext);

        Assert.IsType<ConflictObjectResult>(await controller.Delete(used.Id, CancellationToken.None));
        Assert.IsType<NoContentResult>(await controller.Delete(unused.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Delete(unused.Id, CancellationToken.None));
        Assert.True(await dbContext.StorageLocations.AnyAsync(x => x.Id == used.Id));
    }

    [Fact]
    public async Task DeletingFormerLocationPreservesHistoryAndTheOtherLocation()
    {
        var from = await CreateLocationAsync(ShelfA);
        var to = await CreateLocationAsync(ShelfB);
        var exhibit = await CreateExhibitAsync(SampleExhibit with { Status = "Stored", StorageLocationId = from.Id });
        await using var writeContext = CreateDbContext();
        var exhibits = new ExhibitsController(writeContext);
        GetOk(await exhibits.Move(exhibit.Id, new StorageOperationRequest { StorageLocationId = to.Id }, CancellationToken.None));
        var before = Assert.Single(GetOk(await exhibits.GetOperations(exhibit.Id, CancellationToken.None)));

        await using var deleteContext = CreateDbContext();
        Assert.IsType<NoContentResult>(await new StorageLocationsController(deleteContext).Delete(from.Id, CancellationToken.None));

        await using var readContext = CreateDbContext();
        var history = GetOk(await new ExhibitsController(readContext).GetOperations(exhibit.Id, CancellationToken.None));
        Assert.Equal(before with { FromStorageLocationId = null, FromStorageLocation = null }, Assert.Single(history));
        Assert.Equal(to.Id, (await readContext.Exhibits.SingleAsync()).StorageLocationId);
    }

    [Fact]
    public async Task ExhibitUpdateCanChangeCodeWithoutAddingAStorageOperation()
    {
        var location = await CreateLocationAsync(ShelfA);
        var request = SampleExhibit with { Status = "Stored", StorageLocationId = location.Id };
        var exhibit = await CreateExhibitAsync(request);
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var update = request with { Code = "EXHIBIT-0002", Name = "Naujas pavadinimas" };

        var result = GetOk(await controller.Update(exhibit.Id, update, CancellationToken.None));
        Assert.Equal(update.Code, result.Code);
        Assert.Equal(update.Name, result.Name);
        Assert.Equal(location.Id, result.StorageLocationId);
        Assert.Empty(await dbContext.StorageOperations.ToListAsync());
    }

    [Fact]
    public async Task ExhibitDeletionRemovesOnlyItsHistory()
    {
        var shelf = await CreateLocationAsync(ShelfA);
        var removed = await CreateExhibitAsync(SampleExhibit);
        var kept = await CreateExhibitAsync(SampleExhibit with { Code = "EXHIBIT-0002" });
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var assignment = new StorageOperationRequest { StorageLocationId = shelf.Id };
        GetOk(await controller.CheckIn(removed.Id, assignment, CancellationToken.None));
        GetOk(await controller.CheckIn(kept.Id, assignment, CancellationToken.None));

        Assert.IsType<NoContentResult>(await controller.Delete(removed.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Delete(removed.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>((await controller.GetById(removed.Id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.GetOperations(removed.Id, CancellationToken.None)).Result);
        Assert.Equal(kept.Id, (await dbContext.Exhibits.SingleAsync()).Id);
        Assert.Equal(kept.Id, (await dbContext.StorageOperations.SingleAsync()).ExhibitId);
        Assert.True(await dbContext.StorageLocations.AnyAsync(x => x.Id == shelf.Id));
    }

    [Fact]
    public async Task ExhibitsCanBeFilteredBySearchCategoryStatusZoneAndLocation()
    {
        var zone = await CreateZoneAsync("ZONE-A");
        var firstShelf = await CreateLocationAsync(ShelfA with { StorageZoneId = zone.Id });
        var secondShelf = await CreateLocationAsync(ShelfB with { StorageZoneId = zone.Id });
        var otherShelf = await CreateLocationAsync(ShelfA with { Code = "SHELF-C1" });
        var first = await CreateExhibitAsync(SampleExhibit with { Name = "Žalioji lėkštė", Category = "Keramika", Status = "Stored", StorageLocationId = firstShelf.Id });
        var second = await CreateExhibitAsync(SampleExhibit with { Code = "EXHIBIT-0002", Name = "Įrankis", Status = "Stored", StorageLocationId = secondShelf.Id });
        var checkedOut = await CreateExhibitAsync(SampleExhibit with { Code = "EXHIBIT-0003" });
        await CreateExhibitAsync(SampleExhibit with { Code = "EXHIBIT-0004", Status = "Stored", StorageLocationId = otherShelf.Id });

        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var ct = CancellationToken.None;
        Assert.Equal(first.Id, Assert.Single(GetOk(await controller.GetAll(ct, search: " ŽALIOJI "))).Id);
        Assert.Equal(second.Id, Assert.Single(GetOk(await controller.GetAll(ct, search: "exhibit-0002"))).Id);
        Assert.Equal(first.Id, Assert.Single(GetOk(await controller.GetAll(ct, category: "KERAM"))).Id);
        Assert.Equal(checkedOut.Id, Assert.Single(GetOk(await controller.GetAll(ct, status: "CheckedOut"))).Id);
        Assert.Equal([first.Id, second.Id], GetOk(await controller.GetAll(ct, zoneId: zone.Id)).Select(x => x.Id));
        Assert.Equal(first.Id, Assert.Single(GetOk(await controller.GetAll(ct, storageLocationId: firstShelf.Id))).Id);
        Assert.Equal(second.Id, Assert.Single(GetOk(await controller.GetAll(ct, search: "įrankis", status: "Stored", zoneId: zone.Id, storageLocationId: secondShelf.Id))).Id);
        Assert.Empty(GetOk(await controller.GetAll(ct, status: "CheckedOut", zoneId: zone.Id)));
        AssertValidationError(await controller.GetAll(ct, status: "Invalid"), "status");
    }

    [Fact]
    public async Task ExhibitRequirementsRoundTripAndCanBeCleared()
    {
        var request = SampleExhibit with
        {
            MinTemperature = 18, MaxTemperature = 22, MinHumidity = 40, MaxHumidity = 60,
            MinLightLevel = 0, MaxLightLevel = 200
        };
        var exhibit = await CreateExhibitAsync(request);
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var fetched = GetOk(await controller.GetById(exhibit.Id, CancellationToken.None));
        Assert.Equal(18, fetched.MinTemperature);
        Assert.Equal(22, fetched.MaxTemperature);
        Assert.Equal(40, fetched.MinHumidity);
        Assert.Equal(60, fetched.MaxHumidity);
        Assert.Equal(0, fetched.MinLightLevel);
        Assert.Equal(200, fetched.MaxLightLevel);

        var updated = GetOk(await controller.Update(exhibit.Id, request with { MinTemperature = null, MaxLightLevel = 180 }, CancellationToken.None));
        Assert.Null(updated.MinTemperature);
        Assert.Equal(180, GetOk(await controller.GetById(exhibit.Id, CancellationToken.None)).MaxLightLevel);
    }

    [Theory]
    [InlineData(23, 22, 40, 60, 0, 200, nameof(ExhibitRequest.MinTemperature))]
    [InlineData(-41, 22, 40, 60, 0, 200, nameof(ExhibitRequest.MinTemperature))]
    [InlineData(18, 22, 60, 40, 0, 200, nameof(ExhibitRequest.MinHumidity))]
    [InlineData(18, 22, 40, 101, 0, 200, nameof(ExhibitRequest.MaxHumidity))]
    [InlineData(18, 22, 40, 60, 300, 200, nameof(ExhibitRequest.MinLightLevel))]
    [InlineData(18, 22, 40, 60, -1, 200, nameof(ExhibitRequest.MinLightLevel))]
    public async Task ExhibitRejectsInvalidRanges(double minTemp, double maxTemp, double minHumidity, double maxHumidity,
        double minLight, double maxLight, string field)
    {
        await using var dbContext = CreateDbContext();
        var result = await new ExhibitsController(dbContext).Create(SampleExhibit with
        {
            MinTemperature = minTemp, MaxTemperature = maxTemp, MinHumidity = minHumidity,
            MaxHumidity = maxHumidity, MinLightLevel = minLight, MaxLightLevel = maxLight
        }, CancellationToken.None);
        AssertValidationError(result, field);
    }

    [Theory]
    [InlineData(18, 60, 200, 40.0, "Suitable", "WithinRange", "WithinRange", "WithinRange")]
    [InlineData(24, 55, 200, 40.0, "Unsuitable", "AboveMaximum", "WithinRange", "WithinRange")]
    [InlineData(17, 55, 200, 40.0, "Unsuitable", "BelowMinimum", "WithinRange", "WithinRange")]
    [InlineData(20, 30, 300, 40.0, "Unsuitable", "WithinRange", "BelowMinimum", "AboveMaximum")]
    [InlineData(20, 99, 100, null, "Suitable", "WithinRange", "NotConfigured", "WithinRange")]
    public async Task SuitabilityUsesOnlyTheLatestReadingFromTheExhibitsZone(
        double temperature, double humidity, double lux, double? minHumidity,
        string status, string tempStatus, string humidityStatus, string lightStatus)
    {
        var shelf = await CreateLocationAsync(ShelfA);
        var exhibit = await CreateExhibitAsync(SampleExhibit with
        {
            Status = "Stored", StorageLocationId = shelf.Id, MinTemperature = 18, MaxTemperature = 22,
            MinHumidity = minHumidity, MaxHumidity = minHumidity is null ? null : 60, MaxLightLevel = 200
        });
        await using var dbContext = CreateDbContext();
        var zone = await dbContext.StorageZones.SingleAsync();
        zone.DeviceId = "museum-storage-01";
        var latest = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        dbContext.SensorReadings.AddRange(
            new SensorReading { DeviceId = zone.DeviceId, Temperature = 45, CreatedAt = latest.AddDays(-1) },
            new SensorReading { DeviceId = zone.DeviceId, Temperature = temperature, Humidity = humidity, LightLevel = lux, CreatedAt = latest },
            new SensorReading { DeviceId = "other-device", Temperature = 60, CreatedAt = latest.AddDays(1) });
        await dbContext.SaveChangesAsync();

        var suitability = GetOk(await new ExhibitsController(dbContext).GetStorageSuitability(exhibit.Id, CancellationToken.None));
        Assert.Equal(status, suitability.Status);
        Assert.Equal(latest, suitability.ReadingAt);
        Assert.Equal(new StorageCondition(temperature, 18, 22, tempStatus), suitability.Temperature);
        Assert.Equal(new StorageCondition(humidity, minHumidity, minHumidity is null ? null : 60, humidityStatus), suitability.Humidity);
        Assert.Equal(new StorageCondition(lux, null, 200, lightStatus), suitability.LightLevel);
    }

    [Fact]
    public async Task SuitabilityDistinguishesMissingDeviceMissingReadingsAndCheckedOut()
    {
        var shelf = await CreateLocationAsync(ShelfA);
        var exhibit = await CreateExhibitAsync(SampleExhibit with
        {
            Status = "Stored", StorageLocationId = shelf.Id, MinTemperature = 18
        });
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var noDevice = GetOk(await controller.GetStorageSuitability(exhibit.Id, CancellationToken.None));
        Assert.Equal("NoDevice", noDevice.Status);
        Assert.Equal(new StorageCondition(null, 18, null, "Unknown"), noDevice.Temperature);

        var zone = await dbContext.StorageZones.SingleAsync();
        zone.DeviceId = "museum-storage-01";
        dbContext.SensorReadings.Add(new SensorReading { DeviceId = "other-device" });
        await dbContext.SaveChangesAsync();
        Assert.Equal("NoReading", GetOk(await controller.GetStorageSuitability(exhibit.Id, CancellationToken.None)).Status);

        GetOk(await controller.CheckOut(exhibit.Id, CancellationToken.None));
        Assert.Equal("NotApplicable", GetOk(await controller.GetStorageSuitability(exhibit.Id, CancellationToken.None)).Status);
        Assert.IsType<NotFoundResult>((await controller.GetStorageSuitability(long.MaxValue, CancellationToken.None)).Result);
    }

    private MuseumStorageDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(_connection).Options);

    private async Task<StorageLocationResponse> CreateLocationAsync(StorageLocationRequest request)
    {
        await using var dbContext = CreateDbContext();
        var result = await new StorageLocationsController(dbContext).Create(request, CancellationToken.None);
        return Assert.IsType<StorageLocationResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private async Task<StorageZone> CreateZoneAsync(string code, string name = "Saugykla")
    {
        await using var dbContext = CreateDbContext();
        var request = new StorageZoneRequest { Code = code, Name = name };
        var result = await new StorageZonesController(dbContext).Create(request, CancellationToken.None);
        return Assert.IsType<StorageZone>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private async Task<ExhibitResponse> CreateExhibitAsync(ExhibitRequest request)
    {
        await using var dbContext = CreateDbContext();
        var result = await new ExhibitsController(dbContext).Create(request, CancellationToken.None);
        return Assert.IsType<ExhibitResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static T GetOk<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void AssertValidationError<T>(ActionResult<T> result, string field)
    {
        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(field, Assert.IsType<ValidationProblemDetails>(response.Value).Errors.Keys);
    }
}
