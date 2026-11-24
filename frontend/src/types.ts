export interface SensorReading {
  id: number;
  deviceId: string;
  temperature: number;
  humidity: number;
  lightLevel: number;
  fanEnabled: boolean;
  alarmEnabled: boolean;
  createdAt: string;
}

export interface EnvironmentSettings {
  fanOnTemperature: number;
  fanOffTemperature: number;
  fanOnHumidity: number;
  fanOffHumidity: number;
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
