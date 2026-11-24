using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;
using SQLitePCL;

namespace MuseumStorage.Api.Controllers;

[ApiController]
[Route("api/storage-locations")]
public class StorageLocationsController : ControllerBase
{
    private readonly MuseumStorageDbContext _dbContext;

    public StorageLocationsController(MuseumStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private IQueryable<StorageLocation> LocationsWithZone => _dbContext.StorageLocations.Include(x => x.StorageZone);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StorageLocationResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var locations = await LocationsWithZone.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken);
        return Ok(locations.Select(StorageLocationResponse.From));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<StorageLocationResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var location = await LocationsWithZone.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return location is null ? NotFound() : Ok(StorageLocationResponse.From(location));
    }

    [HttpPost]
    public async Task<ActionResult<StorageLocationResponse>> Create(StorageLocationRequest request, CancellationToken cancellationToken)
    {
        var location = new StorageLocation();
        var error = await SaveAsync(location, request, cancellationToken);
        if (error is not null)
            return error;

        return CreatedAtAction(nameof(GetById), new { id = location.Id }, StorageLocationResponse.From(location));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<StorageLocationResponse>> Update(long id, StorageLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await _dbContext.StorageLocations.FindAsync([id], cancellationToken);
        if (location is null)
            return NotFound();

        var error = await SaveAsync(location, request, cancellationToken);
        return error is null ? Ok(StorageLocationResponse.From(location)) : error;
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var location = await _dbContext.StorageLocations.FindAsync([id], cancellationToken);
        if (location is null)
            return NotFound();

        if (await _dbContext.Exhibits.AnyAsync(x => x.StorageLocationId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Saugojimo vietos pašalinti negalima, kol joje yra eksponatų." });

        await _dbContext.StorageOperations
            .Where(x => x.FromStorageLocationId == id || x.ToStorageLocationId == id)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.FromStorageLocationId, x => x.FromStorageLocationId == id ? null : x.FromStorageLocationId)
                .SetProperty(x => x.ToStorageLocationId, x => x.ToStorageLocationId == id ? null : x.ToStorageLocationId),
                cancellationToken);
        _dbContext.StorageLocations.Remove(location);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> SaveAsync(StorageLocation location, StorageLocationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            ModelState.AddModelError(nameof(request.Code), "Code is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is required.");

        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var zone = await _dbContext.StorageZones.FindAsync([request.StorageZoneId], cancellationToken);
        if (zone is null)
            return InvalidZone();

        location.Code = request.Code.Trim();
        location.Name = request.Name.Trim();
        location.StorageZoneId = zone.Id;
        location.StorageZone = zone;
        if (location.Id == 0)
            _dbContext.StorageLocations.Add(location);
        else
            _dbContext.Entry(location).State = EntityState.Modified;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_UNIQUE })
        {
            ModelState.AddModelError(nameof(request.Code), "A storage location with this code already exists.");
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_FOREIGNKEY })
        {
            return InvalidZone();
        }
    }

    private ActionResult InvalidZone()
    {
        ModelState.AddModelError(nameof(StorageLocationRequest.StorageZoneId), "Pasirinkite esamą saugojimo zoną.");
        return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
    }
}
