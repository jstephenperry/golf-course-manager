import type {
  Course,
  DataState,
  DunningRunResult,
  MaintenanceTask,
  Member,
  MemberApplication,
  PaymentMethod,
  PlayerTab,
  Product,
  Shift,
  StaffMember,
  TeeTime,
  Tournament,
  WeeklyTemplate,
} from "../data/types";

const API_BASE =
  (import.meta.env.VITE_API_BASE as string | undefined) ?? "/api";

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;
  constructor(message: string, status: number, body: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.body = body;
  }
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  init?: RequestInit,
): Promise<T> {
  const url = `${API_BASE}${path}`;
  const headers: Record<string, string> = {
    Accept: "application/json",
    ...((init?.headers as Record<string, string>) ?? {}),
  };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  let res: Response;
  try {
    res = await fetch(url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      ...init,
    });
  } catch (err) {
    throw new ApiError(
      err instanceof Error ? err.message : "network error",
      0,
      null,
    );
  }
  if (res.status === 204) return undefined as T;
  const contentType = res.headers.get("content-type") ?? "";
  const payload = contentType.includes("application/json")
    ? await res.json().catch(() => null)
    : await res.text();
  if (!res.ok) {
    throw new ApiError(
      `${method} ${path} → ${res.status}`,
      res.status,
      payload,
    );
  }
  return payload as T;
}

type CreatePayload = {
  method: PaymentMethod;
  amount: number;
  payerMemberId?: string;
  note: string;
};

const resource = <T extends { id: string }>(path: string) => ({
  list: () => request<T[]>("GET", path),
  create: (dto: Omit<T, "id"> & { id?: string }) =>
    request<T>("POST", path, dto),
  update: (id: string, dto: T) => request<T>("PUT", `${path}/${id}`, dto),
  remove: (id: string) => request<void>("DELETE", `${path}/${id}`),
});

export const api = {
  health: () => request<{ status: string; time: string }>("GET", "/health"),

  members: {
    ...resource<Member>("/members"),
    suspend: (id: string, note?: string) =>
      request<Member>("POST", `/members/${id}/suspend`, { reviewer: null, note: note ?? "" }),
    reinstate: (id: string) =>
      request<Member>("POST", `/members/${id}/reinstate`),
  },
  applications: {
    ...resource<MemberApplication>("/applications"),
    approve: (id: string, reviewer: string, note: string) =>
      request<MemberApplication>("POST", `/applications/${id}/approve`, {
        reviewer,
        note,
      }),
    reject: (id: string, reviewer: string, note: string) =>
      request<MemberApplication>("POST", `/applications/${id}/reject`, {
        reviewer,
        note,
      }),
    activate: (id: string) =>
      request<{ application: MemberApplication; member: Member }>(
        "POST",
        `/applications/${id}/activate`,
      ),
    withdraw: (id: string) =>
      request<MemberApplication>("POST", `/applications/${id}/withdraw`),
  },
  dunning: {
    run: () => request<DunningRunResult>("POST", "/dunning/run"),
  },
  courses: resource<Course>("/courses"),
  teeTimes: resource<TeeTime>("/tee-times"),
  staff: resource<StaffMember>("/staff"),
  shifts: resource<Shift>("/shifts"),
  weeklyTemplates: resource<WeeklyTemplate>("/weekly-templates"),
  products: {
    ...resource<Product>("/products"),
    adjustStock: (id: string, delta: number) =>
      request<Product>("POST", `/products/${id}/adjust-stock`, { delta }),
  },
  tournaments: resource<Tournament>("/tournaments"),
  maintenance: resource<MaintenanceTask>("/maintenance"),

  tabs: {
    list: () => request<PlayerTab[]>("GET", "/tabs"),
    get: (id: string) => request<PlayerTab>("GET", `/tabs/${id}`),
    create: (dto: Omit<PlayerTab, "id"> & { id?: string }) =>
      request<PlayerTab>("POST", "/tabs", dto),
    updateMeta: (id: string, dto: PlayerTab) =>
      request<PlayerTab>("PUT", `/tabs/${id}`, dto),
    void: (id: string) => request<PlayerTab>("POST", `/tabs/${id}/void`),
    settle: (id: string) =>
      request<PlayerTab>("POST", `/tabs/${id}/settle`),
    reopen: (id: string) =>
      request<PlayerTab>("POST", `/tabs/${id}/reopen`),
    addItem: (
      tabId: string,
      productId: string,
      quantity: number,
      notes: string,
    ) =>
      request<PlayerTab>("POST", `/tabs/${tabId}/items`, {
        productId,
        quantity,
        notes,
      }),
    setItemQuantity: (tabId: string, itemId: string, quantity: number) =>
      request<PlayerTab>("PUT", `/tabs/${tabId}/items/${itemId}/quantity`, {
        quantity,
      }),
    removeItem: (tabId: string, itemId: string) =>
      request<PlayerTab>("DELETE", `/tabs/${tabId}/items/${itemId}`),
    addPayment: (tabId: string, body: CreatePayload) =>
      request<PlayerTab>("POST", `/tabs/${tabId}/payments`, body),
    removePayment: (tabId: string, paymentId: string) =>
      request<PlayerTab>("DELETE", `/tabs/${tabId}/payments/${paymentId}`),
  },

  snapshot: () => request<DataState>("GET", "/snapshot"),
  restore: (snapshot: DataState) =>
    request<{ restored: boolean }>("POST", "/snapshot/restore", snapshot),
  reset: () => request<{ reset: boolean }>("POST", "/reset"),
  clear: () => request<{ cleared: boolean }>("POST", "/clear"),
};

export type Api = typeof api;
