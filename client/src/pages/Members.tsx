import { useMemo, useState } from "react";
import { Modal } from "../components/Modal";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type { Member, MembershipTier } from "../data/types";

const TIERS: MembershipTier[] = ["Social", "Weekday", "Full", "Corporate"];

const blank = (): Omit<Member, "id"> => ({
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  tier: "Full",
  handicap: 18,
  joinDate: new Date().toISOString().slice(0, 10),
  active: true,
  balance: 0,
});

export function Members() {
  const { data, members: api } = useStore();
  const toaster = useToaster();
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState("");
  const [tier, setTier] = useState<string>("all");
  const [showActive, setShowActive] = useState<"all" | "active" | "inactive">(
    "all",
  );
  const [editing, setEditing] = useState<Member | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<Member, "id">>(blank());

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return data.members.filter((m) => {
      if (tier !== "all" && m.tier !== tier) return false;
      if (showActive === "active" && !m.active) return false;
      if (showActive === "inactive" && m.active) return false;
      if (!q) return true;
      return (
        `${m.firstName} ${m.lastName}`.toLowerCase().includes(q) ||
        m.email.toLowerCase().includes(q) ||
        m.phone.toLowerCase().includes(q)
      );
    });
  }, [data.members, search, tier, showActive]);

  const startCreate = () => {
    setForm(blank());
    setCreating(true);
  };
  const startEdit = (m: Member) => {
    setEditing(m);
    setForm({ ...m });
  };

  const save = async () => {
    if (!form.firstName.trim() || !form.lastName.trim()) {
      toaster.push({
        kind: "error",
        message: "First and last name are required",
      });
      return;
    }
    setBusy(true);
    const result = editing
      ? await api.update(editing.id, { ...editing, ...form })
      : await api.create(form);
    setBusy(false);
    if (!result) return;
    toaster.push({
      kind: "success",
      message: editing ? "Member updated" : "Member added",
    });
    setEditing(null);
    setCreating(false);
  };

  const remove = async (id: string) => {
    if (!window.confirm("Remove this member? This cannot be undone.")) return;
    setBusy(true);
    const ok = await api.remove(id);
    setBusy(false);
    if (ok) {
      toaster.push({ kind: "success", message: "Member removed" });
      setEditing(null);
    }
  };

  return (
    <div className="stack">
      <div className="card">
        <div className="toolbar">
          <div className="toolbar-left">
            <input
              className="input"
              placeholder="Search by name, email or phone"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ minWidth: 240 }}
            />
            <select
              className="select"
              value={tier}
              onChange={(e) => setTier(e.target.value)}
            >
              <option value="all">All tiers</option>
              {TIERS.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
            <select
              className="select"
              value={showActive}
              onChange={(e) =>
                setShowActive(e.target.value as typeof showActive)
              }
            >
              <option value="all">All members</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </div>
          <button className="btn" onClick={startCreate}>
            + Add Member
          </button>
        </div>

        {filtered.length === 0 ? (
          <div className="empty">No members match the current filters.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Tier</th>
                <th>Handicap</th>
                <th>Email / Phone</th>
                <th>Joined</th>
                <th>Balance</th>
                <th>Status</th>
                <th style={{ width: 1 }}></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((m) => (
                <tr key={m.id}>
                  <td>
                    <strong>
                      {m.firstName} {m.lastName}
                    </strong>
                  </td>
                  <td>
                    <span
                      className={`pill ${
                        m.tier === "Full"
                          ? "green"
                          : m.tier === "Corporate"
                            ? "blue"
                            : m.tier === "Weekday"
                              ? "gold"
                              : ""
                      }`}
                    >
                      {m.tier}
                    </span>
                  </td>
                  <td>{m.handicap.toFixed(1)}</td>
                  <td>
                    <div>{m.email}</div>
                    <div className="muted" style={{ fontSize: 12 }}>
                      {m.phone}
                    </div>
                  </td>
                  <td>{m.joinDate}</td>
                  <td>
                    {m.balance > 0 ? (
                      <span className="pill red">${m.balance.toFixed(2)}</span>
                    ) : (
                      <span className="muted">$0.00</span>
                    )}
                  </td>
                  <td>
                    <span className={`pill ${m.active ? "green" : "gray"}`}>
                      {m.active ? "Active" : "Inactive"}
                    </span>
                  </td>
                  <td>
                    <div className="table-actions">
                      <button
                        className="btn sm secondary"
                        onClick={() => startEdit(m)}
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

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Member" : "Add Member"}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSubmit={save}
          submitLabel={busy ? "Saving…" : editing ? "Save" : "Add"}
        >
          <div className="grid cols-2">
            <div className="field">
              <label>First name</label>
              <input
                className="input"
                value={form.firstName}
                onChange={(e) =>
                  setForm({ ...form, firstName: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Last name</label>
              <input
                className="input"
                value={form.lastName}
                onChange={(e) =>
                  setForm({ ...form, lastName: e.target.value })
                }
              />
            </div>
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Email</label>
              <input
                className="input"
                type="email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
              />
            </div>
            <div className="field">
              <label>Phone</label>
              <input
                className="input"
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
              />
            </div>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Tier</label>
              <select
                className="select"
                value={form.tier}
                onChange={(e) =>
                  setForm({ ...form, tier: e.target.value as MembershipTier })
                }
              >
                {TIERS.map((t) => (
                  <option key={t}>{t}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Handicap</label>
              <input
                className="input"
                type="number"
                step="0.1"
                value={form.handicap}
                onChange={(e) =>
                  setForm({ ...form, handicap: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Join date</label>
              <input
                className="input"
                type="date"
                value={form.joinDate}
                onChange={(e) =>
                  setForm({ ...form, joinDate: e.target.value })
                }
              />
            </div>
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Balance ($)</label>
              <input
                className="input"
                type="number"
                step="0.01"
                value={form.balance}
                onChange={(e) =>
                  setForm({ ...form, balance: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Status</label>
              <select
                className="select"
                value={form.active ? "active" : "inactive"}
                onChange={(e) =>
                  setForm({ ...form, active: e.target.value === "active" })
                }
              >
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
              </select>
            </div>
          </div>
          {editing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => remove(editing.id)}
              >
                Delete member
              </button>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
