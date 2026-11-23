using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Controllers;
using MuseumStorage.Api.Models;
using MuseumStorage.Domain.Entities;
using MuseumStorage.Infrastructure.Data;

namespace MuseumStorage.Tests;

public sealed class StorageOperationTests : IAsyncLifetime
{
    private static readonly ExhibitRequest SampleExhibit = new()
    {
        Code = "EXHIBIT-0001",
        Name = "Ceramic Bowl",
        Status = "CheckedOut"
    };

    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private readonly StorageLocation _firstLocation = new() { Code = "SHELF-A1", Name = "Shelf A1" };
    private readonly StorageLocation _secondLocation = new() { Code = "SHELF-B2", Name = "Shelf B2" };

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        dbContext.StorageLocations.AddRange(_firstLocation, _secondLocation);
        await dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Fact]
    public async Task ExplicitOperationsUpdateExhibitAndReturnHistoryNewestFirst()
    {
        var exhibit = await CreateExhibitAsync(ExhibitStatus.CheckedOut);

        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var checkedIn = GetOk(await controller.CheckIn(exhibit.Id, Target(_firstLocation.Id), CancellationToken.None));
        Assert.Equal("Stored", checkedIn.Status);
        Assert.Equal(_firstLocation.Id, checkedIn.StorageLocationId);

        var moved = GetOk(await controller.Move(exhibit.Id, Target(_secondLocation.Id), CancellationToken.None));
        Assert.Equal("Stored", moved.Status);
        Assert.Equal(_secondLocation.Id, moved.StorageLocationId);

        var checkedOut = GetOk(await controller.CheckOut(exhibit.Id, CancellationToken.None));
        Assert.Equal("CheckedOut", checkedOut.Status);
        Assert.Null(checkedOut.StorageLocationId);
        Assert.Null(checkedOut.StorageLocation);

        await using var readContext = CreateDbContext();
        var persisted = await readContext.Exhibits.SingleAsync();
        Assert.Equal(ExhibitStatus.CheckedOut, persisted.Status);
        Assert.Null(persisted.StorageLocationId);

        var history = GetOk(await new ExhibitsController(readContext).GetOperations(exhibit.Id, CancellationToken.None)).ToArray();
        Assert.Collection(history,
            operation => AssertOperation(operation, exhibit.Id, StorageOperationType.CheckOut, _secondLocation.Id, null),
            operation => AssertOperation(operation, exhibit.Id, StorageOperationType.Move, _firstLocation.Id, _secondLocation.Id),
            operation => AssertOperation(operation, exhibit.Id, StorageOperationType.CheckIn, null, _firstLocation.Id));
        Assert.True(history[0].CreatedAt >= history[1].CreatedAt && history[1].CreatedAt >= history[2].CreatedAt);
        Assert.Equal(_firstLocation.Code, history[1].FromStorageLocation?.Code);
        Assert.Equal(_secondLocation.Name, history[1].ToStorageLocation?.Name);
    }

    [Theory]
    [InlineData(ExhibitStatus.Stored, StorageOperationType.CheckIn, true, nameof(Exhibit.Status))]
    [InlineData(ExhibitStatus.CheckedOut, StorageOperationType.CheckOut, true, nameof(Exhibit.Status))]
    [InlineData(ExhibitStatus.CheckedOut, StorageOperationType.Move, true, nameof(Exhibit.Status))]
    [InlineData(ExhibitStatus.Stored, StorageOperationType.Move, true, nameof(Exhibit.StorageLocationId))]
    [InlineData(ExhibitStatus.CheckedOut, StorageOperationType.CheckIn, false, nameof(Exhibit.StorageLocationId))]
    [InlineData(ExhibitStatus.Stored, StorageOperationType.Move, false, nameof(Exhibit.StorageLocationId))]
    public async Task InvalidOperationReturnsBadRequestWithoutChangingStorage(
        ExhibitStatus initialStatus, StorageOperationType type, bool targetExists, string errorKey)
    {
        var exhibit = await CreateExhibitAsync(initialStatus);
        var destination = Target(targetExists ? _firstLocation.Id : long.MaxValue);

        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var result = await (type switch
        {
            StorageOperationType.CheckIn => controller.CheckIn(exhibit.Id, destination, CancellationToken.None),
            StorageOperationType.CheckOut => controller.CheckOut(exhibit.Id, CancellationToken.None),
            _ => controller.Move(exhibit.Id, destination, CancellationToken.None)
        });

        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(errorKey, Assert.IsType<ValidationProblemDetails>(response.Value).Errors.Keys);
        var persisted = await dbContext.Exhibits.AsNoTracking().SingleAsync();
        Assert.Equal(initialStatus, persisted.Status);
        Assert.Equal(exhibit.StorageLocationId, persisted.StorageLocationId);
        Assert.Empty(await dbContext.StorageOperations.ToListAsync());
    }

    [Theory]
    [InlineData(ExhibitStatus.CheckedOut, ExhibitStatus.Stored, StorageOperationType.CheckIn)]
    [InlineData(ExhibitStatus.Stored, ExhibitStatus.CheckedOut, StorageOperationType.CheckOut)]
    [InlineData(ExhibitStatus.Stored, ExhibitStatus.Stored, StorageOperationType.Move)]
    public async Task PutRecordsEachStorageTransitionExactlyOnce(
        ExhibitStatus initialStatus, ExhibitStatus newStatus, StorageOperationType expectedType)
    {
        var exhibit = await CreateExhibitAsync(initialStatus);
        long? newLocationId = newStatus == ExhibitStatus.Stored ? _secondLocation.Id : null;
        var request = SampleExhibit with { Status = newStatus.ToString(), StorageLocationId = newLocationId };

        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        var updated = GetOk(await controller.Update(exhibit.Id, request, CancellationToken.None));
        Assert.Equal(request.Status, updated.Status);
        Assert.Equal(newLocationId, updated.StorageLocationId);
        GetOk(await controller.Update(exhibit.Id, request, CancellationToken.None));

        await using var readContext = CreateDbContext();
        var persisted = await readContext.Exhibits.SingleAsync();
        Assert.Equal(newStatus, persisted.Status);
        Assert.Equal(newLocationId, persisted.StorageLocationId);

        var history = GetOk(await new ExhibitsController(readContext).GetOperations(exhibit.Id, CancellationToken.None));
        AssertOperation(Assert.Single(history), exhibit.Id, expectedType, exhibit.StorageLocationId, newLocationId);
    }

    [Theory]
    [InlineData(ExhibitStatus.CheckedOut)]
    [InlineData(ExhibitStatus.Stored)]
    public async Task RegistrationAndMetadataOnlyUpdatesHaveEmptyHistory(ExhibitStatus status)
    {
        var exhibit = await CreateExhibitAsync(status);

        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        Assert.Empty(GetOk(await controller.GetOperations(exhibit.Id, CancellationToken.None)));

        var request = SampleExhibit with
        {
            Code = "EXHIBIT-RENAMED",
            Name = "Renamed bowl",
            Status = exhibit.Status,
            StorageLocationId = exhibit.StorageLocationId
        };
        var updated = GetOk(await controller.Update(exhibit.Id, request, CancellationToken.None));
        Assert.Equal(request.Name, updated.Name);
        Assert.Empty(GetOk(await controller.GetOperations(exhibit.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task OperationsAndHistoryReturnNotFoundForUnregisteredExhibit()
    {
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        Assert.IsType<NotFoundResult>((await controller.CheckIn(123, Target(_firstLocation.Id), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.CheckOut(123, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Move(123, Target(_secondLocation.Id), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.GetOperations(123, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task HistoryWriteFailureRollsBackExhibitChange()
    {
        var exhibit = await CreateExhibitAsync(ExhibitStatus.CheckedOut);
        await using var dbContext = CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectHistory BEFORE INSERT ON StorageOperations BEGIN SELECT RAISE(ABORT, 'Simulated history failure'); END;");

        var controller = new ExhibitsController(dbContext);
        await Assert.ThrowsAsync<DbUpdateException>(() => controller.CheckIn(exhibit.Id, Target(_firstLocation.Id), CancellationToken.None));

        await using var readContext = CreateDbContext();
        var persisted = await readContext.Exhibits.SingleAsync();
        Assert.Equal(ExhibitStatus.CheckedOut, persisted.Status);
        Assert.Null(persisted.StorageLocationId);
        Assert.Empty(await readContext.StorageOperations.ToListAsync());
    }

    [Fact]
    public async Task StaleStorageStateCannotOverwriteExhibitOrWriteIncorrectHistory()
    {
        var exhibit = await CreateExhibitAsync(ExhibitStatus.Stored);

        await using var staleContext = CreateDbContext();
        var original = await staleContext.Exhibits.SingleAsync();
        Assert.Equal(_firstLocation.Id, original.StorageLocationId);

        await using var writeContext = CreateDbContext();
        var latest = new ExhibitsController(writeContext);
        GetOk(await latest.Move(exhibit.Id, Target(_secondLocation.Id), CancellationToken.None));

        var stale = new ExhibitsController(staleContext);
        var checkout = await stale.CheckOut(exhibit.Id, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(checkout.Result);

        await using var readContext = CreateDbContext();
        var persisted = await readContext.Exhibits.SingleAsync();
        Assert.Equal(ExhibitStatus.Stored, persisted.Status);
        Assert.Equal(_secondLocation.Id, persisted.StorageLocationId);
        var history = GetOk(await new ExhibitsController(readContext).GetOperations(exhibit.Id, CancellationToken.None));
        AssertOperation(Assert.Single(history), exhibit.Id, StorageOperationType.Move, _firstLocation.Id, _secondLocation.Id);
    }

    [Fact]
    public async Task DatabaseRestrictsDeletionOfExhibitAndLocationReferencedInHistory()
    {
        var exhibit = await CreateExhibitAsync(ExhibitStatus.Stored);
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext);
        GetOk(await controller.CheckOut(exhibit.Id, CancellationToken.None));

        await Assert.ThrowsAsync<SqliteException>(() => dbContext.Exhibits.Where(x => x.Id == exhibit.Id).ExecuteDeleteAsync());
        await Assert.ThrowsAsync<SqliteException>(() => dbContext.StorageLocations.Where(x => x.Id == _firstLocation.Id).ExecuteDeleteAsync());
        Assert.Equal(1, await dbContext.StorageOperations.CountAsync());
    }

    private MuseumStorageDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(_connection).Options);

    private async Task<ExhibitResponse> CreateExhibitAsync(ExhibitStatus status)
    {
        await using var dbContext = CreateDbContext();
        var request = SampleExhibit with
        {
            Status = status.ToString(),
            StorageLocationId = status == ExhibitStatus.Stored ? _firstLocation.Id : null
        };
        var result = await new ExhibitsController(dbContext).Create(request, CancellationToken.None);
        return Assert.IsType<ExhibitResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static StorageOperationRequest Target(long id) => new() { StorageLocationId = id };

    private static T GetOk<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void AssertOperation(StorageOperationResponse operation, long exhibitId, StorageOperationType type,
        long? fromLocationId, long? toLocationId)
    {
        Assert.True(operation.Id > 0);
        Assert.Equal(exhibitId, operation.ExhibitId);
        Assert.Equal(type.ToString(), operation.Type);
        Assert.Equal(fromLocationId, operation.FromStorageLocationId);
        Assert.Equal(toLocationId, operation.ToStorageLocationId);
        Assert.Equal(DateTimeKind.Utc, operation.CreatedAt.Kind);
    }
}
