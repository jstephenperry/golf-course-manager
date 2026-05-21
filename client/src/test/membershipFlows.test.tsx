/**
 * Targeted store tests that drive the registration + dunning APIs via a
 * mocked fetch. Confirms the store dispatches the right HTTP calls, merges
 * server responses into local state, and re-fetches members where required.
 */
import { act, renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi, type Mock } from "vitest";
import { ToasterProvider } from "../components/Toaster";
import { StoreProvider, useStore } from "../data/store";
import type { Member, MemberApplication } from "../data/types";

const emptySnapshot = {
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

const seedMember = (overrides: Partial<Member> = {}): Member => ({
  id: "m1",
  firstName: "Eleanor",
  lastName: "Park",
  email: "e@example.com",
  phone: "555",
  tier: "Full",
  handicap: 8,
  joinDate: "2020-01-01",
  active: true,
  balance: 0,
  status: "Active",
  oldestUnpaidChargeAt: null,
  suspendedAt: null,
  notes: "",
  ...overrides,
});

const seedApplication = (
  overrides: Partial<MemberApplication> = {},
): MemberApplication => ({
  id: "app1",
  firstName: "Hana",
  lastName: "Okafor",
  email: "h@example.com",
  phone: "555",
  requestedTier: "Full",
  sponsoringMemberId: null,
  initiationFee: 500,
  notes: "",
  status: "Pending",
  submittedAt: new Date().toISOString(),
  reviewedAt: null,
  reviewedBy: null,
  reviewNote: null,
  activatedMemberId: null,
  ...overrides,
});

function jsonOk(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ToasterProvider>
      <StoreProvider>{children}</StoreProvider>
    </ToasterProvider>
  );
}

describe("membership store flows", () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);

  afterEach(() => {
    fetchMock.mockReset();
  });

  const expectFetch = (call: number, method: string, path: string) => {
    const [url, init] = fetchMock.mock.calls[call] as [string, RequestInit];
    expect(url).toBe(path);
    expect((init as { method?: string }).method).toBe(method);
  };

  it("approve -> activate inserts new member into store", async () => {
    const application = seedApplication();
    const approved = { ...application, status: "Approved" as const };
    const activated = {
      ...approved,
      status: "Activated" as const,
      activatedMemberId: "m_new",
    };
    const newMember = seedMember({ id: "m_new", balance: 500 });

    // Initial snapshot load + subsequent operations
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      const method = (init as { method?: string } | undefined)?.method ?? "GET";
      if (url === "/api/snapshot" && method === "GET") {
        return jsonOk({ ...emptySnapshot, memberApplications: [application] });
      }
      if (url === `/api/applications/${application.id}/approve`) {
        return jsonOk(approved);
      }
      if (url === `/api/applications/${application.id}/activate`) {
        return jsonOk({ application: activated, member: newMember });
      }
      throw new Error(`unexpected call ${method} ${url}`);
    });

    const { result } = renderHook(() => useStore(), { wrapper });
    await waitFor(() => expect(result.current.initialized).toBe(true));

    await act(async () => {
      await result.current.applications.approve(application.id, "tester", "ok");
    });
    expect(result.current.data.memberApplications[0].status).toBe("Approved");

    await act(async () => {
      await result.current.applications.activate(application.id);
    });
    const stored = result.current.data;
    expect(stored.memberApplications.find((a) => a.id === application.id)?.status).toBe(
      "Activated",
    );
    expect(stored.members.find((m) => m.id === "m_new")).toBeDefined();
  });

  it("runDunning posts to /api/dunning/run and refreshes members", async () => {
    const before = seedMember({
      balance: 200,
      oldestUnpaidChargeAt: new Date(Date.now() - 70 * 86_400_000).toISOString(),
    });
    const after = { ...before, status: "Suspended" as const, active: false };

    let membersListResponses = 0;
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      const method = (init as { method?: string } | undefined)?.method ?? "GET";
      if (url === "/api/snapshot" && method === "GET") {
        return jsonOk({ ...emptySnapshot, members: [before] });
      }
      if (url === "/api/dunning/run" && method === "POST") {
        return jsonOk({
          suspended: 1,
          reinstated: 0,
          affectedMemberIds: [before.id],
        });
      }
      if (url === "/api/members" && method === "GET") {
        membersListResponses++;
        return jsonOk([after]);
      }
      throw new Error(`unexpected call ${method} ${url}`);
    });

    const { result } = renderHook(() => useStore(), { wrapper });
    await waitFor(() => expect(result.current.initialized).toBe(true));

    await act(async () => {
      const res = await result.current.runDunning();
      expect(res).toMatchObject({ suspended: 1 });
    });

    expect(membersListResponses).toBeGreaterThanOrEqual(1);
    expect(result.current.data.members[0].status).toBe("Suspended");
  });

  it("suspend / reinstate update the member in-place", async () => {
    const m = seedMember({ balance: 50 });

    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      const method = (init as { method?: string } | undefined)?.method ?? "GET";
      if (url === "/api/snapshot" && method === "GET") {
        return jsonOk({ ...emptySnapshot, members: [m] });
      }
      if (url === `/api/members/${m.id}/suspend` && method === "POST") {
        return jsonOk({ ...m, status: "Suspended", active: false });
      }
      if (url === `/api/members/${m.id}/reinstate` && method === "POST") {
        return jsonOk({ ...m, status: "Active", active: true });
      }
      throw new Error(`unexpected call ${method} ${url}`);
    });

    const { result } = renderHook(() => useStore(), { wrapper });
    await waitFor(() => expect(result.current.initialized).toBe(true));

    await act(async () => {
      await result.current.members.suspend(m.id);
    });
    expect(result.current.data.members[0].status).toBe("Suspended");

    await act(async () => {
      await result.current.members.reinstate(m.id);
    });
    expect(result.current.data.members[0].status).toBe("Active");

    expectFetch(0, "GET", "/api/snapshot");
  });
});
