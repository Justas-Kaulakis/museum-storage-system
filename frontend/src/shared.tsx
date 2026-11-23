import { useEffect, useState } from "react";
import { errorMessage } from "./api";

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

  return { data, setData, error, loading, refresh };
}

export function ErrorNotice({ message, retry }: { message: string; retry?: () => void }) {
  return (
    <div className="notice notice-error" role="alert">
      <span>{message}</span>
      {retry && <button className="button" type="button" onClick={retry}>Bandyti dar kartą</button>}
    </div>
  );
}

export function LoadNotice({ error, loading, retry }: { error: unknown; loading: boolean; retry: () => void }) {
  if (error) return <ErrorNotice message={errorMessage(error, "Nepavyko įkelti puslapio.")} retry={retry} />;
  if (loading) return <p className="state-text" role="status">Įkeliama…</p>;
  return null;
}

const localTimestamp = new Intl.DateTimeFormat("lt-LT", { dateStyle: "medium", timeStyle: "short" });
export function formatTimestamp(value: string): string {
  const utc = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
  return localTimestamp.format(new Date(utc));
}
