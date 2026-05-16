import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Modal } from "../components/Modal";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type { TeeTime } from "../data/types";
import {
  TEE_SLOT_INTERVAL_MIN,
  courseClose,
  courseOpen,
  generateSlots,
  snapToSlot,
} from "../data/utils";

const todayIso = () => {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
};

const blankForm = (
  date: string,
  courseId: string,
  time: string,
): Omit<TeeTime, "id"> => ({
  date,
  time,
  courseId,
  players: [],
  cart: true,
  status: "Booked",
  notes: "",
});

export function TeeTimes() {
  const { data, teeTimes: api, tabs: tabsApi } = useStore();
  const toaster = useToaster();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const playableCourses = data.courses.filter((c) => c.holes > 0);

  const [date, setDate] = useState(todayIso());
  const [courseId, setCourseId] = useState(playableCourses[0]?.id ?? "");
  const [editing, setEditing] = useState<TeeTime | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<TeeTime, "id">>(
    blankForm(date, courseId, "08:00"),
  );

  useEffect(() => {
    if (!courseId && playableCourses[0]) {
      setCourseId(playableCourses[0].id);
    }
  }, [courseId, playableCourses]);

  const course = data.courses.find((c) => c.id === courseId);
  const slots = useMemo(
    () =>
      course
        ? generateSlots(
            courseOpen(course),
            courseClose(course),
            TEE_SLOT_INTERVAL_MIN,
          )
        : [],
    [course],
  );

  const byTime = useMemo(() => {
    const map = new Map<string, TeeTime>();
    data.teeTimes
      .filter((t) => t.date === date && t.courseId === courseId)
      .forEach((t) => map.set(t.time, t));
    return map;
  }, [data.teeTimes, date, courseId]);

  const offGridBookings = useMemo(
    () =>
      data.teeTimes
        .filter(
          (t) =>
            t.date === date &&
            t.courseId === courseId &&
            !slots.includes(t.time),
        )
        .sort((a, b) => a.time.localeCompare(b.time)),
    [data.teeTimes, date, courseId, slots],
  );

  const memberName = (id: string) => {
    const m = data.members.find((x) => x.id === id);
    return m ? `${m.firstName} ${m.lastName}` : id;
  };

  const openCreate = (time: string) => {
    if (!course) {
      toaster.push({ kind: "error", message: "Add a course first." });
      return;
    }
    setForm(blankForm(date, courseId, time));
    setCreating(true);
  };

  const openEdit = (t: TeeTime) => {
    setEditing(t);
    setForm({ ...t });
  };

  const save = async () => {
    const snapped = course
      ? snapToSlot(form.time, courseOpen(course), TEE_SLOT_INTERVAL_MIN)
      : form.time;
    const next = { ...form, time: snapped };
    setBusy(true);
    const result = editing
      ? await api.update(editing.id, { ...editing, ...next })
      : await api.create(next);
    setBusy(false);
    if (!result) return;
    setEditing(null);
    setCreating(false);
  };

  const remove = async (id: string) => {
    if (!window.confirm("Cancel this tee time?")) return;
    setBusy(true);
    const ok = await api.remove(id);
    setBusy(false);
    if (ok) setEditing(null);
  };

  const setStatus = async (id: string, status: TeeTime["status"]) => {
    const t = data.teeTimes.find((x) => x.id === id);
    if (!t) return;
    await api.update(id, { ...t, status });
  };

  const openOrCreateTab = async (t: TeeTime) => {
    const existing = data.tabs.find(
      (tab) => tab.teeTimeId === t.id && tab.status === "Open",
    );
    if (existing) {
      navigate(`/tabs?tab=${existing.id}`);
      return;
    }
    const created = await tabsApi.create({
      openedAt: new Date().toISOString(),
      status: "Open",
      memberIds: t.players,
      guests: [],
      teeTimeId: t.id,
      items: [],
      payments: [],
      tipAmount: 0,
      taxRate: 0.0825,
      notes: "",
    });
    if (!created) return;
    navigate(`/tabs?tab=${created.id}`);
  };

  const statusPill = (t: TeeTime) => (
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
  );

  if (playableCourses.length === 0) {
    return (
      <div className="card empty">
        Add a playable course (with holes &gt; 0) to begin booking tee times.
      </div>
    );
  }

  const bookedCount = Array.from(byTime.values()).filter(
    (t) => t.status !== "Cancelled",
  ).length;
  const openCount = slots.length - bookedCount;

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
            {course && (
              <div className="muted" style={{ fontSize: 12 }}>
                {courseOpen(course)} – {courseClose(course)} ·{" "}
                {TEE_SLOT_INTERVAL_MIN}-minute intervals
              </div>
            )}
          </div>
          <div className="row">
            <span className="pill green">{bookedCount} booked</span>
            <span className="pill">{openCount} open</span>
          </div>
        </div>

        {slots.length === 0 ? (
          <div className="empty">
            This course has no operating hours configured.
          </div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: 90 }}>Time</th>
                <th>Players</th>
                <th style={{ width: 110 }}>Status</th>
                <th style={{ width: 110 }}>Cart</th>
                <th style={{ width: 1 }}></th>
              </tr>
            </thead>
            <tbody>
              {slots.map((time) => {
                const t = byTime.get(time);
                if (!t) {
                  return (
                    <tr key={time}>
                      <td>
                        <strong>{time}</strong>
                      </td>
                      <td className="muted">Open</td>
                      <td></td>
                      <td></td>
                      <td>
                        <div className="table-actions">
                          <button
                            className="btn sm"
                            onClick={() => openCreate(time)}
                          >
                            + Book
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                }
                return (
                  <tr key={time}>
                    <td>
                      <strong>{time}</strong>
                    </td>
                    <td>
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
                      {t.notes && (
                        <div className="muted" style={{ fontSize: 12 }}>
                          {t.notes}
                        </div>
                      )}
                    </td>
                    <td>{statusPill(t)}</td>
                    <td>
                      <span className="muted">
                        {t.cart ? "Cart" : "Walking"}
                      </span>
                    </td>
                    <td>
                      <div className="table-actions">
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
                            Done
                          </button>
                        )}
                        {(() => {
                          const tab = data.tabs.find(
                            (x) =>
                              x.teeTimeId === t.id && x.status === "Open",
                          );
                          return (
                            <button
                              className="btn sm secondary"
                              onClick={() => openOrCreateTab(t)}
                              title={
                                tab ? "Open existing tab" : "Start a new tab"
                              }
                            >
                              {tab ? "Tab" : "Open Tab"}
                            </button>
                          );
                        })()}
                        <button
                          className="btn sm secondary"
                          onClick={() => openEdit(t)}
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
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

      {offGridBookings.length > 0 && (
        <div className="card">
          <h2 style={{ marginBottom: 8 }}>Off-grid bookings</h2>
          <div className="muted" style={{ fontSize: 12, marginBottom: 8 }}>
            These bookings don't sit on a {TEE_SLOT_INTERVAL_MIN}-minute slot.
            Edit one to snap it to the grid.
          </div>
          <table className="table">
            <thead>
              <tr>
                <th>Time</th>
                <th>Players</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {offGridBookings.map((t) => (
                <tr key={t.id}>
                  <td>
                    <strong>{t.time}</strong>
                  </td>
                  <td>
                    {t.players.length === 0
                      ? "—"
                      : t.players.map(memberName).join(", ")}
                  </td>
                  <td>{statusPill(t)}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn sm secondary"
                        onClick={() => openEdit(t)}
                      >
                        Edit
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Tee Time" : "Book Tee Time"}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={save}
          submitLabel={busy ? "Saving…" : editing ? "Save changes" : "Book"}
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
              <select
                className="select"
                value={form.time}
                onChange={(e) => setForm({ ...form, time: e.target.value })}
              >
                {(() => {
                  const c = data.courses.find((x) => x.id === form.courseId);
                  const slotList = c
                    ? generateSlots(courseOpen(c), courseClose(c))
                    : [];
                  return slotList.includes(form.time)
                    ? slotList.map((s) => <option key={s}>{s}</option>)
                    : [
                        <option key={form.time} value={form.time}>
                          {form.time} (off-grid)
                        </option>,
                        ...slotList.map((s) => <option key={s}>{s}</option>),
                      ];
                })()}
              </select>
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
