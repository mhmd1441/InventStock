# InventStock state

## Current
- Catalogue, stock, suppliers, imports, cash, sales, customers, returns, pricing, reconciliation and internal receipts are implemented in code; transaction release is **OFF**.
- All 37 EF migrations through `InternalSaleReceipts` are applied to local `InventStockDev`; it is development-only, not a shop deployment.
- Last full backend: **207/207** passed on disposable PostgreSQL 17 (2026-10-25; `TestResults/full-backend-v1-final.log`). Current split-role PostgreSQL coverage: **64/64** passed (`inventstock_migrator` setup, `inventstock_app` runtime). Frontend source checks: **23/23**, ESLint and TypeScript passed; Next production packaging is blocked by a Windows `EPERM` generated-`.next` directory error after compilation.
- Disposable browser passes: admin/staff/session, supplier import, linked return, supplier payment and daily reconciliation.
- `Scripts/Run-BrowserSmoke.ps1` is written and exercised through disposable migration/seed/user creation, but has no completed Playwright result yet. Its 2026-10-25 browser pack failed before app interaction because Playwright Chromium was not installed (13 failed, 3 skipped); a reporter override then kept the HTML server open. The runner now preflights Chromium before creating its disposable database, launches via `cmd /c CALL`, and preserves the configured HTML `open: never` setting.
- The transaction browser pack spans nine serial spec files, including sale/receipt, opening correction, complete paper payment, exceptions, legacy return, advanced sale/return and exchange. The complete wrapper has no authoritative PASS yet.
- Encrypted frequent/nightly backup, two-destination verification, bounded retention, 30-minute health check and restore drill passed on disposable PostgreSQL 17; Windows Task Scheduler is not installed/tested on the shop PC.
- Windows LAN deployment scripts/docs are written: one Caddy HTTPS port, loopback-only API/frontend/PostgreSQL, startup/restart tasks, firewall isolation and release/rollback procedure. They are build/syntax verified but not installed on the shop PC.
- Priority: protected real catalogue onboarding, not enabling shop transactions.

## Next steps
1. Run `Run-BrowserSmoke.ps1` from an ordinary owner PowerShell session and retain its final PASS/FAIL logs; the sandbox-run wrapper reached setup but did not return a Playwright result.
2. Install/test deployment plus frequent/nightly backup tasks on the shop PC; verify reboot, LAN HTTPS, offline UI and next-morning backup results.
3. Exchange UI (optional; do not block catalogue pilot).
4. Review 50 real import rows, then scale only after search/privacy/no-movement checks.
5. Run hostile-search benchmark and messy-import matrix on the owner's PC before the 1,000-item import.
6. Ask accountant about legal invoice/receipt obligations.

## Work queue
- Reviewable V1 item 1 (one-command verification): partly done — `Scripts/Verify-All.ps1` is written with disposable-container/database guards and consolidated PASS/FAIL/SKIPPED logs; the wrapper is not run. Its backend child separately passed **207/207**, while the frontend packaging child is blocked by Windows generated-output `EPERM`.
- Reviewable V1 item 2 (pilot/operator docs): done — pilot setup/import/search runbook, one-page counter cheat sheet, and numbered paper-fallback/recovery procedure written; documentation only, not exercised on the shop PC.
- Reviewable V1 item 3 (advanced browser coverage): written, not run — mixed LBP/Whish/debt, committed-response-loss recovery, faulty quarantine/over-return, cut-hose metre and exchange scenarios added; disposable demo seed gains a cut-hose fixture. The wrapper's API/database setup was exercised; live Playwright verification remains pending.
- Reviewable V1 item 4 (messy imports): done and focused-verified — 100-row parser matrix handles two-brand collisions, leading zeros, separators/case, Arabic, unknown brand/cost/TVA and comma/dot decimals; wrong currency, overlong values, excess columns and exact duplicates reject explicitly. Frontend 23/23 and backend import tests 3/3 passed, including PostgreSQL catalogue-only/no-stock/no-cash and duplicate-file rejection.
- Reviewable V1 item 5 (hostile search runner): written, not run as requested — guarded PostgreSQL 17 script executes the existing 20k-SKU/~280k-reference/100k-movement benchmark and writes parsed p50/p95/max Markdown plus raw log.
- Reviewable V1 item 6 (counter speed): code complete and source-verified — search autofocuses; Enter adds the first ranked result; the last tender account is remembered per user; Enter on focused Review submits the unchanged review. Common suggested-price USD sale changes from about 6 explicit actions/4 pointer actions to 5 explicit actions/1 pointer action; no safety review was removed.
- Reviewable V1 item 7 (exception attention): code complete and backend-verified — admin dashboard counts operational exceptions since the latest recorded cash close (or all history before the first close) and links to the private exceptions list; counter dashboard DTO remains unchanged.
- Database security: done and verified on disposable PostgreSQL 17 — script safely transfers a postgres-owned DB to migrator; app DDL plus five ledger mutations denied; `Parts.NeedsRecount` allowed; trigger denial and backup-role dump/restore (`1|1|1|37`) passed. Split-role fixture coverage is **64/64** passed in per-class TRX reports.
- 15-minute backup: script/docs done and disposable verification passed — frequent/nightly retention sets, two encrypted destinations, 30-minute health warning, authenticated restore and source/restore comparisons. Real Windows Task Scheduler installation remains owner/shop-PC work.
- LAN/HTTPS deployment: scripts/docs written and compile/syntax checked — standalone frontend, loopback reverse proxy trust, startup/restart tasks, LAN-only firewall and safe update/rollback. Actual task/firewall/certificate/reboot/LAN installation remains shop-PC work.
- Browser specs: required cheap gaps are written and TypeScript/test discovery pass — receipt print/reprint, opening correction, complete paper payment, exceptions and sold-before return. Live wrapper run remains owner-PC work.
- Reviewable V1 item 8 (exchange UI): code complete and source-verified, browser pending — linked sale exchange UI returns selected original lines, adds replacement lines, explicitly allocates replacement credit/refund/account reduction/payment/change, preserves operation-status recovery, and has a disposable browser spec. No live browser pass yet.

## Open items and risks
- Do not enable transactions: backup/recovery and browser coverage remain incomplete.
- `InventStockDev` still uses a PostgreSQL superuser and lacks DB-level append-only triggers; never use it for sales.
- Task Scheduler backup jobs are documented but not installed/tested under the shop Windows account; scripts alone do not provide scheduled protection.
- Deployment tasks, Caddy private CA trust and firewall rules are not installed/tested; real-PC reboot, second-client LAN and internet-disconnected checks remain mandatory.
- Codex Windows browser orchestration is unreliable; the local smoke script is authoritative.
- Smoke-runner setup corrections were made on 2026-10-25 (valid `Staff` role and owned process-tree cleanup). A sandbox execution stalled after setup with no browser result; it was interrupted and its exact disposable database was dropped.
- Next production packaging is currently blocked by a Windows `EPERM` while Next creates generated `.next` directories, even after a clean cache; source compilation, lint and type checks pass. Treat this as a local Windows/tooling blocker until reproduced on the owner PC.
- Benchmark and messy-import verification are deferred to the owner's PC before a 1,000-item import.
- PDF parsing, hose remnants and formal accounting are deferred; exchange UI is optional.
- Suggested prices may reveal approximate cost to staff who know the formula; accepted tradeoff needs review.
- Cut-hose total does not guarantee a continuous remnant; physical confirmation remains required.
- Pooled/unknown costs must never be shown as factual margin or valuation.

## Decisions
- One pooled shop stock; Center/Zeit/Yesser/fo2 are not separately tracked.
- PostgreSQL 17; modular monolith; LAN-first; 20k+ catalogue rows supported.
- Staff never see supplier/cost/cash-outgoing data; admin private mode is hidden by default.
- Customer name required; phone/address optional; cash sale anonymous; debt USD-denominated.
- Confirmed excess may become credit; staff may reason-correct payments; customer openings admin-only.
- Tender types: physical USD/LBP/EUR plus separate digital Whish; persist native amount and confirmed USD forever.
- Whish refunds may be digital; supplier payments, cash-out and fees are distinct movements.
- Selling price is flexible/manual; no v1 minimum.
- Incorrect stock needs logged exception; quarantine hard-blocks sale.
- Linked returns need no PIN; tender explicit; never exceed original posted refundable value.
- Return can be cash, Whish, account reduction or future exchange; no forced debt-first; faulty return quarantined.
- Legacy returns allowed for staff with known SKU, permanent reason/evidence and no invented historic cap.
- Exchange explicitly balances return, replacement, tender/change, credit/debt; anonymous exchange cannot create account debt/credit.
- Staff count to clear `NeedsRecount`; another shortage waits for count.
- Suggested price is advisory (eligible TVA, optional EUR conversion, markup, ceiling); staff sees suggestion/override only.
- Cut hose is owner-set USD/metre, proportional sale, whole-centimetre stock, no remnant guarantee.
- Inventory uses pieces/centimetres; packs convert on receipt; remnants deferred.
- Catalogue import creates no shop stock; receiving and supplier payment are separate.
- Unknown cost remains null and never blocks sale/receipt.
- No v1 general ledger: cash/wallet movements plus customer ledger and reconciliation.
- Outgoings only when paid; payment/entry times differ; optional historical period.
- Personal-pocket payments never reduce safe; categories are government, utilities, internet, professional, refreshments, personal/family, charity, other; supplier payment is separate.
- Planned expenses have no cash effect.
- Uncertain commands check status; never blindly replay.
- Shortage means physical goods in hand, never supplier promise; quarantine prevents exception.
- Sale = received settlement − explicit change + credit used + unpaid debt; anonymous cannot use credit/leave debt.
- Base price has six decimals; line rounds once to cents; posted totals cap returns.
- Positive customer balance is debt; negative is credit; customer lock protects credit use.

## Commands
```powershell
# Disposable PostgreSQL only
$env:INVENTSTOCK_TEST_POSTGRES='Host=localhost;Port=55439;Database=inventstock_test;Username=postgres;Password=<test-password>'
$env:INVENTSTOCK_TEST_MIGRATOR_POSTGRES='Host=localhost;Port=55439;Database=inventstock_test;Username=inventstock_migrator;Password=<test-password>'
dotnet test backend/InventStock/InventStock.Tests/InventStock.Tests.csproj --no-restore --filter 'FullyQualifiedName~PostgresLegacySaleReturnTests|FullyQualifiedName~SaleApiTests|FullyQualifiedName~ReferenceUpgradeTests'
dotnet test backend/InventStock/InventStock.Tests/InventStock.Tests.csproj --no-restore -m:1 -c Release
dotnet build backend/InventStock/InventStock.sln --no-restore -m:1 -c Release
Set-Location frontend; npm run test:client; npm run lint; npx tsc --noEmit; npm run build -- --webpack
Set-ExecutionPolicy -Scope Process Bypass; .\Scripts\Run-BrowserSmoke.ps1
# Backup/restore: Docs/Backup-Restore.md
```

## Key paths
- Catalogue/search/import: `backend/InventStock/*/{Domain,Application,Infrastructure,Api}`, `frontend/app/(app)/{parts,imports}`.
- Stock/receiving/quarantine: `Application/Services/StockMovementService.cs`, `Infrastructure/Services/ReceivingService.cs`, `frontend/app/(app)/movements`.
- Sales/returns/pricing: `Infrastructure/Services/{SaleService,SaleReturnService,PricingService}.cs`, `frontend/app/(app)/{sales,returns,pricing}`.
- Customers/payments: `Infrastructure/Services/CustomerService.cs`, `frontend/app/(app)/customers`.
- Cash/reconciliation/transfers: `Infrastructure/Services/CashService.cs`, `frontend/app/(app)/cash`.
- Privacy/release: `Api/Middleware/TransactionReleaseMiddleware.cs`, `frontend/components/layout`, `Docs/Release-Checklist.md`.
- Backup/deployment/smoke: `Scripts/{Backup-InventStock,Restore-InventStockBackup,Deployment}*`, `Docs/{Backup-Restore,Real-Database-Setup,Windows-LAN-Deployment,Playwright-Transaction-Smoke}.md`, `frontend/e2e`.

## Safety
- Never migrate, seed, reset or benchmark any non-disposable database **unless the user explicitly authorizes that specific task in the message**.
- Use only named disposable PostgreSQL databases for automated tests/benchmarks; never expose secrets, supplier documents, customer data or backups.
- No Git/GitHub without explicit authorization; preserve IDs/movements/user data; back up/review before any authorized normal-database migration.

## Verified, never rerun (unless covered files change)
- Catalogue/search/populated upgrade: PostgreSQL 17 coverage passed.
- Inventory/receiving/quarantine: focused PostgreSQL coverage passed.
- Sales/customers/returns: 206/206 baseline; atomic/concurrency/rounding/paper recovery coverage passed.
- Cash/reconciliation/supplier payment/transfers: focused PostgreSQL coverage passed, including fee/EUR.
- Database role/security: disposable PostgreSQL 17 app DDL/ledger denial, `NeedsRecount` update, trigger denial, backup-role dump/restore and all 64 split-role PostgreSQL tests passed.
- Backup/recovery: disposable encrypted two-destination dump/restore counts and stock sum matched; 30-minute fresh/stale check and separate frequent/nightly retention passed. Scheduler execution remains unverified.
- Deployment code: backend Release build passed; frontend standalone production build passed; all deployment PowerShell scripts parse. Caddy, scheduled tasks, firewall, certificate trust, reboot and LAN access are not run here.
- Frontend source: 23/23 client tests, ESLint and TypeScript passed on 2026-10-25; production build compiled and type-checked but failed while packaging generated `.next` output with Windows `EPERM`.
- Browser: admin/staff/session, supplier import, linked return, supplier payment and reconciliation passed.
- Browser pack: nine serial spec files are present; legacy return, receipt/reprint, opening correction, exceptions, complete paper payment, advanced sale/return and exchange are written/type-checked but have no authoritative complete-pack pass. Pricing override remains uncovered.
