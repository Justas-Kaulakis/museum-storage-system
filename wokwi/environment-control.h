#pragma once

#include <cmath>

struct EnvironmentSettings
{
    double fanOnTemperature;
    double fanOffTemperature;
    double fanOnHumidity;
    double fanOffHumidity;
    double temperatureAlarm;
    double humidityAlarm;
};

constexpr EnvironmentSettings DEFAULT_ENVIRONMENT_SETTINGS{26.0, 25.0, 70.0, 65.0, 30.0, 80.0};

inline bool isInRange(double value, double minimum, double maximum)
{
    return std::isfinite(value) && value >= minimum && value <= maximum;
}

inline bool validateEnvironmentSettings(const EnvironmentSettings& settings)
{
    return isInRange(settings.fanOnTemperature, -40.0, 80.0) &&
           isInRange(settings.fanOffTemperature, -40.0, 80.0) &&
           isInRange(settings.temperatureAlarm, -40.0, 80.0) &&
           isInRange(settings.fanOnHumidity, 0.0, 100.0) &&
           isInRange(settings.fanOffHumidity, 0.0, 100.0) &&
           isInRange(settings.humidityAlarm, 0.0, 100.0) &&
           settings.fanOffTemperature < settings.fanOnTemperature &&
           settings.fanOffHumidity < settings.fanOnHumidity &&
           settings.temperatureAlarm >= settings.fanOnTemperature &&
           settings.humidityAlarm >= settings.fanOnHumidity;
}

inline bool nextFanState(const EnvironmentSettings& settings, bool enabled, float temperature, float humidity)
{
    if (enabled)
        return !(temperature <= settings.fanOffTemperature && humidity <= settings.fanOffHumidity);

    return temperature >= settings.fanOnTemperature || humidity >= settings.fanOnHumidity;
}

inline bool shouldAlarm(const EnvironmentSettings& settings, float temperature, float humidity)
{
    return temperature >= settings.temperatureAlarm || humidity >= settings.humidityAlarm;
}
