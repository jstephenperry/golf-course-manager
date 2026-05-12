import { useMemo, useState } from "react";
import { Modal } from "../components/Modal";
import { uid, useStore } from "../data/store";
import type { Shift, StaffMember, StaffRole } from "../data/types";

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

export function Staff() {
  const { data, update } = useStore();
  const [tab, setTab] = useState<"roster" | "schedule">("roster");
  const [shiftDate, setShiftDate] = useState(
    new Date().toISOString().slice(0, 10),
  );

  const [staffEditing, setStaffEditing] = useState<StaffMember | null>(null);
  const [staffCreating, setStaffCreating] = useState(false);
  const [staffForm, setStaffForm] = useState<Omit<StaffMember, "id">>(
    blankStaff(),
  );

  const [shiftEditing, setShiftEditing] = useState<Shift | null>(null);
  const [shiftCreating, setShiftCreating] = useState(false);
  const [shiftForm, setShiftForm] = useState<Omit<Shift, "id">>(
    blankShift(shiftDate, data.staff[0]?.id ?? ""),
  );

  const shiftsForDate = useMemo(
    () =>
      data.shifts
        .filter((s) => s.date === shiftDate)
        .sort((a, b) => a.start.localeCompare(b.start)),
    [data.shifts, shiftDate],
  );

  const staffName = (id: string) => {
    const s = data.staff.find((x) => x.id === id);
    return s ? `${s.firstName} ${s.lastName}` : "—";
  };

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
    if (!window.confirm("Remove this staff member and their shifts?")) return;
    update("staff", (list) => list.filter((s) => s.id !== id));
    update("shifts", (list) => list.filter((sh) => sh.staffId !== id));
    setStaffEditing(null);
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

  return (
    <div className="stack">
      <div className="toolbar">
        <div className="toolbar-left">
          <button
            className={`btn ${tab === "roster" ? "" : "secondary"}`}
            onClick={() => setTab("roster")}
          >
            Roster
          </button>
          <button
            className={`btn ${tab === "schedule" ? "" : "secondary"}`}
            onClick={() => setTab("schedule")}
          >
            Schedule
          </button>
        </div>
        {tab === "roster" ? (
          <button
            className="btn"
            onClick={() => {
              setStaffForm(blankStaff());
              setStaffCreating(true);
            }}
          >
            + Add Staff
          </button>
        ) : (
          <button
            className="btn"
            onClick={() => {
              setShiftForm(blankShift(shiftDate, data.staff[0]?.id ?? ""));
              setShiftCreating(true);
            }}
          >
            + Schedule Shift
          </button>
        )}
      </div>

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

      {tab === "schedule" && (
        <div className="card">
          <div className="toolbar">
            <div className="field">
              <label>Date</label>
              <input
                className="input"
                type="date"
                value={shiftDate}
                onChange={(e) => setShiftDate(e.target.value)}
              />
            </div>
          </div>
          {shiftsForDate.length === 0 ? (
            <div className="empty">No shifts scheduled for this date.</div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Staff</th>
                  <th>Role</th>
                  <th>Hours</th>
                  <th>Notes</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {shiftsForDate.map((sh) => {
                  const s = data.staff.find((x) => x.id === sh.staffId);
                  return (
                    <tr key={sh.id}>
                      <td>{staffName(sh.staffId)}</td>
                      <td>
                        <span className="pill blue">{s?.role ?? "—"}</span>
                      </td>
                      <td>
                        {sh.start}–{sh.end}
                      </td>
                      <td>
                        <span className="muted">{sh.notes || "—"}</span>
                      </td>
                      <td>
                        <div className="table-actions">
                          <button
                            className="btn sm secondary"
                            onClick={() => {
                              setShiftEditing(sh);
                              setShiftForm({ ...sh });
                            }}
                          >
                            Edit
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}

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
    </div>
  );
}
