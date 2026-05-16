import { expect, test } from "@playwright/test";

test.describe("smoke", () => {
  test("loads the dashboard and shows seeded counts", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
    // KPI labels should show
    await expect(page.getByText("Tee Times Today")).toBeVisible();
    await expect(page.getByText("Active Members")).toBeVisible();
    await expect(page.getByText("Open Tabs")).toBeVisible();
  });

  test("API health check is reachable", async ({ request }) => {
    const res = await request.get("/api/health");
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.status).toBe("ok");
  });
});
