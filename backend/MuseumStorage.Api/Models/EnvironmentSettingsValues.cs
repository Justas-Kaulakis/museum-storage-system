namespace MuseumStorage.Api.Models;

public sealed record EnvironmentSettingsValues
{
    public required double FanOnTemperature { get; init; }
    public required double FanOffTemperature { get; init; }
    public required double FanOnHumidity { get; init; }
    public required double FanOffHumidity { get; init; }
    public required double TemperatureAlarm { get; init; }
    public required double HumidityAlarm { get; init; }
}
