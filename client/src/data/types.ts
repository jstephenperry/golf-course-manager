export type MembershipTier = "Social" | "Weekday" | "Full" | "Corporate";

export type MemberStatus = "Active" | "Suspended" | "Inactive";

export interface Member {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  tier: MembershipTier;
  handicap: number;
  joinDate: string;
  active: boolean;
  balance: number;
  status: MemberStatus;
  oldestUnpaidChargeAt: string | null;
  suspendedAt: string | null;
  notes: string;
}

export interface MemberOverview {
  member: Member;
  lastPlayedDate: string | null;
  lifetimeRounds: number;
  recentRounds: TeeTime[];
}

export type LedgerEntryType = "Charge" | "Payment" | "Reversal";
export type LedgerSourceKind = "Manual" | "Tab" | "Application";

// Strict-validated server-side; documented for the UI dropdowns.
// "Payment" is reserved for the auto-set category on Payment-type entries
// and must NOT be offered as a charge category.
export const LEDGER_CHARGE_CATEGORIES = [
  "Dues",
  "F&B",
  "ProShop",
  "Tournament",
  "Initiation",
  "Lesson",
  "Adjustment",
] as const;
export type LedgerChargeCategory = (typeof LEDGER_CHARGE_CATEGORIES)[number];

export const LEDGER_METHODS = ["Cash", "Card", "Check", "ACH"] as const;
export type LedgerMethod = (typeof LEDGER_METHODS)[number];

export interface MemberLedgerEntry {
  id: string;
  memberId: string;
  entryType: LedgerEntryType;
  category: string;
  amount: number;
  method: string | null;
  note: string;
  postedAt: string;
  sourceKind: LedgerSourceKind;
  sourceId: string | null;
  reversesEntryId: string | null;
  voidedAt: string | null;
  voidedByEntryId: string | null;
}

export interface MemberLedgerList {
  entries: MemberLedgerEntry[];
  hasMore: boolean;
}

// Per-row result reported by /api/import/<entity>.
export interface ImportRowError {
  index: number;
  id: string | null;
  error: string;
  detail: string | null;
}

export interface ImportResult {
  created: number;
  skipped: number;
  errors: ImportRowError[];
}

export type ApplicationStatus =
  | "Pending"
  | "Approved"
  | "Rejected"
  | "Activated"
  | "Withdrawn";

export interface MemberApplication {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  requestedTier: MembershipTier;
  sponsoringMemberId: string | null;
  initiationFee: number;
  notes: string;
  status: ApplicationStatus;
  submittedAt: string;
  reviewedAt: string | null;
  reviewedBy: string | null;
  reviewNote: string | null;
  activatedMemberId: string | null;
}

export interface DunningRunResult {
  suspended: number;
  reinstated: number;
  affectedMemberIds: string[];
}

export interface Course {
  id: string;
  name: string;
  holes: number;
  par: number;
  yardage: number;
  rating: number;
  slope: number;
  status: "Open" | "Closed" | "Cart Path Only";
  openTime: string;
  closeTime: string;
  notes: string;
}

export interface TeeTime {
  id: string;
  date: string;
  time: string;
  courseId: string;
  players: string[];
  cart: boolean;
  status: "Booked" | "Checked In" | "Completed" | "Cancelled";
  notes: string;
}

export type StaffRole =
  | "Pro"
  | "Assistant Pro"
  | "Greenkeeper"
  | "Caddie Master"
  | "Server"
  | "Manager"
  | "Pro Shop"
  | "Maintenance";

export interface StaffMember {
  id: string;
  firstName: string;
  lastName: string;
  role: StaffRole;
  email: string;
  phone: string;
  hourlyRate: number;
  active: boolean;
}

export interface Shift {
  id: string;
  staffId: string;
  date: string;
  start: string;
  end: string;
  notes: string;
}

export type DayOfWeek = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export interface WeeklyTemplate {
  id: string;
  staffId: string;
  dayOfWeek: DayOfWeek;
  start: string;
  end: string;
  notes: string;
}

export type ProductCategory =
  | "Clubs"
  | "Balls"
  | "Apparel"
  | "Accessories"
  | "Food & Beverage";

export interface Product {
  id: string;
  name: string;
  category: ProductCategory;
  sku: string;
  price: number;
  cost: number;
  stock: number;
  reorderLevel: number;
}

export interface Tournament {
  id: string;
  name: string;
  date: string;
  format: "Stroke Play" | "Match Play" | "Scramble" | "Best Ball" | "Stableford";
  courseId: string;
  entryFee: number;
  maxPlayers: number;
  registered: string[];
  status: "Scheduled" | "In Progress" | "Completed" | "Cancelled";
}

export type MaintenanceCategory =
  | "Mowing"
  | "Irrigation"
  | "Aeration"
  | "Bunker"
  | "Greens"
  | "Tees"
  | "Equipment"
  | "Other";

export interface MaintenanceTask {
  id: string;
  title: string;
  category: MaintenanceCategory;
  courseId: string;
  assignedTo: string;
  dueDate: string;
  priority: "Low" | "Medium" | "High";
  status: "Open" | "In Progress" | "Completed";
  notes: string;
}

export type TabStatus = "Open" | "Settled" | "Voided";

export type PaymentMethod = "Cash" | "Card" | "Member Charge" | "Comp";

export interface TabLineItem {
  id: string;
  productId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  notes: string;
  addedAt: string;
}

export interface TabPayment {
  id: string;
  method: PaymentMethod;
  amount: number;
  payerMemberId?: string;
  note: string;
  paidAt: string;
}

export interface PlayerTab {
  id: string;
  openedAt: string;
  closedAt?: string;
  status: TabStatus;
  memberIds: string[];
  guests: string[];
  teeTimeId?: string;
  items: TabLineItem[];
  payments: TabPayment[];
  tipAmount: number;
  taxRate: number;
  notes: string;
}

export interface DataState {
  members: Member[];
  courses: Course[];
  teeTimes: TeeTime[];
  staff: StaffMember[];
  shifts: Shift[];
  weeklyTemplates: WeeklyTemplate[];
  products: Product[];
  tournaments: Tournament[];
  maintenance: MaintenanceTask[];
  tabs: PlayerTab[];
  memberApplications: MemberApplication[];
}
