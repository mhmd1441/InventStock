import { expect, test, type Page } from "@playwright/test";

const admin = {
  email: process.env.PLAYWRIGHT_ADMIN_EMAIL,
  password: process.env.PLAYWRIGHT_ADMIN_PASSWORD,
};

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

test.describe("daily reconciliation smoke", () => {
  test.skip(!admin.email || !admin.password, "Set disposable PLAYWRIGHT_ADMIN_* credentials before running browser smoke tests.");

  test("admin records a USD physical close with an explicit opening count", async ({ page }) => {
    await signIn(page);
    await page.getByRole("button", { name: "Reveal admin costs" }).click();
    await page.getByRole("link", { name: "Cash out" }).click();

    const card = page.locator(".daily-reconciliation-card");
    await expect(card).toBeVisible();
    const usdAccount = card.getByTestId("reconciliation-SAFE_USD");
    await usdAccount.getByLabel("Opening count (USD)", { exact: true }).fill("100");
    await usdAccount.getByLabel("Physical count (USD)", { exact: true }).fill("100");
    await usdAccount.getByRole("button", { name: "Record physical close" }).click();

    await expect(page.getByText(/Physical safe — USD was reconciled for/)).toBeVisible();
    await expect(usdAccount.getByText("Partial close", { exact: true })).toBeVisible();
    await expect(usdAccount.getByText("Physical count:", { exact: false })).toContainText("100 USD");
  });
});
