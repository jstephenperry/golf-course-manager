import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useStore } from "../data/store";

const NAV = [
  { to: "/", label: "Dashboard", icon: "◎" },
  { to: "/tee-times", label: "Tee Times", icon: "⛳" },
  { to: "/members", label: "Members", icon: "👤" },
  { to: "/courses", label: "Courses", icon: "🏞" },
  { to: "/staff", label: "Staff", icon: "👥" },
  { to: "/pro-shop", label: "Pro Shop", icon: "🛍" },
  { to: "/tournaments", label: "Tournaments", icon: "🏆" },
  { to: "/maintenance", label: "Maintenance", icon: "🛠" },
];

export function Layout() {
  const { reset } = useStore();
  const location = useLocation();
  const current = NAV.find((n) =>
    n.to === "/" ? location.pathname === "/" : location.pathname.startsWith(n.to),
  );

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">F</div>
          <div>
            <div className="brand-name">Fairway HQ</div>
            <div className="brand-sub">Golf Course Manager</div>
          </div>
        </div>
        {NAV.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === "/"}
            className={({ isActive }) =>
              `nav-link${isActive ? " active" : ""}`
            }
          >
            <span className="nav-icon" aria-hidden>
              {item.icon}
            </span>
            {item.label}
          </NavLink>
        ))}
        <div className="footer">
          <button
            className="reset-btn"
            onClick={() => {
              if (
                window.confirm(
                  "Reset all data back to the demo seed? This will wipe local changes.",
                )
              ) {
                reset();
              }
            }}
          >
            Reset demo data
          </button>
          <div style={{ marginTop: 8 }}>v0.1 · local demo</div>
        </div>
      </aside>

      <div className="main">
        <header className="topbar">
          <div>
            <h1>{current?.label ?? "Fairway HQ"}</h1>
            <div className="topbar-sub">
              {new Date().toLocaleDateString(undefined, {
                weekday: "long",
                month: "long",
                day: "numeric",
                year: "numeric",
              })}
            </div>
          </div>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
