import { expect, test } from "@playwright/test";

const email = process.env.PLAYWRIGHT_ADMIN_EMAIL;
const password = process.env.PLAYWRIGHT_ADMIN_PASSWORD;

async function signIn(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await expect(page.getByRole("button", { name: "Sign in" })).toBeEnabled();
  await page.waitForTimeout(500);
  await page.getByLabel("Email").fill(email!);
  await page.getByLabel("Password").fill(password!);
  const login = page.waitForResponse((response) =>
    response.request().method() === "POST" && response.url().endsWith("/api/auth/login"),
  );
  await page.getByRole("button", { name: "Sign in" }).click();
  expect((await login).status()).toBe(200);
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
}

test.describe("supplier import smoke", () => {
  test.skip(!email || !password, "Set disposable PLAYWRIGHT_ADMIN_* credentials before running browser smoke tests.");

  test("admin stages, reviews, and commits a catalogue-only row", async ({ page }) => {
    await signIn(page);
    await page.getByRole("button", { name: "Reveal admin costs" }).click();
    await page.getByRole("link", { name: "Imports" }).click();
    await expect(page.getByRole("heading", { name: "Supplier catalogue imports" })).toBeVisible();

    const supplier = page.locator("select").filter({ has: page.locator("option") }).first();
    await supplier.selectOption({ index: 1 });
    await page.getByRole("textbox", { name: "Extracted rows" }).fill(
      "PartNumber,Name,Brand,SupplierCode,Cost,Currency,TVA\nBROWSER-IMPORT-001,Disposable browser import,FEBI,BR-001,12,USD,Excluded",
    );
    await page.getByRole("button", { name: "Prepare preview" }).click();
    await expect(page.getByText("Preview ready: 1 rows.", { exact: false })).toBeVisible();
    await page.getByRole("button", { name: "Stage 1 rows" }).click();
    await expect(page.getByText("Review every row before committing.", { exact: false })).toBeVisible();

    await page.getByLabel("Decision").selectOption("CreateNew");
    await page.getByLabel("Category").selectOption({ index: 1 });
    await page.getByRole("button", { name: "Save review" }).click();
    await expect(page.getByRole("button", { name: "Commit reviewed import" })).toBeEnabled();
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Commit reviewed import" }).click();
    await expect(page.getByText("No stock was added.", { exact: false })).toBeVisible();
  });
});
