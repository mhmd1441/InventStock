import { expect, test, type Page } from "@playwright/test";

const admin = {
  email: process.env.PLAYWRIGHT_ADMIN_EMAIL,
  password: process.env.PLAYWRIGHT_ADMIN_PASSWORD,
};
const legacyReturnPart = process.env.PLAYWRIGHT_LEGACY_RETURN_PART;

async function signIn(page: Page) {
  await page.goto("/login");
  await expect(page.getByRole("button", { name: "Sign in" })).toBeEnabled();
  await page.waitForTimeout(500);
  await page.getByLabel("Email").fill(admin.email!);
  await page.getByLabel("Password").fill(admin.password!);
  const login = page.waitForResponse((response) =>
    response.request().method() === "POST" && response.url().endsWith("/api/auth/login"),
  );
  await page.getByRole("button", { name: "Sign in" }).click();
  expect((await login).status()).toBe(200);
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
}

test.describe("sold-before-InventStock return smoke", () => {
  test.skip(!admin.email || !admin.password || !legacyReturnPart,
    "Set disposable PLAYWRIGHT_ADMIN_* credentials and PLAYWRIGHT_LEGACY_RETURN_PART before running this smoke test.");

  test("counter user posts a clearly labelled manual cash return", async ({ page }) => {
    await signIn(page);
    await page.getByRole("link", { name: "Returns" }).click();
    await page.getByRole("link", { name: "Sold before InventStock" }).click();
    await expect(page.getByRole("heading", { name: "Sold before InventStock" })).toBeVisible();

    await page.getByLabel("Part number or name").fill(legacyReturnPart!);
    const part = page.locator("button").filter({ hasText: legacyReturnPart! }).first();
    await expect(part).toBeVisible();
    await part.click();
    await page.getByLabel("Manual return value · USD").fill("10");
    await page.getByLabel("Why is this manual return allowed?").fill("Disposable legacy return verification");
    await page.getByRole("button", { name: "Add cash / Whish refund" }).click();
    await page.getByLabel("Native USD").fill("10");
    await expect(page.getByLabel("Confirmed USD")).toHaveValue("10");
    await page.getByRole("button", { name: "Review manual return" }).click();
    await expect(page.getByRole("button", { name: "Confirm sold-before-InventStock return" })).toBeEnabled();
    await page.getByRole("button", { name: "Confirm sold-before-InventStock return" }).click();
    await expect(page.getByText("Sold-before-InventStock return recorded: $10.00.", { exact: false })).toBeVisible();
  });
});
