const BASE = "/api";

/** Selectable configuration data sources (environments). */
export const SOURCES = ["Dev", "FCB"] as const;
export type Source = (typeof SOURCES)[number];

let currentSource: Source = (sessionStorage.getItem("source") as Source) || "Dev";

export function getSource(): Source {
  return currentSource;
}

export function setSource(source: Source) {
  currentSource = source;
  sessionStorage.setItem("source", source);
}

/** The FCB source is the staged master copy and is read-only; only Dev can be edited. */
export function isReadOnly(): boolean {
  return currentSource === "FCB";
}

// Signed-in credentials kept in memory only (never persisted) so on-demand FCB
// staging can connect to the AS/400 as the logged-in user.
let authUserId: string | null = sessionStorage.getItem("uid");
let authPassword: string | null = null;

export function setAuth(userId: string, password: string) {
  authUserId = userId;
  authPassword = password;
}

export function clearAuth() {
  authUserId = null;
  authPassword = null;
}

async function http<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${url}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      "X-Config-Source": currentSource,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`${method} ${url} failed: ${res.status} ${res.statusText}`);
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export const api = {
  list: <T>(route: string) => http<T[]>("GET", `/${route}`),
  get: <T>(route: string, id: number) => http<T>("GET", `/${route}/${id}`),
  create: <T>(route: string, data: T) => http<T>("POST", `/${route}`, data),
  update: <T>(route: string, id: number, data: T) => http<T>("PUT", `/${route}/${id}`, data),
  remove: (route: string, id: number) => http<void>("DELETE", `/${route}/${id}`),
  login: (userId: string, password: string) =>
    http<{ userId: string }>("POST", `/login`, { userId, password }),
  refreshFcb: () =>
    http<{ staged: boolean; error?: string }>("POST", `/fcb/refresh`, {
      userId: authUserId,
      password: authPassword,
    }),
  resolve: (variable: string, server: string, scope?: string, variableId?: number) =>
    http<ResolveResult>(
      "GET",
      `/resolve?variable=${encodeURIComponent(variable)}&server=${encodeURIComponent(server)}${
        scope ? `&scope=${encodeURIComponent(scope)}` : ""
      }${variableId != null ? `&variableId=${variableId}` : ""}`
    ),
  resolveAll: (variable: string, server: string, variableId?: number) =>
    http<ResolveResult[]>(
      "GET",
      `/resolve-all?variable=${encodeURIComponent(variable)}&server=${encodeURIComponent(server)}${
        variableId != null ? `&variableId=${variableId}` : ""
      }`
    ),
};

export interface ResolveResult {
  variable: string;
  server: string;
  value: string | null;
  scopeName: string | null;
  method: string | null;
  found: boolean;
  valueId: number | null;
}
