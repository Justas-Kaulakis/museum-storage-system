export class ApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

function responseMessage(status: number, problem: { errors?: Record<string, unknown> } | null) {
  if (status >= 500) return "The API is unavailable. Check that the backend is running, then try again.";
  if (status === 404) return "The requested record was not found.";
  if (status === 409) return "This exhibit changed while the request was in progress. Refresh to see its current state.";

  const errors = Object.values(problem?.errors ?? {}).flat().filter((message): message is string => typeof message === "string");
  if (errors.length > 0) return [...new Set(errors)].join("\n");

  return status === 400 ? "The backend rejected the request. Check the submitted values." : "The request failed. Try again.";
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api${path}`, options);
  } catch (error) {
    if (options?.signal?.aborted) throw error;
    throw new ApiError(0, "Could not connect to the API. Check that the backend is running, then try again.");
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new ApiError(response.status, responseMessage(response.status, problem));
  }

  try {
    return await response.json() as T;
  } catch {
    throw new ApiError(response.status, "The API returned an unreadable response.");
  }
}

export function jsonRequest(method: "POST" | "PUT", body: unknown): RequestInit {
  return { method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };
}

export function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}
