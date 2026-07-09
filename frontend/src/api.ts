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
  resolve: (variable: string, server: string, scope?: string) =>
    http<ResolveResult>(
      "GET",
      `/resolve?variable=${encodeURIComponent(variable)}&server=${encodeURIComponent(server)}${
        scope ? `&scope=${encodeURIComponent(scope)}` : ""
      }`
    ),
  resolveAll: (variable: string, server: string) =>
    http<ResolveResult[]>(
      "GET",
      `/resolve-all?variable=${encodeURIComponent(variable)}&server=${encodeURIComponent(server)}`
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
