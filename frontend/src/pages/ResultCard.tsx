import { useState } from "react";
import { api, isReadOnly, ResolveResult } from "../api";
import { Row } from "./useLookups";

export type ChangeStatus = "updated" | "unchanged" | "new";

function StatusBadge({ status }: { status?: ChangeStatus }) {
  if (!status) return null;
  const label = status === "updated" ? "Updated" : status === "new" ? "New" : "Unchanged";
  return <span className={`badge badge-${status}`}>{label}</span>;
}

export default function ResultCard({
  result,
  showScope,
  status,
}: {
  result: ResolveResult;
  showScope?: boolean;
  status?: ChangeStatus;
}) {
  const [value, setValue] = useState(result.value);
  const [editValue, setEditValue] = useState(result.value ?? "");
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const [error, setError] = useState<string>();

  // The FCB source is read-only, so inline value editing is not offered there.
  const readOnly = isReadOnly();
  const isSecret = /password|passwd|pwd/i.test(result.variable);
  const mask = isSecret && !revealed;

  const onSave = async () => {
    if (result.valueId == null) return;
    setError(undefined);
    setSaved(false);
    try {
      const current = await api.get<Row>("variable-values", result.valueId);
      await api.update("variable-values", result.valueId, { ...current, value: editValue });
      setValue(editValue);
      setEditing(false);
      setSaved(true);
    } catch (err) {
      setError(String(err));
    }
  };

  if (!result.found) {
    return (
      <div className="result missing">
        <div>
          No value resolved for {result.variable} on {result.server}.{" "}
          <StatusBadge status={status} />
        </div>
      </div>
    );
  }

  return (
    <div className="result ok">
      <div>
        <strong>{result.variable}</strong> on <strong>{result.server}</strong>{" "}
        <StatusBadge status={status} />
      </div>
      {showScope && result.scopeName && (
        <div className="subtle">
          scope <b>{result.scopeName}</b>
          {result.method ? ` (${result.method})` : ""}
        </div>
      )}
      {editing ? (
        <div className="edit-value">
          <input
            type={mask ? "password" : "text"}
            value={editValue}
            onChange={(e) => setEditValue(e.target.value)}
          />
          <button className="btn" type="button" onClick={onSave}>
            Save
          </button>
          <button
            className="btn ghost"
            type="button"
            onClick={() => {
              setEditing(false);
              setEditValue(value ?? "");
            }}
          >
            Cancel
          </button>
        </div>
      ) : (
        <div className="value">{mask ? "********" : value}</div>
      )}
      {isSecret && (
        <button className="btn ghost" type="button" onClick={() => setRevealed((v) => !v)}>
          {revealed ? "Hide" : "Show"}
        </button>
      )}
      {!editing && !readOnly && result.valueId != null && (
        <button
          className="btn ghost"
          type="button"
          onClick={() => {
            setEditValue(value ?? "");
            setEditing(true);
            setSaved(false);
          }}
        >
          Update value
        </button>
      )}
      {saved && <div className="subtle">Saved.</div>}
      {error && <p className="error">{error}</p>}
    </div>
  );
}
