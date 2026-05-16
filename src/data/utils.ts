import type { Course, DayOfWeek, Shift } from "./types";

export const TEE_SLOT_INTERVAL_MIN = 15;
export const DEFAULT_OPEN = "06:00";
export const DEFAULT_CLOSE = "18:00";

export const DAY_LABELS: Record<DayOfWeek, string> = {
  0: "Sun",
  1: "Mon",
  2: "Tue",
  3: "Wed",
  4: "Thu",
  5: "Fri",
  6: "Sat",
};

export const DAY_FULL: Record<DayOfWeek, string> = {
  0: "Sunday",
  1: "Monday",
  2: "Tuesday",
  3: "Wednesday",
  4: "Thursday",
  5: "Friday",
  6: "Saturday",
};

export function toMinutes(time: string): number {
  const [h, m] = time.split(":").map(Number);
  return h * 60 + m;
}

export function toTimeString(minutes: number): string {
  const m = ((minutes % (24 * 60)) + 24 * 60) % (24 * 60);
  return `${String(Math.floor(m / 60)).padStart(2, "0")}:${String(
    m % 60,
  ).padStart(2, "0")}`;
}

export function courseOpen(course: Course | undefined): string {
  return course?.openTime || DEFAULT_OPEN;
}

export function courseClose(course: Course | undefined): string {
  return course?.closeTime || DEFAULT_CLOSE;
}

export function generateSlots(
  open: string,
  close: string,
  interval = TEE_SLOT_INTERVAL_MIN,
): string[] {
  const start = toMinutes(open);
  const end = toMinutes(close);
  const slots: string[] = [];
  for (let m = start; m <= end; m += interval) {
    slots.push(toTimeString(m));
  }
  return slots;
}

export function snapToSlot(
  time: string,
  open = DEFAULT_OPEN,
  interval = TEE_SLOT_INTERVAL_MIN,
): string {
  const base = toMinutes(open);
  const offset = Math.max(0, toMinutes(time) - base);
  const snapped = base + Math.round(offset / interval) * interval;
  return toTimeString(snapped);
}

export function shiftHours(start: string, end: string): number {
  const minutes = toMinutes(end) - toMinutes(start);
  return Math.max(0, minutes) / 60;
}

export function isoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function parseIso(date: string): Date {
  const [y, m, d] = date.split("-").map(Number);
  return new Date(y, (m ?? 1) - 1, d ?? 1);
}

export function startOfWeek(date: string, weekStartsOn: DayOfWeek = 1): string {
  const d = parseIso(date);
  const day = d.getDay();
  const diff = (day - weekStartsOn + 7) % 7;
  d.setDate(d.getDate() - diff);
  return isoDate(d);
}

export function addDays(date: string, days: number): string {
  const d = parseIso(date);
  d.setDate(d.getDate() + days);
  return isoDate(d);
}

export function weekDates(weekStart: string): string[] {
  return Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
}

export function formatShortDate(date: string): string {
  const d = parseIso(date);
  return d.toLocaleDateString(undefined, {
    weekday: "short",
    month: "numeric",
    day: "numeric",
  });
}

export function formatWeekRange(weekStart: string): string {
  const start = parseIso(weekStart);
  const end = parseIso(addDays(weekStart, 6));
  const sameMonth = start.getMonth() === end.getMonth();
  const startFmt = start.toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });
  const endFmt = end.toLocaleDateString(undefined, {
    month: sameMonth ? undefined : "short",
    day: "numeric",
    year: "numeric",
  });
  return `${startFmt} – ${endFmt}`;
}

export function dayOfWeek(date: string): DayOfWeek {
  return parseIso(date).getDay() as DayOfWeek;
}

export function shiftsForDate(shifts: Shift[], date: string): Shift[] {
  return shifts
    .filter((s) => s.date === date)
    .sort((a, b) => a.start.localeCompare(b.start));
}
