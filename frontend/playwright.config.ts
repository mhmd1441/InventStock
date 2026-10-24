import { defineConfig, devices } from "@playwright/test";

/**
 * These tests intentionally target a separately started, disposable instance.
 * They never create a server or database themselves, so they cannot accidentally
 * point at InventStockDev.
 */
export default defineConfig({
  testDir: "./e2e",
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  // The disposable smoke pack intentionally builds one workflow across tests
  // (post sale, then return it), so database-mutating files must run serially.
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3000",
    // Disposable local API runs use the .NET development certificate.
    ignoreHTTPSErrors: true,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
