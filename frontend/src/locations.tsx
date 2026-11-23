import { useState } from "react";
import type { FormEvent } from "react";
import { apiFetch, errorMessage, jsonRequest } from "./api";
import { ErrorNotice, LoadNotice, useApiData } from "./shared";
import type { StorageLocation } from "./types";

const path = "/storage-locations";
const loadLocations = (signal: AbortSignal) => apiFetch<StorageLocation[]>(path, { signal });

export function StorageLocationsPage() {
  const { data, loading, error, refresh } = useApiData(loadLocations);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [notice, setNotice] = useState("");

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    const fields = new FormData(event.currentTarget);
    setSaving(true);
    setSaveError("");
    try {
      const location = await apiFetch<StorageLocation>(path, jsonRequest("POST", {
        code: fields.get("code"),
        name: fields.get("name"),
      }));
      setShowForm(false);
      setNotice(`${location.name} was created.`);
      refresh();
    } catch (failure) {
      setSaveError(errorMessage(failure, "Could not create the storage location."));
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <header className="page-heading">
        <h1>Storage locations</h1>
        <button className="button primary" aria-expanded={showForm} disabled={saving}
          onClick={() => { setShowForm(!showForm); setSaveError(""); setNotice(""); }}>
          {showForm ? "Cancel" : "New location"}
        </button>
      </header>
      <LoadNotice error={error} loading={loading} retry={refresh} />
      {notice && <p className="notice notice-success" role="status">{notice}</p>}

      {showForm && (
        <section className="panel" aria-labelledby="new-location-heading">
          <div className="panel-heading"><h2 id="new-location-heading">New storage location</h2></div>
          <form className="form-body" onSubmit={create}>
            {saveError && <ErrorNotice message={saveError} />}
            <fieldset disabled={saving}>
              <div className="field-grid">
                <label>Code<input name="code" required placeholder="SHELF-A1" /></label>
                <label>Name<input name="name" required /></label>
              </div>
              <div className="form-actions"><button className="button primary" type="submit">{saving ? "Creating..." : "Create location"}</button></div>
            </fieldset>
          </form>
        </section>
      )}

      {data && (
        <section className="panel table-wrap" aria-label="Storage location registry">
          <table>
            <thead><tr><th scope="col">Code</th><th scope="col">Name</th></tr></thead>
            <tbody>
              {data.map((location) => (
                <tr key={location.id}><th scope="row" className="code">{location.code}</th><td>{location.name}</td></tr>
              ))}
              {data.length === 0 && <tr><td colSpan={2} className="empty-cell">No storage locations have been registered.</td></tr>}
            </tbody>
          </table>
        </section>
      )}
    </>
  );
}
