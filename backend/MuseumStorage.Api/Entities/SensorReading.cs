namespace MuseumStorage.Api.Entities;

public class SensorReading
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double LightLevel { get; set; }
    public bool CoolingEnabled { get; set; }
    public bool HeatingEnabled { get; set; }
    public bool HumidifierEnabled { get; set; }
    public bool DehumidifierEnabled { get; set; }
    public int LightIntensity { get; set; }
    public bool AlarmEnabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
