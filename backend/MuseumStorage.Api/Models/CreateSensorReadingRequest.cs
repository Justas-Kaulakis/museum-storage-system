namespace MuseumStorage.Api.Models;

public class CreateSensorReadingRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double LightLevel { get; set; }
    public bool FanEnabled { get; set; }
    public bool? CoolingEnabled { get; set; }
    public bool HeatingEnabled { get; set; }
    public bool HumidifierEnabled { get; set; }
    public bool DehumidifierEnabled { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 100, ErrorMessage = "Apšvietimo galia turi būti nuo 0 iki 100 %.")]
    public int LightIntensity { get; set; }

    public bool AlarmEnabled { get; set; }
}
