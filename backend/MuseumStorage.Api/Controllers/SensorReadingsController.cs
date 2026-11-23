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
    public async Task<ActionResult<IEnumerable<SensorReading>>> GetAll()
    {
        var readings = await _dbContext.SensorReadings
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();

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
            FanEnabled = request.FanEnabled,
            AlarmEnabled = request.AlarmEnabled
        };

        _dbContext.SensorReadings.Add(reading);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = reading.Id }, reading);
    }
}