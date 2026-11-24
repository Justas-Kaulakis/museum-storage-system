import { useCallback, useState } from "react";
import type { FormEvent } from "react";
import { apiFetch, errorMessage, jsonRequest } from "./api";
import { ErrorNotice, formatTimestamp, LoadNotice, useApiData } from "./shared";
import { ZoneOptions, zonesPath } from "./storage";
import type { EnvironmentSettings, SensorReading, StorageZone } from "./types";

const measurementFormat = new Intl.NumberFormat("lt-LT", { maximumFractionDigits: 1 });
type ThresholdField = keyof EnvironmentSettings;
const thresholdGroups: { label: string; unit: string; fields: [ThresholdField, string][] }[] = [
  { label: "Temperatūra", unit: "°C", fields: [
    ["heatingOnTemperature", "Šildymas įjungiamas"],
    ["heatingOffTemperature", "Šildymas išjungiamas"],
    ["coolingOnTemperature", "Vėsinimas įjungiamas"],
    ["coolingOffTemperature", "Vėsinimas išjungiamas"],
    ["temperatureAlarm", "Temperatūros aliarmo riba"],
  ] },
  { label: "Drėgmė", unit: "%", fields: [
    ["humidifierOnHumidity", "Drėkintuvas įjungiamas"],
    ["humidifierOffHumidity", "Drėkintuvas išjungiamas"],
    ["dehumidifierOnHumidity", "Sausintuvas įjungiamas"],
    ["dehumidifierOffHumidity", "Sausintuvas išjungiamas"],
    ["humidityAlarm", "Drėgmės aliarmo riba"],
  ] },
  { label: "Apšvietimas", unit: "lx", fields: [
    ["minLightLevel", "Minimalus apšvietimas"],
    ["maxLightLevel", "Maksimalus apšvietimas"],
  ] },
];

function loadZones(signal: AbortSignal) {
  return apiFetch<StorageZone[]>(zonesPath, { signal });
}

export function EnvironmentPage() {
  const { data: zones, loading, error, refresh } = useApiData(loadZones);
  const [zoneId, setZoneId] = useState<number | null>(null);
  const zone = zones?.find((item) => item.id === zoneId) ?? zones?.[0];

  return (
    <>
      <header className="page-heading"><h1>Aplinkos stebėjimas</h1></header>
      <LoadNotice error={error} loading={loading && !zones} retry={refresh} />
      {zones?.length === 0 && <p className="state-text">Saugojimo zonų dar nėra. Zoną galite sukurti eksponatų registre.</p>}
      {zone && zones && (
        // pakeitus zoną, formą pradedam iš naujo, kad neliktų senų reikšmių
        <ZoneEnvironment
          key={`${zone.id}:${zone.deviceId ?? ""}`}
          zone={zone} zones={zones} selectZone={setZoneId} refreshZones={refresh} loadingZones={loading}
        />
      )}
    </>
  );
}

function ZoneEnvironment({ zone, zones, selectZone, refreshZones, loadingZones }: {
  zone: StorageZone;
  zones: StorageZone[];
  selectZone: (id: number) => void;
  refreshZones: () => void;
  loadingZones: boolean;
}) {
  const settingsPath = `${zonesPath}/${zone.id}/environment-settings`;
  const deviceId = zone.deviceId;
  const loadEnvironment = useCallback(async (signal: AbortSignal) => {
    const [settings, readings] = await Promise.all([
      apiFetch<EnvironmentSettings>(settingsPath, { signal }),
      deviceId ? apiFetch<SensorReading[]>(`/sensor-readings?${new URLSearchParams({ deviceId })}`, { signal }) : [],
    ]);
    return { reading: readings[0], settings };
  }, [settingsPath, deviceId]);
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
  const actuators = [
    { label: "Vėsinimas", enabled: reading?.coolingEnabled },
    { label: "Šildymas", enabled: reading?.heatingEnabled },
    { label: "Drėkintuvas", enabled: reading?.humidifierEnabled },
    { label: "Sausintuvas", enabled: reading?.dehumidifierEnabled },
  ];

  function updateField(field: ThresholdField, value: string) {
    setEdits((previous) => ({ ...previous, [field]: value }));
    setSaved(false);
    setSaveError("");
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!data || saving || loading || loadingZones || error) return;

    const form = new FormData(event.currentTarget);
    const thresholds: EnvironmentSettings = {
      coolingOnTemperature: Number(form.get("coolingOnTemperature")),
      coolingOffTemperature: Number(form.get("coolingOffTemperature")),
      heatingOnTemperature: Number(form.get("heatingOnTemperature")),
      heatingOffTemperature: Number(form.get("heatingOffTemperature")),
      humidifierOnHumidity: Number(form.get("humidifierOnHumidity")),
      humidifierOffHumidity: Number(form.get("humidifierOffHumidity")),
      dehumidifierOnHumidity: Number(form.get("dehumidifierOnHumidity")),
      dehumidifierOffHumidity: Number(form.get("dehumidifierOffHumidity")),
      minLightLevel: Number(form.get("minLightLevel")),
      maxLightLevel: Number(form.get("maxLightLevel")),
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
      <div className="inline-form">
        <label>Saugojimo zona
          <select value={zone.id} onChange={(event) => selectZone(Number(event.target.value))} disabled={saving}>
            <ZoneOptions zones={zones} />
          </select>
        </label>
        <button className="button" onClick={() => { refreshZones(); refresh(); }} disabled={loading || loadingZones || saving}>Atnaujinti</button>
      </div>
      <LoadNotice error={error} loading={loading} retry={refresh} />

      <section className="panel" aria-labelledby="current-heading">
        <div className="panel-heading">
          <h2 id="current-heading">Dabartiniai rodmenys</h2>
          {reading && <p className="report-time">Paskutinis matavimas <time dateTime={reading.createdAt}>{formatTimestamp(reading.createdAt)}</time></p>}
        </div>
        {!deviceId && <p className="state-text panel-message">Šiai zonai nepriskirtas aplinkos stebėjimo įrenginys.</p>}
        {deviceId && !reading && data && !loading && !error && <p className="state-text panel-message">Šiai zonai dar nėra aplinkos matavimo duomenų.</p>}
        <dl className="measurement-grid">
          {measurements.map(({ label, value, unit }) => (
            <div key={label} className="measurement">
              <dt>{label}</dt>
              <dd>{value === undefined ? "—" : measurementFormat.format(value)} <span>{unit}</span></dd>
            </div>
          ))}
        </dl>
        <dl className="device-states">
          {actuators.map(({ label, enabled }) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>{enabled === undefined ? "—" : (
                <span className={enabled ? "badge badge-active" : "badge badge-neutral"}>{enabled ? "Įjungtas" : "Išjungtas"}</span>
              )}</dd>
            </div>
          ))}
          <div><dt>Apšvietimo galia</dt><dd>{reading ? `${reading.lightIntensity} %` : "—"}</dd></div>
          <div>
            <dt>Aliarmas</dt>
            <dd>
              {reading ? <span className={reading.alarmEnabled ? "badge badge-alarm" : "badge badge-good"}>{reading.alarmEnabled ? "Suveikęs" : "Normali būsena"}</span> : "—"}
            </dd>
          </div>
          {reading && <div className="device-id"><dt>Įrenginys</dt><dd className="code">{reading.deviceId}</dd></div>}
        </dl>
      </section>

      {settings && (
        <section className="panel" aria-labelledby="threshold-form-heading">
          <div className="panel-heading"><h2 id="threshold-form-heading">Aplinkos nustatymai</h2></div>
          <form className="form-body" onSubmit={save}>
            {saveError && <ErrorNotice message={saveError} />}
            <fieldset disabled={saving || loading || loadingZones || Boolean(error)}>
              {thresholdGroups.map(({ label, unit, fields }) => (
                <div key={label} className="setting-group">
                  <h3>{label}</h3>
                  <div className="field-grid">
                    {fields.map(([field, name]) => (
                      <label key={field}>
                        {name} ({unit})
                        <input name={field} type="number" step="any" required value={edits[field] ?? settings[field]} onChange={(event) => updateField(field, event.target.value)} />
                      </label>
                    ))}
                  </div>
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
