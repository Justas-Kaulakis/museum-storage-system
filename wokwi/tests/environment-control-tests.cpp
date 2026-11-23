#include "../environment-control.h"

#include <cassert>
#include <limits>

void testDefaultControl()
{
    const auto& settings = DEFAULT_ENVIRONMENT_SETTINGS;
    assert(validateEnvironmentSettings(settings));
    assert(!nextFanState(settings, false, 22.5f, 53.0f));
    assert(!shouldAlarm(settings, 22.5f, 53.0f));
    assert(nextFanState(settings, false, 26.0f, 53.0f));
    assert(nextFanState(settings, false, 22.5f, 70.0f));
    assert(nextFanState(settings, true, 25.5f, 65.0f));
    assert(nextFanState(settings, true, 25.0f, 67.0f));
    assert(!nextFanState(settings, true, 25.0f, 65.0f));
    assert(nextFanState(settings, false, 30.0f, 53.0f));
    assert(shouldAlarm(settings, 30.0f, 53.0f));
    assert(nextFanState(settings, false, 22.5f, 80.0f));
    assert(shouldAlarm(settings, 22.5f, 80.0f));
    assert(!shouldAlarm(settings, 29.9f, 79.9f));
}

void testUpdatedSettings()
{
    const EnvironmentSettings settings{24.0, 23.0, 68.0, 60.0, 25.0, 75.0};
    assert(validateEnvironmentSettings(settings));
    assert(!nextFanState(DEFAULT_ENVIRONMENT_SETTINGS, false, 24.0f, 53.0f));
    assert(nextFanState(settings, false, 24.0f, 53.0f));
    assert(nextFanState(settings, true, 23.5f, 60.0f));
    assert(!nextFanState(settings, true, 23.0f, 60.0f));
    assert(shouldAlarm(settings, 25.0f, 53.0f));
    assert(shouldAlarm(settings, 22.5f, 75.0f));
}

void testSettingsValidation()
{
    assert(validateEnvironmentSettings({80.0, -40.0, 100.0, 0.0, 80.0, 100.0}));
    assert(!validateEnvironmentSettings({26.0, 26.0, 70.0, 65.0, 30.0, 80.0}));
    assert(!validateEnvironmentSettings({26.0, 25.0, 70.0, 71.0, 30.0, 80.0}));
    assert(!validateEnvironmentSettings({26.0, 25.0, 70.0, 65.0, 25.0, 80.0}));
    assert(!validateEnvironmentSettings({26.0, 25.0, 70.0, 65.0, 30.0, 69.0}));
    assert(!validateEnvironmentSettings({26.0, -40.1, 70.0, 65.0, 30.0, 80.0}));
    assert(!validateEnvironmentSettings({26.0, 25.0, 70.0, 65.0, 30.0, 100.1}));
    assert(!validateEnvironmentSettings({26.0, 25.0, 70.0, 65.0, std::numeric_limits<double>::quiet_NaN(), 80.0}));
    assert(!validateEnvironmentSettings({26.0, 25.0, 70.0, 65.0, 30.0, std::numeric_limits<double>::infinity()}));
}

int main()
{
    testDefaultControl();
    testUpdatedSettings();
    testSettingsValidation();
}
