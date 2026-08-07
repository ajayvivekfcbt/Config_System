import { useState } from "react";
import { Link } from "react-router-dom";
import { menuItems } from "../entities";

export default function MenuPage() {
  const [query, setQuery] = useState("");

  const q = query.trim().toLowerCase();
  const filtered = q
    ? menuItems.filter((e) =>
        e.title.toLowerCase().includes(q) ||
        e.legacy.toLowerCase().includes(q) ||
        e.route.toLowerCase().includes(q)
      )
    : menuItems;

  const allMenuCards = [
    ...filtered,
    { route: "goanywhere", title: "GoAnywhere Configuration", legacy: "Project Management (26+ Projects)", special: "goanywhere" },
    { route: "resolve", title: "Resolve Value (Consumption)", legacy: "UT2061-UT2087", special: "resolve" },
  ].filter((e) => !q || e.title.toLowerCase().includes(q) || e.legacy.toLowerCase().includes(q));

  return (
    <div>
      <h2>Configuration Menu</h2>
      <p className="subtle">
        Maintenance of the Utility Configuration System (modernized from RPG program UT2000).
      </p>
      <div className="search-bar">
        <input
          className="search"
          type="search"
          placeholder="Search menu items by name or description..."
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <span className="subtle">
          {allMenuCards.length} items
        </span>
      </div>
      <div className="menu-grid">
        {filtered.map((e, i) => (
          <Link key={e.route} to={`/${e.route}`} className="menu-card">
            <span className="opt">{String(i + 1).padStart(2, "0")}</span>
            <span className="title">{e.title}</span>
            <span className="legacy">{e.legacy}</span>
          </Link>
        ))}
        {filtered.length < menuItems.length && (
          <>
            <Link to="/goanywhere" className="menu-card goanywhere-card">
              <span className="opt">🚀</span>
              <span className="title">GoAnywhere Configuration</span>
              <span className="legacy">Project Management (26+ Projects)</span>
            </Link>
            <Link to="/resolve" className="menu-card accent">
              <span className="opt">81</span>
              <span className="title">Resolve Value (Consumption)</span>
              <span className="legacy">UT2061-UT2087</span>
            </Link>
          </>
        )}
        {allMenuCards.length === 0 && (
          <div className="menu-card" style={{ opacity: 0.5 }}>
            <span className="legacy">No matching items found</span>
          </div>
        )}
      </div>
    </div>
  );
}
