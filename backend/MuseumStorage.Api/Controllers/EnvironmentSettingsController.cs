using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;

namespace MuseumStorage.Api.Controllers;

[ApiController]
[Route("api/storage-zones/{zoneId:long}/environment-settings")]
public class EnvironmentSettingsController : ControllerBase
{
    private readonly MuseumStorageDbContext _dbContext;

    public EnvironmentSettingsController(MuseumStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public Task<ActionResult<EnvironmentSettingsValues>> Get(long zoneId, CancellationToken cancellationToken) =>
        ReadAsync(_dbContext.EnvironmentSettings.Where(x => x.StorageZoneId == zoneId), cancellationToken);

    [HttpGet("/api/environment-settings")]
    public async Task<ActionResult<EnvironmentSettingsValues>> GetByDeviceId([FromQuery] string? deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return NotFound();

        var id = deviceId.Trim();
        return await ReadAsync(_dbContext.EnvironmentSettings.Where(x => x.StorageZone.DeviceId == id), cancellationToken);
    }

    private async Task<ActionResult<EnvironmentSettingsValues>> ReadAsync(IQueryable<EnvironmentSettings> query, CancellationToken cancellationToken)
    {
        var settings = await query
            .Select(x => new EnvironmentSettingsValues
            {
                FanOnTemperature = x.FanOnTemperature,
                FanOffTemperature = x.FanOffTemperature,
                FanOnHumidity = x.FanOnHumidity,
                FanOffHumidity = x.FanOffHumidity,
                TemperatureAlarm = x.TemperatureAlarm,
                HumidityAlarm = x.HumidityAlarm
            })
            .SingleOrDefaultAsync(cancellationToken);

        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<EnvironmentSettingsValues>> Put(
        long zoneId, EnvironmentSettingsValues settings, CancellationToken cancellationToken)
    {
        if (!IsTemperatureInRange(settings.FanOnTemperature) ||
            !IsTemperatureInRange(settings.FanOffTemperature) ||
            !IsTemperatureInRange(settings.TemperatureAlarm))
            ModelState.AddModelError("Temperature", "Temperature thresholds must be between -40 and 80 °C.");

        if (!IsHumidityInRange(settings.FanOnHumidity) ||
            !IsHumidityInRange(settings.FanOffHumidity) ||
            !IsHumidityInRange(settings.HumidityAlarm))
            ModelState.AddModelError("Humidity", "Humidity thresholds must be between 0 and 100%.");

        if (settings.FanOffTemperature >= settings.FanOnTemperature)
            ModelState.AddModelError(nameof(settings.FanOffTemperature), "Fan-off temperature must be below fan-on temperature.");

        if (settings.FanOffHumidity >= settings.FanOnHumidity)
            ModelState.AddModelError(nameof(settings.FanOffHumidity), "Fan-off humidity must be below fan-on humidity.");

        if (settings.TemperatureAlarm < settings.FanOnTemperature)
            ModelState.AddModelError(nameof(settings.TemperatureAlarm), "Alarm temperature must be at or above fan-on temperature.");

        if (settings.HumidityAlarm < settings.FanOnHumidity)
            ModelState.AddModelError(nameof(settings.HumidityAlarm), "Alarm humidity must be at or above fan-on humidity.");

        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var updated = await _dbContext.EnvironmentSettings
            .Where(x => x.StorageZoneId == zoneId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.FanOnTemperature, settings.FanOnTemperature)
                .SetProperty(x => x.FanOffTemperature, settings.FanOffTemperature)
                .SetProperty(x => x.FanOnHumidity, settings.FanOnHumidity)
                .SetProperty(x => x.FanOffHumidity, settings.FanOffHumidity)
                .SetProperty(x => x.TemperatureAlarm, settings.TemperatureAlarm)
                .SetProperty(x => x.HumidityAlarm, settings.HumidityAlarm), cancellationToken);

        return updated == 0 ? NotFound() : Ok(settings);
    }

    private static bool IsTemperatureInRange(double value) => value is >= -40 and <= 80;

    private static bool IsHumidityInRange(double value) => value is >= 0 and <= 100;
}
