#pragma once

#include <algorithm>
#include <cmath>

struct EnvironmentSettings
{
    double coolingOnTemperature;
    double coolingOffTemperature;
    double heatingOnTemperature;
    double heatingOffTemperature;
    double humidifierOnHumidity;
    double humidifierOffHumidity;
    double dehumidifierOnHumidity;
    double dehumidifierOffHumidity;
    double minLightLevel;
    double maxLightLevel;
    double temperatureAlarm;
    double humidityAlarm;
};

constexpr EnvironmentSettings DEFAULT_ENVIRONMENT_SETTINGS{
    26.0, 25.0, 18.0, 19.0, 35.0, 40.0, 70.0, 65.0, 300.0, 700.0, 30.0, 80.0
};

inline bool isInRange(double value, double minimum, double maximum)
{
    return std::isfinite(value) && value >= minimum && value <= maximum;
}

inline bool validateEnvironmentSettings(const EnvironmentSettings& settings)
{
    return isInRange(settings.coolingOnTemperature, -40.0, 80.0) &&
           isInRange(settings.coolingOffTemperature, -40.0, 80.0) &&
           isInRange(settings.heatingOnTemperature, -40.0, 80.0) &&
           isInRange(settings.heatingOffTemperature, -40.0, 80.0) &&
           isInRange(settings.temperatureAlarm, -40.0, 80.0) &&
           isInRange(settings.dehumidifierOnHumidity, 0.0, 100.0) &&
           isInRange(settings.dehumidifierOffHumidity, 0.0, 100.0) &&
           isInRange(settings.humidifierOnHumidity, 0.0, 100.0) &&
           isInRange(settings.humidifierOffHumidity, 0.0, 100.0) &&
           isInRange(settings.humidityAlarm, 0.0, 100.0) &&
           settings.heatingOnTemperature < settings.heatingOffTemperature &&
           settings.coolingOffTemperature < settings.coolingOnTemperature &&
           settings.heatingOffTemperature < settings.coolingOnTemperature &&
           settings.heatingOnTemperature <= settings.coolingOffTemperature &&
           settings.temperatureAlarm >= settings.coolingOnTemperature &&
           settings.humidifierOnHumidity < settings.humidifierOffHumidity &&
           settings.dehumidifierOffHumidity < settings.dehumidifierOnHumidity &&
           settings.humidifierOffHumidity < settings.dehumidifierOnHumidity &&
           settings.humidifierOnHumidity <= settings.dehumidifierOffHumidity &&
           settings.humidityAlarm >= settings.dehumidifierOnHumidity &&
           std::isfinite(settings.minLightLevel) && std::isfinite(settings.maxLightLevel) &&
           settings.minLightLevel >= 0.0 && settings.minLightLevel < settings.maxLightLevel;
}

// skirtingos įjungimo ir išjungimo ribos, kad relė nesijunginėtų ties viena riba
inline bool nextCoolingState(const EnvironmentSettings& settings, bool enabled, float temperature)
{
    return enabled ? temperature > settings.coolingOffTemperature : temperature >= settings.coolingOnTemperature;
}

inline bool nextHeatingState(const EnvironmentSettings& settings, bool enabled, float temperature)
{
    return enabled ? temperature < settings.heatingOffTemperature : temperature <= settings.heatingOnTemperature;
}

inline bool nextHumidifierState(const EnvironmentSettings& settings, bool enabled, float humidity)
{
    return enabled ? humidity < settings.humidifierOffHumidity : humidity <= settings.humidifierOnHumidity;
}

inline bool nextDehumidifierState(const EnvironmentSettings& settings, bool enabled, float humidity)
{
    return enabled ? humidity > settings.dehumidifierOffHumidity : humidity >= settings.dehumidifierOnHumidity;
}

inline int nextLightIntensity(const EnvironmentSettings& settings, int intensity, float lux)
{
    int step = lux < settings.minLightLevel ? 10 : lux > settings.maxLightLevel ? -10 : 0;
    return std::clamp(intensity + step, 0, 100);
}

inline bool shouldAlarm(const EnvironmentSettings& settings, float temperature, float humidity)
{
    return temperature >= settings.temperatureAlarm || humidity >= settings.humidityAlarm;
}
