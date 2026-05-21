import { request as apiRequest, type APIRequestContext } from "@playwright/test";

// The E2E server boots with an EMPTY database — `Seed.EnsureSeeded` is a
// deliberate no-op (synthetic seed was stripped so each test owns its
// fixtures). So specs that exercise members / products / applications must
// provision them first. `seedBaseline` creates the minimal set the specs
// assume and is idempotent: it only creates what's currently missing, so it's
// safe to call from every spec's `beforeEach` (and on Playwright retries).

const BASE_URL = process.env.E2E_BASE_URL ?? "http://localhost:5210";

export async function seedBaseline(): Promise<void> {
  const api = await apiRequest.newContext({ baseURL: BASE_URL });
  try {
    await ensureMember(api);
    await ensureProduct(api);
    await ensurePendingApplication(api);
  } finally {
    await api.dispose();
  }
}

async function ok(api: APIRequestContext, path: string): Promise<unknown[]> {
  const res = await api.get(path);
  if (!res.ok()) throw new Error(`GET ${path} → ${res.status()}`);
  const body = await res.json();
  return Array.isArray(body) ? body : [];
}

async function expectOk(
  res: Awaited<ReturnType<APIRequestContext["post"]>>,
  what: string,
): Promise<void> {
  if (!res.ok()) {
    throw new Error(`seed ${what} failed: ${res.status()} ${await res.text()}`);
  }
}

async function ensureMember(api: APIRequestContext): Promise<void> {
  if ((await ok(api, "/api/members")).length > 0) return;
  await expectOk(
    await api.post("/api/import/members", {
      data: [
        {
          id: "e2e-m1",
          firstName: "Eleanor",
          lastName: "Park",
          email: "eleanor@example.com",
          phone: "555-0100",
          tier: "Full",
          handicap: 8,
          joinDate: "2020-01-01",
          active: true,
          balance: 0,
          status: "Active",
          oldestUnpaidChargeAt: null,
          suspendedAt: null,
          notes: "",
        },
      ],
    }),
    "member",
  );
}

async function ensureProduct(api: APIRequestContext): Promise<void> {
  if ((await ok(api, "/api/products")).length > 0) return;
  await expectOk(
    await api.post("/api/import/products", {
      data: [
        {
          id: "e2e-p1",
          name: "Pro V1 Dozen",
          category: "Balls",
          sku: "PROV1-DZ",
          price: 54.99,
          cost: 30,
          stock: 50,
          reorderLevel: 10,
        },
      ],
    }),
    "product",
  );
}

async function ensurePendingApplication(api: APIRequestContext): Promise<void> {
  const apps = (await ok(api, "/api/applications")) as Array<{
    status?: string;
  }>;
  if (apps.some((a) => a.status === "Pending")) return;
  await expectOk(
    await api.post("/api/applications", {
      data: {
        firstName: "Hana",
        lastName: "Okafor",
        email: "hana@example.com",
        phone: "555-0200",
        requestedTier: "Full",
        initiationFee: 0,
        notes: "",
      },
    }),
    "application",
  );
}
