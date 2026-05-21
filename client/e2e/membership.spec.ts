import { expect, test } from "@playwright/test";
import { seedBaseline } from "./seed";

test.describe("membership", () => {
  // Empty DB on boot — provision a pending application (etc.) before each
  // test. Idempotent and re-run on retry, so the approve→activate test
  // always has a fresh pending row to consume.
  test.beforeEach(async () => {
    await seedBaseline();
  });

  test("dashboard surfaces a pending application", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText("Membership Applications")).toBeVisible();
    // "N pending" count pill — match the count specifically to avoid a
    // strict-mode clash with the "Pending" status pill elsewhere on the page.
    await expect(page.getByText(/\d+ pending/i)).toBeVisible();
  });

  test("applications tab: approve → activate creates a member", async ({
    page,
    request,
  }) => {
    await page.goto("/members");

    // Switch to Applications tab
    await page.getByRole("button", { name: /Applications/ }).click();

    // Pending list should have at least one row (provisioned in beforeEach).
    const pendingRow = page.locator("table tbody tr").first();
    await expect(pendingRow).toBeVisible();

    // Capture the applicant's name so we can verify the member exists later.
    const applicantText = (await pendingRow.locator("td").first().innerText()).trim();
    const [firstName] = applicantText.split(/\s+/);

    await pendingRow.getByRole("button", { name: /Approve/ }).click();

    // Review modal. The reviewer is no longer free text — it's stamped
    // server-side from the token (the modal just shows "Reviewing as …"), so
    // there's nothing to fill; confirm the approval directly.
    const modal = page.locator(".modal");
    await modal.getByRole("button", { name: /^Approve$/ }).click();

    // The row should now be in the Approved card with an Activate button.
    const approvedRow = page
      .locator("table tbody tr", { hasText: firstName })
      .first();
    await expect(approvedRow.getByRole("button", { name: /Activate/ })).toBeVisible();

    page.once("dialog", (d) => d.accept());
    await approvedRow.getByRole("button", { name: /Activate/ }).click();

    // Activation posts asynchronously, so poll /api/members until the newly
    // created member shows up rather than reading once and racing it.
    await expect
      .poll(async () => {
        const members = (await request
          .get("/api/members")
          .then((r) => r.json())) as Array<{ firstName: string }>;
        return members.some((m) => m.firstName === firstName);
      })
      .toBe(true);
  });

  test("dunning button reports a result", async ({ page }) => {
    await page.goto("/members");
    await page.getByRole("button", { name: /Run dunning/ }).click();
    await expect(page.getByText(/Dunning sweep/i)).toBeVisible();
  });
});
