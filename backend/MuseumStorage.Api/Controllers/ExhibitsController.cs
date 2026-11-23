using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;
using SkiaSharp;
using SQLitePCL;
using ZXing;
using ZXing.Common;

namespace MuseumStorage.Api.Controllers;

[ApiController]
[Route("api/exhibits")]
public class ExhibitsController : ControllerBase
{
    private const int MaxQrImageBytes = 5 * 1024 * 1024;
    private const int MaxQrRequestBytes = MaxQrImageBytes + 64 * 1024;
    private const int MaxQrImagePixels = 8_000_000;

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

    [HttpPost("scan-qr")]
    [RequestSizeLimit(MaxQrRequestBytes)]
    public async Task<ActionResult<ExhibitResponse>> ScanQr()
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var contentType) ||
            !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return InvalidRequest("file", "Upload a PNG or JPEG image as a multipart form file.");

        if (Request.ContentLength > MaxQrRequestBytes)
            return InvalidRequest("file", "The image must be at most 5 MiB.");

        var cancellationToken = HttpContext.RequestAborted;
        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(new FormOptions
            {
                ValueCountLimit = 1,
                MultipartBodyLengthLimit = MaxQrImageBytes,
                MemoryBufferThreshold = MaxQrImageBytes
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return InvalidRequest("file", "Upload one complete image file of at most 5 MiB.");
        }

        if (form.Files.Count != 1 || form.Files.GetFile("file") is not { Length: > 0 } file)
            return InvalidRequest("file", "A non-empty image is required in the file field.");

        if (file.Length > MaxQrImageBytes)
            return InvalidRequest("file", "The image must be at most 5 MiB.");

        Result? decoded;
        try
        {
            using var image = file.OpenReadStream();
            using var codec = SKCodec.Create(image);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
                return InvalidRequest("file", "The file must contain a valid PNG or JPEG image.");

            var imageInfo = codec.Info;
            if ((long)imageInfo.Width * imageInfo.Height > MaxQrImagePixels)
                return InvalidRequest("file", "The image must be at most 8 megapixels.");

            using var bitmap = new SKBitmap(imageInfo.Width, imageInfo.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                return InvalidRequest("file", "The image is corrupt or incomplete.");

            var reader = new ZXing.SkiaSharp.BarcodeReader
            {
                Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true }
            };
            decoded = reader.Decode(bitmap);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or ReaderException)
        {
            return InvalidRequest("file", "The image could not be decoded.");
        }

        if (decoded is null)
            return InvalidRequest("file", "No QR code could be decoded from the image.");

        var code = decoded.Text?.Trim();
        if (string.IsNullOrEmpty(code))
            return InvalidRequest("file", "The QR code must contain an exhibit code.");

        return await GetByCode(code, cancellationToken);
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

    [HttpPost("{id:long}/check-in")]
    public Task<ActionResult<ExhibitResponse>> CheckIn(long id, StorageOperationRequest request, CancellationToken cancellationToken) =>
        PerformOperationAsync(id, StorageOperationType.CheckIn, request.StorageLocationId, cancellationToken);

    [HttpPost("{id:long}/check-out")]
    public Task<ActionResult<ExhibitResponse>> CheckOut(long id, CancellationToken cancellationToken) =>
        PerformOperationAsync(id, StorageOperationType.CheckOut, null, cancellationToken);

    [HttpPost("{id:long}/move")]
    public Task<ActionResult<ExhibitResponse>> Move(long id, StorageOperationRequest request, CancellationToken cancellationToken) =>
        PerformOperationAsync(id, StorageOperationType.Move, request.StorageLocationId, cancellationToken);

    [HttpGet("{id:long}/operations")]
    public async Task<ActionResult<IEnumerable<StorageOperationResponse>>> GetOperations(long id, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Exhibits.AnyAsync(x => x.Id == id, cancellationToken))
            return NotFound();

        var operations = await _dbContext.StorageOperations.AsNoTracking()
            .Where(x => x.ExhibitId == id)
            .Include(x => x.FromStorageLocation)
            .Include(x => x.ToStorageLocation)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return Ok(operations.Select(x => new StorageOperationResponse(
            x.Id, x.ExhibitId, x.Type.ToString(), x.FromStorageLocationId, x.ToStorageLocationId,
            x.CreatedAt, x.FromStorageLocation, x.ToStorageLocation)));
    }

    private async Task<ActionResult<ExhibitResponse>> PerformOperationAsync(
        long id, StorageOperationType type, long? targetLocationId, CancellationToken cancellationToken)
    {
        var exhibit = await _dbContext.Exhibits.FindAsync([id], cancellationToken);
        if (exhibit is null)
            return NotFound();

        var statusError = type switch
        {
            StorageOperationType.CheckIn when exhibit.Status != ExhibitStatus.CheckedOut => "Only a checked-out exhibit can be checked in.",
            StorageOperationType.CheckOut when exhibit.Status != ExhibitStatus.Stored => "Only a stored exhibit can be checked out.",
            StorageOperationType.Move when exhibit.Status != ExhibitStatus.Stored => "Only a stored exhibit can be moved.",
            _ => null
        };
        if (statusError is not null)
            return InvalidRequest(nameof(Exhibit.Status), statusError);

        if (type == StorageOperationType.Move && exhibit.StorageLocationId == targetLocationId)
            return InvalidRequest(nameof(Exhibit.StorageLocationId), "The target must differ from the current storage location.");

        StorageLocation? location = null;
        if (targetLocationId is long locationId)
        {
            location = await _dbContext.StorageLocations.FindAsync([locationId], cancellationToken);
            if (location is null)
                return InvalidRequest(nameof(Exhibit.StorageLocationId), "Storage location does not exist.");
        }

        ApplyStorageState(exhibit, location is null ? ExhibitStatus.CheckedOut : ExhibitStatus.Stored, location);
        var error = await PersistAsync(exhibit, cancellationToken);
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
        ApplyStorageState(exhibit, status.Value, location);

        if (exhibit.Id == 0)
            _dbContext.Exhibits.Add(exhibit);
        else
            _dbContext.Entry(exhibit).State = EntityState.Modified;

        return await PersistAsync(exhibit, cancellationToken);
    }

    private void ApplyStorageState(Exhibit exhibit, ExhibitStatus status, StorageLocation? location)
    {
        var fromLocationId = exhibit.StorageLocationId;
        var toLocationId = location?.Id;
        if (exhibit.Id != 0 && (exhibit.Status != status || fromLocationId != toLocationId))
        {
            var type = (exhibit.Status, status) switch
            {
                (ExhibitStatus.CheckedOut, ExhibitStatus.Stored) => StorageOperationType.CheckIn,
                (ExhibitStatus.Stored, ExhibitStatus.CheckedOut) => StorageOperationType.CheckOut,
                _ => StorageOperationType.Move
            };
            _dbContext.StorageOperations.Add(new StorageOperation
            {
                ExhibitId = exhibit.Id,
                Type = type,
                FromStorageLocationId = fromLocationId,
                ToStorageLocationId = toLocationId
            });
        }

        exhibit.Status = status;
        exhibit.StorageLocationId = toLocationId;
        exhibit.StorageLocation = location;
    }

    private async Task<ActionResult?> PersistAsync(Exhibit exhibit, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await _dbContext.Entry(exhibit).GetDatabaseValuesAsync(cancellationToken) is null)
                return NotFound();

            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The exhibit's storage state changed. Reload the exhibit and retry."
            });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_UNIQUE })
        {
            return InvalidRequest(nameof(Exhibit.Code), "An exhibit with this code already exists.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_FOREIGNKEY })
        {
            return InvalidRequest(nameof(Exhibit.StorageLocationId), "Storage location does not exist.");
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
