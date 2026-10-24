import { expect, test, type Page } from "@playwright/test";

const admin = {
  email: process.env.PLAYWRIGHT_ADMIN_EMAIL,
  password: process.env.PLAYWRIGHT_ADMIN_PASSWORD,
};
const enabled = Boolean(admin.email && admin.password);

async function signIn(page: Page, revealPrivate = false) {
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
  if (revealPrivate) await page.getByRole("button", { name: "Reveal admin costs" }).click();
}

function card(page: Page, title: string) {
  return page.getByRole("heading", { name: title, exact: true }).locator("xpath=ancestor::section");
}

test.describe("posting foundation smoke", () => {
  test.skip(!enabled, "Set disposable PLAYWRIGHT_ADMIN_* credentials before running browser smoke tests.");

  test("posts a seeded cash sale and prints/reprints its staff-safe internal receipt", async ({ page }) => {
    await page.addInitScript(() => {
      window.print = () => document.documentElement.setAttribute("data-browser-smoke-print", "called");
    });
    await signIn(page);
    await page.goto("/sales");
    await page.getByLabel("Part number or name").fill("BRK-1001");
    await page.locator("button").filter({ hasText: "BRK-1001" }).first().click();
    await page.getByLabel("Selling price · USD/piece").fill("10");
    await page.locator("select").first().selectOption({ label: "Physical safe — USD · USD (safe)" });
    await page.getByLabel("Native USD").first().fill("10");
    await page.getByRole("button", { name: "Review sale" }).click();
    await page.getByRole("button", { name: "Confirm & post sale" }).click();
    await expect(page.getByText(/Sale #\d+ recorded: \$10\.00/)).toBeVisible();

    await page.getByRole("link", { name: "Print / reprint internal receipt" }).click();
    await expect(page.getByRole("heading", { name: "Internal sale receipt" })).toBeVisible();
    await expect(page.getByText("Internal sale receipt · not a tax invoice", { exact: true })).toBeVisible();
    await expect(page.getByText("BRK-1001", { exact: false })).toBeVisible();
    await expect(page.getByText(/supplier|purchase cost|unit cost/i)).toHaveCount(0);
    await page.getByRole("button", { name: "Print / reprint" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-browser-smoke-print", "called");
    await page.reload();
    await expect(page.getByRole("heading", { name: "Internal sale receipt" })).toBeVisible();
    await page.getByRole("button", { name: "Print / reprint" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-browser-smoke-print", "called");
  });

  test("corrects an opening balance and posts a complete paper-recovery payment", async ({ page }) => {
    await signIn(page, true);
    await page.goto("/customers");
    await page.getByRole("textbox", { name: "Name", exact: true }).fill("Browser ledger recovery customer");
    await page.getByRole("button", { name: "Create customer" }).click();
    await expect(page.getByRole("heading", { name: "Browser ledger recovery customer · account" })).toBeVisible();

    await page.getByLabel("Opening balance · USD").fill("100");
    await page.getByLabel("Review source / reason").fill("Disposable browser opening balance");
    await page.getByRole("button", { name: "Post opening balance" }).click();
    await expect(page.getByText("Reviewed opening balance recorded.", { exact: true })).toBeVisible();

    await page.getByLabel("Corrected opening · USD").fill("120");
    await page.getByLabel("Why is it wrong?").fill("Opening paper total was understated");
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Review & correct opening" }).click();
    await expect(page.getByText("Opening corrected.", { exact: false })).toBeVisible();
    await expect(page.getByText("120.00 USD", { exact: false }).first()).toBeVisible();

    const paper = card(page, "Record paper-era customer payment");
    await paper.getByLabel("Paper reference").fill("BROWSER-PAPER-PAYMENT-001");
    await paper.getByLabel("Why is this being recovered?").fill("Payment was recorded in the outage notebook");
    await paper.locator("select").first().selectOption({ label: "Physical safe — USD · USD (safe)" });
    await paper.getByLabel("Native USD").fill("20");
    await paper.getByRole("button", { name: "Review paper payment" }).click();
    await paper.getByRole("button", { name: "Confirm & record paper payment" }).click();
    await expect(page.getByText(/Paper payment recorded\. New balance 100\.00 USD\./)).toBeVisible();
  });

  test("owner exceptions list contains the opening correction and paper recovery", async ({ page }) => {
    await signIn(page, true);
    await page.goto("/exceptions");
    await expect(page.getByRole("heading", { name: "Transaction exceptions" })).toBeVisible();
    await expect(page.getByText(/Opening-balance corrections: 1/)).toBeVisible();
    await expect(page.getByText(/Paper recovery: 1/)).toBeVisible();
    await expect(page.getByText("Opening paper total was understated", { exact: true })).toBeVisible();
    await expect(page.getByText("Payment was recorded in the outage notebook", { exact: true })).toBeVisible();
  });
});
