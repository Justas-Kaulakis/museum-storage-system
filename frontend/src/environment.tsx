import { useState } from "react";
import type { FormEvent } from "react";
import { apiFetch, errorMessage, jsonRequest } from "./api";
import { ErrorNotice, formatTimestamp, LoadNotice, useApiData } from "./shared";
import type { EnvironmentSettings, SensorReading } from "./types";

const settingsPath = "/environment-settings";
const measurementFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 });
type ThresholdField = keyof EnvironmentSettings;
const thresholdRows: { label: string; temperature: ThresholdField; humidity: ThresholdField }[] = [
  { label: "Ventiliatoriaus įjungimas", temperature: "fanOnTemperature", humidity: "fanOnHumidity" },
  { label: "Ventiliatoriaus išjungimas", temperature: "fanOffTemperature", humidity: "fanOffHumidity" },
  { label: "Aliarmas", temperature: "temperatureAlarm", humidity: "humidityAlarm" },
];
const thresholdLabels: Record<ThresholdField, string> = {
  fanOnTemperature: "Ventiliatoriaus įjungimo temperatūra",
  fanOffTemperature: "Ventiliatoriaus išjungimo temperatūra",
  fanOnHumidity: "Ventiliatoriaus įjungimo drėgmė",
  fanOffHumidity: "Ventiliatoriaus išjungimo drėgmė",
  temperatureAlarm: "Temperatūros aliarmo riba",
  humidityAlarm: "Drėgmės aliarmo riba",
};

async function loadEnvironment(signal: AbortSignal) {
  const [readings, settings] = await Promise.all([
    apiFetch<SensorReading[]>("/sensor-readings", { signal }),
    apiFetch<EnvironmentSettings>(settingsPath, { signal }),
  ]);
  return { reading: readings[0], settings };
}

export function EnvironmentPage() {
  const { data, setData, loading, error, refresh } = useApiData(loadEnvironment);
  const [edits, setEdits] = useState<{ [field: string]: string }>({});
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [saveError, setSaveError] = useState("");
  const reading = data?.reading;
  const settings = data?.settings;
  const measurements = [
    { label: "Temperatūra", value: reading?.temperature, unit: "°C" },
    { label: "Drėgmė", value: reading?.humidity, unit: "%" },
    { label: "Apšvietimas", value: reading?.lightLevel, unit: "lx" },
  ];

  function updateField(field: ThresholdField, value: string) {
    setEdits((previous) => ({ ...previous, [field]: value }));
    setSaved(false);
    setSaveError("");
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!data || saving || loading) return;

    const form = new FormData(event.currentTarget);
    const thresholds: EnvironmentSettings = {
      fanOnTemperature: Number(form.get("fanOnTemperature")),
      fanOffTemperature: Number(form.get("fanOffTemperature")),
      fanOnHumidity: Number(form.get("fanOnHumidity")),
      fanOffHumidity: Number(form.get("fanOffHumidity")),
      temperatureAlarm: Number(form.get("temperatureAlarm")),
      humidityAlarm: Number(form.get("humidityAlarm")),
    };

    setSaving(true);
    setSaved(false);
    setSaveError("");
    try {
      const persisted = await apiFetch<EnvironmentSettings>(settingsPath, jsonRequest("PUT", thresholds));
      setData({ ...data, settings: persisted });
      setEdits({});
      setSaved(true);
    } catch (failure) {
      setSaveError(errorMessage(failure, "Nepavyko išsaugoti ribinių reikšmių."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <header className="page-heading">
        <h1>Aplinkos stebėjimas</h1>
        <button className="button" onClick={refresh} disabled={loading || saving}>Atnaujinti</button>
      </header>
      <LoadNotice error={error} loading={loading} retry={refresh} />

      <section className="panel" aria-labelledby="current-heading">
        <div className="panel-heading">
          <h2 id="current-heading">Dabartiniai rodmenys</h2>
          {reading && <p className="report-time">Paskutinis matavimas <time dateTime={reading.createdAt}>{formatTimestamp(reading.createdAt)}</time></p>}
        </div>
        {!reading && data && <p className="state-text panel-message">Laukiama pirmųjų jutiklių rodmenų.</p>}
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
            <dt>Ventiliatorius</dt>
            <dd>
              {reading ? <span className={reading.fanEnabled ? "badge badge-active" : "badge badge-neutral"}>{reading.fanEnabled ? "Įjungtas" : "Išjungtas"}</span> : "—"}
            </dd>
          </div>
          <div>
            <dt>Aliarmas</dt>
            <dd>
              {reading ? <span className={reading.alarmEnabled ? "badge badge-alarm" : "badge badge-good"}>{reading.alarmEnabled ? "Suveikęs" : "Normali būsena"}</span> : "—"}
            </dd>
          </div>
          {reading && <div className="device-id"><dt>Įrenginys</dt><dd className="code">{reading.deviceId}</dd></div>}
        </dl>
      </section>

      <section className="panel" aria-labelledby="thresholds-heading">
        <div className="panel-heading">
          <h2 id="thresholds-heading">Ribinės reikšmės</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead><tr><th scope="col">Ribinė reikšmė</th><th scope="col">Temperatūra (°C)</th><th scope="col">Drėgmė (%)</th></tr></thead>
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
      {settings && (
        <section className="panel" aria-labelledby="threshold-form-heading">
          <div className="panel-heading"><h2 id="threshold-form-heading">Aplinkos nustatymai</h2></div>
          <form className="form-body" onSubmit={save}>
            {saveError && <ErrorNotice message={saveError} />}
            <fieldset disabled={saving || loading}>
              {thresholdRows.map(({ label, temperature, humidity }) => (
                <div key={label} className="threshold-row">
                  <h3>{label}</h3>
                  <label>
                    {thresholdLabels[temperature]} (°C)
                    <input name={temperature} aria-label={`${thresholdLabels[temperature]} (°C)`} type="number" step="any" required value={edits[temperature] ?? settings[temperature]} onChange={(event) => updateField(temperature, event.target.value)} />
                  </label>
                  <label>
                    {thresholdLabels[humidity]} (%)
                    <input name={humidity} aria-label={`${thresholdLabels[humidity]} (%)`} type="number" step="any" required value={edits[humidity] ?? settings[humidity]} onChange={(event) => updateField(humidity, event.target.value)} />
                  </label>
                </div>
              ))}
              <div className="form-actions">
                <button className="button primary" type="submit">{saving ? "Išsaugoma…" : "Išsaugoti"}</button>
                {saved && <span className="success-text" role="status">Ribinės reikšmės išsaugotos.</span>}
              </div>
            </fieldset>
          </form>
        </section>
      )}
    </>
  );
}
