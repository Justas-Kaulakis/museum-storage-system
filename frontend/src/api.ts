export class ApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

function responseMessage(status: number, problem: { errors?: Record<string, unknown> } | null) {
  if (status >= 500) return "API nepasiekiama. Patikrinkite, ar veikia serveris, ir bandykite dar kartą.";
  if (status === 404) return "Įrašas nerastas.";
  if (status === 409) return "Vykdant užklausą eksponato duomenys pasikeitė. Atnaujinkite puslapį ir patikrinkite dabartinę būseną.";

  const errors = Object.values(problem?.errors ?? {}).flat().filter((message): message is string => typeof message === "string");
  if (errors.length > 0) return [...new Set(errors)].join("\n");

  return status === 400 ? "Serveris atmetė užklausą. Patikrinkite pateiktus duomenis." : "Nepavyko įvykdyti užklausos. Bandykite dar kartą.";
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api${path}`, options);
  } catch (error) {
    if (options?.signal?.aborted) throw error;
    throw new ApiError(0, "Nepavyko prisijungti prie API. Patikrinkite, ar veikia serveris, ir bandykite dar kartą.");
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new ApiError(response.status, responseMessage(response.status, problem));
  }

  if (response.status === 204) return undefined as T;

  try {
    return await response.json() as T;
  } catch {
    throw new ApiError(response.status, "Nepavyko perskaityti API atsakymo.");
  }
}

export function jsonRequest(method: "POST" | "PUT", body: unknown): RequestInit {
  return { method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };
}

export function errorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}
