import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { ApiError, api } from "../api/client";
import { useToaster } from "../components/Toaster";
import type {
  Course,
  DataState,
  DunningRunResult,
  MaintenanceTask,
  Member,
  MemberApplication,
  MemberOverview,
  PaymentMethod,
  PlayerTab,
  Product,
  Shift,
  StaffMember,
  TeeTime,
  Tournament,
  WeeklyTemplate,
} from "./types";

export const EMPTY_DATA: DataState = {
  members: [],
  courses: [],
  teeTimes: [],
  staff: [],
  shifts: [],
  weeklyTemplates: [],
  products: [],
  tournaments: [],
  maintenance: [],
  tabs: [],
  memberApplications: [],
};

type CollectionKey =
  | "members"
  | "courses"
  | "teeTimes"
  | "staff"
  | "shifts"
  | "weeklyTemplates"
  | "products"
  | "tournaments"
  | "maintenance"
  | "memberApplications";

interface ResourceActions<T extends { id: string }> {
  create: (dto: Omit<T, "id"> & { id?: string }) => Promise<T | null>;
  update: (id: string, dto: T) => Promise<T | null>;
  remove: (id: string) => Promise<boolean>;
}

interface TabsActions {
  create: (dto: Omit<PlayerTab, "id"> & { id?: string }) => Promise<PlayerTab | null>;
  updateMeta: (id: string, dto: PlayerTab) => Promise<PlayerTab | null>;
  void: (id: string) => Promise<PlayerTab | null>;
  settle: (id: string) => Promise<PlayerTab | null>;
  reopen: (id: string) => Promise<PlayerTab | null>;
  addItem: (
    tabId: string,
    productId: string,
    quantity: number,
    notes: string,
  ) => Promise<PlayerTab | null>;
  setItemQuantity: (
    tabId: string,
    itemId: string,
    quantity: number,
  ) => Promise<PlayerTab | null>;
  removeItem: (tabId: string, itemId: string) => Promise<PlayerTab | null>;
  addPayment: (
    tabId: string,
    payment: {
      method: PaymentMethod;
      amount: number;
      payerMemberId?: string;
      note: string;
    },
  ) => Promise<PlayerTab | null>;
  removePayment: (
    tabId: string,
    paymentId: string,
  ) => Promise<PlayerTab | null>;
}

interface StoreApi {
  data: DataState;
  loading: boolean;
  initialized: boolean;
  error: string | null;
  refresh: () => Promise<void>;

  members: ResourceActions<Member> & {
    suspend: (id: string, note?: string) => Promise<Member | null>;
    reinstate: (id: string) => Promise<Member | null>;
    loadOverview: (id: string) => Promise<MemberOverview | null>;
  };
  applications: ResourceActions<MemberApplication> & {
    approve: (
      id: string,
      reviewer: string,
      note: string,
    ) => Promise<MemberApplication | null>;
    reject: (
      id: string,
      reviewer: string,
      note: string,
    ) => Promise<MemberApplication | null>;
    activate: (id: string) => Promise<Member | null>;
    withdraw: (id: string) => Promise<MemberApplication | null>;
  };
  courses: ResourceActions<Course>;
  teeTimes: ResourceActions<TeeTime>;
  staff: ResourceActions<StaffMember>;
  shifts: ResourceActions<Shift>;
  weeklyTemplates: ResourceActions<WeeklyTemplate>;
  products: ResourceActions<Product> & {
    adjustStock: (id: string, delta: number) => Promise<Product | null>;
  };
  tournaments: ResourceActions<Tournament>;
  maintenance: ResourceActions<MaintenanceTask>;
  tabs: TabsActions;

  runDunning: () => Promise<DunningRunResult | null>;

  reset: () => Promise<void>;
  clear: () => Promise<void>;
  exportSnapshot: () => Promise<DataState | null>;
  importSnapshot: (snapshot: DataState) => Promise<boolean>;
}

const StoreContext = createContext<StoreApi | null>(null);

export function StoreProvider({ children }: { children: ReactNode }) {
  const toaster = useToaster();
  const [data, setData] = useState<DataState>(EMPTY_DATA);
  const [loading, setLoading] = useState(false);
  const [initialized, setInitialized] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dataRef = useRef(data);
  dataRef.current = data;

  const handleError = useCallback(
    (action: string, err: unknown) => {
      const message =
        err instanceof ApiError
          ? err.status === 0
            ? "Can't reach the server"
            : `Server error (${err.status})`
          : err instanceof Error
            ? err.message
            : "Unknown error";
      const detail =
        err instanceof ApiError && typeof err.body === "object" && err.body
          ? JSON.stringify(err.body)
          : undefined;
      console.error(`[store] ${action} failed`, err);
      toaster.push({ kind: "error", message: `${action}: ${message}`, detail });
      return null;
    },
    [toaster],
  );

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const snap = await api.snapshot();
      setData({
        members: snap.members ?? [],
        courses: snap.courses ?? [],
        teeTimes: snap.teeTimes ?? [],
        staff: snap.staff ?? [],
        shifts: snap.shifts ?? [],
        weeklyTemplates: snap.weeklyTemplates ?? [],
        products: snap.products ?? [],
        tournaments: snap.tournaments ?? [],
        maintenance: snap.maintenance ?? [],
        tabs: snap.tabs ?? [],
        memberApplications: snap.memberApplications ?? [],
      });
      setError(null);
      setInitialized(true);
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 0
          ? "Can't reach the API server. Is it running?"
          : "Failed to load data from the server.",
      );
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const upsert = useCallback(
    <K extends CollectionKey>(key: K, value: DataState[K][number]) => {
      setData((prev) => {
        const list = prev[key] as Array<{ id: string }>;
        const exists = list.some((x) => x.id === value.id);
        const next = exists
          ? list.map((x) => (x.id === value.id ? value : x))
          : [...list, value];
        return { ...prev, [key]: next } as DataState;
      });
    },
    [],
  );

  const removeFrom = useCallback(
    <K extends CollectionKey>(key: K, id: string) => {
      setData((prev) => {
        const list = prev[key] as Array<{ id: string }>;
        return {
          ...prev,
          [key]: list.filter((x) => x.id !== id),
        } as DataState;
      });
    },
    [],
  );

  const upsertTab = useCallback((tab: PlayerTab) => {
    setData((prev) => {
      const exists = prev.tabs.some((t) => t.id === tab.id);
      return {
        ...prev,
        tabs: exists
          ? prev.tabs.map((t) => (t.id === tab.id ? tab : t))
          : [tab, ...prev.tabs],
      };
    });
  }, []);

  const refreshProducts = useCallback(async () => {
    try {
      const list = await api.products.list();
      setData((prev) => ({ ...prev, products: list }));
    } catch (e) {
      handleError("Refresh products", e);
    }
  }, [handleError]);

  const refreshMembers = useCallback(async () => {
    try {
      const list = await api.members.list();
      setData((prev) => ({ ...prev, members: list }));
    } catch (e) {
      handleError("Refresh members", e);
    }
  }, [handleError]);

  const makeResource = useCallback(
    <T extends { id: string }, K extends CollectionKey>(
      key: K,
      apiResource: {
        create: (dto: Omit<T, "id"> & { id?: string }) => Promise<T>;
        update: (id: string, dto: T) => Promise<T>;
        remove: (id: string) => Promise<void>;
      },
      label: string,
    ): ResourceActions<T> => ({
      create: async (dto) => {
        try {
          const created = await apiResource.create(dto);
          upsert(key, created as unknown as DataState[K][number]);
          return created;
        } catch (e) {
          return handleError(`Create ${label}`, e);
        }
      },
      update: async (id, dto) => {
        try {
          const updated = await apiResource.update(id, dto);
          upsert(key, updated as unknown as DataState[K][number]);
          return updated;
        } catch (e) {
          return handleError(`Update ${label}`, e);
        }
      },
      remove: async (id) => {
        try {
          await apiResource.remove(id);
          removeFrom(key, id);
          return true;
        } catch (e) {
          handleError(`Delete ${label}`, e);
          return false;
        }
      },
    }),
    [upsert, removeFrom, handleError],
  );

  const membersBase = useMemo(
    () => makeResource<Member, "members">("members", api.members, "member"),
    [makeResource],
  );
  const members = useMemo(
    () => ({
      ...membersBase,
      suspend: async (id: string, note?: string) => {
        try {
          const m = await api.members.suspend(id, note);
          upsert("members", m);
          toaster.push({ kind: "success", message: "Member suspended" });
          return m;
        } catch (e) {
          return handleError("Suspend member", e);
        }
      },
      reinstate: async (id: string) => {
        try {
          const m = await api.members.reinstate(id);
          upsert("members", m);
          toaster.push({ kind: "success", message: "Member reinstated" });
          return m;
        } catch (e) {
          return handleError("Reinstate member", e);
        }
      },
      loadOverview: async (id: string) => {
        try {
          return await api.members.getOverview(id);
        } catch (e) {
          return handleError("Load member overview", e);
        }
      },
    }),
    [membersBase, upsert, toaster, handleError],
  );

  const applicationsBase = useMemo(
    () =>
      makeResource<MemberApplication, "memberApplications">(
        "memberApplications",
        api.applications,
        "application",
      ),
    [makeResource],
  );
  const applications = useMemo(
    () => ({
      ...applicationsBase,
      approve: async (id: string, reviewer: string, note: string) => {
        try {
          const a = await api.applications.approve(id, reviewer, note);
          upsert("memberApplications", a);
          toaster.push({ kind: "success", message: "Application approved" });
          return a;
        } catch (e) {
          return handleError("Approve application", e);
        }
      },
      reject: async (id: string, reviewer: string, note: string) => {
        try {
          const a = await api.applications.reject(id, reviewer, note);
          upsert("memberApplications", a);
          toaster.push({ kind: "success", message: "Application rejected" });
          return a;
        } catch (e) {
          return handleError("Reject application", e);
        }
      },
      activate: async (id: string) => {
        try {
          const result = await api.applications.activate(id);
          upsert("memberApplications", result.application);
          upsert("members", result.member);
          toaster.push({
            kind: "success",
            message: `Activated ${result.member.firstName} ${result.member.lastName}`,
          });
          return result.member;
        } catch (e) {
          return handleError("Activate application", e);
        }
      },
      withdraw: async (id: string) => {
        try {
          const a = await api.applications.withdraw(id);
          upsert("memberApplications", a);
          return a;
        } catch (e) {
          return handleError("Withdraw application", e);
        }
      },
    }),
    [applicationsBase, upsert, toaster, handleError],
  );
  const courses = useMemo(
    () => makeResource<Course, "courses">("courses", api.courses, "course"),
    [makeResource],
  );
  const teeTimes = useMemo(
    () =>
      makeResource<TeeTime, "teeTimes">("teeTimes", api.teeTimes, "tee time"),
    [makeResource],
  );
  const staffBase = useMemo(
    () =>
      makeResource<StaffMember, "staff">("staff", api.staff, "staff member"),
    [makeResource],
  );
  const shifts = useMemo(
    () => makeResource<Shift, "shifts">("shifts", api.shifts, "shift"),
    [makeResource],
  );
  const weeklyTemplates = useMemo(
    () =>
      makeResource<WeeklyTemplate, "weeklyTemplates">(
        "weeklyTemplates",
        api.weeklyTemplates,
        "template",
      ),
    [makeResource],
  );
  const productsBase = useMemo(
    () =>
      makeResource<Product, "products">("products", api.products, "product"),
    [makeResource],
  );
  const products = useMemo(
    () => ({
      ...productsBase,
      adjustStock: async (id: string, delta: number) => {
        try {
          const updated = await api.products.adjustStock(id, delta);
          upsert("products", updated);
          return updated;
        } catch (e) {
          return handleError("Adjust stock", e);
        }
      },
    }),
    [productsBase, upsert, handleError],
  );
  const tournaments = useMemo(
    () =>
      makeResource<Tournament, "tournaments">(
        "tournaments",
        api.tournaments,
        "tournament",
      ),
    [makeResource],
  );
  const maintenance = useMemo(
    () =>
      makeResource<MaintenanceTask, "maintenance">(
        "maintenance",
        api.maintenance,
        "maintenance task",
      ),
    [makeResource],
  );

  const staff = useMemo<ResourceActions<StaffMember>>(
    () => ({
      ...staffBase,
      remove: async (id) => {
        const ok = await staffBase.remove(id);
        if (ok) {
          try {
            const [sh, tpl] = await Promise.all([
              api.shifts.list(),
              api.weeklyTemplates.list(),
            ]);
            setData((prev) => ({
              ...prev,
              shifts: sh,
              weeklyTemplates: tpl,
            }));
          } catch (e) {
            handleError("Refresh after staff delete", e);
          }
        }
        return ok;
      },
    }),
    [staffBase, handleError],
  );

  const tabs = useMemo<TabsActions>(
    () => ({
      create: async (dto) => {
        try {
          const t = await api.tabs.create(dto);
          upsertTab({
            ...t,
            items: t.items ?? [],
            payments: t.payments ?? [],
          });
          return t;
        } catch (e) {
          return handleError("Open tab", e);
        }
      },
      updateMeta: async (id, dto) => {
        try {
          const t = await api.tabs.updateMeta(id, dto);
          upsertTab(t);
          return t;
        } catch (e) {
          return handleError("Update tab", e);
        }
      },
      void: async (id) => {
        try {
          const t = await api.tabs.void(id);
          upsertTab(t);
          await Promise.all([refreshProducts(), refreshMembers()]);
          return t;
        } catch (e) {
          return handleError("Void tab", e);
        }
      },
      settle: async (id) => {
        try {
          const t = await api.tabs.settle(id);
          upsertTab(t);
          return t;
        } catch (e) {
          return handleError("Settle tab", e);
        }
      },
      reopen: async (id) => {
        try {
          const t = await api.tabs.reopen(id);
          upsertTab(t);
          return t;
        } catch (e) {
          return handleError("Reopen tab", e);
        }
      },
      addItem: async (tabId, productId, quantity, notes) => {
        try {
          const t = await api.tabs.addItem(tabId, productId, quantity, notes);
          upsertTab(t);
          await refreshProducts();
          return t;
        } catch (e) {
          return handleError("Add item to tab", e);
        }
      },
      setItemQuantity: async (tabId, itemId, quantity) => {
        try {
          const t = await api.tabs.setItemQuantity(tabId, itemId, quantity);
          upsertTab(t);
          await refreshProducts();
          return t;
        } catch (e) {
          return handleError("Update item quantity", e);
        }
      },
      removeItem: async (tabId, itemId) => {
        try {
          const t = await api.tabs.removeItem(tabId, itemId);
          upsertTab(t);
          await refreshProducts();
          return t;
        } catch (e) {
          return handleError("Remove item from tab", e);
        }
      },
      addPayment: async (tabId, payment) => {
        try {
          const t = await api.tabs.addPayment(tabId, payment);
          upsertTab(t);
          if (payment.method === "Member Charge") {
            await refreshMembers();
          }
          return t;
        } catch (e) {
          return handleError("Take payment", e);
        }
      },
      removePayment: async (tabId, paymentId) => {
        try {
          const t = await api.tabs.removePayment(tabId, paymentId);
          upsertTab(t);
          await refreshMembers();
          return t;
        } catch (e) {
          return handleError("Remove payment", e);
        }
      },
    }),
    [upsertTab, handleError, refreshProducts, refreshMembers],
  );

  const reset = useCallback(async () => {
    try {
      await api.reset();
      await refresh();
      toaster.push({ kind: "success", message: "Sample data loaded" });
    } catch (e) {
      handleError("Load sample data", e);
    }
  }, [refresh, toaster, handleError]);

  const clear = useCallback(async () => {
    try {
      await api.clear();
      setData(EMPTY_DATA);
      toaster.push({ kind: "success", message: "All data cleared" });
    } catch (e) {
      handleError("Clear data", e);
    }
  }, [toaster, handleError]);

  const runDunning = useCallback(async () => {
    try {
      const result = await api.dunning.run();
      // Affected members' status may have changed — refresh members.
      await refreshMembers();
      const note =
        result.suspended === 0 && result.reinstated === 0
          ? "No status changes"
          : `Suspended ${result.suspended}, reinstated ${result.reinstated}`;
      toaster.push({ kind: "success", message: `Dunning sweep: ${note}` });
      return result;
    } catch (e) {
      return handleError("Run dunning", e);
    }
  }, [refreshMembers, toaster, handleError]);

  const exportSnapshot = useCallback(async () => {
    try {
      return await api.snapshot();
    } catch (e) {
      return handleError("Export snapshot", e);
    }
  }, [handleError]);

  const importSnapshot = useCallback(
    async (snapshot: DataState) => {
      try {
        await api.restore(snapshot);
        await refresh();
        toaster.push({ kind: "success", message: "Backup restored" });
        return true;
      } catch (e) {
        handleError("Restore backup", e);
        return false;
      }
    },
    [refresh, toaster, handleError],
  );

  const value = useMemo<StoreApi>(
    () => ({
      data,
      loading,
      initialized,
      error,
      refresh,
      members,
      applications,
      courses,
      teeTimes,
      staff,
      shifts,
      weeklyTemplates,
      products,
      tournaments,
      maintenance,
      tabs,
      runDunning,
      reset,
      clear,
      exportSnapshot,
      importSnapshot,
    }),
    [
      data,
      loading,
      initialized,
      error,
      refresh,
      members,
      applications,
      courses,
      teeTimes,
      staff,
      shifts,
      weeklyTemplates,
      products,
      tournaments,
      maintenance,
      tabs,
      runDunning,
      reset,
      clear,
      exportSnapshot,
      importSnapshot,
    ],
  );

  return (
    <StoreContext.Provider value={value}>{children}</StoreContext.Provider>
  );
}

export function useStore(): StoreApi {
  const ctx = useContext(StoreContext);
  if (!ctx) throw new Error("useStore must be used inside StoreProvider");
  return ctx;
}

export function uid(prefix = "id"): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 9)}${Date.now().toString(36).slice(-3)}`;
}
