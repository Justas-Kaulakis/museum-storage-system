using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Data;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Models;
using SQLitePCL;

namespace MuseumStorage.Api.Controllers;

[ApiController]
[Route("api/storage-zones")]
public class StorageZonesController(MuseumStorageDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StorageZone>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dbContext.StorageZones.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<StorageZone>> GetById(long id, CancellationToken cancellationToken)
    {
        var zone = await dbContext.StorageZones.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return zone is null ? NotFound() : Ok(zone);
    }

    [HttpPost]
    public async Task<ActionResult<StorageZone>> Create(StorageZoneRequest request, CancellationToken cancellationToken)
    {
        var zone = new StorageZone();
        var error = await SaveAsync(zone, request, cancellationToken);
        return error is null ? CreatedAtAction(nameof(GetById), new { id = zone.Id }, zone) : error;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<StorageZone>> Update(long id, StorageZoneRequest request, CancellationToken cancellationToken)
    {
        var zone = await dbContext.StorageZones.FindAsync([id], cancellationToken);
        if (zone is null)
            return NotFound();

        var error = await SaveAsync(zone, request, cancellationToken);
        return error is null ? Ok(zone) : error;
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await dbContext.StorageLocations.AnyAsync(x => x.StorageZoneId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Zonos pašalinti negalima, kol joje yra saugojimo vietų." });

        var deleted = await dbContext.StorageZones.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private async Task<ActionResult?> SaveAsync(StorageZone zone, StorageZoneRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            ModelState.AddModelError(nameof(request.Code), "Įveskite zonos kodą.");

        if (string.IsNullOrWhiteSpace(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Įveskite zonos pavadinimą.");

        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        zone.Code = request.Code.Trim();
        zone.Name = request.Name.Trim();
        if (zone.Id == 0)
            dbContext.StorageZones.Add(zone);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_UNIQUE })
        {
            ModelState.AddModelError(nameof(request.Code), "Saugojimo zona su šiuo kodu jau yra.");
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
        }
    }
}
