import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, api } from "../api/client";

describe("api client", () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);

  afterEach(() => {
    fetchMock.mockReset();
  });

  it("GET members hits /api/members", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify([{ id: "m1" }]), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
    const list = await api.members.list();
    expect(list).toEqual([{ id: "m1" }]);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/members",
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("throws ApiError with status on non-2xx", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ error: "nope" }), {
        status: 422,
        headers: { "content-type": "application/json" },
      }),
    );
    await expect(api.members.list()).rejects.toMatchObject({
      name: "ApiError",
      status: 422,
      body: { error: "nope" },
    });
  });

  it("translates network failure into ApiError with status 0", async () => {
    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch"));
    try {
      await api.members.list();
      expect.fail("should have thrown");
    } catch (err) {
      expect(err).toBeInstanceOf(ApiError);
      expect((err as ApiError).status).toBe(0);
    }
  });

  it("getLedger GETs /api/members/:id/ledger with limit + before cursor", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ entries: [], hasMore: false }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
    await api.members.getLedger("m1", { limit: 10, before: "2026-05-01T00:00:00Z" });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/members/m1/ledger?limit=10&before=2026-05-01T00%3A00%3A00Z",
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("postCharge POSTs to /api/members/:id/charges with the body", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "led_x", entryType: "Charge" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );
    await api.members.postCharge("m1", { amount: 50, category: "Dues", note: "" });
    const call = fetchMock.mock.calls[0]!;
    expect(call[0]).toBe("/api/members/m1/charges");
    expect(call[1]).toMatchObject({ method: "POST" });
    expect(JSON.parse(call[1].body)).toMatchObject({
      amount: 50,
      category: "Dues",
    });
  });

  it("postPayment POSTs to /api/members/:id/payments with the body", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "led_y", entryType: "Payment" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );
    await api.members.postPayment("m1", { amount: 30, method: "Card", note: "" });
    const call = fetchMock.mock.calls[0]!;
    expect(call[0]).toBe("/api/members/m1/payments");
    expect(JSON.parse(call[1].body)).toMatchObject({
      amount: 30,
      method: "Card",
    });
  });

  it("voidLedgerEntry POSTs to /api/members/ledger/:entryId/void", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "led_r", entryType: "Reversal" }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
    await api.members.voidLedgerEntry("led_x", { note: "mistake" });
    const call = fetchMock.mock.calls[0]!;
    expect(call[0]).toBe("/api/members/ledger/led_x/void");
    expect(call[1]).toMatchObject({ method: "POST" });
  });

  it("getOverview GETs /api/members/:id/overview and returns parsed body", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          member: { id: "m1", firstName: "E", lastName: "P" },
          lastPlayedDate: "2026-05-09",
          lifetimeRounds: 3,
          recentRounds: [],
        }),
        {
          status: 200,
          headers: { "content-type": "application/json" },
        },
      ),
    );
    const overview = await api.members.getOverview("m1");
    expect(overview.lifetimeRounds).toBe(3);
    expect(overview.lastPlayedDate).toBe("2026-05-09");
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/members/m1/overview",
      expect.objectContaining({ method: "GET" }),
    );
  });

  it("encodes JSON body on POST", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "m1" }), {
        status: 201,
        headers: { "content-type": "application/json" },
      }),
    );
    await api.members.create({
      firstName: "T",
      lastName: "U",
      email: "",
      phone: "",
      tier: "Full",
      handicap: 0,
      joinDate: "2024-01-01",
      active: true,
      balance: 0,
      status: "Active",
      oldestUnpaidChargeAt: null,
      suspendedAt: null,
      notes: "",
    });
    const call = fetchMock.mock.calls[0]!;
    expect(call[0]).toBe("/api/members");
    expect(call[1]).toMatchObject({
      method: "POST",
      headers: expect.objectContaining({
        "Content-Type": "application/json",
      }),
    });
    expect(JSON.parse(call[1].body)).toMatchObject({ firstName: "T" });
  });
});
