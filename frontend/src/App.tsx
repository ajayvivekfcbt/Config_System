import { useState } from "react";
import { Link, Route, Routes, useLocation } from "react-router-dom";
import { menuItems } from "./entities";
import { api, getSource, setSource, clearAuth, Source } from "./api";
import MenuPage from "./pages/MenuPage";
import ListPage from "./pages/ListPage";
import EditPage from "./pages/EditPage";
import ResolvePage from "./pages/ResolvePage";
import LoginPage from "./pages/LoginPage";
import GoAnywhereConfigPage from "./pages/GoAnywhereConfigPage";
import ParametersPage from "./pages/ParametersPage";

export default function App() {
  const loc = useLocation();
  const [user, setUser] = useState<string | null>(() => sessionStorage.getItem("uid"));
  const [source, setSourceState] = useState<Source>(() => getSource());
  const [refreshing, setRefreshing] = useState(false);
  const [, setRefreshMsg] = useState<string>();

  if (!user) {
    return (
      <LoginPage
        onLogin={(uid, src) => {
          sessionStorage.setItem("uid", uid);
          setSource(src);
          setSourceState(src);
          setUser(uid);
        }}
      />
    );
  }

  const logout = () => {
    sessionStorage.removeItem("uid");
    clearAuth();
    setUser(null);
  };

  const refreshFcb = async () => {
    setRefreshing(true);
    setRefreshMsg(undefined);
    try {
      await api.refreshFcb();
      setRefreshMsg("Refreshed from FCB.");
    } catch {
      setRefreshMsg("Refresh failed (AS/400 not reachable).");
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <div className="layout">
      <aside className="sidebar">
        <h1>Config Systems</h1>
        <nav>
          {/* IBMi Config Section */}
          <div className="nav-section">
            <div className="nav-section-title">🖥️ IBMi Config</div>
            <Link className={`primary ${loc.pathname === "/" ? "active" : ""}`} to="/">
              Resolve Value
            </Link>
            <Link className={loc.pathname === "/menu" ? "active" : ""} to="/menu">
              Menu
            </Link>
            {menuItems.map((e) => (
              <Link
                key={e.route}
                className={loc.pathname.startsWith(`/${e.route}`) ? "active" : ""}
                to={`/${e.route}`}
              >
                {e.title}
              </Link>
            ))}
          </div>

          {/* GoAnywhere Config Section */}
          <div className="nav-section">
            <div className="nav-section-title">🔄 GoAnywhere Config</div>
            <Link className={loc.pathname === "/goanywhere" ? "active" : ""} to="/goanywhere">
              Projects
            </Link>
            <Link className={loc.pathname === "/parameters" ? "active" : ""} to="/parameters">
              Parameters
            </Link>
          </div>
        </nav>
        <footer>
          <div className="subtle">Modernized from RPG/IBM i</div>
          <div className="user-info-footer">
            <div style={{ display: "flex", flexDirection: "column", gap: "8px", width: "100%" }}>
              <span className="user-label-footer">👤 {user}</span>
              <div style={{ display: "flex", gap: "6px", alignItems: "center", flexWrap: "wrap" }}>
                <span className="source-badge-footer">
                  <span className="source-label">{source}</span>
                  {source === "FCB" && <span className="readonly-indicator">read-only</span>}
                </span>
              </div>
              <div style={{ display: "flex", gap: "6px", marginTop: "4px" }}>
                {source === "FCB" && (
                  <button className="btn ghost" type="button" onClick={refreshFcb} disabled={refreshing} style={{ fontSize: "11px", padding: "4px 8px" }}>
                    {refreshing ? "⟳" : "🔄"}
                  </button>
                )}
                <button className="btn ghost" type="button" onClick={logout} style={{ fontSize: "11px", padding: "4px 8px" }}>
                  Sign out
                </button>
              </div>
            </div>
          </div>
        </footer>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<ResolvePage />} />
          <Route path="/menu" element={<MenuPage />} />
          <Route path="/resolve" element={<ResolvePage />} />
          <Route path="/goanywhere" element={<GoAnywhereConfigPage />} />
          <Route path="/parameters" element={<ParametersPage />} />
          <Route path="/:route" element={<ListPage />} />
          <Route path="/:route/new" element={<EditPage />} />
          <Route path="/:route/:id" element={<EditPage />} />
        </Routes>
      </main>
    </div>
  );
}
