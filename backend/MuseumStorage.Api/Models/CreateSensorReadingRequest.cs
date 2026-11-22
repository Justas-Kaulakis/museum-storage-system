namespace MuseumStorage.Api.Models;

public class CreateSensorReadingRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double LightLevel { get; set; }
    public bool FanEnabled { get; set; }
    public bool AlarmEnabled { get; set; }
}