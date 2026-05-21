import { describe, expect, it } from "vitest";
import {
  addDays,
  generateSlots,
  isoDate,
  shiftHours,
  snapToSlot,
  startOfWeek,
  tabTotals,
  toMinutes,
  toTimeString,
} from "../data/utils";
import type { PlayerTab } from "../data/types";

describe("time helpers", () => {
  it("toMinutes / toTimeString round-trip", () => {
    expect(toMinutes("08:15")).toBe(8 * 60 + 15);
    expect(toTimeString(495)).toBe("08:15");
  });

  it("generateSlots produces 15-minute grid inclusive of close", () => {
    const slots = generateSlots("08:00", "09:00", 15);
    expect(slots).toEqual(["08:00", "08:15", "08:30", "08:45", "09:00"]);
  });

  it("snapToSlot rounds to nearest interval relative to open", () => {
    expect(snapToSlot("08:08", "08:00", 15)).toBe("08:15");
    expect(snapToSlot("08:02", "08:00", 15)).toBe("08:00");
    expect(snapToSlot("10:24", "06:00", 15)).toBe("10:30");
  });

  it("shiftHours computes hours between two HH:MM times", () => {
    expect(shiftHours("08:00", "12:00")).toBe(4);
    expect(shiftHours("08:30", "12:00")).toBe(3.5);
  });
});

describe("week math", () => {
  it("startOfWeek with Monday-start returns the Monday", () => {
    // 2026-05-16 is a Saturday (already verified contextually);
    // pick a known date instead.
    // 2024-03-13 is a Wednesday → Monday is 2024-03-11
    expect(startOfWeek("2024-03-13", 1)).toBe("2024-03-11");
    expect(startOfWeek("2024-03-11", 1)).toBe("2024-03-11");
    expect(startOfWeek("2024-03-17", 1)).toBe("2024-03-11"); // Sunday → previous Monday
  });

  it("addDays / isoDate work across month boundaries", () => {
    expect(addDays("2024-01-31", 1)).toBe("2024-02-01");
    expect(addDays("2024-03-01", -1)).toBe("2024-02-29");
  });

  it("isoDate formats a Date in local YYYY-MM-DD", () => {
    expect(isoDate(new Date(2024, 0, 5))).toBe("2024-01-05");
  });
});

describe("tabTotals", () => {
  const baseTab: PlayerTab = {
    id: "x",
    openedAt: "",
    status: "Open",
    memberIds: [],
    guests: [],
    items: [
      {
        id: "li1",
        productId: "p1",
        name: "Iced Tea",
        unitPrice: 3.5,
        quantity: 2,
        notes: "",
        addedAt: "",
      },
      {
        id: "li2",
        productId: "p2",
        name: "Ball Dozen",
        unitPrice: 54.99,
        quantity: 1,
        notes: "",
        addedAt: "",
      },
    ],
    payments: [],
    tipAmount: 5,
    taxRate: 0.0825,
    notes: "",
  };

  it("computes subtotal, tax, tip, total, balance", () => {
    const t = tabTotals(baseTab);
    expect(t.subtotal).toBeCloseTo(61.99, 5);
    expect(t.tax).toBeCloseTo(61.99 * 0.0825, 5);
    expect(t.tip).toBe(5);
    expect(t.total).toBeCloseTo(t.subtotal + t.tax + t.tip, 5);
    expect(t.paid).toBe(0);
    expect(t.balance).toBeCloseTo(t.total, 5);
  });

  it("balance clamps at zero when overpaid", () => {
    const tab: PlayerTab = {
      ...baseTab,
      payments: [
        {
          id: "p1",
          method: "Card",
          amount: 999,
          note: "",
          paidAt: "",
        },
      ],
    };
    expect(tabTotals(tab).balance).toBe(0);
  });
});
