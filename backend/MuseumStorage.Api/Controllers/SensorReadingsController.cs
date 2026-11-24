using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuseumStorage.Api.Models;
using MuseumStorage.Api.Entities;
using MuseumStorage.Api.Data;

namespace MuseumStorage.Api.Controllers;

[ApiController]
[Route("api/sensor-readings")]
public class SensorReadingsController : ControllerBase
{
    private readonly MuseumStorageDbContext _dbContext;

    public SensorReadingsController(MuseumStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SensorReading>>> GetAll([FromQuery] string? deviceId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SensorReadings.AsNoTracking();
        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(x => x.DeviceId == deviceId);

        var readings = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(readings);
    }

    [HttpPost]
    public async Task<ActionResult<SensorReading>> Create(CreateSensorReadingRequest request)
    {
        var reading = new SensorReading
        {
            DeviceId = request.DeviceId,
            Temperature = request.Temperature,
            Humidity = request.Humidity,
            LightLevel = request.LightLevel,
            CoolingEnabled = request.CoolingEnabled ?? request.FanEnabled,
            HeatingEnabled = request.HeatingEnabled,
            HumidifierEnabled = request.HumidifierEnabled,
            DehumidifierEnabled = request.DehumidifierEnabled,
            LightIntensity = request.LightIntensity,
            AlarmEnabled = request.AlarmEnabled
        };

        _dbContext.SensorReadings.Add(reading);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = reading.Id }, reading);
    }
}
