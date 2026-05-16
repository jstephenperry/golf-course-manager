import { Link } from "react-router-dom";
import { useStore } from "../data/store";
import { formatMoney, tabTotals } from "../data/utils";

const todayIso = () => new Date().toISOString().slice(0, 10);

export function Dashboard() {
  const { data } = useStore();
  const today = todayIso();

  const todaysTeeTimes = data.teeTimes
    .filter((t) => t.date === today && t.status !== "Cancelled")
    .sort((a, b) => a.time.localeCompare(b.time));

  const activeMembers = data.members.filter((m) => m.active).length;
  const openMaintenance = data.maintenance.filter(
    (m) => m.status !== "Completed",
  ).length;
  const lowStock = data.products.filter((p) => p.stock <= p.reorderLevel);
  const openTabs = data.tabs.filter((t) => t.status === "Open");
  const openTabsBalance = openTabs.reduce(
    (sum, t) => sum + tabTotals(t).balance,
    0,
  );
  const upcomingTournaments = data.tournaments
    .filter((t) => t.date >= today && t.status !== "Cancelled")
    .sort((a, b) => a.date.localeCompare(b.date))
    .slice(0, 3);
  const todayShifts = data.shifts.filter((s) => s.date === today);

  const courseName = (id: string) =>
    data.courses.find((c) => c.id === id)?.name ?? "Unknown";
  const memberName = (id: string) => {
    const m = data.members.find((x) => x.id === id);
    return m ? `${m.firstName} ${m.lastName}` : "Guest";
  };
  const staffName = (id: string) => {
    const s = data.staff.find((x) => x.id === id);
    return s ? `${s.firstName} ${s.lastName}` : "Unassigned";
  };

  return (
    <div className="stack">
      <div className="grid cols-5">
        <div className="kpi accent">
          <span className="label">Tee Times Today</span>
          <span className="value">{todaysTeeTimes.length}</span>
          <span className="delta">
            {
              todaysTeeTimes.filter((t) => t.status === "Checked In").length
            }{" "}
            checked in
          </span>
        </div>
        <div className="kpi">
          <span className="label">Active Members</span>
          <span className="value">{activeMembers}</span>
          <span className="delta">of {data.members.length} total</span>
        </div>
        <div className="kpi">
          <span className="label">Open Maintenance</span>
          <span className="value">{openMaintenance}</span>
          <span className="delta">
            {
              data.maintenance.filter(
                (m) => m.priority === "High" && m.status !== "Completed",
              ).length
            }{" "}
            high priority
          </span>
        </div>
        <div className="kpi">
          <span className="label">Low Stock Items</span>
          <span className="value">{lowStock.length}</span>
          <span className="delta">in pro shop</span>
        </div>
        <div className="kpi">
          <span className="label">Open Tabs</span>
          <span className="value">{openTabs.length}</span>
          <span className="delta">
            outstanding {formatMoney(openTabsBalance)}
          </span>
        </div>
      </div>

      <div className="grid cols-2">
        <div className="card">
          <div className="row between" style={{ marginBottom: 10 }}>
            <h2 style={{ margin: 0 }}>Today's Tee Sheet</h2>
            <Link to="/tee-times" className="btn sm secondary">
              Manage
            </Link>
          </div>
          {todaysTeeTimes.length === 0 ? (
            <div className="empty">No tee times scheduled for today.</div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Course</th>
                  <th>Players</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {todaysTeeTimes.map((t) => (
                  <tr key={t.id}>
                    <td>
                      <strong>{t.time}</strong>
                    </td>
                    <td>{courseName(t.courseId)}</td>
                    <td>
                      {t.players.length === 0
                        ? "—"
                        : t.players.map(memberName).join(", ")}
                    </td>
                    <td>
                      <span
                        className={`pill ${
                          t.status === "Checked In"
                            ? "green"
                            : t.status === "Completed"
                              ? "gray"
                              : "gold"
                        }`}
                      >
                        {t.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="card">
          <div className="row between" style={{ marginBottom: 10 }}>
            <h2 style={{ margin: 0 }}>Today on the Crew</h2>
            <Link to="/staff" className="btn sm secondary">
              Schedule
            </Link>
          </div>
          {todayShifts.length === 0 ? (
            <div className="empty">No shifts scheduled today.</div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Staff</th>
                  <th>Role</th>
                  <th>Hours</th>
                </tr>
              </thead>
              <tbody>
                {todayShifts.map((s) => {
                  const member = data.staff.find((x) => x.id === s.staffId);
                  return (
                    <tr key={s.id}>
                      <td>{staffName(s.staffId)}</td>
                      <td>
                        <span className="pill blue">{member?.role ?? "—"}</span>
                      </td>
                      <td>
                        {s.start}–{s.end}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="grid cols-2">
        <div className="card">
          <div className="row between" style={{ marginBottom: 10 }}>
            <h2 style={{ margin: 0 }}>Upcoming Tournaments</h2>
            <Link to="/tournaments" className="btn sm secondary">
              View all
            </Link>
          </div>
          {upcomingTournaments.length === 0 ? (
            <div className="empty">Nothing on the books.</div>
          ) : (
            <ul style={{ margin: 0, padding: 0, listStyle: "none" }}>
              {upcomingTournaments.map((t) => (
                <li
                  key={t.id}
                  style={{
                    padding: "10px 0",
                    borderBottom: "1px solid var(--border)",
                  }}
                >
                  <div className="row between">
                    <div>
                      <strong>{t.name}</strong>
                      <div className="muted" style={{ fontSize: 12 }}>
                        {t.date} · {courseName(t.courseId)} · {t.format}
                      </div>
                    </div>
                    <div className="row">
                      <span className="pill gold">
                        {t.registered.length}/{t.maxPlayers}
                      </span>
                      <span className="pill">${t.entryFee}</span>
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="card">
          <div className="row between" style={{ marginBottom: 10 }}>
            <h2 style={{ margin: 0 }}>Restock Alerts</h2>
            <Link to="/pro-shop" className="btn sm secondary">
              Open Pro Shop
            </Link>
          </div>
          {lowStock.length === 0 ? (
            <div className="empty">Inventory levels look good.</div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Stock</th>
                  <th>Reorder At</th>
                </tr>
              </thead>
              <tbody>
                {lowStock.map((p) => (
                  <tr key={p.id}>
                    <td>{p.name}</td>
                    <td>
                      <span className="pill red">{p.stock}</span>
                    </td>
                    <td>{p.reorderLevel}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}
