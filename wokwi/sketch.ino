#include <Arduino.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <ArduinoJson.h>
#include <DHTesp.h>
#include <HTTPClient.h>
#include <StreamString.h>
#include <WiFi.h>
#include <Wire.h>
#include <freertos/FreeRTOS.h>
#include <freertos/queue.h>
#include <freertos/task.h>
#include "environment-control.h"

constexpr int DHT_PIN = 15;
constexpr int LDR_PIN = 34;
constexpr int RELAY_PIN = 26;
constexpr int BUZZER_PIN = 27;
constexpr int HEATER_PIN = 32;
constexpr int HUMIDIFIER_PIN = 33;
constexpr int DEHUMIDIFIER_PIN = 25;
constexpr int LIGHTING_PIN = 23;
constexpr int OLED_SDA = 21;
constexpr int OLED_SCL = 22;

constexpr int SCREEN_WIDTH = 128;
constexpr int SCREEN_HEIGHT = 64;
constexpr int OLED_RESET = -1;
constexpr int OLED_ADDRESS = 0x3C;

constexpr float LDR_GAMMA = 0.7;
constexpr float LDR_RL10 = 50.0;
constexpr float ADC_MAX = 4095.0;
constexpr float ADC_VOLTAGE = 3.3;

constexpr char WIFI_SSID[] = "Wokwi-GUEST";
constexpr char WIFI_PASSWORD[] = "";
constexpr int WIFI_CHANNEL = 6;
constexpr char BACKEND_URL[] = "http://host.wokwi.internal:5054";
constexpr char DEVICE_ID[] = "museum-storage-01";
constexpr uint32_t SENSOR_INTERVAL_MS = 2000;
constexpr uint32_t TELEMETRY_INTERVAL_MS = 10000;
constexpr uint32_t SETTINGS_INTERVAL_MS = 30000;
constexpr uint32_t WIFI_RECONNECT_INTERVAL_MS = 10000;
constexpr uint16_t HTTP_TIMEOUT_MS = 2500;

struct SensorReading
{
    float temperature;
    float humidity;
    float lux;
    bool coolingEnabled;
    bool heatingEnabled;
    bool humidifierEnabled;
    bool dehumidifierEnabled;
    int lightIntensity;
    bool alarmEnabled;
    bool valid;
};

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);
DHTesp dht;
EnvironmentSettings environmentSettings = DEFAULT_ENVIRONMENT_SETTINGS;
bool displayReady = false;
bool coolingEnabled = false;
bool heatingEnabled = false;
bool humidifierEnabled = false;
bool dehumidifierEnabled = false;
int lightIntensity = 0;
bool alarmEnabled = false;
QueueHandle_t settingsQueue = nullptr;
QueueHandle_t readingsQueue = nullptr;

void updateDisplay(const SensorReading& reading);
void sendSensorReading(const SensorReading& reading);

void setAlarm(bool enabled)
{
    if (enabled == alarmEnabled)
        return;

    alarmEnabled = enabled;
    if (enabled) {
        tone(BUZZER_PIN, 1000);
        Serial.println("ALARM activated");
    } else {
        noTone(BUZZER_PIN);
        Serial.println("ALARM cleared");
    }
}

void updateActuators(float temperature, float humidity)
{
    coolingEnabled = nextCoolingState(environmentSettings, coolingEnabled, temperature);
    heatingEnabled = nextHeatingState(environmentSettings, heatingEnabled, temperature);
    humidifierEnabled = nextHumidifierState(environmentSettings, humidifierEnabled, humidity);
    dehumidifierEnabled = nextDehumidifierState(environmentSettings, dehumidifierEnabled, humidity);
    // pries ijungdami viena isjungiam kita, kad abu trumpam neisijungtu
    if (coolingEnabled)
        digitalWrite(HEATER_PIN, LOW);
    // vesinimo rele ijungia zemas signalas
    digitalWrite(RELAY_PIN, coolingEnabled ? LOW : HIGH);
    digitalWrite(HEATER_PIN, heatingEnabled ? HIGH : LOW);
    if (dehumidifierEnabled)
        digitalWrite(HUMIDIFIER_PIN, LOW);
    digitalWrite(DEHUMIDIFIER_PIN, dehumidifierEnabled ? HIGH : LOW);
    digitalWrite(HUMIDIFIER_PIN, humidifierEnabled ? HIGH : LOW);
    setAlarm(shouldAlarm(environmentSettings, temperature, humidity));
}

float readLux()
{
    int rawValue = analogRead(LDR_PIN);
    // ties adc ribomis grazinam krastines reiksmes, kad nedalytume is nulio
    if (rawValue <= 0)
        return 100000.0;
    if (rawValue >= 4095)
        return 0.0;

    float voltage = rawValue / ADC_MAX * ADC_VOLTAGE;
    float resistance = 10000.0 * voltage / (ADC_VOLTAGE - voltage);
    float lux = pow(LDR_RL10 * 1000.0 * pow(10.0, LDR_GAMMA) / resistance, 1.0 / LDR_GAMMA);
    if (!isfinite(lux))
        return 0.0;
    return lux;
}

void updateDisplay(const SensorReading& reading)
{
    display.clearDisplay();
    display.setTextColor(SSD1306_WHITE);
    display.setTextSize(1);
    display.setTextWrap(false);

    display.setCursor(0, 0);
    display.print("T:");
    if (reading.valid)
        display.print(reading.temperature, 1);
    else
        display.print("--");
    display.print("C H:");
    if (reading.valid)
        display.print(reading.humidity, 1);
    else
        display.print("--");
    display.print("%");

    display.setCursor(0, 12);
    display.print("Lux: ");
    if (reading.lux > 99999)
        display.print(">99999");
    else
        display.print(reading.lux, 0);
    display.setCursor(0, 24);
    display.print("C:");
    display.print(reading.coolingEnabled ? "ON" : "OFF");
    display.print(" H:");
    display.print(reading.heatingEnabled ? "ON" : "OFF");
    display.setCursor(0, 36);
    display.print("HU:");
    display.print(reading.humidifierEnabled ? "ON" : "OFF");
    display.print(" DH:");
    display.print(reading.dehumidifierEnabled ? "ON" : "OFF");
    display.setCursor(0, 48);
    display.print("Light:");
    display.print(reading.lightIntensity);
    display.print("% Alarm:");
    display.print(reading.alarmEnabled ? "ON" : "OK");
    display.display();
}

void updateEnvironment()
{
    if (settingsQueue && xQueueReceive(settingsQueue, &environmentSettings, 0) == pdPASS)
        Serial.println("Environment settings applied");

    TempAndHumidity data = dht.getTempAndHumidity();
    bool valid = isfinite(data.temperature) && isfinite(data.humidity);
    // jei dht neperskaitom, paliekam ankstesnes busenas, bet sviesa vis tiek reguliuojam
    if (valid)
        updateActuators(data.temperature, data.humidity);
    else
        Serial.println("DHT22 read failed");

    float lux = readLux();
    lightIntensity = nextLightIntensity(environmentSettings, lightIntensity, lux);
    // wokwi lempute rodo parinkta ryskuma, o ldr rodmens ji nekeicia
    analogWrite(LIGHTING_PIN, (lightIntensity * 255 + 50) / 100);
    SensorReading reading{data.temperature, data.humidity, lux, coolingEnabled, heatingEnabled, humidifierEnabled, dehumidifierEnabled, lightIntensity, alarmEnabled, valid};
    if (displayReady)
        updateDisplay(reading);
    if (readingsQueue)
        xQueueOverwrite(readingsQueue, &reading);
}

bool beginRequest(HTTPClient& http, WiFiClient& client, const char* path)
{
    http.setConnectTimeout(HTTP_TIMEOUT_MS);
    http.setTimeout(HTTP_TIMEOUT_MS);
    http.setReuse(false);
    return http.begin(client, String(BACKEND_URL) + path);
}

void sendSensorReading(const SensorReading& reading)
{
    JsonDocument json;
    // pagal irenginio id serveris susieja rodmenis su zona
    json["deviceId"] = DEVICE_ID;
    json["temperature"] = reading.temperature;
    json["humidity"] = reading.humidity;
    json["lightLevel"] = reading.lux;
    json["coolingEnabled"] = reading.coolingEnabled;
    json["heatingEnabled"] = reading.heatingEnabled;
    json["humidifierEnabled"] = reading.humidifierEnabled;
    json["dehumidifierEnabled"] = reading.dehumidifierEnabled;
    json["lightIntensity"] = reading.lightIntensity;
    json["alarmEnabled"] = reading.alarmEnabled;
    String body;
    serializeJson(json, body);

    WiFiClient client;
    HTTPClient http;
    if (!beginRequest(http, client, "/api/sensor-readings")) {
        Serial.println("Telemetry POST could not start");
        return;
    }
    http.addHeader("Content-Type", "application/json");
    int status = http.POST(body);
    http.end();
    if (status >= 200 && status < 300)
        Serial.println("Telemetry sent");
    else
        Serial.printf("Telemetry POST failed (%d)\n", status);
}

double readThreshold(JsonVariantConst value)
{
    // trukstama ar netinkama reiksme veliau neleis priimti nustatymu
    return value.is<double>() ? value.as<double>() : NAN;
}

void fetchEnvironmentSettings()
{
    // jei serveris neatsako ar ribos netinka, liekam prie paskutiniu tinkamu nustatymu
    WiFiClient client;
    HTTPClient http;
    const String path = String("/api/environment-settings?deviceId=") + DEVICE_ID;
    if (!beginRequest(http, client, path.c_str())) {
        Serial.println("Settings GET could not start");
        return;
    }
    int status = http.GET();
    if (status != HTTP_CODE_OK) {
        Serial.printf("Settings GET failed (%d)\n", status);
        http.end();
        return;
    }

    StreamString body;
    int bytesRead = http.writeToStream(&body);
    http.end();
    if (bytesRead < 0) {
        Serial.printf("Settings response read failed (%d)\n", bytesRead);
        return;
    }

    JsonDocument json;
    if (deserializeJson(json, body.c_str(), body.length())) {
        Serial.println("Settings JSON could not be parsed");
        return;
    }
    EnvironmentSettings candidate{
        readThreshold(json["coolingOnTemperature"]),
        readThreshold(json["coolingOffTemperature"]),
        readThreshold(json["heatingOnTemperature"]),
        readThreshold(json["heatingOffTemperature"]),
        readThreshold(json["humidifierOnHumidity"]),
        readThreshold(json["humidifierOffHumidity"]),
        readThreshold(json["dehumidifierOnHumidity"]),
        readThreshold(json["dehumidifierOffHumidity"]),
        readThreshold(json["minLightLevel"]),
        readThreshold(json["maxLightLevel"]),
        readThreshold(json["temperatureAlarm"]),
        readThreshold(json["humidityAlarm"])
    };
    if (!validateEnvironmentSettings(candidate)) {
        Serial.println("Invalid environment settings");
        return;
    }
    xQueueOverwrite(settingsQueue, &candidate);
}

void runNetwork(void*)
{
    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(false);
    // laikmacius pastumiam atgal, kad pirmi bandymai prasidetu is karto
    uint32_t lastWiFiAttempt = millis() - WIFI_RECONNECT_INTERVAL_MS;
    uint32_t lastTelemetry = millis() - TELEMETRY_INTERVAL_MS;
    uint32_t lastSettingsFetch = millis() - SETTINGS_INTERVAL_MS;
    bool wasConnected = false;

    for (;;) {
        uint32_t now = millis();
        bool connected = WiFi.status() == WL_CONNECTED;
        if (connected != wasConnected)
            Serial.println(connected ? "Wi-Fi connected" : "Wi-Fi disconnected");
        wasConnected = connected;

        if (!connected && now - lastWiFiAttempt >= WIFI_RECONNECT_INTERVAL_MS) {
            lastWiFiAttempt = now;
            Serial.println("Connecting to Wi-Fi");
            WiFi.begin(WIFI_SSID, WIFI_PASSWORD, WIFI_CHANNEL);
        }
        if (connected && now - lastSettingsFetch >= SETTINGS_INTERVAL_MS) {
            lastSettingsFetch = now;
            fetchEnvironmentSettings();
        }
        now = millis();
        if (WiFi.status() == WL_CONNECTED && now - lastTelemetry >= TELEMETRY_INTERVAL_MS) {
            SensorReading reading;
            if (xQueuePeek(readingsQueue, &reading, 0) == pdPASS && reading.valid) {
                lastTelemetry = now;
                sendSensorReading(reading);
            }
        }
        vTaskDelay(pdMS_TO_TICKS(100));
    }
}

void startNetwork()
{
    // tinklas veikia atskirai, eilese laikom tik naujausius duomenis
    settingsQueue = xQueueCreate(1, sizeof(EnvironmentSettings));
    readingsQueue = xQueueCreate(1, sizeof(SensorReading));
    if (settingsQueue && readingsQueue && xTaskCreate(runNetwork, "network", 8192, nullptr, 1, nullptr) == pdPASS)
        return;

    if (settingsQueue)
        vQueueDelete(settingsQueue);
    if (readingsQueue)
        vQueueDelete(readingsQueue);
    settingsQueue = nullptr;
    readingsQueue = nullptr;
    Serial.println("Network task could not start");
}

void setup()
{
    Serial.begin(115200);

    pinMode(RELAY_PIN, OUTPUT);
    pinMode(BUZZER_PIN, OUTPUT);
    pinMode(HEATER_PIN, OUTPUT);
    pinMode(HUMIDIFIER_PIN, OUTPUT);
    pinMode(DEHUMIDIFIER_PIN, OUTPUT);
    pinMode(LIGHTING_PIN, OUTPUT);
    pinMode(LDR_PIN, INPUT);
    digitalWrite(RELAY_PIN, HIGH);
    digitalWrite(HEATER_PIN, LOW);
    digitalWrite(HUMIDIFIER_PIN, LOW);
    digitalWrite(DEHUMIDIFIER_PIN, LOW);
    analogWrite(LIGHTING_PIN, 0);
    noTone(BUZZER_PIN);

    analogReadResolution(12);
    dht.setup(DHT_PIN, DHTesp::DHT22);

    Wire.begin(OLED_SDA, OLED_SCL);
    displayReady = display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDRESS);
    if (!displayReady)
        Serial.println("OLED initialization failed");
    Serial.println("Museum Storage System");
    startNetwork();
}

void loop()
{
    static uint32_t lastSensorUpdate = millis() - SENSOR_INTERVAL_MS;
    if (millis() - lastSensorUpdate >= SENSOR_INTERVAL_MS) {
        updateEnvironment();
        lastSensorUpdate = millis();
    }
    delay(10);
}
