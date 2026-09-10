import { useEffect, useState } from "react";
import { api, clearAuth, getIsAdmin, setSource, Source } from "./api";
import GoAnywhereConfigPage from "./pages/GoAnywhereConfigPage";
import GoAnywhereAuditPage from "./pages/GoAnywhereAuditPage";
import LoginPage from "./pages/LoginPage";
import ParametersPage from "./pages/ParametersPage";

type Theme = "dark" | "light";

export default function App() {
  const [user, setUser] = useState<string | null>(() => sessionStorage.getItem("uid"));
  const [activeView, setActiveView] = useState<"projects" | "parameters" | "audit">("projects");
  const [theme, setTheme] = useState<Theme>(() => (localStorage.getItem("theme") as Theme) || "dark");

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem("theme", theme);
  }, [theme]);

  const toggleTheme = () => setTheme((t) => (t === "dark" ? "light" : "dark"));

  if (!user) {
    return <LoginPage onLogin={(userId, source: Source) => {
      sessionStorage.setItem("uid", userId);
      setSource(source);
      setUser(userId);
    }} />;
  }

  const logOff = async () => {
    try {
      await api.authLogout();
    } finally {
      clearAuth();
      sessionStorage.removeItem("uid");
      setUser(null);
      setActiveView("projects");
    }
  };

  return (
    <main className="goanywhere-app">
      <nav className="goanywhere-nav" aria-label="GoAnywhere configuration views">
        <button
          className={activeView === "projects" ? "active" : ""}
          onClick={() => setActiveView("projects")}
        >
          Projects
        </button>
        <button
          className={activeView === "parameters" ? "active" : ""}
          onClick={() => setActiveView("parameters")}
        >
          Parameter Editor
        </button>
        <button
          className={activeView === "audit" ? "active" : ""}
          onClick={() => setActiveView("audit")}
        >
          Audit Log
        </button>
        <button
          className="theme-toggle"
          onClick={toggleTheme}
          aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} mode`}
          title={`Switch to ${theme === "dark" ? "light" : "dark"} mode`}
        >
          <span className="theme-toggle-icon" aria-hidden="true">{theme === "dark" ? "☀️" : "🌙"}</span>
          {theme === "dark" ? "Light" : "Dark"}
        </button>
        <span className="signed-in-user">
          {user} ({getIsAdmin() ? "Admin" : "User"})
        </span>
        <button className="logoff-button" onClick={logOff}>
          Log off
        </button>
      </nav>
      {activeView === "projects" && <GoAnywhereConfigPage />}
      {activeView === "parameters" && <ParametersPage />}
      {activeView === "audit" && <GoAnywhereAuditPage />}
    </main>
  );
}
