#include <Wire.h>
#include <math.h>
#include "DHTesp.h"
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

constexpr int DHT_PIN = 15;
constexpr int LDR_PIN = 34;
constexpr int RELAY_PIN = 26;
constexpr int BUZZER_PIN = 27;
constexpr int OLED_SDA = 21;
constexpr int OLED_SCL = 22;

constexpr int SCREEN_WIDTH = 128;
constexpr int SCREEN_HEIGHT = 64;
constexpr int OLED_RESET = -1;
constexpr int OLED_ADDRESS = 0x3C;

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);
DHTesp dht;

float fanOnTemperature = 26.0;
float fanOffTemperature = 25.0;
float fanOnHumidity = 70.0;
float fanOffHumidity = 65.0;

float temperatureAlarm = 30.0;
float humidityAlarm = 80.0;

constexpr float LDR_GAMMA = 0.7;
constexpr float LDR_RL10 = 50.0;
constexpr float ADC_MAX = 4095.0;
constexpr float ADC_VOLTAGE = 3.3;

bool fanEnabled = false;
bool alarmEnabled = false;

void setFan(bool enabled)
{
    fanEnabled = enabled;
    digitalWrite(RELAY_PIN, enabled ? LOW : HIGH);
}

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

void updateFan(float temperature, float humidity)
{
    bool shouldTurnOn = temperature >= fanOnTemperature || humidity >= fanOnHumidity;
    bool canTurnOff = temperature <= fanOffTemperature && humidity <= fanOffHumidity;

    if (!fanEnabled && shouldTurnOn) {
        setFan(true);
        Serial.println("Fan turned ON");
    } else if (fanEnabled && canTurnOff) {
        setFan(false);
        Serial.println("Fan turned OFF");
    }
}

void updateAlarm(float temperature, float humidity)
{
    bool shouldAlarm = temperature >= temperatureAlarm || humidity >= humidityAlarm;
    setAlarm(shouldAlarm);
}

float readLux()
{
    int rawValue = analogRead(LDR_PIN);

    if (rawValue <= 0)
        return 100000.0;

    if (rawValue >= 4095)
        return 0.0;

    float voltage = rawValue / ADC_MAX * ADC_VOLTAGE;
    float resistance = 10000.0 * voltage / (ADC_VOLTAGE - voltage);

    float lux = pow(
        LDR_RL10 * 1000.0 * pow(10.0, LDR_GAMMA) / resistance,
        1.0 / LDR_GAMMA
    );

    if (!isfinite(lux))
        return 0.0;

    return lux;
}

void updateDisplay(float temperature, float humidity, float lux)
{
    display.clearDisplay();
    display.setTextColor(SSD1306_WHITE);
    display.setTextSize(1);
    display.setTextWrap(false);

    display.setCursor(0, 0);
    display.print("Museum Storage");

    display.setCursor(0, 14);
    display.print("Temp: ");
    display.print(temperature, 1);
    display.print(" C");

    display.setCursor(0, 26);
    display.print("Hum:  ");
    display.print(humidity, 1);
    display.print(" %");

    display.setCursor(0, 38);
    display.print("Light: ");

    if (lux > 99999)
        display.print(">99999");
    else
        display.print(lux, 0);

    display.print(" lx");

    display.setCursor(0, 50);
    display.print("Fan: ");
    display.print(fanEnabled ? "ON" : "OFF");

    display.setCursor(64, 50);
    display.print("Alarm:");
    display.print(alarmEnabled ? "ON" : "OK");

    display.display();
}

void printStatus(float temperature, float humidity, float lux)
{
    Serial.println("--------------------------------");

    Serial.print("Temperature: ");
    Serial.print(temperature, 1);
    Serial.println(" C");

    Serial.print("Humidity:    ");
    Serial.print(humidity, 1);
    Serial.println(" %");

    Serial.print("Light:       ");
    Serial.print(lux, 0);
    Serial.println(" lx");

    Serial.print("Fan:         ");
    Serial.println(fanEnabled ? "ON" : "OFF");

    Serial.print("Alarm:       ");
    Serial.println(alarmEnabled ? "ON" : "OFF");

    Serial.print("Fan ON limits:  ");
    Serial.print(fanOnTemperature);
    Serial.print(" C / ");
    Serial.print(fanOnHumidity);
    Serial.println(" %");

    Serial.print("Fan OFF limits: ");
    Serial.print(fanOffTemperature);
    Serial.print(" C / ");
    Serial.print(fanOffHumidity);
    Serial.println(" %");
}

void setup()
{
    Serial.begin(115200);

    pinMode(RELAY_PIN, OUTPUT);
    pinMode(BUZZER_PIN, OUTPUT);
    pinMode(LDR_PIN, INPUT);

    setFan(false);
    noTone(BUZZER_PIN);

    analogReadResolution(12);

    dht.setup(DHT_PIN, DHTesp::DHT22);

    Wire.begin(OLED_SDA, OLED_SCL);

    if (!display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDRESS)) {
        Serial.println("OLED initialization failed!");

        while (true)
            delay(1000);
    }

    display.clearDisplay();
    display.setTextColor(SSD1306_WHITE);
    display.setTextSize(1);

    display.setCursor(0, 0);
    display.println("Museum Storage");

    display.setCursor(0, 16);
    display.println("System starting...");

    display.display();

    Serial.println("Museum Storage System");
    Serial.println("Initialization complete");

    delay(1500);
}

void loop()
{
    TempAndHumidity data = dht.getTempAndHumidity();

    float temperature = data.temperature;
    float humidity = data.humidity;

    if (isnan(temperature) || isnan(humidity)) {
        Serial.println("DHT22 read failed!");
        delay(2000);
        return;
    }

    float lux = readLux();

    updateFan(temperature, humidity);
    updateAlarm(temperature, humidity);
    updateDisplay(temperature, humidity, lux);
    printStatus(temperature, humidity, lux);

    delay(2000);
}