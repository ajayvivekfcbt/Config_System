import { useEffect, useMemo, useState } from "react";
import { getSource } from "../api";
import "./IBMiParametersAuditPage.css";

const API_BASE = "/api";

interface IBMiParameterAuditEntry {
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

export default function IBMiParametersAuditPage() {
  const [entries, setEntries] = useState<IBMiParameterAuditEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [variableFilter, setVariableFilter] = useState("");
  const [userFilter, setUserFilter] = useState("");
  const [actionFilter, setActionFilter] = useState("");
  const [dateRangeStart, setDateRangeStart] = useState("");
  const [dateRangeEnd, setDateRangeEnd] = useState("");

  const loadAuditLogs = async () => {
    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams();
      params.set("limit", "500");
      if (variableFilter.trim()) params.set("variableDefName", variableFilter.trim());
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

      const data: IBMiParameterAuditEntry[] = await response.json();
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

  const uniqueUsers = useMemo(() => {
    const users = entries.map(e => e.changedBy).filter(Boolean);
    return [...new Set(users)].sort();
  }, [entries]);

  const handleClearFilters = () => {
    setVariableFilter("");
    setUserFilter("");
    setActionFilter("");
    setDateRangeStart("");
    setDateRangeEnd("");
  };

  const filteredEntries = useMemo(() => {
    return entries.filter(entry => {
      if (variableFilter && !entry.variableDefName.toLowerCase().includes(variableFilter.toLowerCase())) return false;
      if (userFilter && entry.changedBy.toLowerCase() !== userFilter.toLowerCase()) return false;
      if (actionFilter && entry.action.toLowerCase() !== actionFilter.toLowerCase()) return false;

      if (dateRangeStart) {
        const entryDate = new Date(entry.changedAtUtc);
        const startDate = new Date(dateRangeStart);
        if (entryDate < startDate) return false;
      }

      if (dateRangeEnd) {
        const entryDate = new Date(entry.changedAtUtc);
        const endDate = new Date(dateRangeEnd);
        endDate.setHours(23, 59, 59, 999);
        if (entryDate > endDate) return false;
      }

      return true;
    });
  }, [entries, variableFilter, userFilter, actionFilter, dateRangeStart, dateRangeEnd]);

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

  const exportToCSV = () => {
    if (filteredEntries.length === 0) {
      alert("No entries to export");
      return;
    }

    const headers = ["Timestamp", "Action", "Changed By", "Variable", "Extent", "Old Value", "New Value", "Sensitive"];
    const rows = filteredEntries.map(e => [
      new Date(e.changedAtUtc).toLocaleString(),
      e.action,
      e.changedBy,
      e.variableDefName,
      e.extentName,
      e.isSensitive ? "●●●●●●●●" : e.oldValue || "-",
      e.isSensitive ? "●●●●●●●●" : e.newValue || "-",
      e.isSensitive ? "Yes" : "No"
    ]);

    const csvContent = [
      headers.join(","),
      ...rows.map(row => row.map(cell => `"${(cell ?? "").toString().replace(/"/g, '""')}"`).join(","))
    ].join("\n");

    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = `ibmi-parameters-audit-${new Date().toISOString().split('T')[0]}.csv`;
    link.click();
  };

  return (
    <div className="ibmi-params-audit-page">
      <div className="audit-container">
        <div className="audit-header">
          <h1>📊 Variables Audit Log</h1>
          <p className="subtitle">Review and track variable modifications across the IBM i Configuration System</p>
        </div>

        {/* Filters */}
        <div className="audit-filters">
          <div className="filter-group">
            <label htmlFor="variable-filter">Variable/Parameter</label>
            <input
              id="variable-filter"
              type="text"
              placeholder="Filter by variable name..."
              value={variableFilter}
              onChange={(e) => setVariableFilter(e.target.value)}
              className="filter-input"
            />
          </div>

          <div className="filter-group">
            <label htmlFor="user-filter">Changed By</label>
            <select
              id="user-filter"
              value={userFilter}
              onChange={(e) => setUserFilter(e.target.value)}
              className="filter-input"
            >
              <option value="">All Users</option>
              {uniqueUsers.map((user) => (
                <option key={user} value={user}>
                  {user}
                </option>
              ))}
            </select>
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
            <label htmlFor="date-start">Start Date</label>
            <input
              id="date-start"
              type="date"
              value={dateRangeStart}
              onChange={(e) => setDateRangeStart(e.target.value)}
              className="filter-input"
            />
          </div>

          <div className="filter-group">
            <label htmlFor="date-end">End Date</label>
            <input
              id="date-end"
              type="date"
              value={dateRangeEnd}
              onChange={(e) => setDateRangeEnd(e.target.value)}
              className="filter-input"
            />
          </div>

          <div className="filter-actions">
            <button onClick={handleClearFilters} className="btn-clear-filters">
              Clear Filters
            </button>
            <button onClick={exportToCSV} className="btn-export">
              📥 Export CSV
            </button>
          </div>
        </div>

        {/* Status Messages */}
        {error && <div className="error-message">{error}</div>}
        {loading && <div className="loading-message">Loading audit logs...</div>}

        {/* Results Count */}
        <div className="results-count">
          Showing {filteredEntries.length} of {entries.length} variable changes
        </div>

        {/* Audit Table */}
        <div className="audit-table-wrapper">
          {filteredEntries.length === 0 && !loading ? (
            <div className="empty-state">
              <div className="empty-icon">📋</div>
              <p>No variable changes found</p>
              <small>Variable audit logs will appear here as variables are added or modified</small>
            </div>
          ) : (
            <table className="audit-table">
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Action</th>
                  <th>Parameter</th>
                  <th>Extent/Project</th>
                  <th>Changed By</th>
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
                        {new Date(entry.changedAtUtc).toLocaleDateString()}
                        <br />
                        {new Date(entry.changedAtUtc).toLocaleTimeString()}
                      </span>
                    </td>
                    <td className="action">
                      <span className={`badge ${getActionBadgeClass(entry.action)}`}>{entry.action}</span>
                    </td>
                    <td className="variable">
                      <strong>{entry.variableDefName}</strong>
                      {entry.scopeName && <div className="scope-info">Scope: {entry.scopeName}</div>}
                    </td>
                    <td className="extent">{entry.extentName || "N/A"}</td>
                    <td className="changed-by">
                      <span className="user-badge">{entry.changedBy}</span>
                    </td>
                    <td className="old-value">
                      <code>{entry.isSensitive ? "●●●●●●●●" : entry.oldValue || "-"}</code>
                    </td>
                    <td className="new-value">
                      <code>{entry.isSensitive ? "●●●●●●●●" : entry.newValue || "-"}</code>
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
