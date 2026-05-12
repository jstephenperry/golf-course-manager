import { useState } from "react";
import { Modal } from "../components/Modal";
import { uid, useStore } from "../data/store";
import type { Tournament } from "../data/types";

const FORMATS: Tournament["format"][] = [
  "Stroke Play",
  "Match Play",
  "Scramble",
  "Best Ball",
  "Stableford",
];

const blank = (courseId: string): Omit<Tournament, "id"> => ({
  name: "",
  date: new Date().toISOString().slice(0, 10),
  format: "Stroke Play",
  courseId,
  entryFee: 50,
  maxPlayers: 60,
  registered: [],
  status: "Scheduled",
});

export function Tournaments() {
  const { data, update } = useStore();
  const playable = data.courses.filter((c) => c.holes > 0);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Tournament | null>(null);
  const [form, setForm] = useState<Omit<Tournament, "id">>(
    blank(playable[0]?.id ?? ""),
  );

  const courseName = (id: string) =>
    data.courses.find((c) => c.id === id)?.name ?? "—";
  const memberName = (id: string) => {
    const m = data.members.find((x) => x.id === id);
    return m ? `${m.firstName} ${m.lastName}` : id;
  };

  const save = () => {
    if (!form.name.trim()) {
      alert("Tournament name is required.");
      return;
    }
    if (editing) {
      update("tournaments", (list) =>
        list.map((t) => (t.id === editing.id ? { ...editing, ...form } : t)),
      );
      setEditing(null);
    } else {
      update("tournaments", (list) => [...list, { id: uid("tr"), ...form }]);
      setCreating(false);
    }
  };

  const remove = (id: string) => {
    if (!window.confirm("Delete this tournament?")) return;
    update("tournaments", (list) => list.filter((t) => t.id !== id));
    setEditing(null);
  };

  const toggleRegistration = (tournament: Tournament, memberId: string) => {
    update("tournaments", (list) =>
      list.map((t) => {
        if (t.id !== tournament.id) return t;
        const isRegistered = t.registered.includes(memberId);
        if (isRegistered) {
          return {
            ...t,
            registered: t.registered.filter((id) => id !== memberId),
          };
        }
        if (t.registered.length >= t.maxPlayers) {
          alert("Tournament is full.");
          return t;
        }
        return { ...t, registered: [...t.registered, memberId] };
      }),
    );
  };

  const sorted = [...data.tournaments].sort((a, b) =>
    a.date.localeCompare(b.date),
  );

  return (
    <div className="stack">
      <div className="toolbar">
        <div className="muted">
          Schedule events, manage registrations and entry fees.
        </div>
        <button
          className="btn"
          onClick={() => {
            setForm(blank(playable[0]?.id ?? ""));
            setCreating(true);
          }}
        >
          + New Tournament
        </button>
      </div>

      {sorted.length === 0 ? (
        <div className="card empty">No tournaments scheduled.</div>
      ) : (
        <div className="grid cols-2">
          {sorted.map((t) => (
            <div className="card" key={t.id}>
              <div className="row between">
                <div>
                  <h3 style={{ marginBottom: 4 }}>{t.name}</h3>
                  <div className="muted" style={{ fontSize: 12 }}>
                    {t.date} · {courseName(t.courseId)} · {t.format}
                  </div>
                </div>
                <span
                  className={`pill ${
                    t.status === "Completed"
                      ? "gray"
                      : t.status === "Cancelled"
                        ? "red"
                        : t.status === "In Progress"
                          ? "green"
                          : "gold"
                  }`}
                >
                  {t.status}
                </span>
              </div>

              <div className="grid cols-3" style={{ marginTop: 12 }}>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Entry</span>
                  <span className="value" style={{ fontSize: 18 }}>
                    ${t.entryFee}
                  </span>
                </div>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Field</span>
                  <span className="value" style={{ fontSize: 18 }}>
                    {t.registered.length}/{t.maxPlayers}
                  </span>
                </div>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Purse</span>
                  <span className="value" style={{ fontSize: 18 }}>
                    ${(t.registered.length * t.entryFee).toLocaleString()}
                  </span>
                </div>
              </div>

              <div style={{ marginTop: 12 }}>
                <div
                  className="muted"
                  style={{ fontSize: 12, marginBottom: 6 }}
                >
                  Registered players
                </div>
                {t.registered.length === 0 ? (
                  <div className="muted">No one signed up yet.</div>
                ) : (
                  <div className="chip-list">
                    {t.registered.map((id) => (
                      <span key={id} className="pill green">
                        {memberName(id)}
                      </span>
                    ))}
                  </div>
                )}
              </div>

              <div className="row" style={{ marginTop: 12, flexWrap: "wrap" }}>
                <select
                  className="select"
                  defaultValue=""
                  onChange={(e) => {
                    if (e.target.value) {
                      toggleRegistration(t, e.target.value);
                      e.target.value = "";
                    }
                  }}
                  style={{ maxWidth: 200 }}
                >
                  <option value="">Add/remove member…</option>
                  {data.members
                    .filter((m) => m.active)
                    .map((m) => (
                      <option key={m.id} value={m.id}>
                        {t.registered.includes(m.id) ? "− Remove " : "+ Add "}
                        {m.firstName} {m.lastName}
                      </option>
                    ))}
                </select>
                <button
                  className="btn sm secondary"
                  onClick={() => {
                    setEditing(t);
                    setForm({ ...t });
                  }}
                >
                  Edit
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Tournament" : "New Tournament"}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
          onSubmit={save}
        >
          <div className="field">
            <label>Tournament name</label>
            <input
              className="input"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
          </div>
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
              <label>Course</label>
              <select
                className="select"
                value={form.courseId}
                onChange={(e) =>
                  setForm({ ...form, courseId: e.target.value })
                }
              >
                {playable.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Format</label>
              <select
                className="select"
                value={form.format}
                onChange={(e) =>
                  setForm({
                    ...form,
                    format: e.target.value as Tournament["format"],
                  })
                }
              >
                {FORMATS.map((f) => (
                  <option key={f}>{f}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Entry fee ($)</label>
              <input
                className="input"
                type="number"
                value={form.entryFee}
                onChange={(e) =>
                  setForm({ ...form, entryFee: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Max players</label>
              <input
                className="input"
                type="number"
                value={form.maxPlayers}
                onChange={(e) =>
                  setForm({ ...form, maxPlayers: Number(e.target.value) })
                }
              />
            </div>
          </div>
          <div className="field">
            <label>Status</label>
            <select
              className="select"
              value={form.status}
              onChange={(e) =>
                setForm({
                  ...form,
                  status: e.target.value as Tournament["status"],
                })
              }
            >
              <option>Scheduled</option>
              <option>In Progress</option>
              <option>Completed</option>
              <option>Cancelled</option>
            </select>
          </div>
          {editing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => remove(editing.id)}
              >
                Delete tournament
              </button>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
