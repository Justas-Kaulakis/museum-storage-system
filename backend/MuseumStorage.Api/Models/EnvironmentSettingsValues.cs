namespace MuseumStorage.Api.Models;

public sealed record EnvironmentSettingsValues
{
    public required double CoolingOnTemperature { get; init; }
    public required double CoolingOffTemperature { get; init; }
    public required double HeatingOnTemperature { get; init; }
    public required double HeatingOffTemperature { get; init; }
    public required double HumidifierOnHumidity { get; init; }
    public required double HumidifierOffHumidity { get; init; }
    public required double DehumidifierOnHumidity { get; init; }
    public required double DehumidifierOffHumidity { get; init; }
    public required double MinLightLevel { get; init; }
    public required double MaxLightLevel { get; init; }
    public required double TemperatureAlarm { get; init; }
    public required double HumidityAlarm { get; init; }
}
