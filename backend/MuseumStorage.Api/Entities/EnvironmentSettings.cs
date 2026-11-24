namespace MuseumStorage.Api.Entities;

public class EnvironmentSettings
{
    public long StorageZoneId { get; set; }
    public StorageZone StorageZone { get; set; } = null!;
    public double FanOnTemperature { get; set; } = 26;
    public double FanOffTemperature { get; set; } = 25;
    public double FanOnHumidity { get; set; } = 70;
    public double FanOffHumidity { get; set; } = 65;
    public double TemperatureAlarm { get; set; } = 30;
    public double HumidityAlarm { get; set; } = 80;
}
