import { FormEvent, useState } from "react";
import { api, getSource, setSource, SOURCES, Source } from "../api";

export default function LoginPage({ onLogin }: { onLogin: (userId: string, source: Source) => void }) {
  const [userId, setUserId] = useState("");
  const [password, setPassword] = useState("");
  const [source, setSourceState] = useState<Source>(getSource());
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(undefined);
    setBusy(true);
    setSource(source);
    try {
      const r = await api.login(userId, password);
      onLogin(r.userId, source);
    } catch {
      setError("Sign-in failed. Check your AS/400 user ID and password.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={onSubmit}>
        <h1>UT Config</h1>
        <p className="subtle">Sign in with your IBM i (AS/400) user profile.</p>
        <div className="field">
          <label>Source</label>
          <select value={source} onChange={(e) => setSourceState(e.target.value as Source)}>
            {SOURCES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>User ID</label>
          <input
            value={userId}
            onChange={(e) => setUserId(e.target.value)}
            autoFocus
            autoComplete="username"
            required
          />
        </div>
        <div className="field">
          <label>Password</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </div>
        {error && <p className="error">{error}</p>}
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Signing in..." : "Sign in"}
        </button>
      </form>
    </div>
  );
}
