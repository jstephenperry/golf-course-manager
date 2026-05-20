import { useMemo, useState } from "react";
import { MAINTENANCE_WRITE } from "../auth/permissions";
import { RequirePermission } from "../auth/RequirePermission";
import { Modal } from "../components/Modal";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type { MaintenanceCategory, MaintenanceTask } from "../data/types";
import { isoDate } from "../data/utils";

const CATEGORIES: MaintenanceCategory[] = [
  "Mowing",
  "Irrigation",
  "Aeration",
  "Bunker",
  "Greens",
  "Tees",
  "Equipment",
  "Other",
];

const blank = (courseId: string, staffId: string): Omit<MaintenanceTask, "id"> => ({
  title: "",
  category: "Greens",
  courseId,
  assignedTo: staffId,
  dueDate: isoDate(new Date()),
  priority: "Medium",
  status: "Open",
  notes: "",
});

export function Maintenance() {
  const { data, maintenance: api } = useStore();
  const toaster = useToaster();
  const [busy, setBusy] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string>("active");
  const [courseFilter, setCourseFilter] = useState<string>("all");
  const [editing, setEditing] = useState<MaintenanceTask | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<MaintenanceTask, "id">>(
    blank(data.courses[0]?.id ?? "", data.staff[0]?.id ?? ""),
  );

  const filtered = useMemo(() => {
    return data.maintenance
      .filter((m) => {
        if (statusFilter === "active" && m.status === "Completed") return false;
        if (statusFilter !== "all" && statusFilter !== "active" && m.status !== statusFilter)
          return false;
        if (courseFilter !== "all" && m.courseId !== courseFilter) return false;
        return true;
      })
      .sort((a, b) => {
        const priorityOrder = { High: 0, Medium: 1, Low: 2 } as const;
        return (
          priorityOrder[a.priority] - priorityOrder[b.priority] ||
          a.dueDate.localeCompare(b.dueDate)
        );
      });
  }, [data.maintenance, statusFilter, courseFilter]);

  const courseName = (id: string) =>
    data.courses.find((c) => c.id === id)?.name ?? "—";
  const staffName = (id: string) => {
    const s = data.staff.find((x) => x.id === id);
    return s ? `${s.firstName} ${s.lastName}` : "Unassigned";
  };

  const save = async () => {
    if (!form.title.trim()) {
      toaster.push({ kind: "error", message: "Title is required" });
      return;
    }
    setBusy(true);
    const result = editing
      ? await api.update(editing.id, { ...editing, ...form })
      : await api.create(form);
    setBusy(false);
    if (!result) return;
    setEditing(null);
    setCreating(false);
  };

  const remove = async (id: string) => {
    if (!window.confirm("Delete this task?")) return;
    setBusy(true);
    const ok = await api.remove(id);
    setBusy(false);
    if (ok) setEditing(null);
  };

  const setStatus = async (id: string, status: MaintenanceTask["status"]) => {
    const task = data.maintenance.find((m) => m.id === id);
    if (!task) return;
    await api.update(id, { ...task, status });
  };

  return (
    <div className="stack">
      <div className="card">
        <div className="toolbar">
          <div className="toolbar-left">
            <select
              className="select"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="active">Active</option>
              <option value="all">All</option>
              <option value="Open">Open</option>
              <option value="In Progress">In Progress</option>
              <option value="Completed">Completed</option>
            </select>
            <select
              className="select"
              value={courseFilter}
              onChange={(e) => setCourseFilter(e.target.value)}
            >
              <option value="all">All courses</option>
              {data.courses.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <RequirePermission permission={MAINTENANCE_WRITE}>
            <button
              className="btn"
              onClick={() => {
                setForm(
                  blank(data.courses[0]?.id ?? "", data.staff[0]?.id ?? ""),
                );
                setCreating(true);
              }}
            >
              + New Task
            </button>
          </RequirePermission>
        </div>

        {filtered.length === 0 ? (
          <div className="empty">No maintenance tasks match.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Priority</th>
                <th>Task</th>
                <th>Category</th>
                <th>Course</th>
                <th>Assigned</th>
                <th>Due</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((m) => (
                <tr key={m.id}>
                  <td>
                    <span
                      className={`pill ${
                        m.priority === "High"
                          ? "red"
                          : m.priority === "Medium"
                            ? "gold"
                            : "gray"
                      }`}
                    >
                      {m.priority}
                    </span>
                  </td>
                  <td>
                    <strong>{m.title}</strong>
                    {m.notes && (
                      <div className="muted" style={{ fontSize: 12 }}>
                        {m.notes}
                      </div>
                    )}
                  </td>
                  <td>
                    <span className="pill">{m.category}</span>
                  </td>
                  <td>{courseName(m.courseId)}</td>
                  <td>{staffName(m.assignedTo)}</td>
                  <td>{m.dueDate}</td>
                  <td>
                    <select
                      className="select"
                      value={m.status}
                      onChange={(e) =>
                        setStatus(
                          m.id,
                          e.target.value as MaintenanceTask["status"],
                        )
                      }
                    >
                      <option>Open</option>
                      <option>In Progress</option>
                      <option>Completed</option>
                    </select>
                  </td>
                  <td>
                    <div className="table-actions">
                      <RequirePermission permission={MAINTENANCE_WRITE}>
                        <button
                          className="btn sm secondary"
                          onClick={() => {
                            setEditing(m);
                            setForm({ ...m });
                          }}
                        >
                          Edit
                        </button>
                      </RequirePermission>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Task" : "New Maintenance Task"}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={save}
          submitLabel={busy ? "Saving…" : editing ? "Save" : "Add"}
        >
          <div className="field">
            <label>Title</label>
            <input
              className="input"
              value={form.title}
              onChange={(e) => setForm({ ...form, title: e.target.value })}
            />
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Category</label>
              <select
                className="select"
                value={form.category}
                onChange={(e) =>
                  setForm({
                    ...form,
                    category: e.target.value as MaintenanceCategory,
                  })
                }
              >
                {CATEGORIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Course</label>
              <select
                className="select"
                value={form.courseId}
                onChange={(e) =>
                  setForm({ ...form, courseId: e.target.value })
                }
              >
                {data.courses.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Assigned to</label>
              <select
                className="select"
                value={form.assignedTo}
                onChange={(e) =>
                  setForm({ ...form, assignedTo: e.target.value })
                }
              >
                <option value="">Unassigned</option>
                {data.staff
                  .filter((s) => s.active)
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.firstName} {s.lastName}
                    </option>
                  ))}
              </select>
            </div>
            <div className="field">
              <label>Due date</label>
              <input
                className="input"
                type="date"
                value={form.dueDate}
                onChange={(e) =>
                  setForm({ ...form, dueDate: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Priority</label>
              <select
                className="select"
                value={form.priority}
                onChange={(e) =>
                  setForm({
                    ...form,
                    priority: e.target.value as MaintenanceTask["priority"],
                  })
                }
              >
                <option>Low</option>
                <option>Medium</option>
                <option>High</option>
              </select>
            </div>
          </div>
          <div className="field">
            <label>Notes</label>
            <textarea
              className="textarea"
              rows={3}
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
            />
          </div>
          {editing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => remove(editing.id)}
              >
                Delete task
              </button>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
