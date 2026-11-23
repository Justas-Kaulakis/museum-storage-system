using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Controllers;
using MuseumStorage.Api.Models;
using MuseumStorage.Domain.Entities;
using MuseumStorage.Infrastructure.Data;

namespace MuseumStorage.Tests;

public sealed class StorageRegistryTests : IAsyncLifetime
{
    private static readonly StorageLocationRequest ShelfA = new() { Code = "SHELF-A1", Name = "Shelf A1" };
    private static readonly StorageLocationRequest ShelfB = new() { Code = "SHELF-B1", Name = "Shelf B1" };
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
            new StorageLocationRequest { Code = code, Name = name }, CancellationToken.None);
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
        Assert.Equal(ShelfA.Code, Assert.IsType<StorageLocation>(fetched.StorageLocation).Code);
        Assert.Equal(ShelfA.Name, Assert.IsType<StorageLocation>(created.StorageLocation).Name);
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

    private MuseumStorageDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(_connection).Options);

    private async Task<StorageLocation> CreateLocationAsync(StorageLocationRequest request)
    {
        await using var dbContext = CreateDbContext();
        var result = await new StorageLocationsController(dbContext).Create(request, CancellationToken.None);
        return Assert.IsType<StorageLocation>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
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
