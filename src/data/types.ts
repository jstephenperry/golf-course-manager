export type MembershipTier = "Social" | "Weekday" | "Full" | "Corporate";

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

export interface DataState {
  members: Member[];
  courses: Course[];
  teeTimes: TeeTime[];
  staff: StaffMember[];
  shifts: Shift[];
  products: Product[];
  tournaments: Tournament[];
  maintenance: MaintenanceTask[];
}
