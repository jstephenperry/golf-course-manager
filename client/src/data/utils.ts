import type { Course, DayOfWeek, PlayerTab, Shift } from "./types";

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

export interface TabTotals {
  subtotal: number;
  tax: number;
  tip: number;
  total: number;
  paid: number;
  balance: number;
}

export function tabTotals(tab: PlayerTab): TabTotals {
  const subtotal = tab.items.reduce(
    (sum, li) => sum + li.unitPrice * li.quantity,
    0,
  );
  const tax = subtotal * (tab.taxRate || 0);
  const tip = tab.tipAmount || 0;
  const total = subtotal + tax + tip;
  const paid = tab.payments.reduce((sum, p) => sum + p.amount, 0);
  return {
    subtotal,
    tax,
    tip,
    total,
    paid,
    balance: Math.max(0, total - paid),
  };
}

// Cached formatters — Intl.NumberFormat constructor is non-trivial; reuse
// them across every render rather than building one per call.
const MONEY_FMT = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});
const COUNT_FMT = new Intl.NumberFormat("en-US");

export function formatMoney(amount: number): string {
  return MONEY_FMT.format(amount);
}

// Integer-style display for inventory counts, lifetime totals, etc.
// Adds thousands separators; doesn't touch the sign or units.
export function formatCount(n: number): string {
  return COUNT_FMT.format(n);
}

export function formatDateTime(iso: string): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}
