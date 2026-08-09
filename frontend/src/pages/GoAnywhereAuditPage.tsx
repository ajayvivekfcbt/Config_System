import { useEffect, useMemo, useState } from "react";
import "./GoAnywhereAuditPage.css";

const API_BASE = "http://localhost:5000/api";

interface AuditEntry {
  id: number;
  configId: number | null;
  projectId: number;
  projectName: string;
  environment: string;
  configKey: string;
  oldValue: string | null;
  newValue: string | null;
  action: string;
  changedBy: string;
  isSensitive: boolean;
  changedAtUtc: string;
}

export default function GoAnywhereAuditPage() {
  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [projectFilter, setProjectFilter] = useState("");
  const [keyFilter, setKeyFilter] = useState("");
  const [environmentFilter, setEnvironmentFilter] = useState("");
  const [actionFilter, setActionFilter] = useState("");

  const loadAuditLogs = async () => {
    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams();
      params.set("limit", "500");
      if (projectFilter.trim()) params.set("projectName", projectFilter.trim());
      if (keyFilter.trim()) params.set("configKey", keyFilter.trim());
      if (environmentFilter.trim()) params.set("environment", environmentFilter.trim());
      if (actionFilter.trim()) params.set("action", actionFilter.trim());

      const response = await fetch(`${API_BASE}/goanywhere/audit-logs?${params.toString()}`);
      if (!response.ok) {
        throw new Error(`Failed to load audit logs: ${response.status} ${response.statusText}`);
      }

      const data: AuditEntry[] = await response.json();
      setEntries(data);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Failed to load audit logs";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAuditLogs();
  }, []);

  const environments = useMemo(
    () => [...new Set(entries.map((e) => e.environment))].sort(),
    [entries]
  );

  const actions = useMemo(
    () => [...new Set(entries.map((e) => e.action))].sort(),
    [entries]
  );

  const formatValue = (value: string | null) => {
    if (value === null) return "(null)";
    if (value === "") return "(empty)";
    return value;
  };

  return (
    <div className="ga-audit-page">
      <div className="ga-audit-header">
        <h2>GoAnywhere Parameter Audit Log</h2>
        <p>Track parameter value changes and deletions with old/new values and actor details.</p>
      </div>

      <div className="ga-audit-filters">
        <input
          type="search"
          placeholder="Filter by project"
          value={projectFilter}
          onChange={(e) => setProjectFilter(e.target.value)}
        />
        <input
          type="search"
          placeholder="Filter by parameter key"
          value={keyFilter}
          onChange={(e) => setKeyFilter(e.target.value)}
        />
        <select value={environmentFilter} onChange={(e) => setEnvironmentFilter(e.target.value)}>
          <option value="">All Environments</option>
          {environments.map((env) => (
            <option key={env} value={env}>
              {env}
            </option>
          ))}
        </select>
        <select value={actionFilter} onChange={(e) => setActionFilter(e.target.value)}>
          <option value="">All Actions</option>
          {actions.map((action) => (
            <option key={action} value={action}>
              {action}
            </option>
          ))}
        </select>
        <button onClick={loadAuditLogs} disabled={loading}>
          {loading ? "Loading..." : "Apply Filters"}
        </button>
      </div>

      {error && <div className="ga-audit-error">{error}</div>}

      <div className="ga-audit-table-wrap">
        <table className="ga-audit-table">
          <thead>
            <tr>
              <th>Timestamp (UTC)</th>
              <th>Action</th>
              <th>Project</th>
              <th>Environment</th>
              <th>Parameter</th>
              <th>Old Value</th>
              <th>New Value</th>
              <th>Changed By</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} className="empty-row">Loading audit entries...</td>
              </tr>
            ) : entries.length === 0 ? (
              <tr>
                <td colSpan={8} className="empty-row">No audit entries found.</td>
              </tr>
            ) : (
              entries.map((entry) => (
                <tr key={entry.id}>
                  <td>{new Date(entry.changedAtUtc).toISOString().replace("T", " ").replace(".000Z", "")}</td>
                  <td>
                    <span className={`action-pill action-${entry.action.toLowerCase()}`}>{entry.action}</span>
                  </td>
                  <td>{entry.projectName}</td>
                  <td>{entry.environment}</td>
                  <td>{entry.configKey}</td>
                  <td><code>{formatValue(entry.oldValue)}</code></td>
                  <td><code>{formatValue(entry.newValue)}</code></td>
                  <td>{entry.changedBy}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
