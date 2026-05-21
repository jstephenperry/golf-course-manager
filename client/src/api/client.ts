import type {
  Course,
  DataState,
  DunningRunResult,
  ImportResult,
  MaintenanceTask,
  Member,
  MemberApplication,
  MemberLedgerEntry,
  MemberLedgerList,
  MemberOverview,
  Nine,
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

// ---------------------------------------------------------------------------
// Auth integration
//
// The AuthProvider injects the current access token + a silent-renew
// callback via `setAuth(...)`. Doing it through setters (instead of
// importing the auth context here) keeps `client.ts` free of React deps
// and avoids a circular import — the auth module depends on this module
// for the typed `api`, so this module can't depend on the auth module.
// ---------------------------------------------------------------------------

/** Returns the current access token, or null if the user isn't signed in. */
export type TokenProvider = () => string | null;

/**
 * Requests a fresh access token (silent renew). Returns the new token, or
 * null if renewal failed — caller should let the 401 propagate.
 */
export type TokenRenewer = () => Promise<string | null>;

let tokenProvider: TokenProvider | null = null;
let tokenRenewer: TokenRenewer | null = null;

/**
 * Wire the api module to an auth source. Called once by `AuthProvider` on
 * mount. Passing nulls clears the wiring (useful for tests).
 */
export function setAuth(
  provider: TokenProvider | null,
  renewer: TokenRenewer | null,
): void {
  tokenProvider = provider;
  tokenRenewer = renewer;
}

async function doFetch(
  method: string,
  url: string,
  body: unknown,
  init: RequestInit | undefined,
  token: string | null,
): Promise<Response> {
  const headers: Record<string, string> = {
    Accept: "application/json",
    ...((init?.headers as Record<string, string>) ?? {}),
  };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return fetch(url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    ...init,
  });
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  init?: RequestInit,
): Promise<T> {
  const url = `${API_BASE}${path}`;
  let res: Response;
  try {
    res = await doFetch(method, url, body, init, tokenProvider?.() ?? null);
  } catch (err) {
    throw new ApiError(
      err instanceof Error ? err.message : "network error",
      0,
      null,
    );
  }

  // On 401, try a silent renew once and retry the request with the fresh
  // token. If it still 401s (or renewal fails), let the error propagate —
  // the consumer (ProtectedRoute / login page in a later slice) is
  // responsible for redirecting to interactive login.
  if (res.status === 401 && tokenRenewer) {
    let renewed: string | null = null;
    try {
      renewed = await tokenRenewer();
    } catch {
      renewed = null;
    }
    if (renewed) {
      try {
        res = await doFetch(method, url, body, init, renewed);
      } catch (err) {
        throw new ApiError(
          err instanceof Error ? err.message : "network error",
          0,
          null,
        );
      }
    }
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
    getOverview: (id: string) =>
      request<MemberOverview>("GET", `/members/${id}/overview`),
    getLedger: (id: string, opts?: { limit?: number; before?: string }) => {
      const params = new URLSearchParams();
      if (opts?.limit) params.set("limit", String(opts.limit));
      if (opts?.before) params.set("before", opts.before);
      const q = params.toString();
      return request<MemberLedgerList>(
        "GET",
        `/members/${id}/ledger${q ? `?${q}` : ""}`,
      );
    },
    postCharge: (
      id: string,
      body: { amount: number; category: string; note: string },
    ) => request<MemberLedgerEntry>("POST", `/members/${id}/charges`, body),
    postPayment: (
      id: string,
      body: { amount: number; method: string; note: string },
    ) => request<MemberLedgerEntry>("POST", `/members/${id}/payments`, body),
    voidLedgerEntry: (entryId: string, body: { note: string }) =>
      request<MemberLedgerEntry>(
        "POST",
        `/members/ledger/${entryId}/void`,
        body,
      ),
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
  nines: resource<Nine>("/nines"),
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

  import: {
    members: (rows: unknown[]) => request<ImportResult>("POST", "/import/members", rows),
    nines: (rows: unknown[]) => request<ImportResult>("POST", "/import/nines", rows),
    courses: (rows: unknown[]) => request<ImportResult>("POST", "/import/courses", rows),
    teeTimes: (rows: unknown[]) => request<ImportResult>("POST", "/import/tee-times", rows),
    staff: (rows: unknown[]) => request<ImportResult>("POST", "/import/staff", rows),
    shifts: (rows: unknown[]) => request<ImportResult>("POST", "/import/shifts", rows),
    weeklyTemplates: (rows: unknown[]) => request<ImportResult>("POST", "/import/weekly-templates", rows),
    products: (rows: unknown[]) => request<ImportResult>("POST", "/import/products", rows),
    tournaments: (rows: unknown[]) => request<ImportResult>("POST", "/import/tournaments", rows),
    maintenance: (rows: unknown[]) => request<ImportResult>("POST", "/import/maintenance", rows),
  },

  snapshot: () => request<DataState>("GET", "/snapshot"),
  restore: (snapshot: DataState) =>
    request<{ restored: boolean }>("POST", "/snapshot/restore", snapshot),
  reset: () => request<{ reset: boolean }>("POST", "/reset"),
  clear: () => request<{ cleared: boolean }>("POST", "/clear"),
};

export type Api = typeof api;
