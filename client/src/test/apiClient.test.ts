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
