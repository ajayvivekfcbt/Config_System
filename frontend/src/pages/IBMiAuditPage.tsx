import { useEffect, useMemo, useState } from "react";
import { getSource } from "../api";
import "./IBMiAuditPage.css";

const API_BASE = "/api";

interface IBMiAuditEntry {
  id: number;
  configId: number | null;
  variableDefinitionId: number | null;
  scopeId: number | null;
  scopeName: string;
  variableDefName: string;
  extentName: string;
  environment: string;
  oldValue: string | null;
  newValue: string | null;
  action: string;
  changedBy: string;
  isSensitive: boolean;
  changedAtUtc: string;
}

export default function IBMiAuditPage() {
  const [entries, setEntries] = useState<IBMiAuditEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [scopeFilter, setScopeFilter] = useState("");
  const [variableDefFilter, setVariableDefFilter] = useState("");
  const [extentFilter, setExtentFilter] = useState("");
  const [actionFilter, setActionFilter] = useState("");

  const loadAuditLogs = async () => {
    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams();
      params.set("limit", "500");
      if (scopeFilter.trim()) params.set("scopeName", scopeFilter.trim());
      if (variableDefFilter.trim()) params.set("variableDefName", variableDefFilter.trim());
      if (extentFilter.trim()) params.set("extentName", extentFilter.trim());
      if (actionFilter.trim()) params.set("action", actionFilter.trim());

      const response = await fetch(`${API_BASE}/goanywhere/ibmi/audit-logs?${params.toString()}`, {
        headers: {
          "X-Config-Source": getSource(),
          "X-App-Key": "config-system-web-app",
        },
      });
      if (!response.ok) {
        throw new Error(`Failed to load audit logs: ${response.status} ${response.statusText}`);
      }

      const data: IBMiAuditEntry[] = await response.json();
      setEntries(data);
    } catch (err) {
      setError(`Error loading audit logs: ${err}`);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAuditLogs();
  }, []);

  const uniqueActions = useMemo(() => {
    const actions = entries.map(e => e.action).filter(Boolean);
    return [...new Set(actions)].sort();
  }, [entries]);

  const handleClearFilters = () => {
    setScopeFilter("");
    setVariableDefFilter("");
    setExtentFilter("");
    setActionFilter("");
  };

  const filteredEntries = useMemo(() => {
    return entries.filter(entry => {
      if (scopeFilter && !entry.scopeName.toLowerCase().includes(scopeFilter.toLowerCase())) return false;
      if (variableDefFilter && !entry.variableDefName.toLowerCase().includes(variableDefFilter.toLowerCase())) return false;
      if (extentFilter && !entry.extentName.toLowerCase().includes(extentFilter.toLowerCase())) return false;
      if (actionFilter && entry.action.toLowerCase() !== actionFilter.toLowerCase()) return false;
      return true;
    });
  }, [entries, scopeFilter, variableDefFilter, extentFilter, actionFilter]);

  const getActionBadgeClass = (action: string) => {
    switch (action.toUpperCase()) {
      case "CREATE":
        return "badge-create";
      case "UPDATE":
        return "badge-update";
      case "DELETE":
        return "badge-delete";
      default:
        return "badge-default";
    }
  };

  return (
    <div className="ibmi-audit-page">
      <div className="audit-container">
        <div className="audit-header">
          <h1>🖥️ IBM i Configuration Audit Log</h1>
          <p className="subtitle">Track all changes to IBM i configuration system parameters and values</p>
        </div>

        {/* Filters */}
        <div className="audit-filters">
          <div className="filter-group">
            <label htmlFor="scope-filter">Scope Name</label>
            <input
              id="scope-filter"
              type="text"
              placeholder="Filter by scope..."
              value={scopeFilter}
              onChange={(e) => setScopeFilter(e.target.value)}
              className="filter-input"
            />
          </div>

          <div className="filter-group">
            <label htmlFor="vardef-filter">Variable Definition</label>
            <input
              id="vardef-filter"
              type="text"
              placeholder="Filter by variable definition..."
              value={variableDefFilter}
              onChange={(e) => setVariableDefFilter(e.target.value)}
              className="filter-input"
            />
          </div>

          <div className="filter-group">
            <label htmlFor="extent-filter">Extent (Project)</label>
            <input
              id="extent-filter"
              type="text"
              placeholder="Filter by extent..."
              value={extentFilter}
              onChange={(e) => setExtentFilter(e.target.value)}
              className="filter-input"
            />
          </div>

          <div className="filter-group">
            <label htmlFor="action-filter">Action</label>
            <select
              id="action-filter"
              value={actionFilter}
              onChange={(e) => setActionFilter(e.target.value)}
              className="filter-input"
            >
              <option value="">All Actions</option>
              {uniqueActions.map((action) => (
                <option key={action} value={action}>
                  {action}
                </option>
              ))}
            </select>
          </div>

          <div className="filter-group">
            <button onClick={handleClearFilters} className="btn-clear-filters">
              Clear Filters
            </button>
          </div>
        </div>

        {/* Status Messages */}
        {error && <div className="error-message">{error}</div>}
        {loading && <div className="loading-message">Loading audit logs...</div>}

        {/* Results Count */}
        <div className="results-count">
          Showing {filteredEntries.length} of {entries.length} entries
        </div>

        {/* Audit Table */}
        <div className="audit-table-wrapper">
          {filteredEntries.length === 0 && !loading ? (
            <div className="empty-state">
              <div className="empty-icon">📋</div>
              <p>No audit log entries found</p>
              <small>Audit logs will appear here as changes are made to IBM i configurations</small>
            </div>
          ) : (
            <table className="audit-table">
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Action</th>
                  <th>Changed By</th>
                  <th>Extent (Project)</th>
                  <th>Variable Definition</th>
                  <th>Scope</th>
                  <th>Old Value</th>
                  <th>New Value</th>
                  <th>Sensitive</th>
                </tr>
              </thead>
              <tbody>
                {filteredEntries.map((entry) => (
                  <tr key={entry.id} className={`row-${entry.action.toLowerCase()}`}>
                    <td className="timestamp">
                      <span title={new Date(entry.changedAtUtc).toLocaleString()}>
                        {new Date(entry.changedAtUtc).toLocaleDateString()} {new Date(entry.changedAtUtc).toLocaleTimeString()}
                      </span>
                    </td>
                    <td className="action">
                      <span className={`badge ${getActionBadgeClass(entry.action)}`}>{entry.action}</span>
                    </td>
                    <td className="changed-by">{entry.changedBy}</td>
                    <td className="extent">{entry.extentName}</td>
                    <td className="vardef">{entry.variableDefName}</td>
                    <td className="scope">{entry.scopeName}</td>
                    <td className="old-value">
                      {entry.isSensitive ? "●●●●●●●●" : entry.oldValue || "-"}
                    </td>
                    <td className="new-value">
                      {entry.isSensitive ? "●●●●●●●●" : entry.newValue || "-"}
                    </td>
                    <td className="sensitive">{entry.isSensitive ? "🔒" : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}
