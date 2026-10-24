# InventStock work log

Historical evidence only. For the next action, decisions and safety rules, read `STATE.md` instead.

## 2026-10-22 — checkpoint compacted

- `STATE.md` was rewritten as the short operational checkpoint; historical narrative stays here.

## 2026-10-22 — reconciliation frontend verification and legacy-return smoke harness

- Corrected ESLint configuration to ignore generated Playwright HTML reports and test-result files. The previous lint failure was tooling-only: ESLint was parsing generated/minified browser-report assets, not an application defect.
- The reconciliation client contract fix passed client tests **22/22**, ESLint, TypeScript and an isolated production build. Next temporarily added the isolated build directory to `tsconfig.json`; that generated entry was removed after the build.
- Added a disposable-only Playwright specification for a clearly labelled sold-before-InventStock cash return. It has TypeScript coverage but has not been run against a temporary API/database yet.

## 2026-10-22 — daily-reconciliation browser-smoke specification

- Added `frontend/e2e/daily-reconciliation.spec.ts` and a stable private-card selector. The test signs in as an admin, deliberately reveals private mode, enters matching USD opening/physical counts and confirms the current-day partial close display.
- The first run exposed a real frontend/API contract mismatch: the API returns reconciliation `accountCode` and `accountName`, while the TypeScript client expected `cashAccountCode` and `cashAccountName`. That made account names blank in the card. The client contract and UI were corrected and each account now carries a deterministic test identifier.
- `npx tsc --noEmit` passed. The corrected Chromium smoke passed **1/1** against a fresh disposable PostgreSQL 17/API/Next environment. A read-only database check confirmed `SAFE_USD` recorded a partial close with opening, expected and physical count all **100 USD**, and zero difference. Temporary servers/database were stopped/dropped; no normal database or Git action occurred.

## 2026-10-22 — supplier-payment browser posting smoke

- Added `frontend/e2e/supplier-payment.spec.ts`. On a fresh disposable PostgreSQL 17 database, an admin deliberately revealed private mode, selected a supplier and the USD physical safe, then posted a $100 settlement through the real cash-page form. Chromium passed **1/1**.
- The first attempt found only a test-selector problem: a React refresh could replace the labelled control while Playwright held it. The supplier-payment card now has a stable test-only CSS hook and the smoke test scopes its two native selects through that card. A read-only database check confirmed exactly **1** supplier payment and exactly **one $100** linked cash-out. No normal database was touched. The temporary API/frontend were stopped; Docker became unavailable during the final disposable-database drop, so its removal needs a simple verification when Docker is back.

## 2026-10-22 — linked-return browser posting and JSON contract repair

- Added `frontend/e2e/linked-return.spec.ts`. It finds a temporary paid sale, selects one piece for return, allocates the full refund to the USD safe, reviews and posts. Chromium passed **1/1** against a disposable PostgreSQL 17 environment.
- The test exposed a real integration bug: the UI posts documented `ReturnCondition` strings such as `Sellable`, but the API only accepted numeric enum values. `Program.cs` now configures `JsonStringEnumConverter`, accepting the readable client contract while retaining numeric compatibility. A Release solution build passed (0 errors; only existing offline NuGet audit warnings). A read-only query verified one return line and a net stock-ledger quantity of 34—the original demo count after the temporary sale and return.

## 2026-10-22 — reviewed supplier-import browser smoke

- Added `frontend/e2e/supplier-import.spec.ts`. On a fresh disposable PostgreSQL 17 database with demo data only, an admin pasted one CSV row, prepared it, staged it, selected `Create new SKU`, reviewed it and committed it through the browser. Chromium result: **1/1**.
- A read-only query confirmed the imported `BROWSER-IMPORT-001` SKU had **0** `StockMovements` and **1** supplier cost observation. Temporary API/frontend processes and the disposable database were removed afterwards; no real supplier file or shop database was touched.

## 2026-10-22 — paper-payment and transfer browser smoke

- A fresh temporary PostgreSQL 17 database, Release API and Next development server verified the expanded Chromium smoke harness. The admin flow created a temporary customer, reached the private paper-era customer-payment form, and reached the private transfer/Whish-cash-out form. Staff could use public transaction pages but could not access cash, pricing or exceptions; cross-tab sign-out also passed.
- Results: admin reachability **1/1**, staff privacy **1/1**, cross-tab sign-out **1/1**. Each was run against a fresh temporary API process because the production-style login limiter intentionally rejects repeated diagnostic logins from the same local IP. Only the disposable `inventstock_browser_smoke_20261022` database and temporary ports were used.

## 2026-10-20 — disposable Playwright transaction-screen smoke

- Added a Chromium Playwright harness for admin/private reachability, staff private-screen denial, and cross-tab sign-out. The admin and staff checks passed against a newly migrated disposable PostgreSQL 17 database with temporary API/frontend servers and temporary users.
- The first cross-tab sign-out attempt hit the temporary API's login rate limiter after earlier diagnostics. A clean retry using a fresh temporary API passed **1/1**. All three smoke checks are therefore green; temporary servers and `inventstock_browser_smoke_20261020` were stopped/dropped afterwards. No normal database was touched.

## 2026-10-14 — return browser-smoke attempt blocked by browser control

- A fresh disposable PostgreSQL 17 `inventstock_test` was migrated and demo-seeded by the Release API; a separate Next frontend reached ready on port 3001. No normal/shop database was touched.
- The in-app browser could not reach Windows localhost; the Windows browser-control helper stopped because URL-policy enforcement is unavailable for that browser. No sale or return was posted through a browser, so the smoke test remains open. Test servers were stopped and the disposable container/demo data removed. Next's incidental `tsconfig.json` additions for the test build were reverted. No app behavior was changed in this attempt.

## 2026-10-14 — linked-return counter screen and sale lookup

- Added counter-safe recent sale and exact-sale lookup, plus a return-availability endpoint showing cumulative returned quantity/refund without cost or supplier fields. The return page supports sellable/quarantined items, physical separation confirmation, explicit native cash/Whish refunds and named-customer account reduction, exact original-line cent allocation, review/post and actor-scoped minimal operation-status recovery. Sale confirmation and customer history link to the page.
- Added client allocation tests and HTTP tests for lookup privacy/availability before and after posting. Fixed disposable PostgreSQL fixture setup so `pg_trgm` is shared from `public` across isolated test schemas. No migration was generated or applied to the normal database.
- Checks on uncommitted 2026-10-14 source: 8/8 focused and 158/158 full backend tests, 0 skipped, disposable PostgreSQL 17; backend Release build 0 warnings/errors; 19/19 frontend client tests, ESLint, TypeScript and Next production build. Initial full tests failed from test-only extension placement; fixture repair and final full rerun passed. New return page is not yet browser-smoked. No Git or normal DB action.

## 2026-10-13 — linked-return backend and staff recount

- Added atomic linked-return posting with immutable header/line/tender, cumulative original-line refund cap, physical stock-in, optional quarantined hold, native cash/Whish payout and/or customer-account reduction, permanent idempotency and actor-scoped recovery. Flexible settlement is owner-approved; no forced debt-first rule. Added `20261013151728_LinkedSaleReturns`, generated only, never applied to normal DB.
- Staff can perform a physical count to clear `NeedsRecount`; another shortage sale still waits for the count. Fixed validation so the expected book quantity can be negative after a confirmed shortage. The full suite exposed an obsolete authorization assertion and the negative-count validation gap; both were fixed and retested. The shared operation guard now includes opening corrections and returns.
- On uncommitted 2026-10-13 source: 158/158 full backend tests on disposable PostgreSQL 17, skipped 0; focused linked-return/API and recount tests passed; EF no pending model changes. Frontend: 17/17 client tests, ESLint, TypeScript and production build passed (the sandboxed build hit EPERM; permitted rerun passed). Test container removed; no Git or normal DB action. Return UI, paper-era returns and exchanges are unfinished.

## 2026-10-13 — linked-return allocation foundation

- Added cumulative posted-line refund allocation in `Application/Services/ReturnAllocation.cs`, with over-return/over-refund guard tests in `Tests/Infrastructure/ReturnAllocationTests.cs`; documented the exact-cent rule in `Docs/Sale-Posting.md`. No return endpoint, stock movement, cash movement or migration created in this batch.
- Verification on uncommitted 2026-10-13 source: 3/3 focused tests and 152/152 full backend tests, skipped 0, disposable PostgreSQL 17 `inventstock_test`; test container removed. No normal DB touched. Posting awaits the owner's debt-versus-refund policy; paper-era returns, recount authority and repeat-shortage policy are also open.

## 2026-10-13 — linked customer-opening correction

- Added an admin-only, append-only correction linked to the original customer opening, preserving later transactions. Both the expected current opening and resulting account balance are confirmed, so stale concurrent edits fail. Added actor-scoped idempotent recovery, statement linkage, status endpoint and admin correction form.
- Generated `20261013142722_CustomerOpeningCorrections` but applied it only through disposable PostgreSQL 17 tests, never the normal database. Relevant code: `Domain/Entities/Customer.cs`, `Application/DTOs/Customers/CustomerDtos.cs`, `Application/Validators/CustomerValidators.cs`, `Infrastructure/Services/CustomerService.cs`, `Infrastructure/Data/Configurations/SaleConfiguration.cs`, `Api/Controllers/CustomersController.cs`, `frontend/app/(app)/customers/page.tsx`, `frontend/lib/{types,queries,pending-customer-operation}.ts`.
- Verification on uncommitted 2026-10-13 source: 7/7 focused and 149/149 full backend tests, 0 skipped, disposable PostgreSQL 17; EF no pending model changes; 17/17 frontend client tests, ESLint, TypeScript, Next production build. First production-build attempt hit Windows sandbox EPERM; permitted rerun passed. New admin form not browser-smoked. No Git, no normal DB touched.

## 2026-10-13 — disposable browser integration

- Built the current Release API and migrated/seeding **only** a temporary PostgreSQL 17 container with database `inventstock_test`. A stale Debug launch initially exposed only the old schema; the Release launch applied the four pending migrations to the disposable database. The normal database was not accessed.
- In a real browser, posted an anonymous $20 USD cash sale; a named $30 sale paid by 900,000 LBP confirmed as $10, $10 Whish, and $10 debt; a $15 Whish payment leaving $5 confirmed customer credit; a $5 credit-only sale; and a linked reversal of that payment after the credit was spent. The earlier sale remained and the customer balance became $15 debt.
- Changed `frontend/lib/tender-math.ts`, `frontend/app/(app)/sales/page.tsx`, `frontend/app/(app)/customers/page.tsx`, and `frontend/tests/sale-math.test.mjs` so tender rows default to the USD physical safe while blank preselected rows still permit all-debt/all-credit sales.
- Checks on this uncommitted source state: 17/17 frontend client tests, ESLint, TypeScript, Next production build; Release API build 0 warnings/errors. No backend source/model change, so earlier PostgreSQL test suite was not repeated. Temporary build output and disposable container were removed; servers stopped. No Git used.

## 2026-10-13 — counter/customer UI

- Added counter-sale and internal customer-account pages, typed query/DTO wiring, exact cent helpers, native tender parser, actor-scoped minimal unresolved-operation journals, shared tender editor, and navigation. Key paths: `frontend/app/(app)/sales/page.tsx`, `frontend/app/(app)/customers/page.tsx`, `frontend/lib/{queries,types,sale-math,tender-math,pending-sale-operation,pending-customer-operation}.ts`, `frontend/components/tender-editor.tsx`, `frontend/tests/sale-math.test.mjs`.
- Verified 17/17 client tests, ESLint, TypeScript and production build. The initial sandboxed Next build hit Windows EPERM; permitted local rerun passed. Generated `.next-*` lint output is excluded. No backend/database files changed and no normal DB touched.

## 2026-10-13 — stock-shortage exception

- Added physically confirmed shortage sale that marks a SKU `NeedsRecount`, keeps the actual negative book balance rather than manufacturing stock, blocks a second shortage until a physical count, and never releases held stock. Migration `20261013080730_SaleStockDiscrepancy` generated but not applied to the normal DB.
- On uncommitted code: 147/147 backend tests, skipped 0, disposable PostgreSQL 17 `inventstock_test`; 14/14 sale-focused PostgreSQL tests; 13/13 frontend tests, ESLint, TypeScript, production build; EF no pending model changes. No normal DB touched.
- Relevant backend modules: `InventStock.Domain/Entities/{Part,Sale}.cs`, `InventStock.Infrastructure/Services/SaleService.cs`, `Repositories/{PartRepository,StatisticsRepository}.cs`, `InventStock.Tests/{Infrastructure/PostgresSaleTests,Api/SaleApiTests}.cs`.

## 2026-10-13 — customer accounts and payments

- Added immutable customer identities, audited profiles, statement/debt, opening debt/credit, payments with native cash/Whish and agreed USD, confirmed excess credit, linked employee payment correction, idempotent outcomes and operation status. Migration `20261013074603_CustomerAccountsAndPayments` generated but not applied to normal DB.
- On uncommitted code: 143/143 backend tests, skipped 0, disposable PostgreSQL 17; Release build 0 warnings/errors; EF no pending model changes. No normal DB touched.
- Key modules: `InventStock.Application/DTOs/Customers`, `InventStock.Infrastructure/Services/CustomerService.cs`, `InventStock.Api/Controllers/CustomersController.cs`, `InventStock.Tests/{Infrastructure/PostgresCustomerTests,Api/CustomerApiTests}.cs`.

## 2026-10-12 — atomic sale backend

- Added one-transaction sale header/lines/tenders, stock and customer/cash movements, stable permanent idempotency, actor/role-safe replay, post-commit notifications, private nullable last-actual-cost snapshots and deterministic lock ordering. Migration `20261012203135_AtomicCounterSales` generated but not applied to normal DB.
- On uncommitted code: 137/137 backend tests, skipped 0, disposable PostgreSQL 17; Release build; EF no pending model changes. Sale tests covered competing last unit, opposite carts, duplicate submissions, changed payload/actor, rollback, mixed currencies/change, partial debt, double credit spending, rounding, quarantine, unknown cost and privacy. The 25-file source/model/test manifest SHA-256 was `9c1e131f31be84c027e73a3941772a430818d8099f3bab506e8d98e228f15adc` at that tested state. No Git used.
- Key modules: `InventStock.Application/DTOs/Sales`, `InventStock.Infrastructure/Services/{SaleService,ReceivingService}.cs`, `InventStock.Infrastructure/Data/{OperationGuard,AppDbContext}.cs`, `InventStock.Api/Controllers/SalesController.cs`, `InventStock.Tests/{Infrastructure/PostgresSaleTests,Api/SaleApiTests}.cs`, `Docs/Sale-Posting.md`.

## 2026-10-12 — cash foundation and UI

- Added private cash accounts, opening counts, categorized actual outgoings and linked corrections, plus admin-only UI. Migration `20261012152847_CashFoundation` generated but not applied to normal DB.
- Frontend production build, ESLint, TypeScript and 13/13 client tests passed at that state.
## 2026-10-15 — Older-sale lookup

- Added paged, staff-safe sale lookup by UTC date range, customer name/label/phone, normalized part number/item name, and exact USD total.
- Replaced the fixed recent-only return picker with searchable, paged sale snapshots.
- Added a general `SaleLines.PartId` lookup index while preserving the filtered shortage index; generated `SaleLookupIndexes` without applying it to a normal database.
- Added PostgreSQL coverage for combined filters, older sales, pagination and cost/supplier privacy. Updated the populated migration rehearsal to assert the exact named chain from `ReviewedSupplierImports`.

