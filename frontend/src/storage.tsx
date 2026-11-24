import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router";
import { ApiError, apiFetch, errorMessage, jsonRequest } from "./api";
import { ErrorNotice } from "./shared";
import type { StorageLocation, StorageZone } from "./types";

export const locationsPath = "/storage-locations";
export const zonesPath = "/storage-zones";

export function ZoneOptions({ zones }: { zones: StorageZone[] }) {
  return zones.map((zone) => <option key={zone.id} value={zone.id}>{zone.code} · {zone.name}</option>);
}

export function StorageSection({ items, zones, onChanged, locationLink }: {
  items: (StorageZone | StorageLocation)[] | undefined;
  zones?: StorageZone[];
  onChanged: () => void;
  locationLink?: (location: StorageLocation) => string;
}) {
  const isLocation = zones !== undefined;
  const path = isLocation ? locationsPath : zonesPath;
  const [selected, setSelected] = useState<StorageZone | StorageLocation | "new" | null>(null);
  const [search, setSearch] = useState("");
  const [zoneFilter, setZoneFilter] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const feedback = useRef<HTMLDivElement>(null);
  const editing = selected !== null && selected !== "new" ? selected : null;
  const term = search.trim().toLocaleLowerCase("lt-LT");
  const filtered = items?.filter((item) =>
    (item.code.toLocaleLowerCase("lt-LT").includes(term) || item.name.toLocaleLowerCase("lt-LT").includes(term))
    && (!zoneFilter || ("storageZoneId" in item && String(item.storageZoneId) === zoneFilter)));

  useEffect(() => {
    feedback.current?.scrollIntoView({ block: "nearest" });
  }, [error, notice]);

  function open(item: typeof selected) {
    setSelected(item);
    setError("");
    setNotice("");
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const fields = new FormData(event.currentTarget);
    const body = {
      code: fields.get("code"),
      name: fields.get("name"),
      ...(isLocation ? { storageZoneId: Number(fields.get("storageZoneId")) } : { deviceId: fields.get("deviceId") }),
    };
    setSaving(true);
    setError("");
    setNotice("");
    try {
      await apiFetch(editing ? `${path}/${editing.id}` : path, jsonRequest(editing ? "PUT" : "POST", body));
      setSelected(null);
      setNotice("Pakeitimai išsaugoti.");
      onChanged();
    } catch (failure) {
      setError(errorMessage(failure, "Nepavyko išsaugoti pakeitimų."));
    } finally {
      setSaving(false);
    }
  }

  async function remove(item: StorageZone | StorageLocation) {
    if (!window.confirm(`Ar pašalinti „${item.name}“?`)) return;
    setSaving(true);
    setError("");
    setNotice("");
    try {
      await apiFetch<void>(`${path}/${item.id}`, { method: "DELETE" });
      setSelected(null);
      setNotice("Įrašas pašalintas.");
      onChanged();
    } catch (failure) {
      setError(failure instanceof ApiError && failure.status === 409
        ? (isLocation ? "Saugojimo vietos pašalinti negalima, kol joje yra eksponatų."
          : "Zonos pašalinti negalima, kol joje yra saugojimo vietų.")
        : errorMessage(failure, "Nepavyko pašalinti įrašo."));
    } finally {
      setSaving(false);
    }
  }

  const titleId = isLocation ? "locations-heading" : "zones-heading";
  return (
    <section id={isLocation ? "storage-locations" : "storage-zones"} className="panel" aria-labelledby={titleId}>
      <div className="panel-heading">
        <h2 id={titleId}>{isLocation ? "Saugojimo vietos" : "Saugojimo zonos"}</h2>
        <button className="button primary" onClick={() => open("new")} disabled={saving || !items || zones?.length === 0}>
          {isLocation ? "Nauja saugojimo vieta" : "Nauja saugojimo zona"}
        </button>
      </div>
      {items && (
        <>
          <div className="form-body inline-form">
            <label>Paieška pagal kodą arba pavadinimą
              <input type="search" value={search} onChange={(event) => setSearch(event.target.value)} />
            </label>
            {zones && <label>Saugojimo zona
              <select value={zoneFilter} onChange={(event) => setZoneFilter(event.target.value)}>
                <option value="">Visos zonos</option>
                <ZoneOptions zones={zones} />
              </select>
            </label>}
          </div>
          <div className="table-wrap">
            <table>
              <thead><tr><th scope="col">Kodas</th><th scope="col">Pavadinimas</th><th scope="col">{isLocation ? "Saugojimo zona" : "Įrenginio ID"}</th><th scope="col">Veiksmai</th></tr></thead>
              <tbody>
                {filtered?.map((item) => (
                  <tr key={item.id}>
                    <th scope="row" className="code">{item.code}</th>
                    <td>{"storageZoneId" in item && locationLink
                      ? <Link className="table-link" to={locationLink(item)}>{item.name}</Link>
                      : item.name}</td>
                    <td>{"storageZoneId" in item ? item.storageZoneName : item.deviceId ?? "—"}</td>
                    <td><div className="button-group">
                      <button type="button" className="button" disabled={saving} onClick={() => open(item)}>Redaguoti</button>
                      <button type="button" className="button" disabled={saving} onClick={() => void remove(item)}>Šalinti</button>
                    </div></td>
                  </tr>
                ))}
                {filtered?.length === 0 && <tr><td className="empty-cell" colSpan={4}>
                  {isLocation ? "Saugojimo vietų nerasta." : "Saugojimo zonų nerasta."}
                </td></tr>}
              </tbody>
            </table>
          </div>
        </>
      )}
      {items && zones?.length === 0 && <p className="form-body state-text">Norėdami pridėti saugojimo vietą, pirmiausia sukurkite saugojimo zoną.</p>}
      {(notice || error) && <div ref={feedback} className="form-body">
        {notice && <p className="notice notice-success" role="status">{notice}</p>}
        {error && <ErrorNotice message={error} />}
      </div>}
      {selected && (
        <form key={editing?.id ?? "new"} className="form-body" onSubmit={save}>
          <fieldset disabled={saving}>
            <div className="field-grid">
              <label>Kodas<input name="code" required autoFocus defaultValue={editing?.code} placeholder={isLocation ? "A-SHELF-01" : "ZONE-A"} /></label>
              <label>Pavadinimas<input name="name" required defaultValue={editing?.name} /></label>
              {!isLocation && <label>Įrenginio ID<input name="deviceId" defaultValue={editing && "deviceId" in editing ? editing.deviceId ?? "" : ""} placeholder="pvz. museum-storage-01" /></label>}
              {zones && <label>Saugojimo zona
                <select name="storageZoneId" required defaultValue={editing && "storageZoneId" in editing ? String(editing.storageZoneId) : ""}>
                  <option value="">Pasirinkite saugojimo zoną</option>
                  <ZoneOptions zones={zones} />
                </select>
              </label>}
            </div>
            <div className="form-actions">
              <button className="button primary" type="submit">{saving ? "Išsaugoma…" : editing ? "Išsaugoti" : "Sukurti"}</button>
              <button className="button" type="button" onClick={() => open(null)}>Atšaukti</button>
            </div>
          </fieldset>
        </form>
      )}
    </section>
  );
}
