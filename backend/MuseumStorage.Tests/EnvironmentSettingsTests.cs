using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Controllers;
using MuseumStorage.Api.Models;
using MuseumStorage.Domain.Entities;
using MuseumStorage.Infrastructure.Data;

namespace MuseumStorage.Tests;

public sealed class EnvironmentSettingsTests : IAsyncLifetime
{
    private static readonly EnvironmentSettingsValues DefaultSettings = new()
    {
        FanOnTemperature = 26.0,
        FanOffTemperature = 25.0,
        FanOnHumidity = 70.0,
        FanOffHumidity = 65.0,
        TemperatureAlarm = 30.0,
        HumidityAlarm = 80.0
    };

    private static readonly EnvironmentSettingsValues UpdatedSettings = new()
    {
        FanOnTemperature = 27.0,
        FanOffTemperature = 24.5,
        FanOnHumidity = 72.0,
        FanOffHumidity = 64.0,
        TemperatureAlarm = 27.0,
        HumidityAlarm = 72.0
    };

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Fact]
    public async Task GetReturnsSeededDefaults()
    {
        await using var dbContext = CreateDbContext();
        var controller = new EnvironmentSettingsController(dbContext);

        var result = await controller.Get(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(DefaultSettings, Assert.IsType<EnvironmentSettingsValues>(response.Value));
    }

    [Fact]
    public async Task PutReturnsSavedSettingsAndSubsequentGetReadsThem()
    {
        await using var writeContext = CreateDbContext();
        var writeController = new EnvironmentSettingsController(writeContext);
        var putResult = await writeController.Put(UpdatedSettings, CancellationToken.None);

        var putResponse = Assert.IsType<OkObjectResult>(putResult.Result);
        Assert.Equal(UpdatedSettings, Assert.IsType<EnvironmentSettingsValues>(putResponse.Value));

        await using var readContext = CreateDbContext();
        var readController = new EnvironmentSettingsController(readContext);
        var getResult = await readController.Get(CancellationToken.None);

        var getResponse = Assert.IsType<OkObjectResult>(getResult.Result);
        Assert.Equal(UpdatedSettings, Assert.IsType<EnvironmentSettingsValues>(getResponse.Value));
        Assert.Equal(1, await readContext.EnvironmentSettings.CountAsync());
    }

    [Theory]
    [InlineData(27.0)]
    [InlineData(28.0)]
    public Task PutRejectsFanOffTemperatureAtOrAboveFanOn(double fanOffTemperature) =>
        AssertRejectedAsync(UpdatedSettings with { FanOffTemperature = fanOffTemperature },
            nameof(EnvironmentSettingsValues.FanOffTemperature));

    [Theory]
    [InlineData(72.0)]
    [InlineData(73.0)]
    public Task PutRejectsFanOffHumidityAtOrAboveFanOn(double fanOffHumidity) =>
        AssertRejectedAsync(UpdatedSettings with { FanOffHumidity = fanOffHumidity },
            nameof(EnvironmentSettingsValues.FanOffHumidity));

    [Fact]
    public Task PutRejectsTemperatureAlarmBelowFanOn() =>
        AssertRejectedAsync(UpdatedSettings with { TemperatureAlarm = 26.9 },
            nameof(EnvironmentSettingsValues.TemperatureAlarm));

    [Fact]
    public Task PutRejectsHumidityAlarmBelowFanOn() =>
        AssertRejectedAsync(UpdatedSettings with { HumidityAlarm = 71.9 },
            nameof(EnvironmentSettingsValues.HumidityAlarm));

    [Fact]
    public Task PutRejectsTemperatureBelowRange() =>
        AssertRejectedAsync(UpdatedSettings with { FanOffTemperature = -40.1 }, "Temperature");

    [Theory]
    [InlineData(80.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public Task PutRejectsInvalidAlarmTemperature(double temperatureAlarm) =>
        AssertRejectedAsync(UpdatedSettings with { TemperatureAlarm = temperatureAlarm }, "Temperature");

    [Fact]
    public Task PutRejectsHumidityBelowRange() =>
        AssertRejectedAsync(UpdatedSettings with { FanOffHumidity = -0.1 }, "Humidity");

    [Fact]
    public Task PutRejectsHumidityAboveRange() =>
        AssertRejectedAsync(UpdatedSettings with { HumidityAlarm = 100.1 }, "Humidity");

    [Fact]
    public async Task SensorReadingsCanStillBeCreatedAndRetrieved()
    {
        await using var dbContext = CreateDbContext();
        var controller = new SensorReadingsController(dbContext);
        var request = new CreateSensorReadingRequest
        {
            DeviceId = "storage-controller",
            Temperature = 26.5,
            Humidity = 71.0,
            LightLevel = 13.0,
            FanEnabled = true,
            AlarmEnabled = false
        };

        var postResult = await controller.Create(request);
        var created = Assert.IsType<CreatedAtActionResult>(postResult.Result);
        var saved = Assert.IsType<SensorReading>(created.Value);

        await using var readContext = CreateDbContext();
        var getResult = await new SensorReadingsController(readContext).GetAll();
        var response = Assert.IsType<OkObjectResult>(getResult.Result);
        var reading = Assert.Single(Assert.IsAssignableFrom<IEnumerable<SensorReading>>(response.Value));
        Assert.Equal(saved.Id, reading.Id);
        Assert.Equal(request.DeviceId, reading.DeviceId);
        Assert.Equal(request.Temperature, reading.Temperature);
        Assert.Equal(request.Humidity, reading.Humidity);
        Assert.Equal(request.LightLevel, reading.LightLevel);
        Assert.Equal(request.FanEnabled, reading.FanEnabled);
        Assert.Equal(request.AlarmEnabled, reading.AlarmEnabled);
    }

    private MuseumStorageDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<MuseumStorageDbContext>().UseSqlite(_connection).Options);

    private async Task AssertRejectedAsync(EnvironmentSettingsValues settings, string errorKey)
    {
        await using var dbContext = CreateDbContext();
        var controller = new EnvironmentSettingsController(dbContext);

        var result = await controller.Put(settings, CancellationToken.None);

        var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, response.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(response.Value);
        Assert.Contains(errorKey, problem.Errors.Keys);

        var getResult = await controller.Get(CancellationToken.None);
        var getResponse = Assert.IsType<OkObjectResult>(getResult.Result);
        Assert.Equal(DefaultSettings, Assert.IsType<EnvironmentSettingsValues>(getResponse.Value));
    }
}
