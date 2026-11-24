using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Controllers;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;
using SkiaSharp;
using ZXing;
using ZXing.QrCode;

namespace MuseumStorage.Tests;

public sealed class ExhibitQrScanTests : IAsyncLifetime
{
    private const string ExhibitCode = "EXHIBIT-0001";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private long _exhibitId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var exhibit = new Exhibit
        {
            Code = ExhibitCode,
            Name = "Ceramic Bowl",
            Status = ExhibitStatus.Stored,
            StorageLocation = new StorageLocation { Code = "SHELF-A1", Name = "Shelf A1", StorageZoneId = 1 }
        };
        dbContext.Exhibits.Add(exhibit);
        await dbContext.SaveChangesAsync();
        _exhibitId = exhibit.Id;
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Theory]
    [InlineData(ExhibitCode, ExhibitCode + ".png")]
    [InlineData("ŽENKLAS/001", "ŽENKLAS_001.png")]
    public async Task DownloadReturnsPngContainingOnlyTheExactExhibitCode(string code, string fileName)
    {
        await using var dbContext = CreateDbContext();
        var exhibitId = _exhibitId;
        if (code != ExhibitCode)
        {
            var exhibit = new Exhibit { Code = code, Name = "Sample", Status = ExhibitStatus.CheckedOut };
            dbContext.Exhibits.Add(exhibit);
            await dbContext.SaveChangesAsync();
            exhibitId = exhibit.Id;
        }

        var result = await new ExhibitsController(dbContext).DownloadQr(exhibitId, CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(fileName, file.FileDownloadName);

        using var image = new MemoryStream(file.FileContents);
        using var codec = SKCodec.Create(image);
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        using var bitmap = SKBitmap.Decode(codec);
        var decoded = Assert.IsType<Result>(new ZXing.SkiaSharp.BarcodeReader().Decode(bitmap));
        Assert.Equal(BarcodeFormat.QR_CODE, decoded.BarcodeFormat);
        Assert.Equal(code, decoded.Text);
    }

    [Fact]
    public async Task DownloadReturnsNotFoundForUnknownExhibit()
    {
        await using var dbContext = CreateDbContext();
        var result = await new ExhibitsController(dbContext).DownloadQr(long.MaxValue, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ScanResolvesTheExhibitFromItsDownloadedQr()
    {
        await using var dbContext = CreateDbContext();
        var download = await new ExhibitsController(dbContext).DownloadQr(_exhibitId, CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(download);
        var scan = Assert.IsType<OkObjectResult>((await ScanAsync(file.FileContents, file.FileDownloadName)).Result);
        var exhibit = Assert.IsType<ExhibitResponse>(scan.Value);
        Assert.Equal(_exhibitId, exhibit.Id);
        Assert.Equal(ExhibitCode, exhibit.Code);
    }

    [Fact]
    public async Task DownloadReturnsClientErrorWhenCodeCannotFitInAQr()
    {
        await using var dbContext = CreateDbContext();
        var exhibit = new Exhibit { Code = new string('x', 4000), Name = "Long code", Status = ExhibitStatus.CheckedOut };
        dbContext.Exhibits.Add(exhibit);
        await dbContext.SaveChangesAsync();

        var result = await new ExhibitsController(dbContext).DownloadQr(exhibit.Id, CancellationToken.None);
        Assert.IsType<UnprocessableEntityObjectResult>(result);
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    public async Task ScanReturnsExistingExhibitWithoutChangingItsStorage(SKEncodedImageFormat format)
    {
        var result = await ScanAsync(CreateQrImage(ExhibitCode, format), "upload.bin");

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var exhibit = Assert.IsType<ExhibitResponse>(response.Value);
        Assert.Equal(_exhibitId, exhibit.Id);
        Assert.Equal(ExhibitCode, exhibit.Code);
        Assert.Equal("Ceramic Bowl", exhibit.Name);
        Assert.Equal("Stored", exhibit.Status);
        Assert.Equal("SHELF-A1", exhibit.StorageLocation?.Code);

        await using var dbContext = CreateDbContext();
        var persisted = await dbContext.Exhibits.SingleAsync();
        Assert.Equal(ExhibitStatus.Stored, persisted.Status);
        Assert.Equal(exhibit.StorageLocationId, persisted.StorageLocationId);
        Assert.Empty(await dbContext.StorageOperations.ToListAsync());
    }

    [Theory]
    [InlineData("EXHIBIT-UNKNOWN")]
    [InlineData("exhibit-0001")]
    public async Task ScanReturnsNotFoundForUnknownExactCode(string code)
    {
        var result = await ScanAsync(CreateQrImage(code));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ScanTrimsSurroundingWhitespace()
    {
        var result = await ScanAsync(CreateQrImage($"  {ExhibitCode}\r\n"));

        var response = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(_exhibitId, Assert.IsType<ExhibitResponse>(response.Value).Id);
    }

    [Fact]
    public async Task ScanRejectsWhitespaceQrValue()
    {
        var result = await ScanAsync(CreateQrImage("   "));

        Assert.Contains("must contain an exhibit code", AssertBadRequest(result));
    }

    [Fact]
    public async Task ScanRejectsImageWithoutQrCode()
    {
        using var bitmap = new SKBitmap(240, 240);
        bitmap.Erase(SKColors.White);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        var result = await ScanAsync(data.ToArray());

        Assert.Contains("No QR code", AssertBadRequest(result));
    }

    [Fact]
    public async Task ScanRejectsMissingFile()
    {
        AssertBadRequest(await ScanAsync(null));
    }

    [Fact]
    public async Task ScanRejectsEmptyFile()
    {
        AssertBadRequest(await ScanAsync([]));
    }

    [Fact]
    public async Task ScanRejectsUnsupportedImageFormatWithPngFilename()
    {
        var result = await ScanAsync(CreateQrImage(ExhibitCode, SKEncodedImageFormat.Webp), "scan.png");

        Assert.Contains("valid PNG or JPEG", AssertBadRequest(result));
    }

    [Fact]
    public async Task ScanRejectsCorruptImage()
    {
        var validImage = CreateQrImage(ExhibitCode);
        var result = await ScanAsync(validImage[..(validImage.Length / 2)]);

        AssertBadRequest(result);
    }

    [Fact]
    public async Task ScanRejectsOversizedFile()
    {
        var result = await ScanAsync(new byte[5 * 1024 * 1024 + 1]);

        Assert.Contains("5 MiB", AssertBadRequest(result));
    }

    [Fact]
    public async Task ScanRejectsImageOverPixelLimit()
    {
        using var bitmap = new SKBitmap(3200, 2600);
        bitmap.Erase(SKColors.White);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        var result = await ScanAsync(data.ToArray());

        Assert.Contains("8 megapixels", AssertBadRequest(result));
    }

    private MuseumStorageDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(_connection).Options);

    private async Task<ActionResult<ExhibitResponse>> ScanAsync(byte[]? image, string fileName = "scan.png")
    {
        using var multipart = new MultipartFormDataContent();
        if (image is null)
            multipart.Add(new StringContent("No file"), "note");
        else
            multipart.Add(new ByteArrayContent(image), "file", fileName);

        using var body = new MemoryStream();
        await multipart.CopyToAsync(body);
        body.Position = 0;

        var context = new DefaultHttpContext();
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.ContentLength = body.Length;
        context.Request.Body = body;
        await using var dbContext = CreateDbContext();
        var controller = new ExhibitsController(dbContext) { ControllerContext = new ControllerContext { HttpContext = context } };

        return await controller.ScanQr();
    }

    private static byte[] CreateQrImage(string code, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        var writer = new ZXing.SkiaSharp.BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions { Width = 240, Height = 240, Margin = 4 }
        };
        using var bitmap = writer.Write(code);
        using var data = bitmap.Encode(format, 95);
        return data.ToArray();
    }

    private static string AssertBadRequest(ActionResult<ExhibitResponse> result)
    {
        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, response.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(response.Value);
        return Assert.Single(problem.Errors["file"]);
    }
}
