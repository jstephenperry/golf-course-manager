import { useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { courseNines } from "../data/courseDerived";
import { useStore } from "../data/store";
import type { Hole, Nine } from "../data/types";
import { isoDate } from "../data/utils";

interface NineRows {
  nine: Nine;
  holes: Hole[]; // sorted by Number, length 9
  teeSets: { id: string; name: string; color: string }[]; // sorted by sortOrder
}

function prepareNine(nine: Nine): NineRows {
  const holes = [...nine.holes].sort((a, b) => a.number - b.number);
  const teeSets = [...nine.teeSets].sort((a, b) => a.sortOrder - b.sortOrder);
  return { nine, holes, teeSets };
}

function yardageFor(nine: NineRows, holeIdx: number, teeSetId: string): number | null {
  if (!teeSetId) return null;
  const h = nine.holes[holeIdx];
  if (!h) return null;
  return h.yardages.find((x) => x.teeSetId === teeSetId)?.yards ?? null;
}

function nineTotal(nine: NineRows, teeSetId: string): number {
  if (!teeSetId) return 0;
  return nine.holes.reduce((sum, h) => {
    const y = h.yardages.find((x) => x.teeSetId === teeSetId);
    return sum + (y?.yards ?? 0);
  }, 0);
}

function ninePar(nine: NineRows): number {
  return nine.holes.reduce((sum, h) => sum + (h.par ?? 0), 0);
}

// Match tee sets across two nines by name (case-insensitive). Tour on
// the front and Tour on the back render as one row even though their
// underlying ids differ.
function teeRows(front: NineRows, back: NineRows | null) {
  const rows = front.teeSets.map((t) => ({
    name: t.name,
    color: t.color,
    frontTeeId: t.id,
    backTeeId:
      back?.teeSets.find(
        (b) => b.name.trim().toLowerCase() === t.name.trim().toLowerCase(),
      )?.id ?? "",
  }));
  if (back) {
    for (const t of back.teeSets) {
      if (!rows.some((r) => r.name.trim().toLowerCase() === t.name.trim().toLowerCase())) {
        rows.push({ name: t.name, color: t.color, frontTeeId: "", backTeeId: t.id });
      }
    }
  }
  return rows;
}

type PaperSize = "a6" | "a5" | "letter";
type PrintMode = "score" | "yardages" | "both";

export function Scorecard() {
  const { courseId } = useParams();
  const { data } = useStore();
  const course = data.courses.find((c) => c.id === courseId);
  const { front: frontNine, back: backNine } = course
    ? courseNines(course, data.nines)
    : { front: null, back: null };

  const front = useMemo(
    () => (frontNine ? prepareNine(frontNine) : null),
    [frontNine],
  );
  const back = useMemo(
    () => (backNine ? prepareNine(backNine) : null),
    [backNine],
  );

  const [scoreDate, setScoreDate] = useState(isoDate(new Date()));
  const [playerNames, setPlayerNames] = useState<string[]>(["", "", "", ""]);
  const [paperSize, setPaperSize] = useState<PaperSize>("a6");
  const [printMode, setPrintMode] = useState<PrintMode>("score");

  if (!course) {
    return (
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Course not found</h2>
        <p className="muted">No course with id <code>{courseId}</code>.</p>
        <Link to="/courses" className="btn secondary">← Back to courses</Link>
      </div>
    );
  }

  if (!front) {
    return (
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Not assemblable</h2>
        <p className="muted">
          Assign a front nine to <strong>{course.name}</strong> before printing
          a scorecard.
        </p>
        <Link to="/courses" className="btn secondary">← Back to courses</Link>
      </div>
    );
  }

  const rows = teeRows(front, back);
  const outPar = ninePar(front);
  const inPar = back ? ninePar(back) : 0;
  const totalPar = outPar + inPar;
  const hasBack = back !== null;
  const frontNumbers = front.holes.map((_, i) => i + 1);
  const backNumbers = hasBack ? back!.holes.map((_, i) => i + 10) : [];

  const updatePlayer = (i: number, name: string) =>
    setPlayerNames((prev) => prev.map((p, idx) => (idx === i ? name : p)));

  const ScoreCell = ({ tot = false }: { tot?: boolean }) => (
    <td className={`score-cell${tot ? " total-cell" : ""}`}></td>
  );

  // ---------- shared table layout helpers ----------
  const HoleHeader = () => (
    <thead>
      <tr>
        <th className="row-label">Hole</th>
        {frontNumbers.map((n) => <th key={`fh${n}`}>{n}</th>)}
        <th className="col-total">OUT</th>
        {hasBack && backNumbers.map((n) => <th key={`bh${n}`}>{n}</th>)}
        {hasBack && <th className="col-total">IN</th>}
        {hasBack && <th className="col-total">TOT</th>}
      </tr>
    </thead>
  );

  return (
    <div className="scorecard-page">
      <div className="scorecard-controls card">
        <div className="toolbar">
          <div className="toolbar-left">
            <Link to="/courses" className="btn secondary sm">
              ← Courses
            </Link>
            <div className="field">
              <label>Date</label>
              <input
                className="input"
                type="date"
                value={scoreDate}
                onChange={(e) => setScoreDate(e.target.value)}
              />
            </div>
            <div className="field">
              <label>Paper</label>
              <select
                className="select"
                value={paperSize}
                onChange={(e) => setPaperSize(e.target.value as PaperSize)}
              >
                <option value="a6">A6 landscape (148 × 105 mm)</option>
                <option value="a5">A5 landscape (210 × 148 mm)</option>
                <option value="letter">US Letter landscape (11 × 8.5 in)</option>
              </select>
            </div>
            <div className="field">
              <label>Print</label>
              <select
                className="select"
                value={printMode}
                onChange={(e) => setPrintMode(e.target.value as PrintMode)}
              >
                <option value="score">Score sheet only</option>
                <option value="yardages">Yardage sheet only</option>
                <option value="both">Both (separate pages)</option>
              </select>
            </div>
          </div>
          <button className="btn" onClick={() => window.print()}>
            Print
          </button>
        </div>
        <div className="grid cols-4" style={{ marginTop: 12 }}>
          {playerNames.map((p, i) => (
            <div key={i} className="field">
              <label>Player {i + 1}</label>
              <input
                className="input"
                value={p}
                onChange={(e) => updatePlayer(i, e.target.value)}
                placeholder="(blank — fill on paper)"
              />
            </div>
          ))}
        </div>
        <div className="muted" style={{ fontSize: 12, marginTop: 8 }}>
          The score grid prints empty for handwriting. Both sheets are
          shown below on screen; the Print selector controls what
          actually goes to the printer.
        </div>
      </div>

      {/* @page must be a single value at parse time, so inject it dynamically. */}
      <style>{
        paperSize === "a6"
          ? "@media print { @page { size: A6 landscape; margin: 4mm; } }"
          : paperSize === "a5"
            ? "@media print { @page { size: A5 landscape; margin: 6mm; } }"
            : "@media print { @page { size: letter landscape; margin: 0.4in; } }"
      }</style>

      <div
        className="scorecard"
        data-paper-size={paperSize}
        data-print-mode={printMode}
      >
        {/* ---------- Score sheet ---------- */}
        <section className="scorecard-section section-score">
          <div className="scorecard-header">
            <div>
              <h1>{course.name}</h1>
              <div className="muted">
                {front.nine.name}
                {hasBack ? ` / ${back!.nine.name}` : ""} · {scoreDate}
              </div>
            </div>
            <div className="scorecard-header-meta">
              {course.rating > 0 && (
                <div>Rating <strong>{course.rating.toFixed(1)}</strong></div>
              )}
              {course.slope > 0 && (
                <div>Slope <strong>{course.slope}</strong></div>
              )}
              <div>Par <strong>{totalPar}</strong></div>
            </div>
          </div>

          <table className="scorecard-table">
            <HoleHeader />
            <tbody>
              <tr className="row-par">
                <th className="row-label">Par</th>
                {front.holes.map((h) => <td key={`fp${h.id}`}>{h.par}</td>)}
                <td className="col-total"><strong>{outPar}</strong></td>
                {hasBack && back!.holes.map((h) => <td key={`bp${h.id}`}>{h.par}</td>)}
                {hasBack && <td className="col-total"><strong>{inPar}</strong></td>}
                {hasBack && <td className="col-total"><strong>{totalPar}</strong></td>}
              </tr>

              <tr className="row-hcp">
                <th className="row-label">Hcp</th>
                {front.holes.map((h) => <td key={`fhc${h.id}`}>{h.handicapIndex || "—"}</td>)}
                <td className="col-total">—</td>
                {hasBack && back!.holes.map((h) => <td key={`bhc${h.id}`}>{h.handicapIndex || "—"}</td>)}
                {hasBack && <td className="col-total">—</td>}
                {hasBack && <td className="col-total">—</td>}
              </tr>

              {playerNames.map((name, i) => (
                <tr key={`p${i}`} className="row-player">
                  <th className="row-label">{name || `Player ${i + 1}`}</th>
                  {front.holes.map((h) => <ScoreCell key={`fs${i}${h.id}`} />)}
                  <ScoreCell tot />
                  {hasBack && back!.holes.map((h) => <ScoreCell key={`bs${i}${h.id}`} />)}
                  {hasBack && <ScoreCell tot />}
                  {hasBack && <ScoreCell tot />}
                </tr>
              ))}
            </tbody>
          </table>

          <div className="scorecard-footer muted">
            Hcp = stroke-priority rank within the nine; strokes assigned to
            lowest-index holes first.
          </div>
        </section>

        {/* ---------- Yardage sheet ---------- */}
        <section className="scorecard-section section-yardages">
          <div className="scorecard-header">
            <div>
              <h1>{course.name} — Yardages</h1>
              <div className="muted">
                {front.nine.name}
                {hasBack ? ` / ${back!.nine.name}` : ""}
              </div>
            </div>
            <div className="scorecard-header-meta">
              {course.rating > 0 && (
                <div>Rating <strong>{course.rating.toFixed(1)}</strong></div>
              )}
              {course.slope > 0 && (
                <div>Slope <strong>{course.slope}</strong></div>
              )}
              <div>Par <strong>{totalPar}</strong></div>
            </div>
          </div>

          <table className="scorecard-table">
            <HoleHeader />
            <tbody>
              {rows.map((r) => {
                const out = nineTotal(front, r.frontTeeId);
                const inn = hasBack && r.backTeeId ? nineTotal(back!, r.backTeeId) : 0;
                return (
                  <tr key={r.name} className="row-tee">
                    <th className="row-label" style={{ color: r.color }}>{r.name}</th>
                    {front.holes.map((h, i) => (
                      <td key={h.id}>{yardageFor(front, i, r.frontTeeId) ?? "—"}</td>
                    ))}
                    <td className="col-total">{out > 0 ? out.toLocaleString() : "—"}</td>
                    {hasBack && back!.holes.map((h, i) => (
                      <td key={h.id}>
                        {r.backTeeId ? (yardageFor(back!, i, r.backTeeId) ?? "—") : "—"}
                      </td>
                    ))}
                    {hasBack && (
                      <td className="col-total">{inn > 0 ? inn.toLocaleString() : "—"}</td>
                    )}
                    {hasBack && (
                      <td className="col-total">
                        {(out + inn) > 0 ? (out + inn).toLocaleString() : "—"}
                      </td>
                    )}
                  </tr>
                );
              })}

              <tr className="row-par">
                <th className="row-label">Par</th>
                {front.holes.map((h) => <td key={`yp${h.id}`}>{h.par}</td>)}
                <td className="col-total"><strong>{outPar}</strong></td>
                {hasBack && back!.holes.map((h) => <td key={`ypb${h.id}`}>{h.par}</td>)}
                {hasBack && <td className="col-total"><strong>{inPar}</strong></td>}
                {hasBack && <td className="col-total"><strong>{totalPar}</strong></td>}
              </tr>
            </tbody>
          </table>

          <div className="scorecard-footer muted">
            Tee-name matching is case-insensitive across front and back nines —
            a row spans both nines when the deck exists on each.
          </div>
        </section>
      </div>
    </div>
  );
}
