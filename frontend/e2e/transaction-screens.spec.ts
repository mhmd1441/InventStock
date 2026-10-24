import { expect, test, type Page } from "@playwright/test";

const admin = {
  email: process.env.PLAYWRIGHT_ADMIN_EMAIL,
  password: process.env.PLAYWRIGHT_ADMIN_PASSWORD,
};
const staff = {
  email: process.env.PLAYWRIGHT_STAFF_EMAIL,
  password: process.env.PLAYWRIGHT_STAFF_PASSWORD,
};
const enabled = Boolean(admin.email && admin.password && staff.email && staff.password);

async function signIn(page: Page, credentials: { email?: string; password?: string }) {
  await page.goto("/login");
  const signInButton = page.getByRole("button", { name: "Sign in" });
  await expect(signInButton).toBeEnabled();
  // AuthProvider performs one refresh attempt on page load. Wait for its state
  // update before attaching the listener for the user's separate login request.
  await page.waitForTimeout(500);
  await page.getByLabel("Email").fill(credentials.email!);
  await page.getByLabel("Password").fill(credentials.password!);
  const login = page.waitForResponse((response) =>
    response.request().method() === "POST" && response.url().endsWith("/api/auth/login"));
  await signInButton.click();
  expect((await login).status()).toBe(200);

  // Development-mode RSC transitions can keep the browser URL at /login while
  // dashboard data is streaming. A full protected navigation proves the same
  // persisted refresh-session behavior without making this smoke test router-timing-sensitive.
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
}

test.describe("transaction-screen smoke", () => {
  test.skip(!enabled, "Set disposable PLAYWRIGHT_ADMIN_* and PLAYWRIGHT_STAFF_* credentials before running browser smoke tests.");

  test("admin can reach each implemented transaction screen only after deliberate private reveal", async ({ page }) => {
    await signIn(page, admin);

    // These screens are intentionally hidden until the admin deliberately exposes private data.
    await expect(page.getByRole("link", { name: "Cash out" })).toHaveCount(0);
    await page.getByRole("button", { name: "Reveal admin costs" }).click();

    await page.getByRole("link", { name: "Counter sale" }).click();
    await expect(page.getByRole("heading", { name: "Counter sale" })).toBeVisible();

    await page.getByRole("link", { name: "Returns" }).click();
    await expect(page.getByRole("heading", { name: "Linked return" })).toBeVisible();
    await expect(page.getByRole("link", { name: "Sold before InventStock" })).toBeVisible();

    await page.getByRole("link", { name: "Customers" }).click();
    await expect(page.getByRole("heading", { name: "Customers & accounts" })).toBeVisible();
    await page.getByRole("textbox", { name: "Name", exact: true }).fill("Browser paper-recovery customer");
    await page.getByRole("button", { name: "Create customer" }).click();
    await expect(page.getByRole("heading", { name: "Browser paper-recovery customer · account" })).toBeVisible();
    await expect(page.getByText("Record paper-era customer payment", { exact: true })).toBeVisible();

    await page.getByRole("link", { name: "Cash out" }).click();
    await expect(page.getByRole("heading", { name: "Cash outgoings" })).toBeVisible();
    await expect(page.getByText("Opening cash counts", { exact: true })).toBeVisible();
    await expect(page.getByText("Supplier payments", { exact: true })).toBeVisible();
    await expect(page.getByText("Transfers and Whish cash-out", { exact: true })).toBeVisible();

    await page.getByRole("link", { name: "Pricing rules" }).click();
    await expect(page.getByRole("heading", { name: "Suggested selling price" })).toBeVisible();

    await page.getByRole("link", { name: "Exceptions" }).click();
    await expect(page.getByRole("heading", { name: "Transaction exceptions" })).toBeVisible();
  });

  test("staff sees public transaction screens but cannot reveal private cash, pricing, or exception screens", async ({ page }) => {
    await signIn(page, staff);
    await page.goto("/sales");
    await expect(page.getByRole("heading", { name: "Counter sale" })).toBeVisible();
    await page.goto("/returns");
    await expect(page.getByRole("heading", { name: "Linked return" })).toBeVisible();
    await page.goto("/customers");
    await expect(page.getByRole("heading", { name: "Customers & accounts" })).toBeVisible();

    await expect(page.getByRole("link", { name: "Cash out" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Pricing rules" })).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Exceptions" })).toHaveCount(0);
    await page.goto("/cash");
    await expect(page.getByText("Reveal admin costs to access private cash and outgoing-payment records.")).toBeVisible();
  });

  test("signing out in one tab signs a second tab out too", async ({ browser }) => {
    const adminContext = await browser.newContext();
    const secondPage = await adminContext.newPage();
    const firstPage = await adminContext.newPage();
    await signIn(firstPage, admin);
    await secondPage.goto("/dashboard");
    await expect(secondPage.getByText("InventStock", { exact: true })).toBeVisible();

    await firstPage.getByRole("button", { name: "Sign out" }).click();
    await expect(firstPage).toHaveURL(/\/login$/);
    await secondPage.reload();
    await expect(secondPage).toHaveURL(/\/login$/);
    await adminContext.close();
  });
});
