import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api, isReadOnly } from "../api";
import { entities } from "../entities";
import { Row, useLookups } from "./useLookups";

export default function EditPage() {
  const { route, id } = useParams();
  const def = route ? entities[route] : undefined;
  const isNew = !id;
  const navigate = useNavigate();
  const lookups = useLookups(def!);
  const readOnly = isReadOnly();
  const [form, setForm] = useState<Row>({ id: 0 } as Row);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(!isNew);

  const initial = useMemo(() => {
    const o: Record<string, unknown> = {};
    def?.fields.forEach((f) => {
      o[f.name] = f.type === "bool" ? false : f.type === "number" || f.type === "lookup" ? 0 : "";
    });
    return o;
  }, [def?.route]);

  useEffect(() => {
    if (!def) return;
    if (isNew) {
      setForm({ id: 0, ...initial } as Row);
      return;
    }
    api
      .get<Row>(def.route, Number(id))
      .then((r) => setForm(r))
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [def?.route, id]);

  if (!def) return <p>Unknown entity.</p>;
  if (loading) return <p>Loading…</p>;

  const setField = (name: string, value: unknown) => setForm((f) => ({ ...f, [name]: value }));

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (readOnly) return;
    try {
      if (isNew) await api.create(def.route, form);
      else await api.update(def.route, Number(id), form);
      navigate(`/${def.route}`);
    } catch (err) {
      setError(String(err));
    }
  };

  return (
    <div>
      <h2>
        {readOnly ? "View" : isNew ? "Add" : "Change"} {def.title}{" "}
        <span className="legacy">UT2020 maintain</span>
      </h2>
      {readOnly && (
        <p className="badge readonly">FCB · read-only — switch to the Dev source to make changes.</p>
      )}
      {error && <p className="error">{error}</p>}
      <form className="form" onSubmit={onSubmit}>
        {def.fields.map((f) => (
          <div className="field" key={f.name}>
            <label>{f.label}</label>
            {f.type === "bool" ? (
              <input
                type="checkbox"
                checked={Boolean(form[f.name])}
                disabled={readOnly}
                onChange={(e) => setField(f.name, e.target.checked)}
              />
            ) : f.type === "lookup" && f.lookup ? (
              <select
                value={Number(form[f.name] ?? 0)}
                disabled={readOnly}
                onChange={(e) => setField(f.name, Number(e.target.value))}
              >
                <option value={0}>— select —</option>
                {Object.entries(lookups[f.lookup] ?? {}).map(([k, label]) => (
                  <option key={k} value={k}>
                    {label}
                  </option>
                ))}
              </select>
            ) : f.type === "textarea" ? (
              <textarea
                value={String(form[f.name] ?? "")}
                disabled={readOnly}
                onChange={(e) => setField(f.name, e.target.value)}
              />
            ) : (
              <input
                type={f.type === "number" ? "number" : "text"}
                value={String(form[f.name] ?? "")}
                disabled={readOnly}
                onChange={(e) =>
                  setField(f.name, f.type === "number" ? Number(e.target.value) : e.target.value)
                }
              />
            )}
          </div>
        ))}
        <div className="form-actions">
          {!readOnly && (
            <button className="btn" type="submit">
              Enter=Save
            </button>
          )}
          <button className="btn ghost" type="button" onClick={() => navigate(`/${def.route}`)}>
            {readOnly ? "F12=Back" : "F12=Cancel"}
          </button>
        </div>
      </form>
    </div>
  );
}
