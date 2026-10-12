# InventStock state

## Current

- Task: atomic counter-sale backend posting slice implemented and verified. API/testing only; no sales UI or operational release.
- Next concrete step: customer create/search/detail and account-balance APIs, with immutable IDs, optional distinguishing information, and no automatic name-based merging. Then reviewed opening debt/credit entry and insufficient-stock exception posting. Continue from `Docs/Sale-Posting.md`; do not re-audit completed catalogue/cash work.
- Normal database remains at the previously user-applied `ReviewedSupplierImports`. `CashFoundation` and `AtomicCounterSales` are generated, NOT applied to `InventStockDev`/shop/remote DB.

## Done

- Catalogue/search: brands and aliases, SKU references, exact/prefix/suffix/contains search, Mercedes optional-`A`, audited unlink/restore.
- Privacy/session: separate counter/admin DTOs, customer-safe admin mode, cross-tab logout isolation and delayed exact-session revocation.
- Inventory: idempotent commands, piece/centimeter base units, `NotCounted` checkpoints, append-only quarantine holds.
- Suppliers: private cost history, immutable receiving, and reviewed CSV/TSV/paste imports. Imports create no stock/cash movements and new SKUs remain `NotCounted`.
- Cash foundation: safe USD/LBP/EUR and Whish USD/LBP account definitions; admin opening counts and categorized actual outgoings with native amount, agreed USD, dates/period, actor, funding source and optional receipt reference. Personal-pocket payments never reduce shop cash; corrections are idempotent linked reversals.
- Cash UI: private-mode-only counts/payment form/categories/monthly list/totals/reversals; no expected-safe balance. Migration `20261012152847_CashFoundation` remains unapplied to the normal DB.
- `next.config.ts` supports an isolated `NEXT_DIST_DIR`, allowing a production build while the normal development server owns `.next`.
- Sale backend: one transaction posts immutable header/line/tender snapshots, per-line stock deductions, native cash/Whish movements, USD customer charges/settlements and permanent idempotent result. Costs are nullable private last-actual-purchase estimates.
- Sale safety: aggregate repeated SKU quantities; lock operation, customer, then sorted distinct SKUs; check credit under the customer lock; batch stock reads; receiving now uses the same SKU locks. Shared operation guard prevents cross-command ID reuse.
- Sale recovery/privacy: same actor/payload replays a stable outcome; current DB user/role checked on posting/recovery; staff-safe DTOs and events; notifications only after commit and failures do not undo sales.
- Uncounted sales require physical confirmation/reason and sufficient recorded available stock, retain unknown confidence; shortage override remains unfinished. Quarantine is never released/consumed by a sale.
- New migration: `20261012203135_AtomicCounterSales`, additive tables plus nullable cash-tender link; exercised only on disposable PostgreSQL schemas.

## Checks passed

- `uncommitted + 2026-10-12 23:39 Asia/Beirut`, sale backend state: full backend suite **137/137**, skipped 0 (69s), including **12 new real-PostgreSQL sale/service/HTTP cases**. Disposable PostgreSQL 17 `inventstock_test`; generated test schemas removed and `inventstock-sale-test` stopped/auto-removed afterward.
- Tested 25-file sale/backend manifest SHA-256: `9c1e131f31be84c027e73a3941772a430818d8099f3bab506e8d98e228f15adc` (sorted relative paths + each SHA-256; changed files listed below). No Git used.
- Sale cases: competing last units, opposite carts/repeated SKUs, duplicate concurrent posts, changed payload/actor rejection, recovery, rollback after SQL writes, mixed currencies/change, partial debt, double credit spending, rounding, quarantine/uncounted stock, private DTO/events, post-commit notification failure, disabled user/removed role/stale token, cross-command ID collision.
- Same tested sale state: Release compilation passed; EF reports no pending model changes after `AtomicCounterSales`.
- `uncommitted + 2026-10-12` cash/frontend state: production build, ESLint, TypeScript and **13/13** client tests passed; isolated build output removed. Frontend files unchanged since those checks.
- Frontend unchanged in the sale batch; its previously passed checks were not repeated. Initial sale replay precision mismatch fixed; SQLite decimal-constraint mismatch resolved by running sale HTTP tests on PostgreSQL, without weakening constraints. No outstanding test failures.

## Unresolved / open

- Both pending migrations need a reviewed backup before user application; no normal-database action authorized.
- Sale posting API works in tests; customer create/search/read/opening-credit workflows, later customer payments, insufficient-stock exception correction, sale UI, receipt numbering/printing remain unfinished. No production API exists to create/open customer balances yet.
- Returns/exchanges/refunds, supplier payments, wallet/cash transfers, daily reconciliation, automatic backup/restore and deployment remain unfinished. This is NOT an operational sales release.
- Cash totals are intentionally incomplete until sales, customer payments, refunds, supplier payments, transfers and corrections are all represented.
- Do not import 1,000 authoritative items until the persistent dataset is protected from demo resets and a backup/restore has been tested. Supplier-PDF parsing is deferred; reviewed CSV/TSV/paste import already exists.

## Decisions (do not reopen unless asked)

- One shop-wide stock pool; Center/Zeit/Yesser/fo2 are not separately tracked.
- PostgreSQL modular monolith; local/LAN first, online later. Twenty-thousand-plus catalogue rows are within scope.
- Employees never receive supplier/cost/cash-outgoing data. Admin private data is hidden by default at the counter.
- Customer name required; phone/address optional. Cash sales may be anonymous. Debt is USD-denominated.
- Payments: physical USD/LBP/EUR cash and Whish. Whish is separate from physical-safe liquidity.
- Preserve original tender amount/currency and the explicitly confirmed USD credit/value; historical conversion is never rewritten by a current rate.
- Selling price is flexible/manual; no minimum-price rule in v1.
- Uncounted/incorrect stock later uses an explicit logged exception; quarantined stock is hard blocked.
- Routine linked returns/refunds require no admin PIN. Refund tender is explicit; refunds cannot exceed the original refundable amount.
- Inventory uses integer pieces or centimeters. Packs convert on receipt. Remnants are deferred; confirm continuous length physically.
- Supplier catalogues/imports never establish shop stock. Receiving goods and paying a supplier are separate operations.
- Unknown cost remains null and never blocks receipt/sale. Do not present estimated margin as factual.
- No formal general ledger in v1: use sale-linked native-currency cash/wallet movements and a customer-account ledger with reconciliation.
- Outgoings are recorded only when actually paid. Payment date and recorded timestamp are separate; optional period records what the payment relates to.
- Shop-funded and family-personal-pocket payments are distinct. Personal-pocket payments do not reduce the safe.
- Municipality/government, utilities, internet, professional fees, shop refreshments, personal/family withdrawal, charity and other are separate editable categories. Supplier payments are not expenses.
- Planned expenses have no cash effect and are not recorded as paid outgoings.
- No blind replay of uncertain commands; query operation status and require confirmation if not committed.
- Sale posting: total = received USD settlements - explicit change USD settlements + customer credit used + unpaid debt. Anonymous sales cannot use credit or leave debt. Preserve native amounts and confirmed USD settlements; do not silently infer change or create credit from overpayment.
- Price is per stock base unit with six decimals; each line rounds once to USD cents (midpoint away from zero), total sums posted lines. Posted line totals are the caps for future cumulative refund allocation.
- Customer ledger: positive balance = debt, negative = credit. Named sale posts full charge and negative net tender payment; credit is consumed through that net charge, never debited twice. Detailed posting contract: `Docs/Sale-Posting.md`.

## Commands

```powershell
# Required for both focused sales and full backend tests (only disposable DB)
$env:INVENTSTOCK_TEST_POSTGRES='Host=localhost;Port=<test-port>;Database=inventstock_test;Username=postgres;Password=<test-password>'
# Fast current slice
dotnet test backend/InventStock/InventStock.Tests/InventStock.Tests.csproj --no-restore -m:1 -c Release --filter "FullyQualifiedName~PostgresSaleTests|FullyQualifiedName~SaleApiTests"
# Full backend
dotnet test backend/InventStock/InventStock.Tests/InventStock.Tests.csproj --no-restore -m:1 -c Release

# Backend build / migration-model check
dotnet build backend/InventStock/InventStock.sln --no-restore -m:1 -c Release
dotnet ef migrations has-pending-model-changes --project backend/InventStock/InventStock.Infrastructure/InventStock.Infrastructure.csproj --startup-project backend/InventStock/InventStock.Api/InventStock.Api.csproj --no-build --configuration Release

# Frontend fast / full
Set-Location frontend
npm run test:client
npm run lint
npx tsc --noEmit
$env:NEXT_DIST_DIR='.next-cash-build'
npm run build -- --webpack
```

## Key paths

- Cash domain: `backend/InventStock/InventStock.Domain/Entities/Cash*`, `OutgoingPayment*`, `ExpenseCategory.cs`; `Domain/Enums/CashEnums.cs`
- Cash application: `backend/InventStock/InventStock.Application/DTOs/Cash`, `Interfaces/ICashService.cs`, `Validators/CashValidators.cs`
- Cash infrastructure/API: `backend/InventStock/InventStock.Infrastructure/Services/CashService.cs`, `Data/Configurations/CashConfiguration.cs`, `backend/InventStock/InventStock.Api/Controllers/CashController.cs`
- Cash tests/UI: `backend/InventStock/InventStock.Tests/Api/CashApiTests.cs`, `frontend/app/(app)/cash/page.tsx`
- Other backend modules: `backend/InventStock/InventStock.Domain`, `InventStock.Application`, `InventStock.Infrastructure`, `InventStock.Api`
- Frontend data/navigation: `frontend/lib/types.ts`, `frontend/lib/queries.ts`, `frontend/components/layout`, `frontend/next.config.ts`
- Design/checkpoint: `Docs/Production-Design.md`, `Docs/STATE.md`

## Sale batch changed files

Paths below are relative to `backend/InventStock/` (25 source/model/test files):
- Application: `InventStock.Application/DTOs/Sales/SaleDtos.cs`, `Interfaces/ISaleService.cs`, `Validators/SaleValidators.cs`.
- Domain: `InventStock.Domain/Entities/Sale.cs`, `Entities/Customer.cs`, `Entities/CashMovement.cs`, `Enums/CashEnums.cs`.
- Infrastructure: `InventStock.Infrastructure/Services/SaleService.cs`, `Services/CashService.cs`, `Services/ReceivingService.cs`, `Services/StockHoldService.cs`, `Repositories/StockMovementRepository.cs`, `Data/OperationGuard.cs`, `Data/AppDbContext.cs`, `Data/Configurations/SaleConfiguration.cs`, `Data/Configurations/CashConfiguration.cs`, `DependencyInjection.cs`.
- Migration: `InventStock.Infrastructure/Data/Migrations/20261012203135_AtomicCounterSales.cs`, matching `.Designer.cs`, `AppDbContextModelSnapshot.cs`.
- API: `InventStock.Api/Controllers/SalesController.cs`.
- Tests: `InventStock.Tests/Infrastructure/PostgresSaleTests.cs`, `Api/SaleApiTests.cs`, `Api/PostgresSaleApiFactory.cs`, `Api/ApiFactory.cs`.
- Documentation: `Docs/Sale-Posting.md` and this checkpoint (outside backend; not part of code hash).

## Safety

- Never migrate, seed, reset or benchmark a shop/remote/normal database. Use only an isolated disposable database named `inventstock_test` for automated database tests.
- Never use Git or GitHub unless the user explicitly changes the instruction; no commits, pushes or branches.
- Never commit or expose secrets, local connection strings, supplier documents, exports, backups or customer data.
- Preserve existing IDs, movements and user data. Back up and review before any normal-database migration.
