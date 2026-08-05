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

export default function App() {
  const loc = useLocation();
  const [user, setUser] = useState<string | null>(() => sessionStorage.getItem("uid"));
  const [source, setSourceState] = useState<Source>(() => getSource());
  const [refreshing, setRefreshing] = useState(false);
  const [refreshMsg, setRefreshMsg] = useState<string>();

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
        <h1>UT Config</h1>
        <nav>
          <Link className={`primary ${loc.pathname === "/" ? "active" : ""}`} to="/">
            Resolve Value
          </Link>
          <Link className={loc.pathname === "/menu" ? "active" : ""} to="/menu">
            Menu
          </Link>
          <Link className={loc.pathname === "/goanywhere" ? "active" : ""} to="/goanywhere">
            GoAnywhere Config
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
        </nav>
        <footer>
          <div className="signed-in">
            Signed in as <b>{user}</b>
          </div>
          <div className="signed-in">
            Source <b>{source}</b>
            {source === "FCB" && <span className="badge readonly">read-only</span>}
          </div>
          {source === "FCB" && (
            <>
              <button className="btn ghost" type="button" onClick={refreshFcb} disabled={refreshing}>
                {refreshing ? "Refreshing…" : "Refresh from FCB"}
              </button>
              {refreshMsg && <div className="subtle">{refreshMsg}</div>}
            </>
          )}
          <button className="btn ghost" type="button" onClick={logout}>
            Sign out
          </button>
          <div className="subtle">Modernized from RPG/IBM i</div>
        </footer>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<ResolvePage />} />
          <Route path="/menu" element={<MenuPage />} />
          <Route path="/resolve" element={<ResolvePage />} />
          <Route path="/goanywhere" element={<GoAnywhereConfigPage />} />
          <Route path="/:route" element={<ListPage />} />
          <Route path="/:route/new" element={<EditPage />} />
          <Route path="/:route/:id" element={<EditPage />} />
        </Routes>
      </main>
    </div>
  );
}
