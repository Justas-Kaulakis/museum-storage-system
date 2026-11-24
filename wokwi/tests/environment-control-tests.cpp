#include "../environment-control.h"

#include <cassert>
#include <limits>

void testDefaultControl()
{
    const auto& settings = DEFAULT_ENVIRONMENT_SETTINGS;
    assert(validateEnvironmentSettings(settings));
    assert(!nextCoolingState(settings, false, 22.5f));
    assert(nextCoolingState(settings, false, 26.0f));
    assert(nextCoolingState(settings, true, 25.5f));
    assert(!nextCoolingState(settings, true, 25.0f));
    assert(nextCoolingState(settings, false, 30.0f));

    assert(!nextHeatingState(settings, false, 19.0f));
    assert(nextHeatingState(settings, false, 18.0f));
    assert(nextHeatingState(settings, true, 18.5f));
    assert(!nextHeatingState(settings, true, 19.0f));

    assert(!nextHumidifierState(settings, false, 40.0f));
    assert(nextHumidifierState(settings, false, 35.0f));
    assert(nextHumidifierState(settings, true, 39.0f));
    assert(!nextHumidifierState(settings, true, 40.0f));

    assert(!nextDehumidifierState(settings, false, 53.0f));
    assert(nextDehumidifierState(settings, false, 70.0f));
    assert(nextDehumidifierState(settings, true, 67.0f));
    assert(!nextDehumidifierState(settings, true, 65.0f));
    assert(nextDehumidifierState(settings, false, 80.0f));

    assert(!shouldAlarm(settings, 22.5f, 53.0f));
    assert(shouldAlarm(settings, 30.0f, 53.0f));
    assert(shouldAlarm(settings, 22.5f, 80.0f));
    assert(!shouldAlarm(settings, 29.9f, 79.9f));
}

void testUpdatedSettings()
{
    auto settings = DEFAULT_ENVIRONMENT_SETTINGS;
    settings.coolingOnTemperature = 24;
    settings.coolingOffTemperature = 23;
    settings.dehumidifierOnHumidity = 68;
    settings.dehumidifierOffHumidity = 60;
    settings.temperatureAlarm = 25;
    settings.humidityAlarm = 75;
    assert(validateEnvironmentSettings(settings));
    assert(!nextCoolingState(DEFAULT_ENVIRONMENT_SETTINGS, false, 24.0f));
    assert(nextCoolingState(settings, false, 24.0f));
    assert(nextCoolingState(settings, true, 23.5f));
    assert(!nextCoolingState(settings, true, 23.0f));
    assert(nextDehumidifierState(settings, false, 68.0f));
    assert(nextDehumidifierState(settings, true, 61.0f));
    assert(!nextDehumidifierState(settings, true, 60.0f));
    assert(shouldAlarm(settings, 25.0f, 53.0f));
    assert(shouldAlarm(settings, 22.5f, 75.0f));
}

void testLighting()
{
    const auto& settings = DEFAULT_ENVIRONMENT_SETTINGS;
    assert(nextLightIntensity(settings, 40, 299.0f) == 50);
    assert(nextLightIntensity(settings, 40, 701.0f) == 30);
    assert(nextLightIntensity(settings, 40, 300.0f) == 40);
    assert(nextLightIntensity(settings, 40, 700.0f) == 40);
    assert(nextLightIntensity(settings, 95, 0.0f) == 100);
    assert(nextLightIntensity(settings, 5, 1000.0f) == 0);
}

void testSettingsValidation()
{
    const auto& defaults = DEFAULT_ENVIRONMENT_SETTINGS;
    auto settings = defaults;
    settings.coolingOnTemperature = 80;
    settings.coolingOffTemperature = -40;
    settings.heatingOnTemperature = -40;
    settings.dehumidifierOnHumidity = 100;
    settings.dehumidifierOffHumidity = 0;
    settings.humidifierOnHumidity = 0;
    settings.temperatureAlarm = 80;
    settings.humidityAlarm = 100;
    assert(validateEnvironmentSettings(settings));
    assert(!nextCoolingState(settings, true, -40.0f) && nextHeatingState(settings, false, -40.0f));
    assert(!nextDehumidifierState(settings, true, 0.0f) && nextHumidifierState(settings, false, 0.0f));

    settings = defaults;
    settings.coolingOffTemperature = 26;
    assert(!validateEnvironmentSettings(settings));
    settings.coolingOffTemperature = -40.1;
    assert(!validateEnvironmentSettings(settings));

    settings = defaults;
    settings.dehumidifierOffHumidity = 71;
    assert(!validateEnvironmentSettings(settings));

    settings = defaults;
    settings.temperatureAlarm = 25;
    assert(!validateEnvironmentSettings(settings));
    settings.temperatureAlarm = std::numeric_limits<double>::quiet_NaN();
    assert(!validateEnvironmentSettings(settings));
    settings.temperatureAlarm = std::numeric_limits<double>::infinity();
    assert(!validateEnvironmentSettings(settings));

    settings = defaults;
    settings.humidityAlarm = 69;
    assert(!validateEnvironmentSettings(settings));
    settings.humidityAlarm = 100.1;
    assert(!validateEnvironmentSettings(settings));

    settings = defaults;
    settings.heatingOnTemperature = 19;
    assert(!validateEnvironmentSettings(settings));
    settings.heatingOnTemperature = 18;
    settings.heatingOffTemperature = 26;
    assert(!validateEnvironmentSettings(settings));
    settings.heatingOffTemperature = 19;
    settings.coolingOffTemperature = 16;
    assert(!validateEnvironmentSettings(settings));

    settings = defaults;
    settings.humidifierOffHumidity = 70;
    assert(!validateEnvironmentSettings(settings));
    settings.humidifierOffHumidity = 40;
    settings.dehumidifierOffHumidity = 34;
    assert(!validateEnvironmentSettings(settings));

    settings = defaults;
    settings.minLightLevel = -1;
    assert(!validateEnvironmentSettings(settings));
    settings.minLightLevel = 700;
    assert(!validateEnvironmentSettings(settings));
}

int main()
{
    testDefaultControl();
    testUpdatedSettings();
    testLighting();
    testSettingsValidation();
}
