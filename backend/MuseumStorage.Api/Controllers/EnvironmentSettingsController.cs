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
        // valdikliui nereikia žinoti zonos numerio, užtenka savo įrenginio id
        return await ReadAsync(_dbContext.EnvironmentSettings.Where(x => x.StorageZone.DeviceId == id), cancellationToken);
    }

    private async Task<ActionResult<EnvironmentSettingsValues>> ReadAsync(IQueryable<EnvironmentSettings> query, CancellationToken cancellationToken)
    {
        var settings = await query
            .Select(x => new EnvironmentSettingsValues
            {
                CoolingOnTemperature = x.CoolingOnTemperature,
                CoolingOffTemperature = x.CoolingOffTemperature,
                HeatingOnTemperature = x.HeatingOnTemperature,
                HeatingOffTemperature = x.HeatingOffTemperature,
                HumidifierOnHumidity = x.HumidifierOnHumidity,
                HumidifierOffHumidity = x.HumidifierOffHumidity,
                DehumidifierOnHumidity = x.DehumidifierOnHumidity,
                DehumidifierOffHumidity = x.DehumidifierOffHumidity,
                MinLightLevel = x.MinLightLevel,
                MaxLightLevel = x.MaxLightLevel,
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
        if (!IsTemperatureInRange(settings.CoolingOnTemperature) || !IsTemperatureInRange(settings.CoolingOffTemperature) ||
            !IsTemperatureInRange(settings.HeatingOnTemperature) || !IsTemperatureInRange(settings.HeatingOffTemperature) ||
            !IsTemperatureInRange(settings.TemperatureAlarm))
            ModelState.AddModelError("Temperature", "Temperatūros ribos turi būti nuo −40 iki 80 °C.");

        if (!IsHumidityInRange(settings.DehumidifierOnHumidity) || !IsHumidityInRange(settings.DehumidifierOffHumidity) ||
            !IsHumidityInRange(settings.HumidifierOnHumidity) || !IsHumidityInRange(settings.HumidifierOffHumidity) ||
            !IsHumidityInRange(settings.HumidityAlarm))
            ModelState.AddModelError("Humidity", "Drėgmės ribos turi būti nuo 0 iki 100 %.");

        if (settings.HeatingOnTemperature >= settings.HeatingOffTemperature)
            ModelState.AddModelError(nameof(settings.HeatingOnTemperature), "Šildymo įjungimo temperatūra turi būti žemesnė už išjungimo temperatūrą.");

        if (settings.CoolingOffTemperature >= settings.CoolingOnTemperature)
            ModelState.AddModelError(nameof(settings.CoolingOffTemperature), "Vėsinimo išjungimo temperatūra turi būti žemesnė už įjungimo temperatūrą.");

        if (settings.HeatingOffTemperature >= settings.CoolingOnTemperature || settings.HeatingOnTemperature > settings.CoolingOffTemperature)
            ModelState.AddModelError(nameof(settings.HeatingOffTemperature), "Šildymas turi išsijungti iki įsijungiant vėsinimui, o vėsinimas turi išsijungti iki įsijungiant šildymui.");

        if (settings.TemperatureAlarm < settings.CoolingOnTemperature)
            ModelState.AddModelError(nameof(settings.TemperatureAlarm), "Temperatūros aliarmo riba turi būti ne žemesnė už vėsinimo įjungimo temperatūrą.");

        if (settings.HumidifierOnHumidity >= settings.HumidifierOffHumidity)
            ModelState.AddModelError(nameof(settings.HumidifierOnHumidity), "Drėkintuvo įjungimo riba turi būti mažesnė už išjungimo ribą.");

        if (settings.DehumidifierOffHumidity >= settings.DehumidifierOnHumidity)
            ModelState.AddModelError(nameof(settings.DehumidifierOffHumidity), "Sausintuvo išjungimo riba turi būti mažesnė už įjungimo ribą.");

        if (settings.HumidifierOffHumidity >= settings.DehumidifierOnHumidity || settings.HumidifierOnHumidity > settings.DehumidifierOffHumidity)
            ModelState.AddModelError(nameof(settings.HumidifierOffHumidity), "Drėkintuvas ir sausintuvas turi išsijungti, kol dar neįsijungė kitas įrenginys.");

        if (settings.HumidityAlarm < settings.DehumidifierOnHumidity)
            ModelState.AddModelError(nameof(settings.HumidityAlarm), "Drėgmės aliarmo riba turi būti ne mažesnė už sausintuvo įjungimo ribą.");

        if (!double.IsFinite(settings.MinLightLevel) || !double.IsFinite(settings.MaxLightLevel) ||
            settings.MinLightLevel < 0 || settings.MinLightLevel >= settings.MaxLightLevel)
            ModelState.AddModelError(nameof(settings.MinLightLevel), "Apšvietimo ribos turi būti neneigiamos, o mažiausia riba turi būti mažesnė už didžiausią.");

        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var updated = await _dbContext.EnvironmentSettings
            .Where(x => x.StorageZoneId == zoneId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.CoolingOnTemperature, settings.CoolingOnTemperature)
                .SetProperty(x => x.CoolingOffTemperature, settings.CoolingOffTemperature)
                .SetProperty(x => x.HeatingOnTemperature, settings.HeatingOnTemperature)
                .SetProperty(x => x.HeatingOffTemperature, settings.HeatingOffTemperature)
                .SetProperty(x => x.HumidifierOnHumidity, settings.HumidifierOnHumidity)
                .SetProperty(x => x.HumidifierOffHumidity, settings.HumidifierOffHumidity)
                .SetProperty(x => x.DehumidifierOnHumidity, settings.DehumidifierOnHumidity)
                .SetProperty(x => x.DehumidifierOffHumidity, settings.DehumidifierOffHumidity)
                .SetProperty(x => x.MinLightLevel, settings.MinLightLevel)
                .SetProperty(x => x.MaxLightLevel, settings.MaxLightLevel)
                .SetProperty(x => x.TemperatureAlarm, settings.TemperatureAlarm)
                .SetProperty(x => x.HumidityAlarm, settings.HumidityAlarm), cancellationToken);

        return updated == 0 ? NotFound() : Ok(settings);
    }

    private static bool IsTemperatureInRange(double value) => value is >= -40 and <= 80;

    private static bool IsHumidityInRange(double value) => value is >= 0 and <= 100;
}
