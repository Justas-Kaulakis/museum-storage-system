import { useCallback, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router";
import { ApiError, apiFetch, errorMessage, jsonRequest } from "./api";
import { ErrorNotice, LoadNotice, LocationCell, LocationSelect, StatusBadge, Timestamp, useApiData } from "./shared";
import type { Exhibit, ExhibitInput, ExhibitStatus, StorageLocation, StorageOperation } from "./types";

const exhibitsPath = "/exhibits";
const locationsPath = "/storage-locations";
const loadRegistry = (signal: AbortSignal) => Promise.all([
  apiFetch<Exhibit[]>(exhibitsPath, { signal }),
  apiFetch<StorageLocation[]>(locationsPath, { signal }),
]);

export function ExhibitsPage() {
  const { data, loading, error, refresh } = useApiData(loadRegistry);
  const [panel, setPanel] = useState<"create" | "scan" | null>(null);
  const exhibits = data?.[0];

  return (
    <>
      <header className="page-heading">
        <h1>Exhibits</h1>
        <div className="button-group">
          <button className="button" aria-expanded={panel === "scan"} onClick={() => setPanel(panel === "scan" ? null : "scan")}>Scan QR</button>
          <button className="button primary" aria-expanded={panel === "create"} disabled={!data}
            onClick={() => setPanel(panel === "create" ? null : "create")}>New exhibit</button>
        </div>
      </header>
      <LoadNotice error={error} loading={loading} retry={refresh} />
      {panel === "create" && data && <CreateExhibitForm locations={data[1]} />}
      {panel === "scan" && <ScanQrForm />}

      {exhibits && (
        <section className="panel table-wrap" aria-label="Exhibit registry">
          <table className="exhibit-table">
            <thead><tr><th scope="col">Code</th><th scope="col">Name</th><th scope="col">Category</th><th scope="col">Status</th><th scope="col">Storage location</th></tr></thead>
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
              {exhibits.length === 0 && <tr><td className="empty-cell" colSpan={5}>No exhibits have been registered.</td></tr>}
            </tbody>
          </table>
        </section>
      )}
    </>
  );
}

function CreateExhibitForm({ locations }: { locations: StorageLocation[] }) {
  const navigate = useNavigate();
  const [status, setStatus] = useState<ExhibitStatus>("CheckedOut");
  const [location, setLocation] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const needsLocation = status === "Stored" && locations.length === 0;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving || needsLocation) return;
    const fields = new FormData(event.currentTarget);
    const exhibit: ExhibitInput = {
      code: String(fields.get("code") ?? ""),
      name: String(fields.get("name") ?? ""),
      category: String(fields.get("category") ?? "").trim() || null,
      description: String(fields.get("description") ?? "").trim() || null,
      status,
      storageLocationId: status === "Stored" ? Number(location) : null,
    };
    setSaving(true);
    setError("");
    try {
      const created = await apiFetch<Exhibit>(exhibitsPath, jsonRequest("POST", exhibit));
      navigate(`/exhibits/${created.id}`);
    } catch (failure) {
      setError(errorMessage(failure, "Could not create the exhibit."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="create-heading">
      <div className="panel-heading"><h2 id="create-heading">New exhibit</h2></div>
      <form className="form-body" onSubmit={submit}>
        {error && <ErrorNotice message={error} />}
        <fieldset disabled={saving}>
          <div className="field-grid">
            <label>Code<input name="code" placeholder="EXHIBIT-0001" required /></label>
            <label>Name<input name="name" required /></label>
            <label>Category<input name="category" /></label>
            <label>
              Status
              <select value={status} onChange={(event) => setStatus(event.target.value as ExhibitStatus)}>
                <option value="CheckedOut">Checked out</option>
                <option value="Stored">Stored</option>
              </select>
            </label>
            {status === "Stored" && (
              <label>
                Storage location
                <LocationSelect value={location} locations={locations} onChange={setLocation} />
              </label>
            )}
            <label className="full-width">Description<textarea name="description" rows={3} /></label>
          </div>
          {needsLocation && <p className="state-text"><Link to="/storage-locations">Create a storage location</Link> to register this exhibit as stored.</p>}
          <div className="form-actions"><button className="button primary" disabled={needsLocation} type="submit">{saving ? "Creating..." : "Create exhibit"}</button></div>
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
        ? "No exhibit matches the code in this image."
        : errorMessage(failure, "Could not scan the QR image."));
    } finally {
      setScanning(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="scan-heading">
      <div className="panel-heading"><h2 id="scan-heading">Scan a QR image</h2></div>
      <form className="form-body" onSubmit={scan}>
        {error && <ErrorNotice message={error} />}
        <fieldset className="inline-form" disabled={scanning}>
          <label>QR image (PNG or JPEG)<input type="file" name="file" accept="image/png,image/jpeg" required /></label>
          <button className="button primary" type="submit">{scanning ? "Scanning..." : "Find exhibit"}</button>
        </fieldset>
      </form>
    </section>
  );
}

const actions = {
  "check-in": { label: "Check in", success: "Exhibit checked in." },
  "check-out": { label: "Check out", success: "Exhibit checked out." },
  move: { label: "Move", success: "Exhibit moved." },
} as const;
type ExhibitAction = keyof typeof actions;
const operationLabels = { CheckIn: "Check in", CheckOut: "Check out", Move: "Move" };

export function ExhibitDetail({ id }: { id: string }) {
  const path = `${exhibitsPath}/${id}`;
  const load = useCallback((signal: AbortSignal) => Promise.all([
    apiFetch<Exhibit>(path, { signal }),
    apiFetch<StorageOperation[]>(`${path}/operations`, { signal }),
    apiFetch<StorageLocation[]>(locationsPath, { signal }),
  ]), [path]);
  const { data, error, loading, refresh } = useApiData(load);
  const [location, setLocation] = useState("");
  const [working, setWorking] = useState<ExhibitAction | null>(null);
  const [actionError, setActionError] = useState("");
  const [notice, setNotice] = useState("");
  const exhibit = data?.[0];
  const isStored = exhibit?.status === "Stored";
  const locations = (data?.[2] ?? []).filter((item) => item.id !== exhibit?.storageLocationId);
  const assignmentAction = isStored ? "move" : "check-in";
  const missing = error instanceof ApiError && error.status === 404;

  async function apply(action: ExhibitAction) {
    if (working || loading || error) return;
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
      setActionError(errorMessage(failure, "The storage operation failed."));
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
      <Link className="back-link" to="/exhibits">Exhibits</Link>
      <header className="page-heading detail-heading">
        <div>
          <h1>{missing ? "Exhibit not found" : exhibit?.name ?? "Exhibit details"}</h1>
          {exhibit && !missing && <p className="code exhibit-code">{exhibit.code}</p>}
        </div>
        <div className="button-group">
          {exhibit && !missing && <StatusBadge status={exhibit.status} />}
          {!missing && <button className="button" disabled={loading || Boolean(working)} onClick={refresh}>Refresh</button>}
        </div>
      </header>
      {!missing && <LoadNotice error={error} loading={loading} retry={refresh} />}
      {data && exhibit && !missing && (
        <>
          <section className="panel" aria-labelledby="details-heading">
            <div className="panel-heading"><h2 id="details-heading">Exhibit record</h2></div>
            <dl className="fact-grid">
              <div><dt>Category</dt><dd>{exhibit.category || "—"}</dd></div>
              <div><dt>Storage location</dt><dd><LocationCell location={exhibit.storageLocation} id={exhibit.storageLocationId} /></dd></div>
              <div className="full-width"><dt>Description</dt><dd className="description">{exhibit.description || "—"}</dd></div>
            </dl>
          </section>
          <section className="panel" aria-labelledby="actions-heading">
            <div className="panel-heading"><h2 id="actions-heading">Storage actions</h2></div>
            <form className="form-body" onSubmit={(event) => { event.preventDefault(); void apply(assignmentAction); }}>
              {notice && <p className="notice notice-success" role="status">{notice}</p>}
              {actionError && <ErrorNotice message={actionError} />}
              <fieldset className="inline-form" disabled={loading || Boolean(working) || Boolean(error)}>
                {locations.length > 0 ? (
                  <>
                    <label>
                      Destination
                      <LocationSelect value={location} locations={locations} onChange={setLocation} />
                    </label>
                    <button className="button primary" type="submit">{working === assignmentAction ? "Saving..." : actions[assignmentAction].label}</button>
                  </>
                ) : (
                  <p className="state-text">
                    <Link to="/storage-locations">Create a storage location</Link> {isStored ? "to move this exhibit." : "to check in this exhibit."}
                  </p>
                )}
                {isStored && <button className="button" type="button" onClick={() => void apply("check-out")}>{working === "check-out" ? "Saving..." : "Check out"}</button>}
              </fieldset>
            </form>
          </section>
          <section className="panel" aria-labelledby="history-heading">
            <div className="panel-heading"><h2 id="history-heading">Operation history</h2></div>
            <div className="table-wrap">
              <table className="history-table">
                <thead><tr><th scope="col">Operation</th><th scope="col">From</th><th scope="col">To</th><th scope="col">Timestamp</th></tr></thead>
                <tbody>
                  {data[1].map((operation) => (
                    <tr key={operation.id}>
                      <th scope="row">{operationLabels[operation.type]}</th>
                      <td><LocationCell location={operation.fromStorageLocation} id={operation.fromStorageLocationId} /></td>
                      <td><LocationCell location={operation.toStorageLocation} id={operation.toStorageLocationId} /></td>
                      <td><Timestamp value={operation.createdAt} /></td>
                    </tr>
                  ))}
                  {data[1].length === 0 && <tr><td colSpan={4} className="empty-cell">No storage operations have been recorded.</td></tr>}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </>
  );
}
