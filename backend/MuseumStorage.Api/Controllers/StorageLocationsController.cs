using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Models;
using MuseumStorage.Domain.Entities;
using MuseumStorage.Infrastructure.Data;
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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StorageLocation>>> GetAll(CancellationToken cancellationToken)
    {
        var locations = await _dbContext.StorageLocations.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken);
        return Ok(locations);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<StorageLocation>> GetById(long id, CancellationToken cancellationToken)
    {
        var location = await _dbContext.StorageLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return location is null ? NotFound() : Ok(location);
    }

    [HttpPost]
    public async Task<ActionResult<StorageLocation>> Create(StorageLocationRequest request, CancellationToken cancellationToken)
    {
        var location = new StorageLocation();
        var error = await SaveAsync(location, request, cancellationToken);
        if (error is not null)
            return error;

        return CreatedAtAction(nameof(GetById), new { id = location.Id }, location);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<StorageLocation>> Update(long id, StorageLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await _dbContext.StorageLocations.FindAsync([id], cancellationToken);
        if (location is null)
            return NotFound();

        var error = await SaveAsync(location, request, cancellationToken);
        return error is null ? Ok(location) : error;
    }

    private async Task<ActionResult?> SaveAsync(StorageLocation location, StorageLocationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            ModelState.AddModelError(nameof(request.Code), "Code is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is required.");

        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        location.Code = request.Code.Trim();
        location.Name = request.Name.Trim();
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
    }
}
