using System.Text;
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
using ZXing.QrCode;

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

    private IQueryable<Exhibit> ExhibitsWithLocation =>
        _dbContext.Exhibits.AsNoTracking().Include(x => x.StorageLocation).ThenInclude(x => x!.StorageZone);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExhibitResponse>>> GetAll(
        CancellationToken cancellationToken, [FromQuery] string? search = null, [FromQuery] string? category = null,
        [FromQuery] string? status = null, [FromQuery] long? zoneId = null, [FromQuery] long? storageLocationId = null)
    {
        var query = ExhibitsWithLocation;
        if (status is not null)
        {
            if (ParseStatus(status) is not { } exhibitStatus)
                return InvalidRequest(nameof(status), "Pasirinkite galimą eksponato būseną.");

            query = query.Where(x => x.Status == exhibitStatus);
        }

        if (zoneId.HasValue)
            query = query.Where(x => x.StorageLocation!.StorageZoneId == zoneId.Value);

        if (storageLocationId.HasValue)
            query = query.Where(x => x.StorageLocationId == storageLocationId.Value);

        IEnumerable<Exhibit> exhibits = await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        // filtruojam atmintyje, kad ž ir Ž paieškoje sutaptų
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            exhibits = exhibits.Where(x => x.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var term = category.Trim();
            exhibits = exhibits.Where(x => x.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
        }

        return Ok(exhibits.Select(ToResponse).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ExhibitResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var exhibit = await ExhibitsWithLocation.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return exhibit is null ? NotFound() : Ok(ToResponse(exhibit));
    }

    [HttpGet("{id:long}/qr")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DownloadQr(long id, CancellationToken cancellationToken)
    {
        var code = await _dbContext.Exhibits.Where(x => x.Id == id)
            .Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
        if (code is null)
            return NotFound();

        var writer = new ZXing.SkiaSharp.BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions { Width = 512, Height = 512, Margin = 4, CharacterSet = "UTF-8" }
        };

        try
        {
            using var bitmap = writer.Write(code);
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return File(png.ToArray(), "image/png", GetQrDownloadName(code));
        }
        catch (WriterException)
        {
            return UnprocessableEntity(new ProblemDetails { Title = "Eksponato kodas per ilgas QR kodui." });
        }
    }

    private static string GetQrDownloadName(string code)
    {
        // trumpinam tik failo vardą, o qr kode paliekam visą eksponato kodą
        var name = string.Concat(code.EnumerateRunes().Take(60).Select(character =>
            Rune.IsControl(character) || (character.IsAscii && "<>:\"/\\|?*".Contains((char)character.Value))
                ? "_" : character.ToString()));
        return $"{name}.png";
    }

    [HttpGet("{id:long}/storage-suitability")]
    public async Task<ActionResult<StorageSuitabilityResponse>> GetStorageSuitability(long id, CancellationToken cancellationToken)
    {
        var exhibit = await ExhibitsWithLocation.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (exhibit is null)
            return NotFound();

        // perkėlus eksponatą, tinkamumą tikrinam pagal naujos zonos rodmenis
        var deviceId = exhibit.StorageLocation?.StorageZone?.DeviceId;
        var reading = exhibit.Status == ExhibitStatus.Stored && deviceId is not null
            ? await _dbContext.SensorReadings.AsNoTracking().Where(x => x.DeviceId == deviceId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken)
            : null;

        var temperature = EvaluateCondition(reading?.Temperature, exhibit.MinTemperature, exhibit.MaxTemperature);
        var humidity = EvaluateCondition(reading?.Humidity, exhibit.MinHumidity, exhibit.MaxHumidity);
        var light = EvaluateCondition(reading?.LightLevel, exhibit.MinLightLevel, exhibit.MaxLightLevel);
        var status = exhibit.Status == ExhibitStatus.CheckedOut ? "NotApplicable"
            : deviceId is null ? "NoDevice"
            : reading is null ? "NoReading"
            : new[] { temperature, humidity, light }.Any(x => x.Status is "BelowMinimum" or "AboveMaximum") ? "Unsuitable"
            : "Suitable";

        return Ok(new StorageSuitabilityResponse(status, reading?.CreatedAt, temperature, humidity, light));
    }

    private static StorageCondition EvaluateCondition(double? value, double? min, double? max)
    {
        var status = min is null && max is null ? "NotConfigured"
            : value is null ? "Unknown"
            : value < min ? "BelowMinimum"
            : value > max ? "AboveMaximum"
            : "WithinRange";
        return new StorageCondition(value, min, max, status);
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

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.StorageOperations.Where(x => x.ExhibitId == id).ExecuteDeleteAsync(cancellationToken);
        var deleted = await _dbContext.Exhibits.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
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
            .ThenInclude(x => x!.StorageZone)
            .Include(x => x.ToStorageLocation)
            .ThenInclude(x => x!.StorageZone)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return Ok(operations.Select(x => new StorageOperationResponse(
            x.Id, x.ExhibitId, x.Type.ToString(), x.FromStorageLocationId, x.ToStorageLocationId,
            x.CreatedAt, StorageLocationResponse.From(x.FromStorageLocation), StorageLocationResponse.From(x.ToStorageLocation))));
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
            location = await _dbContext.StorageLocations.Include(x => x.StorageZone).SingleOrDefaultAsync(x => x.Id == locationId, cancellationToken);
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

        var status = ParseStatus(request.Status);

        if (status is null)
            ModelState.AddModelError(nameof(request.Status), "Status must be Stored or CheckedOut.");
        else if (status == ExhibitStatus.Stored && request.StorageLocationId is null)
            ModelState.AddModelError(nameof(request.StorageLocationId), "A stored exhibit requires a storage location.");
        else if (status == ExhibitStatus.CheckedOut && request.StorageLocationId is not null)
            ModelState.AddModelError(nameof(request.StorageLocationId), "A checked-out exhibit cannot have a storage location.");

        ValidateRange(request.MinTemperature, request.MaxTemperature, -40, 80, nameof(request.MinTemperature), nameof(request.MaxTemperature), "Temperatūra turi būti nuo −40 iki 80 °C.");
        ValidateRange(request.MinHumidity, request.MaxHumidity, 0, 100, nameof(request.MinHumidity), nameof(request.MaxHumidity), "Drėgmė turi būti nuo 0 iki 100 %.");
        ValidateRange(request.MinLightLevel, request.MaxLightLevel, 0, double.MaxValue, nameof(request.MinLightLevel), nameof(request.MaxLightLevel), "Apšvietimas turi būti neneigiamas skaičius.");

        if (status is null || !ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        StorageLocation? location = null;
        if (request.StorageLocationId is long locationId)
        {
            location = await _dbContext.StorageLocations.Include(x => x.StorageZone).SingleOrDefaultAsync(x => x.Id == locationId, cancellationToken);
            if (location is null)
                return InvalidRequest(nameof(request.StorageLocationId), "Storage location does not exist.");
        }

        exhibit.Code = request.Code.Trim();
        exhibit.Name = request.Name.Trim();
        exhibit.Description = request.Description;
        exhibit.Category = request.Category;
        exhibit.MinTemperature = request.MinTemperature;
        exhibit.MaxTemperature = request.MaxTemperature;
        exhibit.MinHumidity = request.MinHumidity;
        exhibit.MaxHumidity = request.MaxHumidity;
        exhibit.MinLightLevel = request.MinLightLevel;
        exhibit.MaxLightLevel = request.MaxLightLevel;
        ApplyStorageState(exhibit, status.Value, location);

        if (exhibit.Id == 0)
            _dbContext.Exhibits.Add(exhibit);
        else
            _dbContext.Entry(exhibit).State = EntityState.Modified;

        return await PersistAsync(exhibit, cancellationToken);
    }

    private void ValidateRange(double? min, double? max, double lowerBound, double upperBound, string minKey, string maxKey, string rangeError)
    {
        if (min is double minimum && (!double.IsFinite(minimum) || minimum < lowerBound || minimum > upperBound))
            ModelState.AddModelError(minKey, rangeError);

        if (max is double maximum && (!double.IsFinite(maximum) || maximum < lowerBound || maximum > upperBound))
            ModelState.AddModelError(maxKey, rangeError);

        if (min > max)
            ModelState.AddModelError(minKey, "Mažiausia reikšmė negali viršyti didžiausios.");
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
            // operaciją pridedam čia, kad ją išsaugotume kartu su nauja būsena
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

    private static ExhibitStatus? ParseStatus(string status) => status switch
    {
        nameof(ExhibitStatus.Stored) => ExhibitStatus.Stored,
        nameof(ExhibitStatus.CheckedOut) => ExhibitStatus.CheckedOut,
        _ => null
    };

    private static ExhibitResponse ToResponse(Exhibit exhibit) =>
        new(exhibit.Id, exhibit.Code, exhibit.Name, exhibit.Description, exhibit.Category,
            exhibit.Status.ToString(), exhibit.StorageLocationId, StorageLocationResponse.From(exhibit.StorageLocation),
            exhibit.MinTemperature, exhibit.MaxTemperature, exhibit.MinHumidity, exhibit.MaxHumidity, exhibit.MinLightLevel, exhibit.MaxLightLevel);
}
