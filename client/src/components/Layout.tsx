import { useRef, type ReactNode } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import {
  COURSES_READ,
  MAINTENANCE_READ,
  MEMBERS_READ,
  PRODUCTS_READ,
  STAFF_READ,
  TABS_READ,
  TEE_TIMES_READ,
  TOURNAMENTS_READ,
} from "../auth/permissions";
import { useStore } from "../data/store";
import { useToaster } from "./Toaster";

interface NavItem {
  to: string;
  label: string;
  icon: string;
  /** Permission required to see this item; omit for "any authed user". */
  permission?: string;
}

const NAV: NavItem[] = [
  { to: "/", label: "Dashboard", icon: "◎" },
  { to: "/tee-times", label: "Tee Times", icon: "⛳", permission: TEE_TIMES_READ },
  { to: "/members", label: "Members", icon: "👤", permission: MEMBERS_READ },
  { to: "/courses", label: "Courses", icon: "🏞", permission: COURSES_READ },
  { to: "/staff", label: "Staff", icon: "👥", permission: STAFF_READ },
  { to: "/pro-shop", label: "Pro Shop", icon: "🛍", permission: PRODUCTS_READ },
  { to: "/tabs", label: "Player Tabs", icon: "🧾", permission: TABS_READ },
  {
    to: "/tournaments",
    label: "Tournaments",
    icon: "🏆",
    permission: TOURNAMENTS_READ,
  },
  {
    to: "/maintenance",
    label: "Maintenance",
    icon: "🛠",
    permission: MAINTENANCE_READ,
  },
];

export function Layout({ children }: { children?: ReactNode }) {
  const { clear, data, exportSnapshot, importSnapshot, loading, error } =
    useStore();
  const toaster = useToaster();
  const navigate = useNavigate();
  const fileInput = useRef<HTMLInputElement>(null);
  const location = useLocation();
  const { user, roles, hasPermission, logout, isAuthenticated } = useAuth();
  // Hide nav items the user can't enter; the Dashboard entry has no
  // permission gate (it's always visible to authenticated users).
  const visibleNav = NAV.filter(
    (n) => !n.permission || hasPermission(n.permission),
  );
  const current = visibleNav.find((n) =>
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
        {visibleNav.map((item) => (
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
          {isAuthenticated && user && (
            <div className="current-user">
              <div className="current-user-name">
                {user.name ?? user.username}
              </div>
              <div className="current-user-role">
                {roles.length > 0 ? roles.join(" · ") : "no role"}
              </div>
              <button
                className="reset-btn"
                onClick={() => {
                  void logout();
                }}
                style={{ marginTop: 6 }}
              >
                Sign out
              </button>
            </div>
          )}
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
            onClick={() => navigate("/import")}
          >
            Import data…
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
          {children ?? <Outlet />}
        </main>
      </div>
    </div>
  );
}
