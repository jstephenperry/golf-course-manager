import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  MEMBERS_SUSPEND,
  MEMBERS_WRITE,
} from "../auth/permissions";
import { RequirePermission } from "../auth/RequirePermission";
import { Modal } from "../components/Modal";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type {
  Member,
  MemberApplication,
  MemberStatus,
  MembershipTier,
} from "../data/types";
import { formatMoney, isoDate } from "../data/utils";

const TIERS: MembershipTier[] = ["Social", "Weekday", "Full", "Corporate"];

type Tab = "members" | "applications";

const blankMember = (): Omit<Member, "id"> => ({
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  tier: "Full",
  handicap: 18,
  joinDate: isoDate(new Date()),
  active: true,
  balance: 0,
  status: "Active",
  oldestUnpaidChargeAt: null,
  suspendedAt: null,
  notes: "",
});

const blankApplication = (): Omit<MemberApplication, "id"> => ({
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  requestedTier: "Full",
  sponsoringMemberId: null,
  initiationFee: 0,
  notes: "",
  status: "Pending",
  submittedAt: new Date().toISOString(),
  reviewedAt: null,
  reviewedBy: null,
  reviewNote: null,
  activatedMemberId: null,
});

const daysBetween = (a: string | null | undefined, nowMs: number): number => {
  if (!a) return 0;
  const t = new Date(a).getTime();
  if (Number.isNaN(t)) return 0;
  return Math.floor((nowMs - t) / (1000 * 60 * 60 * 24));
};

export function Members() {
  const {
    data,
    members: api,
    applications: appsApi,
    runDunning,
  } = useStore();
  const toaster = useToaster();
  const [tab, setTab] = useState<Tab>("members");

  // ---------- Members tab state ----------
  const [busy, setBusy] = useState(false);
  const [search, setSearch] = useState("");
  const [tier, setTier] = useState<string>("all");
  const [statusFilter, setStatusFilter] = useState<
    "all" | MemberStatus | "past-due"
  >("all");
  const [editing, setEditing] = useState<Member | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<Omit<Member, "id">>(blankMember());

  const nowMs = Date.now();

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return data.members.filter((m) => {
      if (tier !== "all" && m.tier !== tier) return false;
      if (statusFilter === "Active" && m.status !== "Active") return false;
      if (statusFilter === "Suspended" && m.status !== "Suspended") return false;
      if (statusFilter === "Inactive" && m.status !== "Inactive") return false;
      if (statusFilter === "past-due" && !m.oldestUnpaidChargeAt) return false;
      if (!q) return true;
      return (
        `${m.firstName} ${m.lastName}`.toLowerCase().includes(q) ||
        m.email.toLowerCase().includes(q) ||
        m.phone.toLowerCase().includes(q)
      );
    });
  }, [data.members, search, tier, statusFilter]);

  const startCreate = () => {
    setForm(blankMember());
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

  const suspend = async (m: Member) => {
    if (!window.confirm(`Suspend ${m.firstName} ${m.lastName}?`)) return;
    await api.suspend(m.id);
  };

  const reinstate = async (m: Member) => {
    if (m.balance > 0) {
      if (
        !window.confirm(
          `${m.firstName} still owes ${formatMoney(m.balance)}. Reinstate anyway?`,
        )
      )
        return;
    }
    await api.reinstate(m.id);
  };

  // ---------- Applications tab state ----------
  const [appCreating, setAppCreating] = useState(false);
  const [appEditing, setAppEditing] = useState<MemberApplication | null>(null);
  const [appForm, setAppForm] = useState<Omit<MemberApplication, "id">>(
    blankApplication(),
  );
  const [appBusy, setAppBusy] = useState(false);
  const [reviewing, setReviewing] = useState<{
    app: MemberApplication;
    kind: "approve" | "reject";
  } | null>(null);
  const [reviewer, setReviewer] = useState("");
  const [reviewNote, setReviewNote] = useState("");

  const appsByStatus = useMemo(() => {
    const groups = { Pending: [], Approved: [], Rejected: [], Activated: [], Withdrawn: [] } as Record<MemberApplication["status"], MemberApplication[]>;
    for (const a of data.memberApplications) {
      groups[a.status].push(a);
    }
    return groups;
  }, [data.memberApplications]);

  const submitApplication = async () => {
    if (!appForm.firstName.trim() || !appForm.lastName.trim()) {
      toaster.push({ kind: "error", message: "Name is required" });
      return;
    }
    setAppBusy(true);
    const created = appEditing
      ? await appsApi.update(appEditing.id, { ...appEditing, ...appForm })
      : await appsApi.create(appForm);
    setAppBusy(false);
    if (!created) return;
    setAppCreating(false);
    setAppEditing(null);
    toaster.push({
      kind: "success",
      message: appEditing ? "Application updated" : "Application submitted",
    });
  };

  const openReview = (app: MemberApplication, kind: "approve" | "reject") => {
    setReviewing({ app, kind });
    setReviewer("");
    setReviewNote("");
  };

  const submitReview = async () => {
    if (!reviewing) return;
    const { app, kind } = reviewing;
    setAppBusy(true);
    const fn = kind === "approve" ? appsApi.approve : appsApi.reject;
    const result = await fn(app.id, reviewer.trim() || "staff", reviewNote);
    setAppBusy(false);
    if (result) setReviewing(null);
  };

  const activate = async (app: MemberApplication) => {
    if (
      !window.confirm(
        `Activate ${app.firstName} ${app.lastName}? This creates a member${
          app.initiationFee > 0
            ? ` and posts ${formatMoney(app.initiationFee)} initiation fee to their account.`
            : "."
        }`,
      )
    )
      return;
    await appsApi.activate(app.id);
  };

  const withdraw = async (app: MemberApplication) => {
    if (!window.confirm("Withdraw this application?")) return;
    await appsApi.withdraw(app.id);
  };

  // ---------- Render ----------
  const pastDueCount = data.members.filter((m) => m.oldestUnpaidChargeAt).length;
  const suspendedCount = data.members.filter((m) => m.status === "Suspended").length;

  return (
    <div className="stack">
      <div className="toolbar">
        <div className="toolbar-left">
          <button
            className={`btn ${tab === "members" ? "" : "secondary"}`}
            onClick={() => setTab("members")}
          >
            Members ({data.members.length})
          </button>
          <button
            className={`btn ${tab === "applications" ? "" : "secondary"}`}
            onClick={() => setTab("applications")}
          >
            Applications ({appsByStatus.Pending.length} pending)
          </button>
        </div>
        {tab === "members" ? (
          <div className="row" style={{ gap: 8 }}>
            <button
              className="btn secondary"
              onClick={runDunning}
              title="Sweep accounts; auto-suspend members past NET60"
            >
              Run dunning
            </button>
            <RequirePermission permission={MEMBERS_WRITE}>
              <button className="btn" onClick={startCreate}>
                + Add Member
              </button>
            </RequirePermission>
          </div>
        ) : (
          <button
            className="btn"
            onClick={() => {
              setAppForm(blankApplication());
              setAppCreating(true);
            }}
          >
            + New Application
          </button>
        )}
      </div>

      {tab === "members" && (
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
                value={statusFilter}
                onChange={(e) =>
                  setStatusFilter(e.target.value as typeof statusFilter)
                }
              >
                <option value="all">All statuses</option>
                <option value="Active">Active</option>
                <option value="Suspended">
                  Suspended ({suspendedCount})
                </option>
                <option value="Inactive">Inactive</option>
                <option value="past-due">
                  Past-due ({pastDueCount})
                </option>
              </select>
            </div>
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
                  <th>Balance / Aging</th>
                  <th>Status</th>
                  <th style={{ width: 1 }}></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((m) => {
                  const aging = daysBetween(m.oldestUnpaidChargeAt, nowMs);
                  return (
                    <tr key={m.id}>
                      <td>
                        <Link
                          to={`/members/${m.id}`}
                          className="member-name-link"
                        >
                          <strong>
                            {m.firstName} {m.lastName}
                          </strong>
                        </Link>
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
                          <>
                            <span
                              className={`pill ${
                                aging >= 60
                                  ? "red"
                                  : aging >= 30
                                    ? "gold"
                                    : "gray"
                              }`}
                            >
                              {formatMoney(m.balance)}
                            </span>
                            {m.oldestUnpaidChargeAt && (
                              <div
                                className="muted"
                                style={{ fontSize: 11, marginTop: 2 }}
                              >
                                {aging}d aged
                              </div>
                            )}
                          </>
                        ) : (
                          <span className="muted">$0.00</span>
                        )}
                      </td>
                      <td>
                        <span
                          className={`pill ${
                            m.status === "Active"
                              ? "green"
                              : m.status === "Suspended"
                                ? "red"
                                : "gray"
                          }`}
                        >
                          {m.status}
                        </span>
                        {m.suspendedAt && (
                          <div
                            className="muted"
                            style={{ fontSize: 11, marginTop: 2 }}
                          >
                            since {m.suspendedAt.slice(0, 10)}
                          </div>
                        )}
                      </td>
                      <td>
                        <div className="table-actions">
                          <RequirePermission permission={MEMBERS_SUSPEND}>
                            {m.status === "Suspended" ? (
                              <button
                                className="btn sm secondary"
                                onClick={() => reinstate(m)}
                              >
                                Reinstate
                              </button>
                            ) : m.status === "Active" ? (
                              <button
                                className="btn sm secondary"
                                onClick={() => suspend(m)}
                              >
                                Suspend
                              </button>
                            ) : null}
                          </RequirePermission>
                          <RequirePermission permission={MEMBERS_WRITE}>
                            <button
                              className="btn sm secondary"
                              onClick={() => startEdit(m)}
                            >
                              Edit
                            </button>
                          </RequirePermission>
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

      {tab === "applications" && (
        <>
          <div className="card">
            <h2 style={{ margin: 0, marginBottom: 10 }}>
              Pending review ({appsByStatus.Pending.length})
            </h2>
            {appsByStatus.Pending.length === 0 ? (
              <div className="empty">No applications waiting.</div>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Tier</th>
                    <th>Contact</th>
                    <th>Sponsor</th>
                    <th>Initiation</th>
                    <th>Submitted</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {appsByStatus.Pending.map((a) => {
                    const sponsor = a.sponsoringMemberId
                      ? data.members.find((m) => m.id === a.sponsoringMemberId)
                      : null;
                    return (
                      <tr key={a.id}>
                        <td>
                          <strong>
                            {a.firstName} {a.lastName}
                          </strong>
                          {a.notes && (
                            <div className="muted" style={{ fontSize: 12 }}>
                              {a.notes}
                            </div>
                          )}
                        </td>
                        <td>
                          <span className="pill">{a.requestedTier}</span>
                        </td>
                        <td>
                          <div>{a.email}</div>
                          <div className="muted" style={{ fontSize: 12 }}>
                            {a.phone}
                          </div>
                        </td>
                        <td>
                          {sponsor
                            ? `${sponsor.firstName} ${sponsor.lastName}`
                            : "—"}
                        </td>
                        <td>{formatMoney(a.initiationFee)}</td>
                        <td>{a.submittedAt.slice(0, 10)}</td>
                        <td>
                          <div className="table-actions">
                            <button
                              className="btn sm"
                              onClick={() => openReview(a, "approve")}
                            >
                              Approve
                            </button>
                            <button
                              className="btn sm secondary"
                              onClick={() => openReview(a, "reject")}
                            >
                              Reject
                            </button>
                            <button
                              className="btn sm secondary"
                              onClick={() => {
                                setAppEditing(a);
                                setAppForm({ ...a });
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

          <div className="card">
            <h2 style={{ margin: 0, marginBottom: 10 }}>
              Approved — ready to activate ({appsByStatus.Approved.length})
            </h2>
            {appsByStatus.Approved.length === 0 ? (
              <div className="empty">Nothing approved yet.</div>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Tier</th>
                    <th>Initiation</th>
                    <th>Reviewed by</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {appsByStatus.Approved.map((a) => (
                    <tr key={a.id}>
                      <td>
                        <strong>
                          {a.firstName} {a.lastName}
                        </strong>
                      </td>
                      <td>
                        <span className="pill">{a.requestedTier}</span>
                      </td>
                      <td>{formatMoney(a.initiationFee)}</td>
                      <td>{a.reviewedBy || "—"}</td>
                      <td>
                        <div className="table-actions">
                          <button
                            className="btn sm"
                            onClick={() => activate(a)}
                          >
                            Activate → create member
                          </button>
                          <button
                            className="btn sm secondary"
                            onClick={() => withdraw(a)}
                          >
                            Withdraw
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          {(appsByStatus.Activated.length +
            appsByStatus.Rejected.length +
            appsByStatus.Withdrawn.length >
            0) && (
            <div className="card">
              <h2 style={{ margin: 0, marginBottom: 10 }}>History</h2>
              <table className="table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Status</th>
                    <th>Reviewed</th>
                    <th>Note</th>
                  </tr>
                </thead>
                <tbody>
                  {[
                    ...appsByStatus.Activated,
                    ...appsByStatus.Rejected,
                    ...appsByStatus.Withdrawn,
                  ]
                    .sort((a, b) =>
                      (b.reviewedAt || b.submittedAt).localeCompare(
                        a.reviewedAt || a.submittedAt,
                      ),
                    )
                    .map((a) => (
                      <tr key={a.id}>
                        <td>
                          <strong>
                            {a.firstName} {a.lastName}
                          </strong>
                        </td>
                        <td>
                          <span
                            className={`pill ${
                              a.status === "Activated"
                                ? "green"
                                : a.status === "Rejected"
                                  ? "red"
                                  : "gray"
                            }`}
                          >
                            {a.status}
                          </span>
                        </td>
                        <td>
                          {(a.reviewedAt || a.submittedAt).slice(0, 10)}
                          {a.reviewedBy && (
                            <span className="muted">
                              {" "}
                              by {a.reviewedBy}
                            </span>
                          )}
                        </td>
                        <td className="muted">{a.reviewNote || "—"}</td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {/* ----- Member create/edit modal ----- */}
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
                value={form.status}
                onChange={(e) => {
                  const status = e.target.value as MemberStatus;
                  setForm({
                    ...form,
                    status,
                    active: status === "Active",
                  });
                }}
              >
                <option value="Active">Active</option>
                <option value="Suspended">Suspended</option>
                <option value="Inactive">Inactive</option>
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

      {/* ----- Application create/edit modal ----- */}
      {(appCreating || appEditing) && (
        <Modal
          title={appEditing ? "Edit Application" : "New Membership Application"}
          onClose={() => {
            setAppCreating(false);
            setAppEditing(null);
          }}
          onSubmit={submitApplication}
          submitLabel={appBusy ? "Saving…" : appEditing ? "Save" : "Submit"}
        >
          <div className="grid cols-2">
            <div className="field">
              <label>First name</label>
              <input
                className="input"
                value={appForm.firstName}
                onChange={(e) =>
                  setAppForm({ ...appForm, firstName: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Last name</label>
              <input
                className="input"
                value={appForm.lastName}
                onChange={(e) =>
                  setAppForm({ ...appForm, lastName: e.target.value })
                }
              />
            </div>
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Email</label>
              <input
                className="input"
                value={appForm.email}
                onChange={(e) =>
                  setAppForm({ ...appForm, email: e.target.value })
                }
              />
            </div>
            <div className="field">
              <label>Phone</label>
              <input
                className="input"
                value={appForm.phone}
                onChange={(e) =>
                  setAppForm({ ...appForm, phone: e.target.value })
                }
              />
            </div>
          </div>
          <div className="grid cols-3">
            <div className="field">
              <label>Requested tier</label>
              <select
                className="select"
                value={appForm.requestedTier}
                onChange={(e) =>
                  setAppForm({
                    ...appForm,
                    requestedTier: e.target.value as MembershipTier,
                  })
                }
              >
                {TIERS.map((t) => (
                  <option key={t}>{t}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Sponsoring member</label>
              <select
                className="select"
                value={appForm.sponsoringMemberId ?? ""}
                onChange={(e) =>
                  setAppForm({
                    ...appForm,
                    sponsoringMemberId: e.target.value || null,
                  })
                }
              >
                <option value="">— none —</option>
                {data.members
                  .filter((m) => m.status === "Active")
                  .map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.firstName} {m.lastName}
                    </option>
                  ))}
              </select>
            </div>
            <div className="field">
              <label>Initiation fee ($)</label>
              <input
                className="input"
                type="number"
                step="0.01"
                value={appForm.initiationFee}
                onChange={(e) =>
                  setAppForm({
                    ...appForm,
                    initiationFee: Number(e.target.value),
                  })
                }
              />
            </div>
          </div>
          <div className="field">
            <label>Notes</label>
            <textarea
              className="textarea"
              rows={3}
              value={appForm.notes}
              onChange={(e) =>
                setAppForm({ ...appForm, notes: e.target.value })
              }
            />
          </div>
        </Modal>
      )}

      {/* ----- Application review modal ----- */}
      {reviewing && (
        <Modal
          title={`${reviewing.kind === "approve" ? "Approve" : "Reject"} application`}
          onClose={() => setReviewing(null)}
          onSubmit={submitReview}
          submitLabel={
            appBusy
              ? "Working…"
              : reviewing.kind === "approve"
                ? "Approve"
                : "Reject"
          }
        >
          <div className="muted">
            {reviewing.app.firstName} {reviewing.app.lastName} ·{" "}
            {reviewing.app.requestedTier}
          </div>
          <div className="field">
            <label>Reviewer</label>
            <input
              className="input"
              placeholder="e.g. M. Park"
              value={reviewer}
              onChange={(e) => setReviewer(e.target.value)}
            />
          </div>
          <div className="field">
            <label>Note</label>
            <textarea
              className="textarea"
              rows={3}
              placeholder={
                reviewing.kind === "approve"
                  ? "Optional approval note"
                  : "Reason for rejection"
              }
              value={reviewNote}
              onChange={(e) => setReviewNote(e.target.value)}
            />
          </div>
        </Modal>
      )}
    </div>
  );
}
