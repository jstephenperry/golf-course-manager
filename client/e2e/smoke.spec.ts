import { expect, test } from "@playwright/test";
import { seedBaseline } from "./seed";

test.describe("smoke", () => {
  test.beforeEach(async () => {
    await seedBaseline();
  });

  test("loads the dashboard and shows KPI counts", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
    // KPI labels should show
    // exact: true — these KPI labels are substrings of empty-state copy
    // elsewhere on the dashboard (e.g. "No active members carrying a balance").
    await expect(page.getByText("Tee Times Today", { exact: true })).toBeVisible();
    await expect(page.getByText("Active Members", { exact: true })).toBeVisible();
    await expect(
      page.getByText("Open Maintenance", { exact: true }),
    ).toBeVisible();
  });

  test("API health check is reachable", async ({ request }) => {
    const res = await request.get("/api/health");
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.status).toBe("ok");
  });
});
