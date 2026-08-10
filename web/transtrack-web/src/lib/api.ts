const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5034";

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

/**
 * Every request carries the httpOnly session cookie (`credentials: "include"`)
 * — the API reads auth off that, never off a token this client holds itself.
 * A non-2xx response is turned into an ApiError carrying the API's own
 * `{ message }` body when there is one, so callers can show it directly.
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers,
    },
  });

  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = await res.json();
      message = body?.message ?? message;
    } catch {
      // No JSON body — the status text is all we have.
    }
    throw new ApiError(res.status, message);
  }

  // Several endpoints return a bare `Ok()` — 200 with an empty body, not
  // 204 — e.g. VehiclesController.Save. res.json() throws on an empty
  // string, so check the body is actually there before parsing it.
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

/**
 * For endpoints that return a file (LR/Bill PDFs, report exports) instead of
 * JSON — pulls the filename off the Content-Disposition header the API sets
 * via `File(bytes, contentType, downloadName)` so the caller doesn't have to
 * duplicate that name on the frontend.
 */
async function requestFile(path: string, fallbackName: string): Promise<File> {
  const res = await fetch(`${API_URL}${path}`, { credentials: "include" });

  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = await res.json();
      message = body?.message ?? message;
    } catch {
      // No JSON body — the status text is all we have.
    }
    throw new ApiError(res.status, message);
  }

  const disposition = res.headers.get("Content-Disposition") ?? "";
  const match = /filename="?([^";]+)"?/.exec(disposition);
  const filename = match?.[1] ?? fallbackName;
  const blob = await res.blob();
  return new File([blob], filename, { type: blob.type });
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: body !== undefined ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PUT", body: body !== undefined ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),
  getFile: requestFile,
};
