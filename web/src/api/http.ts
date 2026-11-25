// The panel API's one request helper: same-origin JSON with the session cookie (the browser adds Origin and
// Sec-Fetch-Site, which the CSRF check needs), {error: {code, message}} as an ApiError. A 401 on a signed-in route, or
// a 403 that says the session is restricted, is reported to the listener (the session store), which re-reads who is
// signed in. The fetch function is injectable for the unit tests.

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
    /** seconds, from Retry-After (rate limits, lockouts) */
    readonly retryAfter: number | null = null,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export const isApiError = (e: unknown): e is ApiError => e instanceof ApiError;

export type AuthSignal = "unauthenticated" | "restricted" | "reauth";
export type FetchLike = (input: string, init: RequestInit) => Promise<Response>;

const RESTRICTED = new Set(["password_change_required", "2fa_setup_required", "2fa_signin_required"]);

export interface Api {
  get<T>(path: string): Promise<T>;
  post<T>(path: string, body?: unknown): Promise<T>;
  put<T>(path: string, body?: unknown): Promise<T>;
  patch<T>(path: string, body?: unknown): Promise<T>;
  del<T>(path: string): Promise<T>;
  /** A raw body (an image, a file's bytes) with its own content type; the same errors and signals as the JSON calls. */
  raw<T>(method: "POST" | "PUT", path: string, body: Blob | ArrayBuffer | Uint8Array, contentType: string): Promise<T>;
  /** Called with "unauthenticated" or "restricted" (not for the sign-in routes themselves). */
  onAuthSignal(listener: (s: AuthSignal) => void): () => void;
}

/** Routes whose 401 is an answer about the credentials, not about the session. */
const SIGN_IN_ROUTES = ["/api/auth/login", "/api/auth/login/2fa", "/api/auth/setup", "/api/auth/launcher"];

export function createApi(fetchImpl: FetchLike = (i, init) => fetch(i, init)): Api {
  const listeners = new Set<(s: AuthSignal) => void>();
  const signal = (s: AuthSignal) => {
    for (const l of [...listeners]) {
      try {
        l(s);
      } catch (e) {
        console.error("auth listener", e);
      }
    }
  };

  async function request<T>(method: string, path: string, body?: unknown, raw?: { body: BodyInit; type: string }): Promise<T> {
    if (!path.startsWith("/api/")) throw new Error(`not an API path: ${path}`);
    const init: RequestInit = { method, credentials: "same-origin", headers: { Accept: "application/json" }, cache: "no-store" };
    if (raw) {
      init.headers = { ...init.headers, "Content-Type": raw.type };
      init.body = raw.body;
    } else if (body !== undefined) {
      init.headers = { ...init.headers, "Content-Type": "application/json" };
      init.body = JSON.stringify(body);
    }
    let res: Response;
    try {
      res = await fetchImpl(path, init);
    } catch (e) {
      throw new ApiError(0, "network", `The panel did not answer (${e instanceof Error ? e.message : String(e)}). Check that the server is running, then try again.`);
    }
    if (res.status === 204 || res.status === 205) return undefined as T;
    const text = await res.text();
    let json: unknown = null;
    if (text) {
      try {
        json = JSON.parse(text);
      } catch {
        if (res.ok) throw new ApiError(res.status, "bad_response", "the panel's answer is not JSON");
      }
    }
    if (res.ok) return json as T;
    const err = (json as { error?: { code?: unknown; message?: unknown } } | null)?.error;
    const code = typeof err?.code === "string" ? err.code : `http_${res.status}`;
    const message = typeof err?.message === "string" ? err.message : `${res.status} ${res.statusText}`.trim();
    const ra = Number(res.headers.get("Retry-After"));
    const e = new ApiError(res.status, code, message, Number.isFinite(ra) && ra > 0 ? ra : null);
    if (!SIGN_IN_ROUTES.includes(path)) {
      if (res.status === 401) signal("unauthenticated");
      else if (res.status === 403 && RESTRICTED.has(code)) signal("restricted");
      else if (res.status === 403 && code === "reauth_required") signal("reauth");
    }
    throw e;
  }

  return {
    get: (p) => request("GET", p),
    post: (p, b) => request("POST", p, b),
    put: (p, b) => request("PUT", p, b),
    patch: (p, b) => request("PATCH", p, b),
    del: (p) => request("DELETE", p),
    raw: (m, p, b, type) => request(m, p, undefined, { body: b as BodyInit, type }),
    onAuthSignal(l) {
      listeners.add(l);
      return () => listeners.delete(l);
    },
  };
}

/** A path segment, encoded. */
export const seg = (s: string) => encodeURIComponent(s);
