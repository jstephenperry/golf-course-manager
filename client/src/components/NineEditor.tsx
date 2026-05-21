import { useEffect, useMemo, useRef, useState } from "react";
import { Modal } from "./Modal";
import { useToaster } from "./Toaster";
import { useStore, uid } from "../data/store";
import type { Hole, HoleYardage, Nine, NineTeeSet } from "../data/types";

type Props = {
  // null = create; an existing Nine = edit.
  nine: Nine | null;
  onClose: () => void;
};

const DEFAULT_TEES: Array<{ name: string; color: string }> = [
  { name: "Black", color: "#111111" },
  { name: "Blue", color: "#1f4d8a" },
  { name: "White", color: "#dddddd" },
  { name: "Red", color: "#b73838" },
];

// Build a fresh editable draft. When creating, this gives the user a
// standard 4-tee, 9-hole skeleton with sensible pars so they only need
// to fill in yardages. When editing, we deep-clone the server's Nine.
function makeDraft(existing: Nine | null): Nine {
  if (existing) {
    return {
      ...existing,
      teeSets: existing.teeSets.map((t) => ({ ...t })),
      holes: existing.holes.map((h) => ({
        ...h,
        yardages: h.yardages.map((y) => ({ ...y })),
      })),
    };
  }
  const teeSets: NineTeeSet[] = DEFAULT_TEES.map((t, i) => ({
    id: uid("nts"),
    nineId: "",
    name: t.name,
    color: t.color,
    sortOrder: i,
  }));
  const holes: Hole[] = Array.from({ length: 9 }, (_, i) => {
    const holeId = uid("h");
    return {
      id: holeId,
      nineId: "",
      number: i + 1,
      par: 4,
      handicapIndex: i + 1,
      notes: "",
      yardages: teeSets.map((t) => ({
        id: uid("hy"),
        holeId,
        teeSetId: t.id,
        yards: 0,
      })),
    };
  });
  return {
    id: "",
    name: "",
    description: "",
    notes: "",
    teeSets,
    holes,
  };
}

// Tag every numeric cell with a stable key so we can compute the next
// input to focus on Enter. Column order: par → hcp → tee1..teeN →
// (next hole). Notes are excluded from the Enter chain to avoid
// blowing past them with a stray keystroke.
function cellKey(holeIdx: number, col: string) {
  return `cell-${holeIdx}-${col}`;
}

export function NineEditor({ nine, onClose }: Props) {
  const { nines: api } = useStore();
  const toaster = useToaster();
  const [busy, setBusy] = useState(false);
  const [draft, setDraft] = useState<Nine>(() => makeDraft(nine));
  const nameRef = useRef<HTMLInputElement | null>(null);
  const cellsRef = useRef<Map<string, HTMLInputElement>>(new Map());

  // Re-seed the draft when the parent swaps which nine we're editing.
  useEffect(() => {
    setDraft(makeDraft(nine));
  }, [nine]);

  // Focus the name field on open so users can start typing immediately.
  useEffect(() => {
    nameRef.current?.focus();
  }, []);

  const totals = useMemo(() => {
    const par = draft.holes.reduce((acc, h) => acc + (h.par || 0), 0);
    const perTee: Record<string, number> = {};
    for (const t of draft.teeSets) {
      perTee[t.id] = draft.holes.reduce((acc, h) => {
        const y = h.yardages.find((x) => x.teeSetId === t.id);
        return acc + (y?.yards ?? 0);
      }, 0);
    }
    return { par, perTee };
  }, [draft]);

  // Build the linear column order used by Enter-to-advance.
  const columnOrder = useMemo(() => {
    const cols = ["par", "hcp", ...draft.teeSets.map((t) => `tee-${t.id}`)];
    return cols;
  }, [draft.teeSets]);

  const focusCell = (holeIdx: number, col: string) => {
    const el = cellsRef.current.get(cellKey(holeIdx, col));
    if (el) {
      el.focus();
      el.select();
    }
  };

  const advance = (holeIdx: number, col: string) => {
    const idx = columnOrder.indexOf(col);
    if (idx < 0) return;
    if (idx < columnOrder.length - 1) {
      focusCell(holeIdx, columnOrder[idx + 1]);
    } else if (holeIdx < draft.holes.length - 1) {
      // Wrap to the next hole's first column.
      focusCell(holeIdx + 1, columnOrder[0]);
    }
  };

  const setHole = (idx: number, patch: Partial<Hole>) => {
    setDraft((d) => ({
      ...d,
      holes: d.holes.map((h, i) => (i === idx ? { ...h, ...patch } : h)),
    }));
  };

  // Update one (hole, tee) yardage cell. If the cell row doesn't exist
  // yet (e.g. after adding a new tee set), insert it.
  const setYardage = (holeIdx: number, teeSetId: string, yards: number) => {
    setDraft((d) => ({
      ...d,
      holes: d.holes.map((h, i) => {
        if (i !== holeIdx) return h;
        const existing = h.yardages.find((y) => y.teeSetId === teeSetId);
        const nextYardages: HoleYardage[] = existing
          ? h.yardages.map((y) =>
              y.teeSetId === teeSetId ? { ...y, yards } : y,
            )
          : [
              ...h.yardages,
              { id: uid("hy"), holeId: h.id, teeSetId, yards },
            ];
        return { ...h, yardages: nextYardages };
      }),
    }));
  };

  const addTeeSet = () => {
    const id = uid("nts");
    setDraft((d) => ({
      ...d,
      teeSets: [
        ...d.teeSets,
        {
          id,
          nineId: d.id,
          name: "New Tee",
          color: "#888888",
          sortOrder: d.teeSets.length,
        },
      ],
      holes: d.holes.map((h) => ({
        ...h,
        yardages: [
          ...h.yardages,
          { id: uid("hy"), holeId: h.id, teeSetId: id, yards: 0 },
        ],
      })),
    }));
  };

  const removeTeeSet = (id: string) => {
    setDraft((d) => ({
      ...d,
      teeSets: d.teeSets.filter((t) => t.id !== id),
      holes: d.holes.map((h) => ({
        ...h,
        yardages: h.yardages.filter((y) => y.teeSetId !== id),
      })),
    }));
  };

  const setTeeSet = (id: string, patch: Partial<NineTeeSet>) => {
    setDraft((d) => ({
      ...d,
      teeSets: d.teeSets.map((t) => (t.id === id ? { ...t, ...patch } : t)),
    }));
  };

  // Bulk helpers — frequent shortcuts when rapidly seeding a new nine
  // from a printed scorecard.
  const setAllPar = (par: number) => {
    setDraft((d) => ({
      ...d,
      holes: d.holes.map((h) => ({ ...h, par })),
    }));
  };
  const sequentialHandicaps = () => {
    setDraft((d) => ({
      ...d,
      holes: d.holes.map((h, i) => ({ ...h, handicapIndex: i + 1 })),
    }));
  };

  const save = async () => {
    if (!draft.name.trim()) {
      toaster.push({ kind: "error", message: "Nine name is required" });
      return;
    }
    if (draft.holes.length !== 9) {
      toaster.push({
        kind: "error",
        message: "A nine must have exactly 9 holes",
      });
      return;
    }
    setBusy(true);
    const result = nine
      ? await api.update(nine.id, draft)
      : await api.create(draft);
    setBusy(false);
    if (!result) return;
    toaster.push({
      kind: "success",
      message: nine ? "Nine updated" : "Nine created",
    });
    onClose();
  };

  // Shared handler for every numeric cell. Enter advances to the next
  // input; arrow up/down jumps row-wise within a column.
  const cellKeyDown = (
    e: React.KeyboardEvent<HTMLInputElement>,
    holeIdx: number,
    col: string,
  ) => {
    if (e.key === "Enter") {
      e.preventDefault();
      advance(holeIdx, col);
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      if (holeIdx < draft.holes.length - 1) focusCell(holeIdx + 1, col);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      if (holeIdx > 0) focusCell(holeIdx - 1, col);
    }
  };

  const registerCell = (holeIdx: number, col: string) =>
    (el: HTMLInputElement | null) => {
      const key = cellKey(holeIdx, col);
      if (el) cellsRef.current.set(key, el);
      else cellsRef.current.delete(key);
    };

  return (
    <Modal
      title={nine ? `Edit Nine — ${nine.name}` : "Add Nine"}
      onClose={onClose}
      onSubmit={save}
      submitLabel={busy ? "Saving…" : nine ? "Save" : "Create"}
      size="wide"
    >
      <div className="grid cols-2">
        <div className="field">
          <label>Name</label>
          <input
            ref={nameRef}
            className="input"
            placeholder="e.g. Oak"
            value={draft.name}
            onChange={(e) => setDraft({ ...draft, name: e.target.value })}
          />
        </div>
        <div className="field">
          <label>Short description</label>
          <input
            className="input"
            placeholder="e.g. Tree-lined, water on 3 & 7"
            value={draft.description}
            onChange={(e) =>
              setDraft({ ...draft, description: e.target.value })
            }
          />
        </div>
      </div>

      <div className="field">
        <label>
          Tee sets{" "}
          <span className="muted" style={{ fontWeight: "normal" }}>
            (defines the yardage columns below)
          </span>
        </label>
        <table className="table compact">
          <thead>
            <tr>
              <th>Name</th>
              <th style={{ width: 90 }}>Color</th>
              <th style={{ width: 80 }}>Order</th>
              <th style={{ width: 1 }}></th>
            </tr>
          </thead>
          <tbody>
            {draft.teeSets.map((t) => (
              <tr key={t.id}>
                <td>
                  <input
                    className="input"
                    style={{ textAlign: "left" }}
                    value={t.name}
                    onChange={(e) =>
                      setTeeSet(t.id, { name: e.target.value })
                    }
                  />
                </td>
                <td>
                  <input
                    className="input"
                    type="color"
                    value={t.color || "#888888"}
                    onChange={(e) =>
                      setTeeSet(t.id, { color: e.target.value })
                    }
                  />
                </td>
                <td>
                  <input
                    className="input"
                    type="number"
                    value={t.sortOrder}
                    onChange={(e) =>
                      setTeeSet(t.id, {
                        sortOrder: Number(e.target.value),
                      })
                    }
                  />
                </td>
                <td>
                  <button
                    type="button"
                    className="btn sm danger"
                    onClick={() => removeTeeSet(t.id)}
                    disabled={draft.teeSets.length <= 1}
                    title={
                      draft.teeSets.length <= 1
                        ? "At least one tee set is required"
                        : "Remove tee set"
                    }
                  >
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <button
          type="button"
          className="btn sm secondary"
          onClick={addTeeSet}
          style={{ marginTop: 8 }}
        >
          + Add tee set
        </button>
      </div>

      <div className="field">
        <div className="row between" style={{ alignItems: "flex-end" }}>
          <label style={{ marginBottom: 0 }}>Holes</label>
          <div className="row" style={{ gap: 4, flexWrap: "wrap" }}>
            <span className="muted" style={{ fontSize: 12, marginRight: 6 }}>
              Quick fill:
            </span>
            <button
              type="button"
              className="btn sm secondary"
              onClick={() => setAllPar(3)}
            >
              All Par 3
            </button>
            <button
              type="button"
              className="btn sm secondary"
              onClick={() => setAllPar(4)}
            >
              All Par 4
            </button>
            <button
              type="button"
              className="btn sm secondary"
              onClick={() => setAllPar(5)}
            >
              All Par 5
            </button>
            <button
              type="button"
              className="btn sm secondary"
              onClick={sequentialHandicaps}
              title="Set handicap indices 1..9 in hole order"
            >
              HCP 1–9
            </button>
          </div>
        </div>
        <table className="table compact">
          <thead>
            <tr>
              <th style={{ width: 40 }}>#</th>
              <th style={{ width: 60 }}>Par</th>
              <th style={{ width: 60 }}>HCP</th>
              {draft.teeSets.map((t) => (
                <th key={t.id} style={{ width: 80 }}>
                  <span
                    title={t.name}
                    style={{
                      display: "inline-block",
                      width: 10,
                      height: 10,
                      borderRadius: "50%",
                      background: t.color || "#888",
                      marginRight: 6,
                      verticalAlign: "middle",
                    }}
                  />
                  {t.name || "Tee"}
                </th>
              ))}
              <th>Notes</th>
            </tr>
          </thead>
          <tbody>
            {draft.holes.map((h, i) => (
              <tr key={h.id}>
                <td>
                  <strong>{h.number}</strong>
                </td>
                <td>
                  <input
                    ref={registerCell(i, "par")}
                    className="input"
                    type="number"
                    min={3}
                    max={6}
                    value={h.par}
                    onFocus={(e) => e.currentTarget.select()}
                    onKeyDown={(e) => cellKeyDown(e, i, "par")}
                    onChange={(e) =>
                      setHole(i, { par: Number(e.target.value) })
                    }
                  />
                </td>
                <td>
                  <input
                    ref={registerCell(i, "hcp")}
                    className="input"
                    type="number"
                    min={1}
                    max={9}
                    value={h.handicapIndex}
                    onFocus={(e) => e.currentTarget.select()}
                    onKeyDown={(e) => cellKeyDown(e, i, "hcp")}
                    onChange={(e) =>
                      setHole(i, {
                        handicapIndex: Number(e.target.value),
                      })
                    }
                  />
                </td>
                {draft.teeSets.map((t) => {
                  const col = `tee-${t.id}`;
                  const y = h.yardages.find((x) => x.teeSetId === t.id);
                  return (
                    <td key={t.id}>
                      <input
                        ref={registerCell(i, col)}
                        className="input"
                        type="number"
                        min={0}
                        value={y?.yards ?? 0}
                        onFocus={(e) => e.currentTarget.select()}
                        onKeyDown={(e) => cellKeyDown(e, i, col)}
                        onChange={(e) =>
                          setYardage(i, t.id, Number(e.target.value))
                        }
                      />
                    </td>
                  );
                })}
                <td>
                  <input
                    className="input"
                    style={{ textAlign: "left" }}
                    value={h.notes}
                    onChange={(e) =>
                      setHole(i, { notes: e.target.value })
                    }
                  />
                </td>
              </tr>
            ))}
            <tr>
              <td>
                <strong>Total</strong>
              </td>
              <td>
                <strong>{totals.par}</strong>
              </td>
              <td></td>
              {draft.teeSets.map((t) => (
                <td key={t.id}>
                  <strong>{totals.perTee[t.id]?.toLocaleString() ?? 0}</strong>
                </td>
              ))}
              <td></td>
            </tr>
          </tbody>
        </table>
        <div className="muted" style={{ fontSize: 12, marginTop: 6 }}>
          Tip: Enter advances to the next field, ↑/↓ jump within a column.
        </div>
      </div>

      <div className="field">
        <label>Nine notes</label>
        <textarea
          className="textarea"
          rows={2}
          value={draft.notes}
          onChange={(e) => setDraft({ ...draft, notes: e.target.value })}
        />
      </div>
    </Modal>
  );
}
