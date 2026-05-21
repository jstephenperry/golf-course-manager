import { expect, test } from "@playwright/test";
import { seedBaseline } from "./seed";

/**
 * End-to-end golden path for opening a tab from a tee time, adding an item,
 * taking card payment, and settling.
 *
 * The server boots with an empty DB (seed is a no-op), so provision the
 * member + product this flow needs first.
 */
test.beforeEach(async () => {
  await seedBaseline();
});

test("open tab → add item → pay → settle", async ({ page, request }) => {
  // 1. Member + product were provisioned in beforeEach.
  const members = await request.get("/api/members");
  expect(members.ok()).toBeTruthy();
  expect(await members.json()).not.toHaveLength(0);

  // 2. Go to the player tabs page (no dedicated <h1>; assert a stable control)
  await page.goto("/tabs");
  await expect(
    page.getByRole("button", { name: /Open Tab/ }).first(),
  ).toBeVisible();

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

  // 6. Settle (specific name — /Settle/ alone also matches the "Settled (N)" filter)
  await page.getByRole("button", { name: /Settle & close out/ }).click();
  await expect(
    page.locator(".tab-modal .pill", { hasText: "Settled" }),
  ).toBeVisible();
});
