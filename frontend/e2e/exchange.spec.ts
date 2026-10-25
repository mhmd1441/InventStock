import { expect, test, type Page } from "@playwright/test";

const admin = { email: process.env.PLAYWRIGHT_ADMIN_EMAIL, password: process.env.PLAYWRIGHT_ADMIN_PASSWORD };
test.skip(!admin.email || !admin.password, "Set disposable PLAYWRIGHT_ADMIN_* credentials before browser smoke.");

async function signIn(page: Page) {
  await page.goto("/login"); await page.waitForTimeout(500);
  await page.getByLabel("Email").fill(admin.email!); await page.getByLabel("Password").fill(admin.password!);
  await page.getByRole("button", { name: "Sign in" }).click(); await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
}

test("posts a zero-cash linked exchange using return value as replacement credit", async ({ page }) => {
  await signIn(page);
  await page.goto("/sales");
  await page.getByLabel("Part number or name").fill("BRK-1001");
  await page.locator("button").filter({ hasText: "BRK-1001" }).first().click();
  await page.getByLabel("Selling price · USD/piece").fill("10");
  await page.locator("select").first().selectOption({ label: "Physical safe — USD · USD (safe)" });
  await page.getByLabel("Native USD").first().fill("10");
  await page.getByRole("button", { name: "Review sale" }).click();
  const posted = page.waitForResponse((response) => response.url().endsWith("/api/sales") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Confirm & post sale" }).click();
  const original = await (await posted).json() as { id: string };

  await page.goto(`/returns/exchange?sale=${original.id}`);
  await expect(page.getByRole("heading", { name: "Exchange an item" })).toBeVisible();
  await page.getByLabel("Return quantity · pieces").fill("1");
  await page.getByLabel("Replacement part number or name").fill("FLT-2001");
  await page.locator("button").filter({ hasText: "FLT-2001" }).first().click();
  await page.getByLabel("Selling price · USD/piece").fill("10");
  await page.getByLabel("Exchange credit applied to replacement · USD").fill("10");
  await page.getByLabel("Why is this exchange happening?").fill("Disposable exchange workflow");
  await page.getByRole("button", { name: "Review exchange" }).click();
  await page.getByRole("button", { name: "Confirm & post exchange" }).click();
  await expect(page.getByText(/Exchange recorded: returned \$10\.00/)).toBeVisible();
});
