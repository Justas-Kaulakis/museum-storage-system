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

export const thresholdRows = [
  { label: "Fan on", temperature: "fanOnTemperature", humidity: "fanOnHumidity" },
  { label: "Fan off", temperature: "fanOffTemperature", humidity: "fanOffHumidity" },
  { label: "Alarm", temperature: "temperatureAlarm", humidity: "humidityAlarm" },
] as const;

type ThresholdRow = (typeof thresholdRows)[number];
export type Threshold = ThresholdRow["temperature" | "humidity"];
export type EnvironmentSettings = Record<Threshold, number>;

export interface StorageLocation {
  id: number;
  code: string;
  name: string;
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
