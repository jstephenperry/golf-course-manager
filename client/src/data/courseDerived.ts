import type { Course, Hole, Nine } from "./types";

// Helpers that compute summary stats for an assembled Course from its
// front + (optional) back Nine. The server doesn't store these — the
// authoritative numbers live in the underlying Hole rows — so the
// client recomputes whenever it renders.

export function courseNines(course: Course, nines: Nine[]): {
  front: Nine | null;
  back: Nine | null;
} {
  const front = course.frontNineId
    ? (nines.find((n) => n.id === course.frontNineId) ?? null)
    : null;
  const back = course.backNineId
    ? (nines.find((n) => n.id === course.backNineId) ?? null)
    : null;
  return { front, back };
}

// True when this course can be booked / shown in the tee sheet. Either
// nine alone makes the course playable (9-hole rounds), front nine
// being the required half.
export function isPlayable(course: Course): boolean {
  return Boolean(course.frontNineId);
}

export function courseHoleCount(course: Course, nines: Nine[]): number {
  const { front, back } = courseNines(course, nines);
  return (front?.holes.length ?? 0) + (back?.holes.length ?? 0);
}

export function coursePar(course: Course, nines: Nine[]): number {
  const { front, back } = courseNines(course, nines);
  return sumPar(front?.holes) + sumPar(back?.holes);
}

// Yardage for the named tee, summed across both nines. Tee names are
// matched case-insensitively across nines so an "Oak/Blue + Redbud/Blue"
// pairing combines naturally even though the underlying tee-set ids
// differ.
export function courseYardage(
  course: Course,
  nines: Nine[],
  teeName: string,
): number {
  const { front, back } = courseNines(course, nines);
  return sumNineYardage(front, teeName) + sumNineYardage(back, teeName);
}

// All distinct tee-set names that appear on either nine (case-folded
// for de-duplication, original casing preserved for display).
export function courseTeeNames(course: Course, nines: Nine[]): string[] {
  const { front, back } = courseNines(course, nines);
  const seen = new Map<string, string>();
  for (const n of [front, back]) {
    if (!n) continue;
    for (const t of n.teeSets) {
      const key = t.name.trim().toLowerCase();
      if (key && !seen.has(key)) seen.set(key, t.name);
    }
  }
  return Array.from(seen.values());
}

function sumPar(holes: Hole[] | undefined): number {
  if (!holes) return 0;
  return holes.reduce((acc, h) => acc + (h.par || 0), 0);
}

function sumNineYardage(nine: Nine | null, teeName: string): number {
  if (!nine) return 0;
  const target = teeName.trim().toLowerCase();
  const tee = nine.teeSets.find((t) => t.name.trim().toLowerCase() === target);
  if (!tee) return 0;
  return nine.holes.reduce((acc, h) => {
    const y = h.yardages.find((x) => x.teeSetId === tee.id);
    return acc + (y?.yards ?? 0);
  }, 0);
}
