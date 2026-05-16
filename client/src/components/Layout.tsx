import { useRef } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useStore } from "../data/store";
import { useToaster } from "./Toaster";

const NAV = [
  { to: "/", label: "Dashboard", icon: "◎" },
  { to: "/tee-times", label: "Tee Times", icon: "⛳" },
  { to: "/members", label: "Members", icon: "👤" },
  { to: "/courses", label: "Courses", icon: "🏞" },
  { to: "/staff", label: "Staff", icon: "👥" },
  { to: "/pro-shop", label: "Pro Shop", icon: "🛍" },
  { to: "/tabs", label: "Player Tabs", icon: "🧾" },
  { to: "/tournaments", label: "Tournaments", icon: "🏆" },
  { to: "/maintenance", label: "Maintenance", icon: "🛠" },
];

export function Layout() {
  const { reset, clear, data, exportSnapshot, importSnapshot, loading, error } =
    useStore();
  const toaster = useToaster();
  const fileInput = useRef<HTMLInputElement>(null);
  const location = useLocation();
  const current = NAV.find((n) =>
    n.to === "/" ? location.pathname === "/" : location.pathname.startsWith(n.to),
  );

  const hasData =
    data.courses.length +
      data.members.length +
      data.teeTimes.length +
      data.staff.length +
      data.products.length +
      data.tournaments.length +
      data.maintenance.length >
    0;

  const downloadBackup = async () => {
    const snap = await exportSnapshot();
    if (!snap) return;
    const blob = new Blob([JSON.stringify(snap, null, 2)], {
      type: "application/json",
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    const ts = new Date()
      .toISOString()
      .replace(/[:.]/g, "-")
      .slice(0, 19);
    a.href = url;
    a.download = `fairway-hq-backup-${ts}.json`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
    toaster.push({ kind: "success", message: "Backup downloaded" });
  };

  const onFile = async (file: File) => {
    try {
      const text = await file.text();
      const parsed = JSON.parse(text);
      await importSnapshot(parsed);
    } catch {
      toaster.push({
        kind: "error",
        message: "Could not parse that file",
        detail: "Expected a JSON backup",
      });
    }
  };

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
          <div className="server-status">
            <span
              className={`status-dot ${
                error ? "bad" : loading ? "warn" : "ok"
              }`}
              aria-hidden
            />
            {error ? "API offline" : loading ? "Syncing…" : "API online"}
          </div>
          <button
            className="reset-btn"
            onClick={() => {
              if (
                hasData &&
                !window.confirm(
                  "Replace current data with the sample dataset?",
                )
              )
                return;
              void reset();
            }}
          >
            Load sample data
          </button>
          <button className="reset-btn" onClick={downloadBackup}>
            Download backup
          </button>
          <button
            className="reset-btn"
            onClick={() => fileInput.current?.click()}
          >
            Restore from backup…
          </button>
          <input
            ref={fileInput}
            type="file"
            accept="application/json"
            style={{ display: "none" }}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) void onFile(f);
              e.target.value = "";
            }}
          />
          {hasData && (
            <button
              className="reset-btn"
              onClick={() => {
                if (
                  window.confirm(
                    "Clear ALL data on the server? This cannot be undone.",
                  )
                ) {
                  void clear();
                }
              }}
            >
              Clear all data
            </button>
          )}
          <div style={{ marginTop: 8 }}>
            v{__APP_VERSION__} · build {__BUILD_TIME__}
          </div>
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
          {error && (
            <div className="banner error" role="alert">
              {error}{" "}
              <button
                className="btn sm secondary"
                onClick={() => window.location.reload()}
              >
                Retry
              </button>
            </div>
          )}
          <Outlet />
        </main>
      </div>
    </div>
  );
}
