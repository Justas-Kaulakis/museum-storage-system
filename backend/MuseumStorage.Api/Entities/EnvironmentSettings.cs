namespace MuseumStorage.Api.Entities;

public class EnvironmentSettings
{
    public long StorageZoneId { get; set; }
    public StorageZone StorageZone { get; set; } = null!;
    public double CoolingOnTemperature { get; set; } = 26;
    public double CoolingOffTemperature { get; set; } = 25;
    public double HeatingOnTemperature { get; set; } = 18;
    public double HeatingOffTemperature { get; set; } = 19;
    public double HumidifierOnHumidity { get; set; } = 35;
    public double HumidifierOffHumidity { get; set; } = 40;
    public double DehumidifierOnHumidity { get; set; } = 70;
    public double DehumidifierOffHumidity { get; set; } = 65;
    public double MinLightLevel { get; set; } = 300;
    public double MaxLightLevel { get; set; } = 700;
    public double TemperatureAlarm { get; set; } = 30;
    public double HumidityAlarm { get; set; } = 80;
}
