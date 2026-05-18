import { useState } from "react";
import { api, ApiError } from "../api/client";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type { ImportResult } from "../data/types";

interface EntityRow {
  key: string;
  label: string;
  templateFile: string;
  importer: (rows: unknown[]) => Promise<ImportResult>;
  note?: string;
}

const ENTITIES: EntityRow[] = [
  {
    key: "nines",
    label: "Nines",
    templateFile: "nines.template.json",
    importer: api.import.nines,
    note: "Each row owns its tee sets, 9 holes, and per-tee yardages.",
  },
  {
    key: "courses",
    label: "Courses",
    templateFile: "courses.template.json",
    importer: api.import.courses,
    note: "Optional FKs to Nines (FrontNineId, BackNineId) — import Nines first.",
  },
  {
    key: "staff",
    label: "Staff",
    templateFile: "staff.template.json",
    importer: api.import.staff,
  },
  {
    key: "products",
    label: "Products",
    templateFile: "products.template.json",
    importer: api.import.products,
  },
  {
    key: "members",
    label: "Members",
    templateFile: "members.template.json",
    importer: api.import.members,
  },
  {
    key: "tee-times",
    label: "Tee times",
    templateFile: "tee-times.template.json",
    importer: api.import.teeTimes,
    note: "Courses must be imported first (FK).",
  },
  {
    key: "tournaments",
    label: "Tournaments",
    templateFile: "tournaments.template.json",
    importer: api.import.tournaments,
    note: "Courses must be imported first (FK).",
  },
  {
    key: "maintenance",
    label: "Maintenance tasks",
    templateFile: "maintenance.template.json",
    importer: api.import.maintenance,
    note: "Optional FKs to Courses and Staff.",
  },
  {
    key: "shifts",
    label: "Shifts",
    templateFile: "shifts.template.json",
    importer: api.import.shifts,
    note: "Staff must be imported first (FK).",
  },
  {
    key: "weekly-templates",
    label: "Weekly templates",
    templateFile: "weekly-templates.template.json",
    importer: api.import.weeklyTemplates,
    note: "Staff must be imported first (FK).",
  },
];

export function Import() {
  const toaster = useToaster();
  const { refresh } = useStore();
  const [results, setResults] = useState<Record<string, ImportResult>>({});
  const [busy, setBusy] = useState<string | null>(null);

  const handleUpload = async (entity: EntityRow, file: File) => {
    setBusy(entity.key);
    try {
      const text = await file.text();
      let parsed: unknown;
      try {
        parsed = JSON.parse(text);
      } catch (e) {
        toaster.push({
          kind: "error",
          message: `${entity.label}: invalid JSON`,
          detail: e instanceof Error ? e.message : undefined,
        });
        setBusy(null);
        return;
      }
      if (!Array.isArray(parsed)) {
        toaster.push({
          kind: "error",
          message: `${entity.label}: expected a JSON array at the top level`,
        });
        setBusy(null);
        return;
      }
      const result = await entity.importer(parsed);
      setResults((r) => ({ ...r, [entity.key]: result }));
      toaster.push({
        kind: result.errors.length === 0 ? "success" : "info",
        message: `${entity.label}: ${result.created} created, ${result.skipped} skipped, ${result.errors.length} errors`,
      });
      // Pull a fresh snapshot so the rest of the app sees the new rows.
      await refresh();
    } catch (e) {
      const msg =
        e instanceof ApiError
          ? `Server error (${e.status})`
          : e instanceof Error
            ? e.message
            : "Unknown error";
      toaster.push({
        kind: "error",
        message: `${entity.label}: ${msg}`,
      });
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="page">
      <div className="card" style={{ marginBottom: 12 }}>
        <h2 style={{ marginTop: 0 }}>Bulk import</h2>
        <p className="muted" style={{ marginTop: 0 }}>
          Provide your initial dataset by uploading one JSON file per entity.
          Each upload validates per-row; invalid rows are reported, valid rows
          commit. Re-uploading a file with the same ids is safe (duplicates
          report <code>id_exists</code> and skip).
        </p>
        <p className="muted">
          Foreign keys are checked against existing data only — import in
          dependency order. Templates and full reference live at{" "}
          <a
            href="/templates/README.md"
            target="_blank"
            rel="noreferrer"
          >
            /templates/README.md
          </a>
          .
        </p>
      </div>

      <div className="card">
        <table className="table">
          <thead>
            <tr>
              <th>Entity</th>
              <th>Template</th>
              <th>Upload</th>
              <th>Last result</th>
            </tr>
          </thead>
          <tbody>
            {ENTITIES.map((e) => {
              const r = results[e.key];
              return (
                <tr key={e.key}>
                  <td>
                    <strong>{e.label}</strong>
                    {e.note && (
                      <div className="muted" style={{ fontSize: 11, marginTop: 2 }}>
                        {e.note}
                      </div>
                    )}
                  </td>
                  <td>
                    <a
                      href={`/templates/${e.templateFile}`}
                      download={e.templateFile}
                    >
                      Download
                    </a>
                  </td>
                  <td>
                    <label
                      className={`btn sm ${busy === e.key ? "secondary" : ""}`}
                      style={{ cursor: "pointer" }}
                    >
                      {busy === e.key ? "Uploading…" : "Upload JSON"}
                      <input
                        type="file"
                        accept="application/json,.json"
                        style={{ display: "none" }}
                        disabled={busy !== null}
                        onChange={(ev) => {
                          const f = ev.target.files?.[0];
                          if (f) void handleUpload(e, f);
                          ev.target.value = "";
                        }}
                      />
                    </label>
                  </td>
                  <td>
                    {r ? (
                      <div>
                        <div>
                          <span className="pill green">{r.created} created</span>{" "}
                          {r.skipped > 0 && (
                            <span className="pill gray">{r.skipped} skipped</span>
                          )}{" "}
                          {r.errors.length > 0 && (
                            <span className="pill red">
                              {r.errors.length} errors
                            </span>
                          )}
                        </div>
                        {r.errors.length > 0 && (
                          <details style={{ marginTop: 6 }}>
                            <summary
                              className="muted"
                              style={{ fontSize: 12, cursor: "pointer" }}
                            >
                              Show errors
                            </summary>
                            <ul style={{ fontSize: 12, marginTop: 6 }}>
                              {r.errors.slice(0, 20).map((err, i) => (
                                <li key={i}>
                                  row {err.index}
                                  {err.id ? ` (${err.id})` : ""}: {err.error}
                                  {err.detail ? ` — ${err.detail}` : ""}
                                </li>
                              ))}
                              {r.errors.length > 20 && (
                                <li className="muted">
                                  …and {r.errors.length - 20} more
                                </li>
                              )}
                            </ul>
                          </details>
                        )}
                      </div>
                    ) : (
                      <span className="muted">—</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
