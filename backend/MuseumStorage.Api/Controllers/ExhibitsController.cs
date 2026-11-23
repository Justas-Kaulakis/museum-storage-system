using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Models;
using MuseumStorage.Domain.Entities;
using MuseumStorage.Infrastructure.Data;
using SQLitePCL;

namespace MuseumStorage.Api.Controllers;

[ApiController]
[Route("api/exhibits")]
public class ExhibitsController : ControllerBase
{
    private readonly MuseumStorageDbContext _dbContext;

    public ExhibitsController(MuseumStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private IQueryable<Exhibit> ExhibitsWithLocation => _dbContext.Exhibits.AsNoTracking().Include(x => x.StorageLocation);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExhibitResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var exhibits = await ExhibitsWithLocation.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        return Ok(exhibits.Select(ToResponse));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ExhibitResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var exhibit = await ExhibitsWithLocation.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return exhibit is null ? NotFound() : Ok(ToResponse(exhibit));
    }

    [HttpGet("by-code/{**code}")]
    public async Task<ActionResult<ExhibitResponse>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var exhibit = await ExhibitsWithLocation.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        return exhibit is null ? NotFound() : Ok(ToResponse(exhibit));
    }

    [HttpPost]
    public async Task<ActionResult<ExhibitResponse>> Create(ExhibitRequest request, CancellationToken cancellationToken)
    {
        var exhibit = new Exhibit();
        var error = await SaveAsync(exhibit, request, cancellationToken);
        if (error is not null)
            return error;

        return CreatedAtAction(nameof(GetById), new { id = exhibit.Id }, ToResponse(exhibit));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ExhibitResponse>> Update(long id, ExhibitRequest request, CancellationToken cancellationToken)
    {
        var exhibit = await _dbContext.Exhibits.FindAsync([id], cancellationToken);
        if (exhibit is null)
            return NotFound();

        var error = await SaveAsync(exhibit, request, cancellationToken);
        return error is null ? Ok(ToResponse(exhibit)) : error;
    }

    private async Task<ActionResult?> SaveAsync(Exhibit exhibit, ExhibitRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            ModelState.AddModelError(nameof(request.Code), "Code is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is required.");

        var status = request.Status switch
        {
            nameof(ExhibitStatus.Stored) => ExhibitStatus.Stored,
            nameof(ExhibitStatus.CheckedOut) => ExhibitStatus.CheckedOut,
            _ => (ExhibitStatus?)null
        };

        if (status is null)
            ModelState.AddModelError(nameof(request.Status), "Status must be Stored or CheckedOut.");
        else if (status == ExhibitStatus.Stored && request.StorageLocationId is null)
            ModelState.AddModelError(nameof(request.StorageLocationId), "A stored exhibit requires a storage location.");
        else if (status == ExhibitStatus.CheckedOut && request.StorageLocationId is not null)
            ModelState.AddModelError(nameof(request.StorageLocationId), "A checked-out exhibit cannot have a storage location.");

        if (status is null || !ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        StorageLocation? location = null;
        if (request.StorageLocationId is long locationId)
        {
            location = await _dbContext.StorageLocations.FindAsync([locationId], cancellationToken);
            if (location is null)
                return InvalidRequest(nameof(request.StorageLocationId), "Storage location does not exist.");
        }

        exhibit.Code = request.Code.Trim();
        exhibit.Name = request.Name.Trim();
        exhibit.Description = request.Description;
        exhibit.Category = request.Category;
        exhibit.Status = status.Value;
        exhibit.StorageLocationId = request.StorageLocationId;
        exhibit.StorageLocation = location;

        if (exhibit.Id == 0)
            _dbContext.Exhibits.Add(exhibit);
        else
            _dbContext.Entry(exhibit).State = EntityState.Modified;

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
            return InvalidRequest(nameof(request.Code), "An exhibit with this code already exists.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_FOREIGNKEY })
        {
            return InvalidRequest(nameof(request.StorageLocationId), "Storage location does not exist.");
        }
    }

    private ActionResult InvalidRequest(string key, string message)
    {
        ModelState.AddModelError(key, message);
        return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
    }

    private static ExhibitResponse ToResponse(Exhibit exhibit) =>
        new(exhibit.Id, exhibit.Code, exhibit.Name, exhibit.Description, exhibit.Category,
            exhibit.Status.ToString(), exhibit.StorageLocationId, exhibit.StorageLocation);
}
