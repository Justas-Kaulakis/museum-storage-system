import { useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router";
import { apiFetch, errorMessage, jsonRequest } from "./api";
import { Badge, ErrorNotice, LoadNotice, Timestamp, useApiData } from "./shared";
import { thresholdRows } from "./types";
import type { EnvironmentSettings, SensorReading, Threshold } from "./types";

const settingsPath = "/environment-settings";
const loadSettings = (signal: AbortSignal) => apiFetch<EnvironmentSettings>(settingsPath, { signal });
const loadDashboard = (signal: AbortSignal) => Promise.all([
  apiFetch<SensorReading[]>("/sensor-readings", { signal }),
  loadSettings(signal),
]);
const measurementFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });

export function Dashboard() {
  const { data, loading, error, refresh } = useApiData(loadDashboard);
  const reading = data?.[0][0];
  const settings = data?.[1];
  const measurements = [
    { label: "Temperature", value: reading?.temperature, unit: "°C" },
    { label: "Humidity", value: reading?.humidity, unit: "%" },
    { label: "Light level", value: reading?.lightLevel, unit: "lx" },
  ];

  return (
    <>
      <header className="page-heading">
        <h1>Dashboard</h1>
        <button className="button" onClick={refresh} disabled={loading}>Refresh</button>
      </header>
      <LoadNotice error={error} loading={loading} retry={refresh} />

      <section className="panel" aria-labelledby="current-heading">
        <div className="panel-heading">
          <h2 id="current-heading">Current environment</h2>
          {reading && <p className="report-time">Last reading <Timestamp value={reading.createdAt} /></p>}
        </div>
        {!reading && data && <p className="state-text panel-message">Waiting for the first sensor reading.</p>}
        <dl className="measurement-grid">
          {measurements.map(({ label, value, unit }) => (
            <div key={label} className="measurement">
              <dt>{label}</dt>
              <dd>{value === undefined ? "—" : measurementFormat.format(value)} <span>{unit}</span></dd>
            </div>
          ))}
        </dl>
        <dl className="device-states">
          <div>
            <dt>Fan</dt>
            <dd>{reading ? <Badge label={reading.fanEnabled ? "On" : "Off"} tone={reading.fanEnabled ? "active" : "neutral"} /> : "—"}</dd>
          </div>
          <div>
            <dt>Alarm</dt>
            <dd>{reading ? <Badge label={reading.alarmEnabled ? "Active" : "Normal"} tone={reading.alarmEnabled ? "alarm" : "good"} /> : "—"}</dd>
          </div>
          {reading && <div className="device-id"><dt>Device</dt><dd className="code">{reading.deviceId}</dd></div>}
        </dl>
      </section>

      <section className="panel" aria-labelledby="thresholds-heading">
        <div className="panel-heading">
          <h2 id="thresholds-heading">Environmental thresholds</h2>
          <Link className="button" to="/environment-settings">Edit thresholds</Link>
        </div>
        <div className="table-wrap">
          <table>
            <thead><tr><th scope="col">Threshold</th><th scope="col">Temperature (°C)</th><th scope="col">Humidity (%)</th></tr></thead>
            <tbody>
              {thresholdRows.map(({ label, temperature, humidity }) => (
                <tr key={label}>
                  <th scope="row">{label}</th>
                  <td className="numeric">{settings?.[temperature] ?? "—"}</td>
                  <td className="numeric">{settings?.[humidity] ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}

type SettingsDraft = Record<Threshold, string>;
const thresholdFields = thresholdRows.flatMap(({ temperature, humidity }) => [temperature, humidity]);

function toDraft(settings: EnvironmentSettings) {
  return Object.fromEntries(thresholdFields.map((field) => [field, String(settings[field])])) as SettingsDraft;
}

export function EnvironmentSettingsPage() {
  const { data, loading, error, refresh } = useApiData(loadSettings);
  const [draft, setDraft] = useState<SettingsDraft | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [saveError, setSaveError] = useState("");
  const fields = draft ?? (data && toDraft(data));

  function updateField(field: Threshold, value: string) {
    if (!fields) return;
    setDraft({ ...fields, [field]: value });
    setSaved(false);
    setSaveError("");
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!fields || saving) return;
    setSaving(true);
    setSaved(false);
    setSaveError("");
    const settings = Object.fromEntries(thresholdFields.map((field) => [field, Number(fields[field])]));
    try {
      const persisted = await apiFetch<EnvironmentSettings>(settingsPath, jsonRequest("PUT", settings));
      setDraft(toDraft(persisted));
      setSaved(true);
    } catch (failure) {
      setSaveError(errorMessage(failure, "Could not save the thresholds."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <header className="page-heading"><h1>Environment settings</h1></header>
      <LoadNotice error={error} loading={loading} retry={refresh} />
      {fields && (
        <section className="panel" aria-labelledby="threshold-form-heading">
          <div className="panel-heading"><h2 id="threshold-form-heading">Control thresholds</h2></div>
          <form className="form-body" onSubmit={save}>
            {saveError && <ErrorNotice message={saveError} />}
            <fieldset disabled={saving} className="threshold-form">
              {thresholdRows.map(({ label, temperature, humidity }) => (
                <div key={label} className="threshold-row">
                  <h3>{label}</h3>
                  <label>
                    Temperature (°C)
                    <input aria-label={`${label} temperature (°C)`} type="number" step="any" required value={fields[temperature]} onChange={(event) => updateField(temperature, event.target.value)} />
                  </label>
                  <label>
                    Humidity (%)
                    <input aria-label={`${label} humidity (%)`} type="number" step="any" required value={fields[humidity]} onChange={(event) => updateField(humidity, event.target.value)} />
                  </label>
                </div>
              ))}
              <div className="form-actions">
                <button className="button primary" type="submit">{saving ? "Saving..." : "Save thresholds"}</button>
                {saved && <span className="success-text" role="status">Thresholds saved.</span>}
              </div>
            </fieldset>
          </form>
        </section>
      )}
    </>
  );
}
