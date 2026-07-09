import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, isReadOnly } from "../api";
import { entities } from "../entities";
import { Row, useLookups } from "./useLookups";

export default function ListPage() {
  const { route } = useParams();
  const def = route ? entities[route] : undefined;
  const [rows, setRows] = useState<Row[]>([]);
  const [error, setError] = useState<string>();
  const [query, setQuery] = useState("");
  const lookups = useLookups(def!);
  const navigate = useNavigate();
  const readOnly = isReadOnly();

  useEffect(() => {
    if (!def) return;
    setQuery("");
    api.list<Row>(def.route).then(setRows).catch((e) => setError(String(e)));
  }, [def?.route]);

  if (!def) return <p>Unknown entity.</p>;

  const cols = def.fields.filter((f) => f.inList);

  const display = (f: (typeof def.fields)[number], row: Row) => {
    const v = row[f.name];
    if (f.type === "bool") return v ? "Yes" : "No";
    if (f.type === "lookup" && f.lookup) return lookups[f.lookup]?.[v as number] ?? String(v);
    return String(v ?? "");
  };

  const q = query.trim().toLowerCase();
  const filtered = q
    ? rows.filter((row) => cols.some((c) => display(c, row).toLowerCase().includes(q)))
    : rows;

  const onDelete = async (id: number) => {
    if (!confirm("Delete this row? (option 4=Delete)")) return;
    try {
      await api.remove(def.route, id);
      setRows((r) => r.filter((x) => x.id !== id));
    } catch (e) {
      setError(String(e));
    }
  };

  return (
    <div>
      <div className="page-head">
        <h2>
          Work with {def.title} <span className="legacy">{def.legacy}</span>
        </h2>
        {readOnly ? (
          <span className="badge readonly" title="The FCB source is read-only">
            FCB · read-only
          </span>
        ) : (
          <Link className="btn" to={`/${def.route}/new`}>
            F6=Add
          </Link>
        )}
      </div>
      <div className="search-bar">
        <input
          className="search"
          type="search"
          placeholder={`Search ${def.title} by ${cols.map((c) => c.label).join(", ")}...`}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <span className="subtle">
          {filtered.length} of {rows.length}
        </span>
      </div>
      {error && <p className="error">{error}</p>}
      <table className="grid">
        <thead>
          <tr>
            {!readOnly && <th>Opt</th>}
            {cols.map((c) => (
              <th key={c.name}>{c.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {filtered.map((row) => (
            <tr key={row.id}>
              {!readOnly && (
                <td className="actions">
                  <button title="2=Change" onClick={() => navigate(`/${def.route}/${row.id}`)}>
                    ✎
                  </button>
                  <button title="4=Delete" onClick={() => onDelete(row.id)}>
                    🗑
                  </button>
                </td>
              )}
              {cols.map((c) => (
                <td key={c.name}>{display(c, row)}</td>
              ))}
            </tr>
          ))}
          {filtered.length === 0 && (
            <tr>
              <td colSpan={cols.length + (readOnly ? 0 : 1)} className="subtle">
                {rows.length === 0 ? "No rows." : "No matches."}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
