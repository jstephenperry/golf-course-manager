import { expect, test } from "@playwright/test";

/**
 * End-to-end golden path for opening a tab from a tee time, adding an item,
 * taking card payment, and settling.
 *
 * Assumes the server is freshly seeded (the e2e server script wipes the DB
 * on each launch).
 */
test("open tab → add item → pay → settle", async ({ page, request }) => {
  // 1. Wait for seed to be in place
  const members = await request.get("/api/members");
  expect(members.ok()).toBeTruthy();

  // 2. Go to the player tabs page
  await page.goto("/tabs");
  await expect(page.getByRole("heading", { name: "Player Tabs" })).toBeVisible();

  // 3. Open a fresh tab via the "+ Open Tab" button
  await page.getByRole("button", { name: /Open Tab/ }).first().click();

  // The new-tab modal should appear
  const modal = page.locator(".modal");
  await expect(modal).toBeVisible();
  // Pick the first member option
  await modal
    .getByRole("listbox", { name: /Members on tab/i })
    .or(modal.locator("select[multiple]").first())
    .selectOption({ index: 0 });
  await modal.getByRole("button", { name: /^Open tab$/ }).click();

  // The detail modal opens after creation. Confirm it's an Open tab.
  await expect(page.locator(".tab-modal")).toBeVisible();
  await expect(page.locator(".tab-modal .pill", { hasText: "Open" })).toBeVisible();

  // 4. Add a product to the tab
  const addCard = page.locator(".tab-modal .card", { hasText: "Add from inventory" });
  await addCard.locator("select").first().selectOption({ index: 1 });
  await addCard.getByRole("button", { name: /Add to tab/ }).click();

  // The items table should now have at least one row
  const items = page.locator(".tab-modal table tbody tr");
  await expect(items.first()).toBeVisible();

  // 5. Take a card payment using "Set to balance"
  await page.getByRole("button", { name: /Set to balance/ }).click();
  await page.getByRole("button", { name: /Apply payment/ }).click();

  // 6. Settle
  await page.getByRole("button", { name: /Settle/ }).click();
  await expect(
    page.locator(".tab-modal .pill", { hasText: "Settled" }),
  ).toBeVisible();
});
