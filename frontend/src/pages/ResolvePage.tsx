import { FormEvent, useEffect, useRef, useState } from "react";
import { api, getSource, ResolveResult } from "../api";
import { Row } from "./useLookups";
import Combobox from "./Combobox";
import ResultCard, { ChangeStatus } from "./ResultCard";

// Stable key so the same variable/scope can be matched across resolves.
const keyOf = (r: ResolveResult) =>
  r.valueId != null ? `id:${r.valueId}` : `${r.variable}|${r.server}|${r.scopeName ?? ""}`;

export default function ResolvePage() {
  const [variables, setVariables] = useState<Row[]>([]);
  const [servers, setServers] = useState<Row[]>([]);
  const [contexts, setContexts] = useState<Row[]>([]);
  const [extents, setExtents] = useState<Row[]>([]);
  const [scopes, setScopes] = useState<Row[]>([]);
  const [context, setContext] = useState("");
  const [extent, setExtent] = useState("");
  const [variable, setVariable] = useState("");
  const [scope, setScope] = useState("");
  // The resolution server follows the source the user signed in with.
  const server = getSource() === "FCB" ? "FCB" : "DEV";
  // FCB is read-only and returns all values (no scope override).
  const readOnly = server === "FCB";
  const [results, setResults] = useState<ResolveResult[]>();
  const [statuses, setStatuses] = useState<Record<string, ChangeStatus>>();
  const [error, setError] = useState<string>();
  // Snapshot of the values from the previous Resolve, keyed by keyOf().
  const prevValues = useRef<Map<string, string | null> | null>(null);

  useEffect(() => {
    api.list<Row>("variable-definitions").then(setVariables);
    api.list<Row>("servers").then(setServers);
    api.list<Row>("contexts").then(setContexts);
    api.list<Row>("extents").then(setExtents);
    api.list<Row>("scopes").then(setScopes);
  }, []);

  // extent id -> context id, so a chosen context can filter the variable list.
  const extentToContext: Record<number, number> = {};
  for (const e of extents) extentToContext[e.id as number] = e["contextId"] as number;

  const contextId = contexts.find((c) => String(c["name"]) === context)?.id as number | undefined;
  // Extents available for the chosen context (or all if no context picked).
  const shownExtents = contextId
    ? extents.filter((e) => (e["contextId"] as number) === contextId)
    : extents;
  const extentId = shownExtents.find((e) => String(e["name"]) === extent)?.id as number | undefined;

  const shownVariables = extentId
    ? variables.filter((v) => (v["extentId"] as number) === extentId)
    : contextId
    ? variables.filter((v) => extentToContext[v["extentId"] as number] === contextId)
    : variables;

  // Scope overrides are limited to the server of the signed-in source.
  const serverId = servers.find((s) => String(s["name"]) === server)?.id as number | undefined;
  const scopeOptions = Array.from(
    new Set(
      scopes
        .filter((s) => (s["serverId"] as number) === serverId)
        .map((s) => String(s["name"]))
    )
  );

  const onContextChange = (value: string) => {
    setContext(value);
    setExtent("");
    setVariable("");
  };

  const onExtentChange = (value: string) => {
    setExtent(value);
    setVariable("");
  };

  // When the variable is picked on its own, fill in its Context/Extent so the
  // exact definition (names repeat across extents) is what gets resolved.
  const onVariableChange = (value: string) => {
    setVariable(value);
    if (!value) return;
    const def = shownVariables.find((v) => String(v["name"]) === value);
    if (!def) return;
    const xtnId = def["extentId"] as number;
    if (!extent) {
      const xtn = extents.find((e) => e.id === xtnId);
      if (xtn) setExtent(String(xtn["name"]));
    }
    if (!context) {
      const ctx = contexts.find((c) => c.id === extentToContext[xtnId]);
      if (ctx) setContext(String(ctx["name"]));
    }
  };

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(undefined);
    try {
      let list: ResolveResult[];
      // Use the exact definition for the chosen context/extent (names repeat across extents).
      const variableId = shownVariables.find((v) => String(v["name"]) === variable)?.id as
        | number
        | undefined;
      if (scope && !readOnly) {
        // Explicit scope override: a single, directly-resolved value.
        list = [await api.resolve(variable, server, scope, variableId)];
      } else {
        // No override (always the case for FCB): show every candidate value.
        list = await api.resolveAll(variable, server, variableId);
      }

      // Compare freshly-read values against the previous Resolve to flag changes.
      // On the first Resolve there is no prior snapshot, so everything is "new".
      const prev = prevValues.current;
      const next: Record<string, ChangeStatus> = {};
      for (const r of list) {
        const k = keyOf(r);
        next[k] = !prev || !prev.has(k) ? "new" : prev.get(k) === r.value ? "unchanged" : "updated";
      }
      setStatuses(next);

      // Store the new snapshot for the next Resolve.
      const snap = new Map<string, string | null>();
      for (const r of list) snap.set(keyOf(r), r.value);
      prevValues.current = snap;

      setResults(list);
    } catch (err) {
      setError(String(err));
    }
  };

  return (
    <div>
      <h2>
        Retrieve Configuration Value <span className="legacy">consumption UT2061-UT2087</span>
      </h2>
      <p className="subtle">
        {readOnly ? (
          <>
            Retrieve <b>all</b> effective values for a variable on the <b>{server}</b> server (from
            your signed-in source).
          </>
        ) : (
          <>
            Retrieve a variable's effective value for the <b>{server}</b> server (from your signed-in
            source). An exact scope match overrides the global default. Optionally narrow by context,
            or pick a specific scope to resolve directly.
          </>
        )}
      </p>
      <form className="form inline" onSubmit={onSubmit}>
        <div className="field">
          <label>Context</label>
          <Combobox
            className={context ? "filled" : ""}
            placeholder="— all — (type to search)"
            value={context}
            options={contexts.map((c) => String(c["name"]))}
            onChange={onContextChange}
          />
        </div>
        <div className="field">
          <label>Extent</label>
          <Combobox
            className={extent ? "filled" : ""}
            placeholder="— all — (type to search)"
            value={extent}
            options={shownExtents.map((e) => String(e["name"]))}
            onChange={onExtentChange}
          />
        </div>
        <div className="field">
          <label>Variable</label>
          <Combobox
            className={variable ? "filled" : ""}
            placeholder="type to search..."
            value={variable}
            options={shownVariables.map((v) => String(v["name"]))}
            onChange={onVariableChange}
            required
          />
        </div>
        {!readOnly && (
          <div className="field">
            <label>Scope (override)</label>
            <Combobox
              className={scope ? "filled" : ""}
              placeholder="— by server — (type to search)"
              value={scope}
              options={scopeOptions}
              onChange={setScope}
            />
          </div>
        )}
        <button className="btn" type="submit">
          Resolve
        </button>
      </form>
      {error && <p className="error">{error}</p>}
      {results && (
        results.length > 1 ? (
          <div className="result-grid">
            {results.map((r) => (
              <ResultCard key={keyOf(r)} result={r} showScope status={statuses?.[keyOf(r)]} />
            ))}
          </div>
        ) : (
          <ResultCard
            key={keyOf(results[0])}
            result={results[0]}
            status={statuses?.[keyOf(results[0])]}
          />
        )
      )}
    </div>
  );
}
