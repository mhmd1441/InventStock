import { expect, test, type Page } from "@playwright/test";

const admin = { email: process.env.PLAYWRIGHT_ADMIN_EMAIL, password: process.env.PLAYWRIGHT_ADMIN_PASSWORD };
const enabled = Boolean(admin.email && admin.password);

async function signIn(page: Page, reveal = false) {
  await page.goto("/login");
  await page.waitForTimeout(500);
  await page.getByLabel("Email").fill(admin.email!);
  await page.getByLabel("Password").fill(admin.password!);
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  if (reveal) await page.getByRole("button", { name: "Reveal admin costs" }).click();
}

async function addPart(page: Page, number: string) {
  await page.getByLabel("Part number or name").fill(number);
  await page.locator("button").filter({ hasText: number }).first().click();
}

test.describe.serial("advanced transaction smoke", () => {
  test.skip(!enabled, "Set disposable browser-smoke administrator credentials.");

  test("mixed LBP plus Whish plus debt sale", async ({ page }) => {
    await signIn(page, true);
    await page.goto("/customers");
    await page.getByRole("textbox", { name: "Name", exact: true }).fill("Mixed tender browser customer");
    await page.getByRole("button", { name: "Create customer" }).click();
    await expect(page.getByRole("heading", { name: /Mixed tender browser customer · account/ })).toBeVisible();

    await page.goto("/sales");
    await addPart(page, "BRK-1002");
    await page.getByLabel("Selling price · USD/piece").fill("20");
    await page.getByPlaceholder("Search customer name or phone").fill("Mixed tender browser customer");
    await page.getByRole("button", { name: /Mixed tender browser customer/ }).click();
    const paymentCard = page.getByRole("heading", { name: "4 · Payment" }).locator("xpath=ancestor::section");
    await paymentCard.locator("select").first().selectOption({ label: "Physical safe — LBP · LBP (safe)" });
    await paymentCard.getByLabel("Native LBP").fill("900000");
    await paymentCard.getByLabel("Confirmed USD").fill("10");
    await paymentCard.getByRole("button", { name: "Add payment" }).click();
    await paymentCard.locator("select").nth(1).selectOption({ label: "Whish wallet — USD · USD (Whish)" });
    await paymentCard.getByLabel("Native USD").fill("5");
    await paymentCard.getByLabel("Leave unpaid debt · USD").fill("5");
    await page.getByRole("button", { name: "Review sale" }).click();
    await page.getByRole("button", { name: "Confirm & post sale" }).click();
    await expect(page.getByText(/Sale #\d+ recorded: \$20\.00/)).toBeVisible();
  });

  test("reload after a committed response loss resolves by operation status", async ({ page }) => {
    await signIn(page);
    await page.goto("/sales");
    await addPart(page, "FLT-2001");
    await page.getByLabel("Selling price · USD/piece").fill("4");
    await page.locator("select").first().selectOption({ label: "Physical safe — USD · USD (safe)" });
    await page.getByLabel("Native USD").first().fill("4");
    await page.getByRole("button", { name: "Review sale" }).click();
    await page.route("**/api/sales", async (route) => {
      const response = await route.fetch();
      expect(response.status()).toBe(200);
      await route.abort("connectionfailed");
    }, { times: 1 });
    await page.getByRole("button", { name: "Confirm & post sale" }).click();
    await expect(page.getByText(/uncertain result|response was lost/i)).toBeVisible();
    await page.reload();
    await expect(page.getByText(/Sale #\d+ recorded: \$4\.00/)).toBeVisible();
  });

  test("faulty return is quarantined and an over-return is rejected", async ({ page }) => {
    await signIn(page);
    await page.goto("/sales");
    await addPart(page, "ELC-3002");
    await page.getByLabel("Selling price · USD/piece").fill("12");
    await page.locator("select").first().selectOption({ label: "Physical safe — USD · USD (safe)" });
    await page.getByLabel("Native USD").first().fill("12");
    await page.getByRole("button", { name: "Review sale" }).click();
    const posted = page.waitForResponse((r) => r.url().endsWith("/api/sales") && r.request().method() === "POST");
    await page.getByRole("button", { name: "Confirm & post sale" }).click();
    const sale = await (await posted).json() as { id: string };

    await page.goto(`/returns?sale=${sale.id}`);
    await page.getByLabel("Return quantity · pieces").fill("2");
    await page.getByLabel("Why is this being returned?").fill("Faulty sensor browser test");
    await page.getByRole("button", { name: "Review return" }).click();
    await expect(page.getByText(/no greater than the remaining sold quantity/i)).toBeVisible();
    await page.getByLabel("Return quantity · pieces").fill("1");
    await page.getByLabel("Returned stock condition").selectOption("Quarantined");
    await page.getByLabel("Issue with returned goods").fill("Electrical fault confirmed at counter");
    await page.getByText("Physically separated or tagged").click();
    await page.getByRole("button", { name: "Add cash / Whish refund" }).click();
    await page.getByLabel("Native USD").fill("12");
    await page.getByRole("button", { name: "Review return" }).click();
    await page.getByRole("button", { name: "Confirm & post return" }).click();
    await expect(page.getByText(/Return recorded: \$12\.00/)).toBeVisible();
    await page.reload();
    await expect(page.getByText("Fully returned")).toBeVisible();
  });

  test("cut hose is entered in metres and posts the proportional total", async ({ page }) => {
    await signIn(page);
    await page.goto("/sales");
    await addPart(page, "HOSE-CUT-8");
    await page.getByLabel("Length · metres").fill("0.50");
    await page.getByLabel("Selling price · USD/metre").fill("6");
    await expect(page.getByText("0.50 m = 50 cm recorded in stock.", { exact: false })).toBeVisible();
    await page.locator("select").first().selectOption({ label: "Physical safe — USD · USD (safe)" });
    await page.getByLabel("Native USD").first().fill("3");
    await page.getByRole("button", { name: "Review sale" }).click();
    await page.getByRole("button", { name: "Confirm & post sale" }).click();
    await expect(page.getByText(/Sale #\d+ recorded: \$3\.00/)).toBeVisible();
  });
});
