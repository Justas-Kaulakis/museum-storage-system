export interface SensorReading {
  id: number;
  deviceId: string;
  temperature: number;
  humidity: number;
  lightLevel: number;
  coolingEnabled: boolean;
  heatingEnabled: boolean;
  humidifierEnabled: boolean;
  dehumidifierEnabled: boolean;
  lightIntensity: number;
  alarmEnabled: boolean;
  createdAt: string;
}

export interface EnvironmentSettings {
  coolingOnTemperature: number;
  coolingOffTemperature: number;
  heatingOnTemperature: number;
  heatingOffTemperature: number;
  humidifierOnHumidity: number;
  humidifierOffHumidity: number;
  dehumidifierOnHumidity: number;
  dehumidifierOffHumidity: number;
  minLightLevel: number;
  maxLightLevel: number;
  temperatureAlarm: number;
  humidityAlarm: number;
}

export interface StorageZone {
  id: number;
  code: string;
  name: string;
  deviceId: string | null;
}

export interface StorageLocation {
  id: number;
  code: string;
  name: string;
  storageZoneId: number;
  storageZoneCode: string;
  storageZoneName: string;
}

export type ExhibitStatus = "Stored" | "CheckedOut";

export interface ExhibitInput {
  code: string;
  name: string;
  category: string | null;
  description: string | null;
  status: ExhibitStatus;
  storageLocationId: number | null;
  minTemperature: number | null;
  maxTemperature: number | null;
  minHumidity: number | null;
  maxHumidity: number | null;
  minLightLevel: number | null;
  maxLightLevel: number | null;
}

export interface StorageCondition {
  value: number | null;
  min: number | null;
  max: number | null;
  status: "WithinRange" | "BelowMinimum" | "AboveMaximum" | "NotConfigured" | "Unknown";
}

export interface StorageSuitability {
  status: "Suitable" | "Unsuitable" | "NoDevice" | "NoReading" | "NotApplicable";
  readingAt: string | null;
  temperature: StorageCondition;
  humidity: StorageCondition;
  lightLevel: StorageCondition;
}

export interface Exhibit extends ExhibitInput {
  id: number;
  storageLocation: StorageLocation | null;
}

export interface StorageOperation {
  id: number;
  type: "CheckIn" | "CheckOut" | "Move";
  fromStorageLocationId: number | null;
  toStorageLocationId: number | null;
  fromStorageLocation: StorageLocation | null;
  toStorageLocation: StorageLocation | null;
  createdAt: string;
}
