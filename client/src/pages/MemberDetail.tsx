import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type { MemberOverview } from "../data/types";
import { formatMoney } from "../data/utils";

export function MemberDetail() {
  const { memberId } = useParams();
  const { data, members } = useStore();
  const toaster = useToaster();

  const member = useMemo(
    () => data.members.find((m) => m.id === memberId) ?? null,
    [data.members, memberId],
  );

  const [overview, setOverview] = useState<MemberOverview | null>(null);
  const [overviewLoading, setOverviewLoading] = useState(false);

  const [notesDraft, setNotesDraft] = useState("");
  const [savingNotes, setSavingNotes] = useState(false);

  // Pull a fresh overview when the URL id changes or when tee-times data has
  // moved (a new round added in another tab would otherwise look stale).
  useEffect(() => {
    if (!memberId) return;
    setOverviewLoading(true);
    members
      .loadOverview(memberId)
      .then((o) => setOverview(o))
      .finally(() => setOverviewLoading(false));
  }, [memberId, members, data.teeTimes.length]);

  // Keep the notes editor in sync with the canonical store record. We trust
  // the store entry (already updated by save()) over the snapshot inside
  // `overview`, which is fetched separately.
  useEffect(() => {
    setNotesDraft(member?.notes ?? "");
  }, [member?.id, member?.notes]);

  if (!memberId) return null;

  if (!member) {
    return (
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Member not found</h2>
        <p className="muted">
          No member with id <code>{memberId}</code>.
        </p>
        <Link to="/members" className="btn secondary">
          ← Back to members
        </Link>
      </div>
    );
  }

  const courseName = (id: string) =>
    data.courses.find((c) => c.id === id)?.name ?? id;

  const dirty = notesDraft !== (member.notes ?? "");

  const saveNotes = async () => {
    setSavingNotes(true);
    try {
      const updated = await members.update(member.id, {
        ...member,
        notes: notesDraft,
      });
      if (updated) toaster.push({ kind: "success", message: "Notes saved" });
    } finally {
      setSavingNotes(false);
    }
  };

  return (
    <div className="page member-detail">
      <div className="toolbar" style={{ marginBottom: 12 }}>
        <Link to="/members" className="btn secondary sm">
          ← Members
        </Link>
      </div>

      {/* Header strip */}
      <div className="card" style={{ marginBottom: 12 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            gap: 16,
            flexWrap: "wrap",
          }}
        >
          <div>
            <h2 style={{ margin: 0 }}>
              {member.firstName} {member.lastName}
            </h2>
            <div className="muted" style={{ marginTop: 4 }}>
              {member.email} · {member.phone}
            </div>
          </div>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <span
              className={`pill ${
                member.tier === "Full"
                  ? "green"
                  : member.tier === "Corporate"
                    ? "blue"
                    : member.tier === "Weekday"
                      ? "gold"
                      : ""
              }`}
            >
              {member.tier}
            </span>
            <span
              className={`pill ${
                member.status === "Active"
                  ? "green"
                  : member.status === "Suspended"
                    ? "red"
                    : "gray"
              }`}
            >
              {member.status}
            </span>
          </div>
        </div>
      </div>

      <div className="grid cols-2" style={{ marginBottom: 12 }}>
        {/* Profile card */}
        <div className="card">
          <h3 style={{ marginTop: 0 }}>Profile</h3>
          <dl
            style={{
              display: "grid",
              gridTemplateColumns: "max-content 1fr",
              columnGap: 16,
              rowGap: 6,
              margin: 0,
            }}
          >
            <dt className="muted">Handicap</dt>
            <dd style={{ margin: 0 }}>{member.handicap.toFixed(1)}</dd>
            <dt className="muted">Joined</dt>
            <dd style={{ margin: 0 }}>{member.joinDate}</dd>
            <dt className="muted">Balance</dt>
            <dd style={{ margin: 0 }}>
              {member.balance > 0 ? formatMoney(member.balance) : "$0.00"}
            </dd>
            {member.oldestUnpaidChargeAt && (
              <>
                <dt className="muted">Oldest unpaid</dt>
                <dd style={{ margin: 0 }}>
                  {member.oldestUnpaidChargeAt.slice(0, 10)}
                </dd>
              </>
            )}
            {member.suspendedAt && (
              <>
                <dt className="muted">Suspended</dt>
                <dd style={{ margin: 0 }}>{member.suspendedAt.slice(0, 10)}</dd>
              </>
            )}
          </dl>
        </div>

        {/* Activity stats */}
        <div className="card">
          <h3 style={{ marginTop: 0 }}>Activity</h3>
          <div className="grid cols-3">
            <div className="kpi">
              <span className="label">Lifetime rounds</span>
              <span className="value">
                {overview ? overview.lifetimeRounds : "—"}
              </span>
            </div>
            <div className="kpi">
              <span className="label">Last played</span>
              <span className="value" style={{ fontSize: 18 }}>
                {overview
                  ? (overview.lastPlayedDate ?? "Never")
                  : "—"}
              </span>
            </div>
            <div className="kpi">
              <span className="label">Balance</span>
              <span className="value" style={{ fontSize: 18 }}>
                {formatMoney(member.balance)}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Recent rounds */}
      <div className="card" style={{ marginBottom: 12 }}>
        <h3 style={{ marginTop: 0 }}>Recent rounds</h3>
        {overviewLoading && !overview ? (
          <div className="muted">Loading…</div>
        ) : !overview || overview.recentRounds.length === 0 ? (
          <div className="empty">No rounds yet.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Time</th>
                <th>Course</th>
                <th>Cart</th>
                <th>Notes</th>
              </tr>
            </thead>
            <tbody>
              {overview.recentRounds.map((r) => (
                <tr key={r.id}>
                  <td>{r.date}</td>
                  <td>{r.time}</td>
                  <td>{courseName(r.courseId)}</td>
                  <td>{r.cart ? "Cart" : "Walking"}</td>
                  <td className="muted">{r.notes}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Staff notes */}
      <div className="card">
        <h3 style={{ marginTop: 0 }}>Staff notes</h3>
        <p className="muted" style={{ marginTop: 0 }}>
          Internal CRM notes. Visible to all staff.
        </p>
        <textarea
          className="textarea"
          value={notesDraft}
          onChange={(e) => setNotesDraft(e.target.value)}
          rows={6}
          maxLength={5000}
          placeholder="e.g., walks only, allergic to bees, prefers caddie Jim…"
          style={{ width: "100%" }}
        />
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginTop: 8,
          }}
        >
          <span className="muted" style={{ fontSize: 12 }}>
            {notesDraft.length} / 5000
          </span>
          <button
            className="btn primary"
            onClick={saveNotes}
            disabled={!dirty || savingNotes}
          >
            {savingNotes ? "Saving…" : "Save notes"}
          </button>
        </div>
      </div>
    </div>
  );
}
