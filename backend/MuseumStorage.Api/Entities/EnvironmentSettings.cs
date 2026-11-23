namespace MuseumStorage.Api.Entities;

public class EnvironmentSettings
{
    public const int GlobalId = 1;

    public int Id { get; set; }
    public double FanOnTemperature { get; set; }
    public double FanOffTemperature { get; set; }
    public double FanOnHumidity { get; set; }
    public double FanOffHumidity { get; set; }
    public double TemperatureAlarm { get; set; }
    public double HumidityAlarm { get; set; }
}
