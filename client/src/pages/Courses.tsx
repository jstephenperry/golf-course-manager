import { useState } from "react";
import { Link } from "react-router-dom";
import { Modal } from "../components/Modal";
import { NineEditor } from "../components/NineEditor";
import { useToaster } from "../components/Toaster";
import {
  courseHoleCount,
  coursePar,
  courseTeeNames,
  courseYardage,
  isPlayable,
} from "../data/courseDerived";
import { useStore } from "../data/store";
import type { Course, Nine } from "../data/types";

type Tab = "courses" | "nines";

const blankCourse = (): Omit<Course, "id"> => ({
  name: "",
  frontNineId: null,
  backNineId: null,
  rating: 0,
  slope: 0,
  status: "Open",
  openTime: "06:00",
  closeTime: "18:00",
  notes: "",
});

export function Courses() {
  const [tab, setTab] = useState<Tab>("courses");
  return (
    <div className="stack">
      <div className="toolbar">
        <div className="row" style={{ gap: 4 }}>
          <button
            className={`btn ${tab === "courses" ? "" : "secondary"}`}
            onClick={() => setTab("courses")}
          >
            Courses
          </button>
          <button
            className={`btn ${tab === "nines" ? "" : "secondary"}`}
            onClick={() => setTab("nines")}
          >
            Nines
          </button>
        </div>
        <div className="muted" style={{ fontSize: 12 }}>
          Courses are 9- or 18-hole rounds assembled from Nines. Edit Nines to
          change hole-level par, yardage, and handicap.
        </div>
      </div>

      {tab === "courses" ? <CoursesTab /> : <NinesTab />}
    </div>
  );
}

function CoursesTab() {
  const { data, courses: api } = useStore();
  const toaster = useToaster();
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<Course | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<Course, "id">>(blankCourse());

  const startCreate = () => {
    setForm(blankCourse());
    setCreating(true);
  };
  const startEdit = (c: Course) => {
    setEditing(c);
    setForm({ ...c });
  };
  const save = async () => {
    if (!form.name.trim()) {
      toaster.push({ kind: "error", message: "Course name is required" });
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
    if (!window.confirm("Delete this course?")) return;
    setBusy(true);
    const ok = await api.remove(id);
    setBusy(false);
    if (ok) setEditing(null);
  };

  const setStatus = async (c: Course, status: Course["status"]) => {
    await api.update(c.id, { ...c, status });
  };

  const nineName = (id: string | null) =>
    id ? (data.nines.find((n) => n.id === id)?.name ?? "—") : "—";

  return (
    <>
      <div className="toolbar">
        <div className="muted">
          {data.courses.length === 0
            ? "No courses yet. Add a Nine first, then assemble it into a Course."
            : `${data.courses.length} course${data.courses.length === 1 ? "" : "s"}`}
        </div>
        <button
          className="btn"
          onClick={startCreate}
          disabled={data.nines.length === 0}
          title={
            data.nines.length === 0
              ? "Add a Nine first — Courses are assembled from Nines"
              : ""
          }
        >
          + Add Course
        </button>
      </div>

      <div className="grid cols-2">
        {data.courses.map((c) => {
          const holes = courseHoleCount(c, data.nines);
          const par = coursePar(c, data.nines);
          const tees = courseTeeNames(c, data.nines);
          const primaryTee = tees[0];
          return (
            <div className="card" key={c.id}>
              <div className="row between">
                <div>
                  <h3 style={{ marginBottom: 4 }}>{c.name}</h3>
                  <div className="muted" style={{ fontSize: 12 }}>
                    {holes > 0
                      ? `${holes} holes · Par ${par}${
                          primaryTee
                            ? ` · ${courseYardage(c, data.nines, primaryTee).toLocaleString()} yds (${primaryTee})`
                            : ""
                        }`
                      : "Unassembled — assign a front nine to make playable"}
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

              <div className="grid cols-2" style={{ marginTop: 12 }}>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Front nine</span>
                  <span className="value" style={{ fontSize: 14 }}>
                    {nineName(c.frontNineId)}
                  </span>
                </div>
                <div className="kpi" style={{ padding: 10 }}>
                  <span className="label">Back nine</span>
                  <span className="value" style={{ fontSize: 14 }}>
                    {nineName(c.backNineId)}
                  </span>
                </div>
              </div>

              {tees.length > 1 && (
                <div className="muted" style={{ fontSize: 12, marginTop: 10 }}>
                  Yardages:{" "}
                  {tees
                    .map(
                      (t) =>
                        `${t} ${courseYardage(c, data.nines, t).toLocaleString()}`,
                    )
                    .join(" · ")}
                </div>
              )}

              {(c.rating > 0 || c.slope > 0) && (
                <div className="grid cols-2" style={{ marginTop: 10 }}>
                  <div className="kpi" style={{ padding: 10 }}>
                    <span className="label">Rating</span>
                    <span className="value" style={{ fontSize: 16 }}>
                      {c.rating.toFixed(1)}
                    </span>
                  </div>
                  <div className="kpi" style={{ padding: 10 }}>
                    <span className="label">Slope</span>
                    <span className="value" style={{ fontSize: 16 }}>
                      {c.slope}
                    </span>
                  </div>
                </div>
              )}

              <div className="muted" style={{ fontSize: 12, marginTop: 10 }}>
                Operating hours: {c.openTime || "06:00"} –{" "}
                {c.closeTime || "18:00"}
              </div>

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
                {isPlayable(c) && (
                  <Link
                    to={`/courses/${c.id}/scorecard`}
                    className="btn sm secondary"
                  >
                    Scorecard
                  </Link>
                )}
                <button
                  className="btn sm secondary"
                  onClick={() => startEdit(c)}
                >
                  Edit
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Course" : "Add Course"}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSubmit={save}
          submitLabel={busy ? "Saving…" : editing ? "Save" : "Add"}
        >
          <div className="field">
            <label>Course name</label>
            <input
              className="input"
              placeholder="e.g. Oak / Redbud"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Front nine</label>
              <select
                className="select"
                value={form.frontNineId ?? ""}
                onChange={(e) =>
                  setForm({
                    ...form,
                    frontNineId: e.target.value || null,
                  })
                }
              >
                <option value="">— Select a nine —</option>
                {data.nines.map((n) => (
                  <option key={n.id} value={n.id}>
                    {n.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Back nine (optional)</label>
              <select
                className="select"
                value={form.backNineId ?? ""}
                onChange={(e) =>
                  setForm({
                    ...form,
                    backNineId: e.target.value || null,
                  })
                }
              >
                <option value="">— None (9-hole course) —</option>
                {data.nines.map((n) => (
                  <option key={n.id} value={n.id}>
                    {n.name}
                  </option>
                ))}
              </select>
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
    </>
  );
}

function NinesTab() {
  const { data, nines: api } = useStore();
  const toaster = useToaster();
  const [editing, setEditing] = useState<Nine | null>(null);
  const [creating, setCreating] = useState(false);

  const remove = async (n: Nine) => {
    const inUse = data.courses.some(
      (c) => c.frontNineId === n.id || c.backNineId === n.id,
    );
    if (inUse) {
      toaster.push({
        kind: "error",
        message: `${n.name} is in use by one or more courses. Edit those courses first.`,
      });
      return;
    }
    if (!window.confirm(`Delete the "${n.name}" nine and all its holes?`))
      return;
    await api.remove(n.id);
  };

  const usageFor = (nineId: string) =>
    data.courses
      .filter((c) => c.frontNineId === nineId || c.backNineId === nineId)
      .map((c) => c.name);

  return (
    <>
      <div className="toolbar">
        <div className="muted">
          {data.nines.length === 0
            ? "No nines yet. A Nine is a set of 9 holes — the building block for Courses."
            : `${data.nines.length} nine${data.nines.length === 1 ? "" : "s"}`}
        </div>
        <button className="btn" onClick={() => setCreating(true)}>
          + Add Nine
        </button>
      </div>

      <div className="grid cols-2">
        {data.nines.map((n) => {
          const par = n.holes.reduce((acc, h) => acc + (h.par || 0), 0);
          const longestTee = n.teeSets[0];
          const longestYds = longestTee
            ? n.holes.reduce((acc, h) => {
                const y = h.yardages.find((x) => x.teeSetId === longestTee.id);
                return acc + (y?.yards ?? 0);
              }, 0)
            : 0;
          const usage = usageFor(n.id);
          return (
            <div className="card" key={n.id}>
              <div className="row between">
                <div>
                  <h3 style={{ marginBottom: 4 }}>{n.name}</h3>
                  <div className="muted" style={{ fontSize: 12 }}>
                    {n.holes.length} holes · Par {par}
                    {longestTee && longestYds > 0
                      ? ` · ${longestYds.toLocaleString()} yds (${longestTee.name})`
                      : ""}
                  </div>
                </div>
                <div className="row" style={{ gap: 4 }}>
                  {n.teeSets.map((t) => (
                    <span
                      key={t.id}
                      title={t.name}
                      style={{
                        display: "inline-block",
                        width: 12,
                        height: 12,
                        borderRadius: "50%",
                        background: t.color || "#888",
                        border: "1px solid rgba(0,0,0,0.2)",
                      }}
                    />
                  ))}
                </div>
              </div>

              {n.description && (
                <p className="muted" style={{ marginTop: 8, fontSize: 13 }}>
                  {n.description}
                </p>
              )}

              {n.holes.length > 0 && (
                <div style={{ overflowX: "auto", marginTop: 10 }}>
                  <table className="table" style={{ fontSize: 12 }}>
                    <thead>
                      <tr>
                        <th>Hole</th>
                        {n.holes.map((h) => (
                          <th key={h.id}>{h.number}</th>
                        ))}
                        <th>Tot</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr>
                        <td>Par</td>
                        {n.holes.map((h) => (
                          <td key={h.id}>{h.par}</td>
                        ))}
                        <td>
                          <strong>{par}</strong>
                        </td>
                      </tr>
                      <tr>
                        <td>HCP</td>
                        {n.holes.map((h) => (
                          <td key={h.id}>{h.handicapIndex}</td>
                        ))}
                        <td></td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              )}

              <div className="muted" style={{ fontSize: 12, marginTop: 8 }}>
                {usage.length === 0
                  ? "Not used by any course"
                  : `Used by: ${usage.join(", ")}`}
              </div>

              <div className="row" style={{ marginTop: 12 }}>
                <button
                  className="btn sm secondary"
                  onClick={() => setEditing(n)}
                >
                  Edit
                </button>
                <button
                  className="btn sm danger"
                  onClick={() => remove(n)}
                  disabled={usage.length > 0}
                >
                  Delete
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {(creating || editing) && (
        <NineEditor
          nine={editing}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}
    </>
  );
}
