import type { AuthResult } from "./types";

export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:7054";

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = "ApiError";
  }

  /** Flattens ASP.NET validation problem details into one line per field. */
  get fieldMessages(): string[] {
    if (!this.fieldErrors) return [];
    return Object.entries(this.fieldErrors).flatMap(([, messages]) => messages);
  }
}

/**
 * The access token is deliberately kept in memory only. The long-lived refresh token lives in an
 * HttpOnly cookie the browser manages, so neither survives an XSS payload reading localStorage.
 */
let accessToken: string | null = null;
let refreshInFlight: Promise<string | null> | null = null;
let onSessionLost: (() => void) | null = null;
let sessionVersion = 0;
let sessionRequests = new AbortController();
export const EXPLICIT_SIGN_OUT_KEY = "inventstock.explicitly-signed-out";
const CURRENT_SESSION_KEY = "inventstock.current-session";
const PENDING_REVOCATION_KEY = "inventstock.pending-session-revocation";

function browserStorage() {
  return typeof window !== "undefined" ? window.localStorage : undefined;
}

export function isExplicitlySignedOut() {
  return browserStorage()?.getItem(EXPLICIT_SIGN_OUT_KEY) === "1";
}

export function markExplicitlySignedOut() {
  browserStorage()?.setItem(EXPLICIT_SIGN_OUT_KEY, "1");
  resetSession();
}

function clearExplicitSignOut() {
  browserStorage()?.removeItem(EXPLICIT_SIGN_OUT_KEY);
}

function setCurrentSession(sessionId: string) {
  browserStorage()?.setItem(CURRENT_SESSION_KEY, sessionId);
}

function takeCurrentSession() {
  const storage = browserStorage();
  const sessionId = storage?.getItem(CURRENT_SESSION_KEY) ?? null;
  storage?.removeItem(CURRENT_SESSION_KEY);
  return sessionId;
}

function queueRevocation(sessionId: string) {
  browserStorage()?.setItem(PENDING_REVOCATION_KEY, JSON.stringify({ sessionId, attemptedAt: Date.now() }));
}

export async function retryPendingRevocation() {
  const storage = browserStorage();
  const raw = storage?.getItem(PENDING_REVOCATION_KEY);
  if (!raw) return;
  try {
    const pending = JSON.parse(raw) as { sessionId: string; attemptedAt: number };
    // Refresh tokens expire after seven days by default; do not retry stale cleanup forever.
    if (Date.now() - pending.attemptedAt > 8 * 24 * 60 * 60 * 1000) {
      storage?.removeItem(PENDING_REVOCATION_KEY);
      return;
    }
    const response = await fetch(buildUrl("/api/auth/logout"), {
      method: "POST", credentials: "include", cache: "no-store",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sessionId: pending.sessionId }),
    });
    if (response.ok) storage?.removeItem(PENDING_REVOCATION_KEY);
  } catch {
    // Offline: retain the old session-specific revocation request for the next online attempt.
  }
}

/** End the old request scope before another person can use this browser. */
export function resetSession() {
  sessionVersion += 1;
  sessionRequests.abort();
  sessionRequests = new AbortController();
  accessToken = null;
  refreshInFlight = null;
}

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

export function setSessionLostHandler(handler: (() => void) | null) {
  onSessionLost = handler;
}

async function parseError(response: Response): Promise<ApiError> {
  let title = response.statusText || "Request failed";
  let detail: string | undefined;
  let fieldErrors: Record<string, string[]> | undefined;

  try {
    const body = await response.json();
    title = body.title ?? title;
    detail = body.detail;
    fieldErrors = body.errors;
  } catch {
    // A non-JSON error body leaves the status text as the best available message.
  }

  const message = detail ?? title;
  return new ApiError(response.status, message, fieldErrors);
}

function buildUrl(path: string, query?: Record<string, unknown>) {
  const url = new URL(path, API_BASE_URL);

  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === "") continue;
      url.searchParams.set(key, String(value));
    }
  }

  return url.toString();
}

async function refreshAccessToken(): Promise<string | null> {
  const version = sessionVersion;
  refreshInFlight ??= (async () => {
    try {
      const response = await fetch(buildUrl("/api/auth/refresh"), {
        method: "POST",
        credentials: "include",
        signal: sessionRequests.signal,
        cache: "no-store",
      });

      if (!response.ok) return null;

      const auth = (await response.json()) as AuthResult;
      if (version !== sessionVersion) return null;
      accessToken = auth.accessToken;
      setCurrentSession(auth.sessionId);
      return auth.accessToken;
    } catch {
      return null;
    } finally {
      if (version === sessionVersion) refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

interface RequestOptions {
  signal?: AbortSignal;
  method?: string;
  body?: unknown;
  query?: Record<string, unknown>;
  /** Set for the auth endpoints themselves, which must not trigger a refresh loop. */
  skipRefresh?: boolean;
}

export async function apiFetch<T>(
  path: string,
  { method = "GET", body, query, skipRefresh = false, signal }: RequestOptions = {},
): Promise<T> {
  const version = sessionVersion;
  const requestSignal = signal ? AbortSignal.any([signal, sessionRequests.signal]) : sessionRequests.signal;
  const assertCurrentSession = () => {
    if (version !== sessionVersion || requestSignal.aborted)
      throw new DOMException("Request belongs to an ended session.", "AbortError");
  };
  const send = async (token: string | null) =>
    fetch(buildUrl(path, query), {
      method,
      credentials: "include",
      signal: requestSignal,
      cache: "no-store",
      headers: {
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });

  let response = await send(accessToken);
  assertCurrentSession();

  if (response.status === 401 && !skipRefresh) {
    const refreshed = await refreshAccessToken();
    assertCurrentSession();

    if (!refreshed) {
      resetSession();
      onSessionLost?.();
      throw await parseError(response);
    }

    response = await send(refreshed);
    assertCurrentSession();
  }

  if (!response.ok) {
    throw await parseError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const result = (await response.json()) as T;
  assertCurrentSession();
  return result;
}

export const api = {
  get: <T>(path: string, query?: Record<string, unknown>, signal?: AbortSignal) =>
    apiFetch<T>(path, { query, signal }),
  post: <T>(path: string, body?: unknown) =>
    apiFetch<T>(path, { method: "POST", body }),
  put: <T>(path: string, body?: unknown) =>
    apiFetch<T>(path, { method: "PUT", body }),
  patch: <T>(path: string, body?: unknown) =>
    apiFetch<T>(path, { method: "PATCH", body }),
  delete: <T>(path: string) => apiFetch<T>(path, { method: "DELETE" }),
};

export async function login(email: string, password: string) {
  await retryPendingRevocation();
  resetSession();
  const auth = await apiFetch<AuthResult>("/api/auth/login", {
    method: "POST",
    body: { email, password },
    skipRefresh: true,
  });

  clearExplicitSignOut();
  accessToken = auth.accessToken;
  setCurrentSession(auth.sessionId);
  return auth;
}

export async function restoreSession() {
  if (isExplicitlySignedOut()) return null;
  const token = await refreshAccessToken();
  if (!token) return null;

  return apiFetch<AuthResult["user"]>("/api/auth/me");
}

export async function logout() {
  // Persist this before attempting the network call. An unreachable server cannot revoke its
  // HttpOnly cookie, but reloads and other tabs must still remain visibly signed out.
  const sessionId = takeCurrentSession();
  if (sessionId) queueRevocation(sessionId);
  markExplicitlySignedOut();
  const version = sessionVersion;
  try {
    await apiFetch<void>("/api/auth/logout", {
      method: "POST",
      body: sessionId ? { sessionId } : undefined,
      skipRefresh: true,
    });
    if (sessionId) browserStorage()?.removeItem(PENDING_REVOCATION_KEY);
  } finally {
    if (version === sessionVersion) resetSession();
  }
}
