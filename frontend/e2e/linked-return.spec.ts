import { expect, test, type Page } from "@playwright/test";

const admin = {
  email: process.env.PLAYWRIGHT_ADMIN_EMAIL,
  password: process.env.PLAYWRIGHT_ADMIN_PASSWORD,
};
const returnPart = process.env.PLAYWRIGHT_RETURN_PART;

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

test.describe("linked return smoke", () => {
  test.skip(!admin.email || !admin.password || !returnPart,
    "Set disposable PLAYWRIGHT_ADMIN_* credentials and PLAYWRIGHT_RETURN_PART before running this smoke test.");

  test("counter user posts a fully allocated linked cash return", async ({ page }) => {
    await signIn(page);
    await page.getByRole("link", { name: "Returns" }).click();
    await expect(page.getByRole("heading", { name: "Linked return" })).toBeVisible();

    await page.getByLabel("Part").fill(returnPart!);
    await page.getByRole("button", { name: "Search sales" }).click();
    const sale = page.locator("button").filter({ hasText: returnPart! }).first();
    await expect(sale).toBeVisible();
    await sale.click();

    await expect(page.getByLabel("Return quantity · pieces")).toBeVisible();
    await page.getByLabel("Return quantity · pieces").fill("1");
    await page.getByLabel("Why is this being returned?").fill("Disposable browser return verification");
    await page.getByRole("button", { name: "Add cash / Whish refund" }).click();
    await page.getByLabel("Native USD").fill("10");
    await expect(page.getByLabel("Confirmed USD")).toHaveValue("10");
    await page.getByRole("button", { name: "Review return" }).click();
    await expect(page.getByRole("button", { name: "Confirm & post return" })).toBeEnabled();
    await page.getByRole("button", { name: "Confirm & post return" }).click();
    await expect(page.getByText("Return recorded: $10.00.", { exact: false })).toBeVisible();
  });
});
