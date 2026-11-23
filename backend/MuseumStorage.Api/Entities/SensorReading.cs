namespace MuseumStorage.Api.Entities;

public class SensorReading
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double LightLevel { get; set; }
    public bool FanEnabled { get; set; }
    public bool AlarmEnabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}