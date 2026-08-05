import { Link } from "react-router-dom";
import { menuItems } from "../entities";

export default function MenuPage() {
  return (
    <div>
      <h2>Configuration Menu</h2>
      <p className="subtle">
        Maintenance of the Utility Configuration System (modernized from RPG program UT2000).
      </p>
      <div className="menu-grid">
        {menuItems.map((e, i) => (
          <Link key={e.route} to={`/${e.route}`} className="menu-card">
            <span className="opt">{String(i + 1).padStart(2, "0")}</span>
            <span className="title">{e.title}</span>
            <span className="legacy">{e.legacy}</span>
          </Link>
        ))}
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
      </div>
    </div>
  );
}
