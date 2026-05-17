import { expect, test } from "@playwright/test";

test.describe("membership", () => {
  test("dashboard surfaces seeded pending applications", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText("Membership Applications")).toBeVisible();
    await expect(page.getByText(/pending/i)).toBeVisible();
  });

  test("applications tab: approve → activate creates a member", async ({
    page,
    request,
  }) => {
    await page.goto("/members");

    // Switch to Applications tab
    await page.getByRole("button", { name: /Applications/ }).click();

    // Pending list should have at least one seeded row.
    const pendingRow = page.locator("table tbody tr").first();
    await expect(pendingRow).toBeVisible();

    // Capture the applicant's name so we can verify the member exists later.
    const applicantText = (await pendingRow.locator("td").first().innerText()).trim();
    const [firstName] = applicantText.split(/\s+/);

    await pendingRow.getByRole("button", { name: /Approve/ }).click();

    // Review modal
    const modal = page.locator(".modal");
    await modal.getByLabel(/Reviewer/).fill("e2e");
    await modal.getByRole("button", { name: /^Approve$/ }).click();

    // The row should now be in the Approved card with an Activate button.
    const approvedRow = page
      .locator("table tbody tr", { hasText: firstName })
      .first();
    await expect(approvedRow.getByRole("button", { name: /Activate/ })).toBeVisible();

    page.once("dialog", (d) => d.accept());
    await approvedRow.getByRole("button", { name: /Activate/ }).click();

    // The new member should appear in /api/members
    const members = await request
      .get("/api/members")
      .then((r) => r.json());
    expect(
      (members as Array<{ firstName: string }>).some(
        (m) => m.firstName === firstName,
      ),
    ).toBe(true);
  });

  test("dunning button reports a result", async ({ page }) => {
    await page.goto("/members");
    await page.getByRole("button", { name: /Run dunning/ }).click();
    await expect(page.getByText(/Dunning sweep/i)).toBeVisible();
  });
});
