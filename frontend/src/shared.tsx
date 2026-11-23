import { useEffect, useState } from "react";
import { errorMessage } from "./api";
import type { ExhibitStatus, StorageLocation } from "./types";

export function useApiData<T>(load: (signal: AbortSignal) => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal).then(
      (result) => {
        if (controller.signal.aborted) return;
        setData(result);
        setLoading(false);
      },
      (failure: unknown) => {
        if (controller.signal.aborted) return;
        setError(failure);
        setLoading(false);
      },
    );
    return () => controller.abort();
  }, [load, revision]);

  function refresh() {
    setLoading(true);
    setError(null);
    setRevision((previous) => previous + 1);
  }

  return { data, error, loading, refresh };
}

export function ErrorNotice({ message, retry }: { message: string; retry?: () => void }) {
  return (
    <div className="notice notice-error" role="alert">
      <span>{message}</span>
      {retry && <button className="button" type="button" onClick={retry}>Retry</button>}
    </div>
  );
}

export function LoadNotice({ error, loading, retry }: { error: unknown; loading: boolean; retry: () => void }) {
  if (error) return <ErrorNotice message={errorMessage(error, "Could not load this page.")} retry={retry} />;
  if (loading) return <p className="state-text" role="status">Loading...</p>;
  return null;
}

export function Badge({ label, tone = "neutral" }: { label: string; tone?: "neutral" | "good" | "active" | "alarm" }) {
  return <span className={`badge badge-${tone}`}>{label}</span>;
}

export function StatusBadge({ status }: { status: ExhibitStatus }) {
  return status === "Stored" ? <Badge label="Stored" tone="good" /> : <Badge label="Checked out" />;
}

export function LocationCell({ location, id }: { location: StorageLocation | null; id?: number | null }) {
  if (!location) return <span className="muted">{id == null ? "—" : `Location ${id}`}</span>;
  return <span className="location-value"><span className="code">{location.code}</span>{location.name}</span>;
}

export function LocationSelect({ value, locations, onChange }: {
  value: string;
  locations: StorageLocation[];
  onChange: (value: string) => void;
}) {
  return (
    <select value={value} required onChange={(event) => onChange(event.target.value)}>
      <option value="">Select a location</option>
      {locations.map((location) => (
        <option key={location.id} value={location.id}>{location.code} · {location.name}</option>
      ))}
    </select>
  );
}

const localTimestamp = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });
export function Timestamp({ value }: { value: string }) {
  const utc = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
  return <time dateTime={utc}>{localTimestamp.format(new Date(utc))}</time>;
}
