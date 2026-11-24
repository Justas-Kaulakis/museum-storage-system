import { Fragment, useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router";
import { ApiError, apiFetch, errorMessage, jsonRequest } from "./api";
import { ErrorNotice, formatTimestamp, LoadNotice, useApiData } from "./shared";
import { locationsPath, StorageSection, ZoneOptions, zonesPath } from "./storage";
import type { Exhibit, ExhibitInput, ExhibitStatus, StorageCondition, StorageLocation, StorageOperation, StorageSuitability, StorageZone } from "./types";

const exhibitsPath = "/exhibits";
const storageLocationsLink = "/exhibits#storage-locations";
const exhibitFilterNames = ["search", "category", "status", "zoneId", "storageLocationId"] as const;

async function loadRegistry(signal: AbortSignal) {
  const [zones, locations] = await Promise.all([
    apiFetch<StorageZone[]>(zonesPath, { signal }),
    apiFetch<StorageLocation[]>(locationsPath, { signal }),
  ]);
  return { zones, locations };
}

export function RegistryPage() {
  const { data, loading, error, refresh } = useApiData(loadRegistry);
  const [panel, setPanel] = useState<"create" | "scan" | null>(null);
  const [revision, setRevision] = useState(0);
  const [filters, setFilters] = useSearchParams();
  const { hash, key } = useLocation();
  const query = filters.toString();

  useEffect(() => {
    if (hash === "#storage-locations" || hash === "#exhibits-heading") {
      document.getElementById(hash.slice(1))?.scrollIntoView();
    }
  }, [hash, key, loading]);

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const fields = new FormData(event.currentTarget);
    const next = new URLSearchParams();
    for (const name of exhibitFilterNames) {
      const value = String(fields.get(name) ?? "").trim();
      if (value) next.set(name, value);
    }
    setFilters(next);
  }

  function shelfLink(location: StorageLocation) {
    const next = new URLSearchParams(filters);
    next.set("storageLocationId", String(location.id));
    if (next.get("status") === "CheckedOut") next.delete("status");
    if (next.has("zoneId") && next.get("zoneId") !== String(location.storageZoneId)) next.delete("zoneId");
    return `${exhibitsPath}?${next}#exhibits-heading`;
  }

  function clearLocation() {
    const next = new URLSearchParams(filters);
    next.delete("storageLocationId");
    setFilters(next);
  }

  function refreshRegistry() {
    refresh();
    setRevision((previous) => previous + 1);
  }

  return (
    <>
      <header className="page-heading">
        <h1>Eksponatų registras</h1>
        <div className="button-group">
          <button className="button" aria-expanded={panel === "scan"} onClick={() => setPanel(panel === "scan" ? null : "scan")}>Nuskaityti QR kodą</button>
          <button className="button primary" aria-expanded={panel === "create"} disabled={!data}
            onClick={() => setPanel(panel === "create" ? null : "create")}>Naujas eksponatas</button>
        </div>
      </header>
      <LoadNotice error={error} loading={loading} retry={refreshRegistry} />
      {panel === "create" && data && <ExhibitForm locations={data.locations} onCancel={() => setPanel(null)} />}
      {panel === "scan" && <ScanQrForm />}

      <section className="panel" aria-labelledby="exhibits-heading">
        <div className="panel-heading"><h2 id="exhibits-heading">Eksponatai</h2></div>
        <form key={query} className="form-body" onSubmit={applyFilters}>
          <div className="field-grid">
            <label>Paieška pagal kodą arba pavadinimą<input type="search" name="search" defaultValue={filters.get("search") ?? ""} /></label>
            <label>Kategorija<input type="search" name="category" defaultValue={filters.get("category") ?? ""} /></label>
            <label>Būsena
              <select name="status" defaultValue={filters.get("status") ?? ""}>
                <option value="">Visos būsenos</option>
                <option value="Stored">Saugykloje</option>
                <option value="CheckedOut">Paimtas iš saugyklos</option>
              </select>
            </label>
            <label>Saugojimo zona
              <select name="zoneId" defaultValue={filters.get("zoneId") ?? ""} key={data ? "loaded" : "loading"}>
                <option value="">Visos zonos</option>
                <ZoneOptions zones={data?.zones ?? []} />
              </select>
            </label>
            <label>Saugojimo vieta
              <select name="storageLocationId" defaultValue={filters.get("storageLocationId") ?? ""} key={data ? "loaded" : "loading"}>
                <option value="">Visos saugojimo vietos</option>
                <LocationOptions locations={data?.locations ?? []} />
              </select>
            </label>
          </div>
          <div className="form-actions">
            <button className="button primary" type="submit">Filtruoti</button>
            <button className="button" type="button" onClick={(event) => { event.currentTarget.form?.reset(); setFilters({}); }}>Išvalyti filtrus</button>
            {filters.has("storageLocationId") && <button className="button" type="button" onClick={clearLocation}>Pašalinti saugojimo vietos filtrą</button>}
          </div>
        </form>
        <ExhibitResults key={`${revision}:${query}`} query={query} />
      </section>
      <StorageSection items={data?.zones} onChanged={refreshRegistry} />
      <StorageSection items={data?.locations} zones={data?.zones ?? []} onChanged={refreshRegistry} locationLink={shelfLink} />
    </>
  );
}

function ExhibitResults({ query }: { query: string }) {
  const load = useCallback((signal: AbortSignal) =>
    apiFetch<Exhibit[]>(query ? `${exhibitsPath}?${query}` : exhibitsPath, { signal }), [query]);
  const { data: exhibits, loading, error, refresh } = useApiData(load);

  if (!exhibits) return <div className="form-body"><LoadNotice error={error} loading={loading} retry={refresh} /></div>;

  return (
    <div className="table-wrap">
      <table className="exhibit-table">
        <thead><tr><th scope="col">Kodas</th><th scope="col">Pavadinimas</th><th scope="col">Kategorija</th><th scope="col">Būsena</th><th scope="col">Saugojimo vieta</th></tr></thead>
        <tbody>
          {exhibits.map((exhibit) => (
            <tr key={exhibit.id}>
              <th scope="row"><Link className="code table-link" to={`/exhibits/${exhibit.id}`}>{exhibit.code}</Link></th>
              <td>{exhibit.name}</td>
              <td>{exhibit.category || "—"}</td>
              <td><StatusBadge status={exhibit.status} /></td>
              <td><LocationCell location={exhibit.storageLocation} id={exhibit.storageLocationId} /></td>
            </tr>
          ))}
          {exhibits.length === 0 && <tr><td className="empty-cell" colSpan={5}>Eksponatų nerasta.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}

const conditionFields = [
  { key: "temperature", name: "Temperatūra", min: "minTemperature", max: "maxTemperature", unit: "°C", low: "Per žema", high: "Per aukšta" },
  { key: "humidity", name: "Drėgmė", min: "minHumidity", max: "maxHumidity", unit: "%", low: "Per maža", high: "Per didelė" },
  { key: "lightLevel", name: "Apšvietimas", min: "minLightLevel", max: "maxLightLevel", unit: "lx", low: "Per mažas", high: "Per didelis" },
] as const;

function optionalNumber(value: FormDataEntryValue | null) {
  return value === null || value === "" ? null : Number(value);
}

function ExhibitForm({ locations, exhibit, onSaved, onCancel }: {
  locations: StorageLocation[];
  exhibit?: Exhibit;
  onSaved?: () => void;
  onCancel: () => void;
}) {
  const navigate = useNavigate();
  const [status, setStatus] = useState<ExhibitStatus>(exhibit?.status ?? "CheckedOut");
  const [location, setLocation] = useState(String(exhibit?.storageLocationId ?? ""));
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const needsLocation = status === "Stored" && locations.length === 0;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || needsLocation) return;
    const fields = new FormData(event.currentTarget);
    const input: ExhibitInput = {
      code: String(fields.get("code") ?? ""),
      name: String(fields.get("name") ?? ""),
      category: String(fields.get("category") ?? "").trim() || null,
      description: String(fields.get("description") ?? "").trim() || null,
      status,
      storageLocationId: status === "Stored" ? Number(location) : null,
      minTemperature: optionalNumber(fields.get("minTemperature")),
      maxTemperature: optionalNumber(fields.get("maxTemperature")),
      minHumidity: optionalNumber(fields.get("minHumidity")),
      maxHumidity: optionalNumber(fields.get("maxHumidity")),
      minLightLevel: optionalNumber(fields.get("minLightLevel")),
      maxLightLevel: optionalNumber(fields.get("maxLightLevel")),
    };
    setSaving(true);
    setError("");
    try {
      const saved = await apiFetch<Exhibit>(exhibit ? `${exhibitsPath}/${exhibit.id}` : exhibitsPath, jsonRequest(exhibit ? "PUT" : "POST", input));
      if (exhibit) onSaved?.();
      else navigate(`/exhibits/${saved.id}`);
    } catch (failure) {
      setError(errorMessage(failure, "Nepavyko išsaugoti eksponato."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="exhibit-form-heading">
      <div className="panel-heading"><h2 id="exhibit-form-heading">{exhibit ? "Redaguoti eksponatą" : "Naujas eksponatas"}</h2></div>
      <form className="form-body" onSubmit={submit}>
        {error && <ErrorNotice message={error} />}
        <fieldset disabled={saving}>
          <div className="field-grid">
            <label>Kodas<input name="code" placeholder="EXHIBIT-0001" defaultValue={exhibit?.code} required /></label>
            <label>Pavadinimas<input name="name" defaultValue={exhibit?.name} required /></label>
            <label>Kategorija<input name="category" defaultValue={exhibit?.category ?? ""} /></label>
            <label>
              Būsena
              <select value={status} onChange={(event) => setStatus(event.target.value as ExhibitStatus)}>
                <option value="CheckedOut">Paimtas iš saugyklos</option>
                <option value="Stored">Saugykloje</option>
              </select>
            </label>
            {status === "Stored" && (
              <label>
                Saugojimo vieta
                <LocationSelect value={location} locations={locations} onChange={setLocation} />
              </label>
            )}
            <label className="full-width">Aprašymas<textarea name="description" rows={3} defaultValue={exhibit?.description ?? ""} /></label>
          </div>
          {needsLocation && <p className="state-text"><Link to={storageLocationsLink}>Sukurkite saugojimo vietą</Link>, kad galėtumėte užregistruoti eksponatą kaip esantį saugykloje.</p>}
          <fieldset className="condition-fields">
            <legend>Tinkamos laikymo sąlygos</legend>
            <div className="field-grid">
              {conditionFields.map(({ name, min, max, unit }) => (
                <Fragment key={min}>
                  <label>{name} – nuo ({unit})<input name={min} type="number" step="any" defaultValue={exhibit?.[min] ?? ""} /></label>
                  <label>{name} – iki ({unit})<input name={max} type="number" step="any" defaultValue={exhibit?.[max] ?? ""} /></label>
                </Fragment>
              ))}
            </div>
          </fieldset>
          <div className="form-actions">
            <button className="button primary" disabled={needsLocation} type="submit">{saving ? "Išsaugoma…" : exhibit ? "Išsaugoti" : "Sukurti"}</button>
            <button className="button" type="button" onClick={onCancel}>Atšaukti</button>
          </div>
        </fieldset>
      </form>
    </section>
  );
}

function ScanQrForm() {
  const navigate = useNavigate();
  const [error, setError] = useState("");
  const [scanning, setScanning] = useState(false);

  async function scan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (scanning) return;
    const body = new FormData(event.currentTarget);
    setScanning(true);
    setError("");
    try {
      const exhibit = await apiFetch<Exhibit>(`${exhibitsPath}/scan-qr`, { method: "POST", body });
      navigate(`/exhibits/${exhibit.id}`);
    } catch (failure) {
      setError(failure instanceof ApiError && failure.status === 404
        ? "Eksponatas pagal nuotraukoje esantį QR kodą nerastas."
        : errorMessage(failure, "Nepavyko nuskaityti QR kodo iš nuotraukos."));
    } finally {
      setScanning(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="scan-heading">
      <div className="panel-heading"><h2 id="scan-heading">Įkelti QR kodo nuotrauką</h2></div>
      <form className="form-body" onSubmit={scan}>
        {error && <ErrorNotice message={error} />}
        <fieldset className="inline-form" disabled={scanning}>
          <label>Pasirinkti nuotrauką (PNG arba JPEG)<input type="file" name="file" accept="image/png,image/jpeg" required /></label>
          <button className="button primary" type="submit">{scanning ? "Nuskaitoma…" : "Rasti eksponatą"}</button>
        </fieldset>
      </form>
    </section>
  );
}

const actions = {
  "check-in": { label: "Padėti į saugyklą", success: "Eksponatas padėtas į saugyklą." },
  "check-out": { label: "Paimti iš saugyklos", success: "Eksponatas paimtas iš saugyklos." },
  move: { label: "Perkelti", success: "Eksponatas perkeltas." },
} as const;
type ExhibitAction = keyof typeof actions;
const operationLabels = { CheckIn: "Padėti į saugyklą", CheckOut: "Paimti iš saugyklos", Move: "Perkelti" };

export function ExhibitDetail({ id }: { id: string }) {
  const navigate = useNavigate();
  const path = `${exhibitsPath}/${id}`;
  const load = useCallback((signal: AbortSignal) => Promise.all([
    apiFetch<Exhibit>(path, { signal }),
    apiFetch<StorageOperation[]>(`${path}/operations`, { signal }),
    apiFetch<StorageLocation[]>(locationsPath, { signal }),
    apiFetch<StorageSuitability>(`${path}/storage-suitability`, { signal }),
  ]), [path]);
  const { data, error, loading, refresh } = useApiData(load);
  const [location, setLocation] = useState("");
  const [working, setWorking] = useState<ExhibitAction | null>(null);
  const [actionError, setActionError] = useState("");
  const [notice, setNotice] = useState("");
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [detailError, setDetailError] = useState("");
  const [editNotice, setEditNotice] = useState("");
  const exhibit = data?.[0];
  const isStored = exhibit?.status === "Stored";
  const locations = (data?.[2] ?? []).filter((item) => item.id !== exhibit?.storageLocationId);
  const assignmentAction = isStored ? "move" : "check-in";
  const missing = error instanceof ApiError && error.status === 404;
  const busy = loading || Boolean(working) || deleting || editing;

  async function remove() {
    if (!exhibit || busy || !window.confirm(`Ar pašalinti eksponatą „${exhibit.name}“ ir jo operacijų istoriją?`)) return;
    setDeleting(true);
    setDetailError("");
    setEditNotice("");
    try {
      await apiFetch<void>(path, { method: "DELETE" });
      navigate(exhibitsPath, { replace: true });
    } catch (failure) {
      setDetailError(errorMessage(failure, "Nepavyko pašalinti eksponato."));
    } finally {
      setDeleting(false);
    }
  }

  function saved() {
    setEditing(false);
    setEditNotice("Eksponato pakeitimai išsaugoti.");
    setNotice("");
    setActionError("");
    setLocation("");
    refresh();
  }

  async function apply(action: ExhibitAction) {
    if (busy || error) return;
    setWorking(action);
    setActionError("");
    setNotice("");
    try {
      const options = action === "check-out" ? { method: "POST" } : jsonRequest("POST", { storageLocationId: Number(location) });
      await apiFetch<Exhibit>(`${path}/${action}`, options);
      setLocation("");
      setNotice(actions[action].success);
      refresh();
    } catch (failure) {
      setActionError(errorMessage(failure, "Nepavyko atlikti saugojimo operacijos."));
      if (failure instanceof ApiError && failure.status === 409) {
        setLocation("");
        refresh();
      }
    } finally {
      setWorking(null);
    }
  }

  return (
    <>
      <Link className="back-link" to="/exhibits">Grįžti į eksponatų registrą</Link>
      <header className="page-heading detail-heading">
        <div>
          <h1>{missing ? "Eksponatas nerastas" : exhibit?.name ?? "Eksponato informacija"}</h1>
          {exhibit && !missing && <p className="code exhibit-code">{exhibit.code}</p>}
        </div>
        <div className="button-group">
          {exhibit && !missing && <StatusBadge status={exhibit.status} />}
          {exhibit && !missing && (
            <>
              <button className="button" disabled={busy || Boolean(error)} onClick={() => { setDetailError(""); setEditNotice(""); setEditing(true); }}>Redaguoti</button>
              <button className="button" disabled={busy || Boolean(error)} onClick={() => void remove()}>{deleting ? "Šalinama…" : "Šalinti"}</button>
            </>
          )}
          {!missing && <button className="button" disabled={busy} onClick={refresh}>Atnaujinti</button>}
        </div>
      </header>
      {!missing && <LoadNotice error={error} loading={loading} retry={refresh} />}
      {detailError && <ErrorNotice message={detailError} />}
      {editNotice && <p className="notice notice-success" role="status">{editNotice}</p>}
      {data && exhibit && !missing && (
        <>
          {editing && <ExhibitForm exhibit={exhibit} locations={data[2]} onSaved={saved} onCancel={() => setEditing(false)} />}
          <section className="panel" aria-labelledby="details-heading">
            <div className="panel-heading"><h2 id="details-heading">Eksponato duomenys</h2></div>
            <dl className="fact-grid">
              <div><dt>Kategorija</dt><dd>{exhibit.category || "—"}</dd></div>
              <div><dt>Dabartinė saugojimo vieta</dt><dd><LocationCell location={exhibit.storageLocation} id={exhibit.storageLocationId} /></dd></div>
              <div><dt>Saugojimo zona</dt><dd>{exhibit.storageLocation?.storageZoneName ?? "—"}</dd></div>
              <div className="full-width"><dt>Aprašymas</dt><dd className="description">{exhibit.description || "—"}</dd></div>
            </dl>
          </section>
          <SuitabilityPanel suitability={data[3]} />
          <section className="panel" aria-labelledby="actions-heading">
            <div className="panel-heading"><h2 id="actions-heading">Saugojimo operacijos</h2></div>
            <form className="form-body" onSubmit={(event) => { event.preventDefault(); void apply(assignmentAction); }}>
              {notice && <p className="notice notice-success" role="status">{notice}</p>}
              {actionError && <ErrorNotice message={actionError} />}
              <fieldset className="inline-form" disabled={busy || Boolean(error)}>
                {locations.length > 0 ? (
                  <>
                    <label>
                      Paskirties saugojimo vieta
                      <LocationSelect value={location} locations={locations} onChange={setLocation} />
                    </label>
                    <button className="button primary" type="submit">{working === assignmentAction ? "Išsaugoma…" : actions[assignmentAction].label}</button>
                  </>
                ) : (
                  <p className="state-text">
                    <Link to={storageLocationsLink}>Sukurkite saugojimo vietą</Link>{isStored ? ", kad galėtumėte perkelti eksponatą." : ", kad galėtumėte padėti eksponatą į saugyklą."}
                  </p>
                )}
                {isStored && <button className="button" type="button" onClick={() => void apply("check-out")}>{working === "check-out" ? "Išsaugoma…" : "Paimti iš saugyklos"}</button>}
              </fieldset>
            </form>
          </section>
          <section className="panel" aria-labelledby="history-heading">
            <div className="panel-heading"><h2 id="history-heading">Operacijų istorija</h2></div>
            <div className="table-wrap">
              <table className="history-table">
                <thead><tr><th scope="col">Operacija</th><th scope="col">Iš</th><th scope="col">Į</th><th scope="col">Data ir laikas</th></tr></thead>
                <tbody>
                  {data[1].map((operation) => (
                    <tr key={operation.id}>
                      <th scope="row">{operationLabels[operation.type]}</th>
                      <td><LocationCell location={operation.fromStorageLocation} id={operation.fromStorageLocationId} /></td>
                      <td><LocationCell location={operation.toStorageLocation} id={operation.toStorageLocationId} /></td>
                      <td><time dateTime={operation.createdAt}>{formatTimestamp(operation.createdAt)}</time></td>
                    </tr>
                  ))}
                  {data[1].length === 0 && <tr><td colSpan={4} className="empty-cell">Saugojimo operacijų dar neužregistruota.</td></tr>}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </>
  );
}

const suitabilityLabels = {
  Suitable: "Laikymo sąlygos tinkamos",
  Unsuitable: "Laikymo sąlygos netinkamos",
  NoDevice: "Zonai nepriskirtas įrenginys",
  NoReading: "Nėra matavimo duomenų",
  NotApplicable: "Eksponatas šiuo metu nėra saugykloje",
};

const conditionNumber = new Intl.NumberFormat("lt-LT", { maximumFractionDigits: 2 });

function formatRange({ min, max }: StorageCondition, unit: string) {
  if (min === null && max === null) return "Nenurodyta";
  if (min === null) return `Iki ${conditionNumber.format(max!)} ${unit}`;
  if (max === null) return `Nuo ${conditionNumber.format(min)} ${unit}`;
  return `${conditionNumber.format(min)}–${conditionNumber.format(max)} ${unit}`;
}

function SuitabilityPanel({ suitability }: { suitability: StorageSuitability }) {
  const badge = suitability.status === "Suitable" ? "badge-good"
    : suitability.status === "Unsuitable" ? "badge-alarm" : "badge-neutral";
  return (
    <section className="panel" aria-labelledby="suitability-heading">
      <div className="panel-heading">
        <h2 id="suitability-heading">Laikymo sąlygų būklė</h2>
        <span className={`badge ${badge}`}>{suitabilityLabels[suitability.status]}</span>
      </div>
      <div className="table-wrap">
        <table className="suitability-table">
          <thead><tr><th scope="col">Rodiklis</th><th scope="col">Rodmuo</th><th scope="col">Reikalavimas</th><th scope="col">Įvertinimas</th></tr></thead>
          <tbody>
            {conditionFields.map(({ key, name, unit, low, high }) => {
              const condition = suitability[key];
              const assessment = { WithinRange: "Tinkama", BelowMinimum: low, AboveMaximum: high, NotConfigured: "—", Unknown: "—" }[condition.status];
              const outOfRange = condition.status === "BelowMinimum" || condition.status === "AboveMaximum";
              return (
                <tr key={key}>
                  <th scope="row">{name}</th>
                  <td>{condition.value === null ? "—" : `${conditionNumber.format(condition.value)} ${unit}`}</td>
                  <td>{formatRange(condition, unit)}</td>
                  <td>{outOfRange ? <span className="badge badge-alarm">{assessment}</span> : assessment}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {suitability.readingAt && <p className="form-body report-time">Paskutinis matavimas <time dateTime={suitability.readingAt}>{formatTimestamp(suitability.readingAt)}</time></p>}
    </section>
  );
}

function StatusBadge({ status }: { status: ExhibitStatus }) {
  return status === "Stored"
    ? <span className="badge badge-good">Saugykloje</span>
    : <span className="badge badge-neutral">Paimtas iš saugyklos</span>;
}

function LocationCell({ location, id }: { location: StorageLocation | null; id: number | null }) {
  if (!location) return <span className="muted">{id === null ? "—" : `Saugojimo vieta ${id}`}</span>;
  return <span className="location-value"><span className="code">{location.code}</span>{location.name}</span>;
}

function LocationSelect({ value, locations, onChange }: {
  value: string;
  locations: StorageLocation[];
  onChange: (value: string) => void;
}) {
  return (
    <select value={value} required onChange={(event) => onChange(event.target.value)}>
      <option value="">Pasirinkite saugojimo vietą</option>
      <LocationOptions locations={locations} />
    </select>
  );
}

function LocationOptions({ locations }: { locations: StorageLocation[] }) {
  return locations.map((location) => <option key={location.id} value={location.id}>{location.code} · {location.name}</option>);
}
