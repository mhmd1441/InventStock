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

test.describe("supplier payment smoke", () => {
  test.skip(!admin.email || !admin.password, "Set disposable PLAYWRIGHT_ADMIN_* credentials before running browser smoke tests.");

  test("admin records a supplier settlement from the USD safe", async ({ page }) => {
    await signIn(page);
    await page.getByRole("button", { name: "Reveal admin costs" }).click();
    await page.getByRole("link", { name: "Cash out" }).click();
    const card = page.locator(".supplier-payments-card");
    await expect(card).toBeVisible();
    // The shared Field component renders a native label/select pair. Scope through
    // this card rather than React's transient labelled control during query refresh.
    await card.locator("select").nth(0).selectOption({ index: 1 });
    await card.locator("select").nth(1).selectOption({ label: "Physical safe — USD (USD)" });
    await card.getByLabel("Supplier settlement (USD)", { exact: true }).fill("100");
    await expect(card.getByLabel("Settlement agreed USD", { exact: true })).toHaveValue("100");
    await card.getByRole("button", { name: "Record supplier payment" }).click();
    await expect(page.getByText("Supplier payment recorded.", { exact: false })).toBeVisible();
    await expect(card.getByText("Supplier: 100 USD", { exact: false })).toBeVisible();
  });
});
