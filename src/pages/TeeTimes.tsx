import { useMemo, useState } from "react";
import { Modal } from "../components/Modal";
import { uid, useStore } from "../data/store";
import type { TeeTime } from "../data/types";

const todayIso = () => new Date().toISOString().slice(0, 10);

const blankForm = (date: string, courseId: string): Omit<TeeTime, "id"> => ({
  date,
  time: "08:00",
  courseId,
  players: [],
  cart: true,
  status: "Booked",
  notes: "",
});

export function TeeTimes() {
  const { data, update } = useStore();
  const playableCourses = data.courses.filter((c) => c.holes > 0);

  const [date, setDate] = useState(todayIso());
  const [courseId, setCourseId] = useState(playableCourses[0]?.id ?? "");
  const [editing, setEditing] = useState<TeeTime | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<TeeTime, "id">>(
    blankForm(date, courseId),
  );

  const slots = useMemo(
    () =>
      data.teeTimes
        .filter((t) => t.date === date && t.courseId === courseId)
        .sort((a, b) => a.time.localeCompare(b.time)),
    [data.teeTimes, date, courseId],
  );

  const memberName = (id: string) => {
    const m = data.members.find((x) => x.id === id);
    return m ? `${m.firstName} ${m.lastName}` : id;
  };

  const startCreate = () => {
    setForm(blankForm(date, courseId));
    setCreating(true);
  };

  const startEdit = (t: TeeTime) => {
    setEditing(t);
    setForm({ ...t });
  };

  const save = () => {
    if (editing) {
      const updated: TeeTime = { ...editing, ...form };
      update("teeTimes", (list) =>
        list.map((t) => (t.id === editing.id ? updated : t)),
      );
      setEditing(null);
    } else {
      const created: TeeTime = { id: uid("tt"), ...form };
      update("teeTimes", (list) => [...list, created]);
      setCreating(false);
    }
  };

  const remove = (id: string) => {
    if (!window.confirm("Cancel this tee time?")) return;
    update("teeTimes", (list) => list.filter((t) => t.id !== id));
    setEditing(null);
  };

  const setStatus = (id: string, status: TeeTime["status"]) => {
    update("teeTimes", (list) =>
      list.map((t) => (t.id === id ? { ...t, status } : t)),
    );
  };

  return (
    <div className="stack">
      <div className="card">
        <div className="toolbar">
          <div className="toolbar-left">
            <div className="field">
              <label>Date</label>
              <input
                className="input"
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
              />
            </div>
            <div className="field">
              <label>Course</label>
              <select
                className="select"
                value={courseId}
                onChange={(e) => setCourseId(e.target.value)}
              >
                {playableCourses.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <button className="btn" onClick={startCreate}>
            + Book Tee Time
          </button>
        </div>

        {slots.length === 0 ? (
          <div className="empty">
            No tee times for this course on this date. Click "Book Tee Time" to
            add one.
          </div>
        ) : (
          <div className="grid cols-3">
            {slots.map((t) => (
              <div className="slot" key={t.id}>
                <div className="slot-head">
                  <span className="time">{t.time}</span>
                  <span
                    className={`pill ${
                      t.status === "Checked In"
                        ? "green"
                        : t.status === "Completed"
                          ? "gray"
                          : t.status === "Cancelled"
                            ? "red"
                            : "gold"
                    }`}
                  >
                    {t.status}
                  </span>
                </div>
                <div>
                  {t.players.length === 0 ? (
                    <span className="muted">No players assigned</span>
                  ) : (
                    <div className="chip-list">
                      {t.players.map((id) => (
                        <span key={id} className="pill">
                          {memberName(id)}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
                <div className="muted" style={{ fontSize: 12 }}>
                  {t.cart ? "Cart" : "Walking"}
                  {t.notes ? ` · ${t.notes}` : ""}
                </div>
                <div className="row" style={{ gap: 6, flexWrap: "wrap" }}>
                  {t.status === "Booked" && (
                    <button
                      className="btn sm"
                      onClick={() => setStatus(t.id, "Checked In")}
                    >
                      Check In
                    </button>
                  )}
                  {t.status === "Checked In" && (
                    <button
                      className="btn sm secondary"
                      onClick={() => setStatus(t.id, "Completed")}
                    >
                      Mark Complete
                    </button>
                  )}
                  <button
                    className="btn sm secondary"
                    onClick={() => startEdit(t)}
                  >
                    Edit
                  </button>
                  <button
                    className="btn sm danger"
                    onClick={() => remove(t.id)}
                  >
                    Cancel
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Tee Time" : "Book Tee Time"}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={save}
          submitLabel={editing ? "Save changes" : "Book"}
        >
          <div className="grid cols-2">
            <div className="field">
              <label>Date</label>
              <input
                className="input"
                type="date"
                value={form.date}
                onChange={(e) => setForm({ ...form, date: e.target.value })}
              />
            </div>
            <div className="field">
              <label>Time</label>
              <input
                className="input"
                type="time"
                value={form.time}
                onChange={(e) => setForm({ ...form, time: e.target.value })}
              />
            </div>
          </div>
          <div className="field">
            <label>Course</label>
            <select
              className="select"
              value={form.courseId}
              onChange={(e) => setForm({ ...form, courseId: e.target.value })}
            >
              {playableCourses.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Players (up to 4)</label>
            <select
              className="select"
              multiple
              size={5}
              value={form.players}
              onChange={(e) =>
                setForm({
                  ...form,
                  players: Array.from(e.target.selectedOptions)
                    .map((o) => o.value)
                    .slice(0, 4),
                })
              }
            >
              {data.members
                .filter((m) => m.active)
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.firstName} {m.lastName} · {m.tier}
                  </option>
                ))}
            </select>
            <small className="muted">Hold Ctrl/Cmd to select multiple.</small>
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Cart</label>
              <select
                className="select"
                value={form.cart ? "yes" : "no"}
                onChange={(e) =>
                  setForm({ ...form, cart: e.target.value === "yes" })
                }
              >
                <option value="yes">Cart</option>
                <option value="no">Walking</option>
              </select>
            </div>
            <div className="field">
              <label>Status</label>
              <select
                className="select"
                value={form.status}
                onChange={(e) =>
                  setForm({
                    ...form,
                    status: e.target.value as TeeTime["status"],
                  })
                }
              >
                <option>Booked</option>
                <option>Checked In</option>
                <option>Completed</option>
                <option>Cancelled</option>
              </select>
            </div>
          </div>
          <div className="field">
            <label>Notes</label>
            <textarea
              className="textarea"
              rows={2}
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
            />
          </div>
        </Modal>
      )}
    </div>
  );
}
