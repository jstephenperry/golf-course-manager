import { useMemo, useState } from "react";
import { Modal } from "../components/Modal";
import { uid, useStore } from "../data/store";
import type {
  DayOfWeek,
  Shift,
  StaffMember,
  StaffRole,
  WeeklyTemplate,
} from "../data/types";
import {
  DAY_FULL,
  DAY_LABELS,
  addDays,
  formatShortDate,
  formatWeekRange,
  isoDate,
  shiftHours,
  shiftsForDate,
  startOfWeek,
  weekDates,
} from "../data/utils";

const ROLES: StaffRole[] = [
  "Manager",
  "Pro",
  "Assistant Pro",
  "Greenkeeper",
  "Maintenance",
  "Pro Shop",
  "Caddie Master",
  "Server",
];

const DOW: DayOfWeek[] = [1, 2, 3, 4, 5, 6, 0];

const blankStaff = (): Omit<StaffMember, "id"> => ({
  firstName: "",
  lastName: "",
  role: "Pro Shop",
  email: "",
  phone: "",
  hourlyRate: 18,
  active: true,
});

const blankShift = (date: string, staffId: string): Omit<Shift, "id"> => ({
  staffId,
  date,
  start: "08:00",
  end: "16:00",
  notes: "",
});

const blankTemplate = (staffId: string): Omit<WeeklyTemplate, "id"> => ({
  staffId,
  dayOfWeek: 1,
  start: "08:00",
  end: "16:00",
  notes: "",
});

type Tab = "roster" | "weekly" | "coverage" | "templates";

export function Staff() {
  const { data, update } = useStore();
  const [tab, setTab] = useState<Tab>("weekly");
  const [weekAnchor, setWeekAnchor] = useState(
    startOfWeek(isoDate(new Date()), 1),
  );

  const weekStart = startOfWeek(weekAnchor, 1);
  const week = weekDates(weekStart);

  /* -------- Staff CRUD -------- */
  const [staffEditing, setStaffEditing] = useState<StaffMember | null>(null);
  const [staffCreating, setStaffCreating] = useState(false);
  const [staffForm, setStaffForm] = useState<Omit<StaffMember, "id">>(
    blankStaff(),
  );

  const saveStaff = () => {
    if (!staffForm.firstName.trim() || !staffForm.lastName.trim()) {
      alert("Name is required.");
      return;
    }
    if (staffEditing) {
      update("staff", (list) =>
        list.map((s) =>
          s.id === staffEditing.id ? { ...staffEditing, ...staffForm } : s,
        ),
      );
      setStaffEditing(null);
    } else {
      update("staff", (list) => [...list, { id: uid("s"), ...staffForm }]);
      setStaffCreating(false);
    }
  };

  const removeStaff = (id: string) => {
    if (
      !window.confirm(
        "Remove this staff member, along with their shifts and templates?",
      )
    )
      return;
    update("staff", (list) => list.filter((s) => s.id !== id));
    update("shifts", (list) => list.filter((sh) => sh.staffId !== id));
    update("weeklyTemplates", (list) => list.filter((t) => t.staffId !== id));
    setStaffEditing(null);
  };

  /* -------- Shift CRUD -------- */
  const [shiftEditing, setShiftEditing] = useState<Shift | null>(null);
  const [shiftCreating, setShiftCreating] = useState(false);
  const [shiftForm, setShiftForm] = useState<Omit<Shift, "id">>(
    blankShift(weekStart, data.staff[0]?.id ?? ""),
  );

  const openCreateShift = (date: string, staffId: string) => {
    setShiftForm(blankShift(date, staffId));
    setShiftCreating(true);
  };
  const openEditShift = (sh: Shift) => {
    setShiftEditing(sh);
    setShiftForm({ ...sh });
  };
  const saveShift = () => {
    if (!shiftForm.staffId) {
      alert("Select a staff member.");
      return;
    }
    if (shiftEditing) {
      update("shifts", (list) =>
        list.map((s) =>
          s.id === shiftEditing.id ? { ...shiftEditing, ...shiftForm } : s,
        ),
      );
      setShiftEditing(null);
    } else {
      update("shifts", (list) => [...list, { id: uid("sh"), ...shiftForm }]);
      setShiftCreating(false);
    }
  };
  const removeShift = (id: string) => {
    update("shifts", (list) => list.filter((s) => s.id !== id));
    setShiftEditing(null);
  };

  /* -------- Template CRUD -------- */
  const [tplEditing, setTplEditing] = useState<WeeklyTemplate | null>(null);
  const [tplCreating, setTplCreating] = useState(false);
  const [tplForm, setTplForm] = useState<Omit<WeeklyTemplate, "id">>(
    blankTemplate(data.staff[0]?.id ?? ""),
  );

  const saveTemplate = () => {
    if (!tplForm.staffId) {
      alert("Select a staff member.");
      return;
    }
    if (tplEditing) {
      update("weeklyTemplates", (list) =>
        list.map((t) =>
          t.id === tplEditing.id ? { ...tplEditing, ...tplForm } : t,
        ),
      );
      setTplEditing(null);
    } else {
      update("weeklyTemplates", (list) => [
        ...list,
        { id: uid("wt"), ...tplForm },
      ]);
      setTplCreating(false);
    }
  };
  const removeTemplate = (id: string) => {
    update("weeklyTemplates", (list) => list.filter((t) => t.id !== id));
    setTplEditing(null);
  };

  const applyTemplatesToWeek = () => {
    if (data.weeklyTemplates.length === 0) {
      alert("No templates defined yet.");
      return;
    }
    const existing = new Set(
      data.shifts.map(
        (s) => `${s.staffId}|${s.date}|${s.start}|${s.end}`,
      ),
    );
    const created: Shift[] = [];
    for (const tpl of data.weeklyTemplates) {
      const offset = (tpl.dayOfWeek - 1 + 7) % 7;
      const date = addDays(weekStart, offset);
      const key = `${tpl.staffId}|${date}|${tpl.start}|${tpl.end}`;
      if (existing.has(key)) continue;
      existing.add(key);
      created.push({
        id: uid("sh"),
        staffId: tpl.staffId,
        date,
        start: tpl.start,
        end: tpl.end,
        notes: tpl.notes,
      });
    }
    if (created.length === 0) {
      alert("All template shifts already exist for this week.");
      return;
    }
    update("shifts", (list) => [...list, ...created]);
    alert(`Applied ${created.length} shift${created.length === 1 ? "" : "s"}.`);
  };

  /* -------- Derived: coverage -------- */
  const rolesInUse = useMemo(() => {
    const set = new Set<StaffRole>();
    data.staff.forEach((s) => set.add(s.role));
    return ROLES.filter((r) => set.has(r));
  }, [data.staff]);

  const coverage = useMemo(() => {
    const table = new Map<
      StaffRole,
      { staff: Set<string>; hours: number }[]
    >();
    rolesInUse.forEach((r) =>
      table.set(
        r,
        week.map(() => ({ staff: new Set<string>(), hours: 0 })),
      ),
    );
    for (let i = 0; i < week.length; i++) {
      const day = week[i];
      for (const sh of shiftsForDate(data.shifts, day)) {
        const staff = data.staff.find((s) => s.id === sh.staffId);
        if (!staff) continue;
        const cells = table.get(staff.role);
        if (!cells) continue;
        cells[i].staff.add(staff.id);
        cells[i].hours += shiftHours(sh.start, sh.end);
      }
    }
    return table;
  }, [rolesInUse, week, data.shifts, data.staff]);

  /* -------- Render helpers -------- */
  const weekToolbar = (
    <div className="row" style={{ flexWrap: "wrap", gap: 8 }}>
      <button
        className="btn sm secondary"
        onClick={() => setWeekAnchor(addDays(weekStart, -7))}
      >
        ← Prev
      </button>
      <button
        className="btn sm secondary"
        onClick={() => setWeekAnchor(startOfWeek(isoDate(new Date()), 1))}
      >
        This week
      </button>
      <button
        className="btn sm secondary"
        onClick={() => setWeekAnchor(addDays(weekStart, 7))}
      >
        Next →
      </button>
      <strong style={{ marginLeft: 8 }}>{formatWeekRange(weekStart)}</strong>
    </div>
  );

  return (
    <div className="stack">
      <div className="toolbar">
        <div className="toolbar-left">
          {(["roster", "weekly", "coverage", "templates"] as Tab[]).map((t) => (
            <button
              key={t}
              className={`btn ${tab === t ? "" : "secondary"}`}
              onClick={() => setTab(t)}
            >
              {t === "roster"
                ? "Roster"
                : t === "weekly"
                  ? "Weekly Schedule"
                  : t === "coverage"
                    ? "Department Coverage"
                    : "Weekly Templates"}
            </button>
          ))}
        </div>
        {tab === "roster" && (
          <button
            className="btn"
            onClick={() => {
              setStaffForm(blankStaff());
              setStaffCreating(true);
            }}
          >
            + Add Staff
          </button>
        )}
        {tab === "templates" && (
          <button
            className="btn"
            onClick={() => {
              setTplForm(blankTemplate(data.staff[0]?.id ?? ""));
              setTplCreating(true);
            }}
          >
            + New Template
          </button>
        )}
      </div>

      {/* ---------- Roster ---------- */}
      {tab === "roster" && (
        <div className="card">
          {data.staff.length === 0 ? (
            <div className="empty">No staff yet. Add your first hire.</div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Role</th>
                  <th>Email / Phone</th>
                  <th>Hourly</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {data.staff.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <strong>
                        {s.firstName} {s.lastName}
                      </strong>
                    </td>
                    <td>
                      <span className="pill blue">{s.role}</span>
                    </td>
                    <td>
                      <div>{s.email}</div>
                      <div className="muted" style={{ fontSize: 12 }}>
                        {s.phone}
                      </div>
                    </td>
                    <td>${s.hourlyRate.toFixed(2)}</td>
                    <td>
                      <span className={`pill ${s.active ? "green" : "gray"}`}>
                        {s.active ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td>
                      <div className="table-actions">
                        <button
                          className="btn sm secondary"
                          onClick={() => {
                            setStaffEditing(s);
                            setStaffForm({ ...s });
                          }}
                        >
                          Edit
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* ---------- Weekly Schedule ---------- */}
      {tab === "weekly" && (
        <div className="card">
          <div className="toolbar">
            {weekToolbar}
            <div className="row">
              <button
                className="btn secondary"
                onClick={applyTemplatesToWeek}
                title="Materialize weekly templates into shifts for this week"
              >
                Apply templates to week
              </button>
            </div>
          </div>
          {data.staff.length === 0 ? (
            <div className="empty">
              Add staff to begin scheduling shifts.
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table week-grid">
                <thead>
                  <tr>
                    <th style={{ width: 180 }}>Staff</th>
                    {week.map((d, i) => (
                      <th key={d}>
                        <div>{DAY_LABELS[DOW[i]]}</div>
                        <div className="muted" style={{ fontWeight: 400 }}>
                          {formatShortDate(d).split(" ")[1]}
                        </div>
                      </th>
                    ))}
                    <th style={{ width: 80 }}>Total</th>
                  </tr>
                </thead>
                <tbody>
                  {data.staff.map((s) => {
                    let total = 0;
                    return (
                      <tr key={s.id}>
                        <td>
                          <strong>
                            {s.firstName} {s.lastName}
                          </strong>
                          <div>
                            <span className="pill blue">{s.role}</span>
                          </div>
                        </td>
                        {week.map((d) => {
                          const cellShifts = data.shifts
                            .filter(
                              (sh) => sh.staffId === s.id && sh.date === d,
                            )
                            .sort((a, b) => a.start.localeCompare(b.start));
                          const cellHours = cellShifts.reduce(
                            (sum, sh) => sum + shiftHours(sh.start, sh.end),
                            0,
                          );
                          total += cellHours;
                          return (
                            <td
                              key={d}
                              className="week-cell"
                              onClick={(e) => {
                                if (
                                  (e.target as HTMLElement).closest(
                                    ".shift-pill",
                                  )
                                )
                                  return;
                                openCreateShift(d, s.id);
                              }}
                            >
                              {cellShifts.length === 0 ? (
                                <span className="cell-add">+</span>
                              ) : (
                                <div className="cell-shifts">
                                  {cellShifts.map((sh) => (
                                    <button
                                      key={sh.id}
                                      className="shift-pill"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        openEditShift(sh);
                                      }}
                                      title={sh.notes || ""}
                                    >
                                      {sh.start}–{sh.end}
                                    </button>
                                  ))}
                                </div>
                              )}
                            </td>
                          );
                        })}
                        <td>
                          <strong>{total.toFixed(1)}h</strong>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* ---------- Department Coverage ---------- */}
      {tab === "coverage" && (
        <div className="card">
          <div className="toolbar">{weekToolbar}</div>
          {rolesInUse.length === 0 ? (
            <div className="empty">
              Add staff (with roles) to see coverage by department.
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Department</th>
                    {week.map((d, i) => (
                      <th key={d} style={{ textAlign: "center" }}>
                        <div>{DAY_LABELS[DOW[i]]}</div>
                        <div className="muted" style={{ fontWeight: 400 }}>
                          {formatShortDate(d).split(" ")[1]}
                        </div>
                      </th>
                    ))}
                    <th style={{ textAlign: "right" }}>Week total</th>
                  </tr>
                </thead>
                <tbody>
                  {rolesInUse.map((role) => {
                    const cells = coverage.get(role)!;
                    const totalHours = cells.reduce(
                      (sum, c) => sum + c.hours,
                      0,
                    );
                    return (
                      <tr key={role}>
                        <td>
                          <span className="pill blue">{role}</span>
                        </td>
                        {cells.map((c, i) => {
                          const cls =
                            c.staff.size === 0
                              ? "coverage-cell empty"
                              : c.staff.size === 1
                                ? "coverage-cell low"
                                : "coverage-cell good";
                          return (
                            <td
                              key={i}
                              className={cls}
                              style={{ textAlign: "center" }}
                            >
                              {c.staff.size === 0 ? (
                                <span className="muted">—</span>
                              ) : (
                                <>
                                  <div>
                                    <strong>{c.staff.size}</strong> staff
                                  </div>
                                  <div
                                    className="muted"
                                    style={{ fontSize: 11 }}
                                  >
                                    {c.hours.toFixed(1)}h
                                  </div>
                                </>
                              )}
                            </td>
                          );
                        })}
                        <td style={{ textAlign: "right" }}>
                          <strong>{totalHours.toFixed(1)}h</strong>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              <div
                className="muted"
                style={{ fontSize: 12, marginTop: 8, textAlign: "right" }}
              >
                Yellow = 1 person covering · Red = no coverage
              </div>
            </div>
          )}
        </div>
      )}

      {/* ---------- Weekly Templates ---------- */}
      {tab === "templates" && (
        <div className="card">
          <div className="muted" style={{ marginBottom: 12 }}>
            Define a person's recurring weekly pattern. Use "Apply templates to
            week" in the Weekly Schedule tab to materialize them into shifts.
          </div>
          {data.staff.length === 0 ? (
            <div className="empty">Add staff first.</div>
          ) : (
            <div className="stack">
              {data.staff.map((s) => {
                const tpls = data.weeklyTemplates
                  .filter((t) => t.staffId === s.id)
                  .sort(
                    (a, b) =>
                      ((a.dayOfWeek - 1 + 7) % 7) -
                        ((b.dayOfWeek - 1 + 7) % 7) ||
                      a.start.localeCompare(b.start),
                  );
                const totalHours = tpls.reduce(
                  (sum, t) => sum + shiftHours(t.start, t.end),
                  0,
                );
                return (
                  <div
                    key={s.id}
                    style={{
                      paddingBottom: 12,
                      borderBottom: "1px solid var(--border)",
                    }}
                  >
                    <div className="row between" style={{ marginBottom: 6 }}>
                      <div>
                        <strong>
                          {s.firstName} {s.lastName}
                        </strong>{" "}
                        <span className="pill blue">{s.role}</span>
                      </div>
                      <div className="muted" style={{ fontSize: 12 }}>
                        {tpls.length} template{tpls.length === 1 ? "" : "s"} ·{" "}
                        {totalHours.toFixed(1)}h/week
                      </div>
                    </div>
                    {tpls.length === 0 ? (
                      <div className="muted" style={{ fontSize: 13 }}>
                        No recurring schedule.
                      </div>
                    ) : (
                      <div className="chip-list">
                        {tpls.map((t) => (
                          <button
                            key={t.id}
                            className="shift-pill"
                            onClick={() => {
                              setTplEditing(t);
                              setTplForm({ ...t });
                            }}
                            title={t.notes || ""}
                          >
                            {DAY_LABELS[t.dayOfWeek]} {t.start}–{t.end}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* ---------- Modals ---------- */}
      {(staffCreating || staffEditing) && (
        <Modal
          title={staffEditing ? "Edit Staff" : "Add Staff"}
          onClose={() => {
            setStaffEditing(null);
            setStaffCreating(false);
          }}
          onSubmit={saveStaff}
        >
          <div className="grid cols-2">
            <div className="field">
              <label>First name</label>
              <input
                className="input"
                value={staffForm.firstName}
                onChange={(e) =>
                  setStaffForm({ ...staffForm, firstName: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Last name</label>
              <input
                className="input"
                value={staffForm.lastName}
                onChange={(e) =>
                  setStaffForm({ ...staffForm, lastName: e.target.value })
                }
              />
            </div>
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Email</label>
              <input
                className="input"
                value={staffForm.email}
                onChange={(e) =>
                  setStaffForm({ ...staffForm, email: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Phone</label>
              <input
                className="input"
                value={staffForm.phone}
                onChange={(e) =>
                  setStaffForm({ ...staffForm, phone: e.target.value })
                }
              />
            </div>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Role</label>
              <select
                className="select"
                value={staffForm.role}
                onChange={(e) =>
                  setStaffForm({
                    ...staffForm,
                    role: e.target.value as StaffRole,
                  })
                }
              >
                {ROLES.map((r) => (
                  <option key={r}>{r}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Hourly rate</label>
              <input
                className="input"
                type="number"
                step="0.5"
                value={staffForm.hourlyRate}
                onChange={(e) =>
                  setStaffForm({
                    ...staffForm,
                    hourlyRate: Number(e.target.value),
                  })
                }
              />
            </div>
            <div className="field">
              <label>Status</label>
              <select
                className="select"
                value={staffForm.active ? "active" : "inactive"}
                onChange={(e) =>
                  setStaffForm({
                    ...staffForm,
                    active: e.target.value === "active",
                  })
                }
              >
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
              </select>
            </div>
          </div>
          {staffEditing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => removeStaff(staffEditing.id)}
              >
                Remove staff
              </button>
            </div>
          )}
        </Modal>
      )}

      {(shiftCreating || shiftEditing) && (
        <Modal
          title={shiftEditing ? "Edit Shift" : "Schedule Shift"}
          onClose={() => {
            setShiftCreating(false);
            setShiftEditing(null);
          }}
          onSubmit={saveShift}
        >
          <div className="field">
            <label>Staff member</label>
            <select
              className="select"
              value={shiftForm.staffId}
              onChange={(e) =>
                setShiftForm({ ...shiftForm, staffId: e.target.value })
              }
            >
              <option value="">— Select —</option>
              {data.staff
                .filter((s) => s.active)
                .map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.firstName} {s.lastName} · {s.role}
                  </option>
                ))}
            </select>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Date</label>
              <input
                className="input"
                type="date"
                value={shiftForm.date}
                onChange={(e) =>
                  setShiftForm({ ...shiftForm, date: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Start</label>
              <input
                className="input"
                type="time"
                value={shiftForm.start}
                onChange={(e) =>
                  setShiftForm({ ...shiftForm, start: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>End</label>
              <input
                className="input"
                type="time"
                value={shiftForm.end}
                onChange={(e) =>
                  setShiftForm({ ...shiftForm, end: e.target.value })
                }
              />
            </div>
          </div>
          <div className="field">
            <label>Notes</label>
            <textarea
              className="textarea"
              rows={2}
              value={shiftForm.notes}
              onChange={(e) =>
                setShiftForm({ ...shiftForm, notes: e.target.value })
              }
            />
          </div>
          {shiftEditing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => removeShift(shiftEditing.id)}
              >
                Delete shift
              </button>
            </div>
          )}
        </Modal>
      )}

      {(tplCreating || tplEditing) && (
        <Modal
          title={tplEditing ? "Edit Template" : "New Weekly Template"}
          onClose={() => {
            setTplCreating(false);
            setTplEditing(null);
          }}
          onSubmit={saveTemplate}
        >
          <div className="field">
            <label>Staff member</label>
            <select
              className="select"
              value={tplForm.staffId}
              onChange={(e) =>
                setTplForm({ ...tplForm, staffId: e.target.value })
              }
            >
              <option value="">— Select —</option>
              {data.staff.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.firstName} {s.lastName} · {s.role}
                </option>
              ))}
            </select>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Day of week</label>
              <select
                className="select"
                value={tplForm.dayOfWeek}
                onChange={(e) =>
                  setTplForm({
                    ...tplForm,
                    dayOfWeek: Number(e.target.value) as DayOfWeek,
                  })
                }
              >
                {DOW.map((d) => (
                  <option key={d} value={d}>
                    {DAY_FULL[d]}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Start</label>
              <input
                className="input"
                type="time"
                value={tplForm.start}
                onChange={(e) =>
                  setTplForm({ ...tplForm, start: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>End</label>
              <input
                className="input"
                type="time"
                value={tplForm.end}
                onChange={(e) =>
                  setTplForm({ ...tplForm, end: e.target.value })
                }
              />
            </div>
          </div>
          <div className="field">
            <label>Notes</label>
            <textarea
              className="textarea"
              rows={2}
              value={tplForm.notes}
              onChange={(e) =>
                setTplForm({ ...tplForm, notes: e.target.value })
              }
            />
          </div>
          {tplEditing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => removeTemplate(tplEditing.id)}
              >
                Delete template
              </button>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
