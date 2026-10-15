# InventStock work log

Historical evidence only. For the next action, decisions and safety rules, read `STATE.md` instead.

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
