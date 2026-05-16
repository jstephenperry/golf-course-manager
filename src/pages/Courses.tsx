import { useState } from "react";
import { Modal } from "../components/Modal";
import { uid, useStore } from "../data/store";
import type { Course } from "../data/types";

const blank = (): Omit<Course, "id"> => ({
  name: "",
  holes: 18,
  par: 72,
  yardage: 6800,
  rating: 72,
  slope: 130,
  status: "Open",
  openTime: "06:00",
  closeTime: "18:00",
  notes: "",
});

export function Courses() {
  const { data, update } = useStore();
  const [editing, setEditing] = useState<Course | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<Course, "id">>(blank());

  const startCreate = () => {
    setForm(blank());
    setCreating(true);
  };
  const startEdit = (c: Course) => {
    setEditing(c);
    setForm({ ...c });
  };
  const save = () => {
    if (!form.name.trim()) {
      alert("Course name is required.");
      return;
    }
    if (editing) {
      update("courses", (list) =>
        list.map((c) => (c.id === editing.id ? { ...editing, ...form } : c)),
      );
      setEditing(null);
    } else {
      update("courses", (list) => [...list, { id: uid("c"), ...form }]);
      setCreating(false);
    }
  };
  const remove = (id: string) => {
    if (!window.confirm("Delete this course?")) return;
    update("courses", (list) => list.filter((c) => c.id !== id));
    setEditing(null);
  };

  const setStatus = (c: Course, status: Course["status"]) => {
    update("courses", (list) =>
      list.map((x) => (x.id === c.id ? { ...x, status } : x)),
    );
  };

  return (
    <div className="stack">
      <div className="toolbar">
        <div className="muted">
          Manage course details, course rating and conditions.
        </div>
        <button className="btn" onClick={startCreate}>
          + Add Course
        </button>
      </div>

      <div className="grid cols-2">
        {data.courses.map((c) => (
          <div className="card" key={c.id}>
            <div className="row between">
              <div>
                <h3 style={{ marginBottom: 4 }}>{c.name}</h3>
                <div className="muted" style={{ fontSize: 12 }}>
                  {c.holes > 0
                    ? `${c.holes} holes · Par ${c.par} · ${c.yardage.toLocaleString()} yds`
                    : "Practice facility"}
                </div>
              </div>
              <span
                className={`pill ${
                  c.status === "Open"
                    ? "green"
                    : c.status === "Closed"
                      ? "red"
                      : "gold"
                }`}
              >
                {c.status}
              </span>
            </div>

            {c.holes > 0 && (
              <div className="grid cols-3" style={{ marginTop: 12 }}>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Rating</span>
                  <span className="value" style={{ fontSize: 18 }}>
                    {c.rating.toFixed(1)}
                  </span>
                </div>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Slope</span>
                  <span className="value" style={{ fontSize: 18 }}>
                    {c.slope}
                  </span>
                </div>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Holes</span>
                  <span className="value" style={{ fontSize: 18 }}>
                    {c.holes}
                  </span>
                </div>
              </div>
            )}

            {c.holes > 0 && (
              <div className="muted" style={{ fontSize: 12, marginTop: 10 }}>
                Operating hours: {c.openTime || "06:00"} –{" "}
                {c.closeTime || "18:00"}
              </div>
            )}

            {c.notes && (
              <p className="muted" style={{ marginTop: 12 }}>
                {c.notes}
              </p>
            )}

            <div className="row" style={{ marginTop: 12, flexWrap: "wrap" }}>
              <select
                className="select"
                value={c.status}
                onChange={(e) =>
                  setStatus(c, e.target.value as Course["status"])
                }
                style={{ maxWidth: 180 }}
              >
                <option>Open</option>
                <option>Cart Path Only</option>
                <option>Closed</option>
              </select>
              <button
                className="btn sm secondary"
                onClick={() => startEdit(c)}
              >
                Edit details
              </button>
            </div>
          </div>
        ))}
      </div>

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Course" : "Add Course"}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSubmit={save}
        >
          <div className="field">
            <label>Course name</label>
            <input
              className="input"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Holes</label>
              <input
                className="input"
                type="number"
                value={form.holes}
                onChange={(e) =>
                  setForm({ ...form, holes: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Par</label>
              <input
                className="input"
                type="number"
                value={form.par}
                onChange={(e) =>
                  setForm({ ...form, par: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Yardage</label>
              <input
                className="input"
                type="number"
                value={form.yardage}
                onChange={(e) =>
                  setForm({ ...form, yardage: Number(e.target.value) })
                }
              />
            </div>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Rating</label>
              <input
                className="input"
                type="number"
                step="0.1"
                value={form.rating}
                onChange={(e) =>
                  setForm({ ...form, rating: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Slope</label>
              <input
                className="input"
                type="number"
                value={form.slope}
                onChange={(e) =>
                  setForm({ ...form, slope: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Status</label>
              <select
                className="select"
                value={form.status}
                onChange={(e) =>
                  setForm({
                    ...form,
                    status: e.target.value as Course["status"],
                  })
                }
              >
                <option>Open</option>
                <option>Cart Path Only</option>
                <option>Closed</option>
              </select>
            </div>
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Opens at</label>
              <input
                className="input"
                type="time"
                step={900}
                value={form.openTime}
                onChange={(e) =>
                  setForm({ ...form, openTime: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Closes at</label>
              <input
                className="input"
                type="time"
                step={900}
                value={form.closeTime}
                onChange={(e) =>
                  setForm({ ...form, closeTime: e.target.value })
                }
              />
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
                Delete course
              </button>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
