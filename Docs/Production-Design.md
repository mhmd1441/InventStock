# InventStock: reviewed shop design and implementation contract

Reviewed 10 October 2026 against the repository and the owner's requirements.

Status: implementation in progress. Catalogue privacy, indexed search, brand/variant identity and public reference linking are implemented; the transaction design below is not yet implemented. This document supersedes earlier conversational plans where they conflict. The current application remains an inventory prototype, not a verified sales/cash system.

## 1. Scope and corrections to earlier advice

The first useful release supports identifying parts, remembering supplier costs, and recording counts. The first operational transaction release must then support a complete receive/sell/pay/return/reconcile cycle. A read-only catalogue pilot can precede the transaction release; an incomplete money workflow cannot be presented as ready for normal shop operations.

Confirmed owner rules:

- Desktop use first; several people can work simultaneously with individual logins. Public online/mobile access is later.
- Inventory is pooled across Center, Zeit, Yesser and Fo2. Do not require choosing or transferring between those places. Optional descriptive storage notes do not affect quantities.
- Pool quantities for the same sellable SKU, not across different brands, sizes, packages or versions. Bosch and Hella remain distinct choices even if they share references.
- Selling prices are chosen per transaction in USD. No compulsory price tiers or discount workflow.
- Customer debts are USD. Accept USD/LBP/EUR payments at explicitly recorded rates.
- Employees can create debt sales, accept customer payments and see customer account history.
- Customer name is required; phone and address are OPTIONAL. Earlier assistant messages incorrectly changed the phone rule.
- Supplier name and phone are required for approved supplier records; other contact information is optional. Imported incomplete records stay in review until resolved.
- Supplier prices, purchase history and identity are admin-only. Personal withdrawals and charity are admin-only.
- Supplier payable balances are deferred. This does not remove the need to record cash actually paid to suppliers.
- Preserve sticker codes and their historical context. Keep English UI; do not reject valid names merely because they contain non-Latin characters.
- Print a customer document when requested. Internally record every committed sale whether printed or not. Legal/tax invoice requirements are a separate pre-release check, not established by this preference.
- No compulsory photos, storage-location accounting, dead-stock alerts, purchase orders, reservations or supplier invoice allocation in the first release.

Corrections:

- A supplier catalogue is not evidence of shop stock. Zero imported stock movements must display as **Not counted**, not **Out of stock**.
- The owner's 11% purchase TVA is a configurable starting setting from the conversation, not a verified legal conclusion. Inclusive prices already contain TVA: do not multiply them by 1.11 again.
- EUR 15 inclusive at USD 1.17 per EUR is USD 17.55 total purchase cost; EUR 15 exclusive at 11% becomes USD 19.4805 before final rounding. The earlier example double-counted TVA and miscalculated the result.
- Multiplying a cost by 1.25 gives a 25% markup, not a 25% gross margin. No automatic profit promise or mandatory markup is required here.
- Old or unknown costs are not zero costs. Physical shelf provenance cannot be reconstructed from software allocation alone.
- A simple encoded sticker is not a security control. It remains a family convention; decoded values stay private in the app.

## 2. Critique decisions

| Suggestion | Decision and reason |
| --- | --- |
| Flatten SKU identity and references | Adopt. Families are optional browsing labels, never identity or compatibility containers. |
| Reference uniqueness by type/brand/number | Use scoped reference records plus many-to-many SKU links. A reference can legitimately describe several variants; uniqueness must not prohibit those links. Supplier references also need private supplier scope. |
| Pairwise compatibility with evidence | Adopt, including conditions and direction. Never infer A fits C from A fits B and B fits C. |
| Logical stock bins | Adopt as stock conditions, hidden from normal sales: sellable and quarantined. A supplier-return case is a workflow; no physical warehouse/bin choice is required. |
| Lots identify every physical unit | Amend. Receipt lines preserve evidence; specific physical attribution is optional and labelled known/unknown. FIFO, if used for costing, is an accounting assumption. |
| Block all negative stock vs freely allow it | Owner prefers enforcing availability. Special customer procurement uses an explicit receipt-and-sale workflow, not unrestricted negative stock. Unknown stock is separate from zero. |
| Existing code lacks concurrency control | Incorrect for its present stock endpoint: it uses PostgreSQL row locks. Real PostgreSQL concurrency coverage and multi-line transaction design are missing. |
| Idempotent sale/payment commands | Adopt, enforced by a database uniqueness rule in the same transaction as all effects. |
| Separate admin/counter DTOs | Adopt, with private routes, search filtering, event filtering, cache isolation, and tests. DTOs alone do not cover every leak. |
| Minimum employee price | Defer for family pilot; selling prices remain flexible. Never expose a reversible cost-plus formula to employees. |
| Apply customer payments to invoices | Not mandatory for the simple account. Preserve every cause/document link, but show a running USD account without pretending individual invoices are paid. Allocation can be added later. |
| pg_trgm for partial search | Adopt provisionally and benchmark. It also supports suffix LIKE patterns. A reversed-string index is optional, not mandatory in addition. |
| Strip all non-numeric/non-alphanumeric characters | Reject blanket stripping. Keep raw values; normalize approved separators, preserve letters and leading zeros, and review collisions. |
| Always classify brands as better/cheaper | Reject automatic brand-quality claims. Show brand, product details and any owner-curated advice. Cost-based ordering is private. |
| Rewrite Next.js to Vite now | Defer. Existing pages work with Webpack; a tool crash does not establish a framework rewrite is beneficial. Review packaging at deployment. |
| Backups and audit only after rollout | Reject. They must be verified before storing real business transactions. Prototype development can proceed without a live deployment. |

## 3. Repository findings and limits of current evidence

Paths below are relative to the repository root. Line references describe the reviewed revision and may move during implementation.

| ID | Evidence | Implication / required work |
| --- | --- | --- |
| R01 | `InventStock.Domain/Entities/Part.cs`: one SupplierId and UnitPrice; `CatalogConfigurations.cs:69`: unique PartNumber | Cannot represent the required offers, dated prices, brands and overlapping references. Expand schema before importing real catalogue data. |
| R02 | `PartsController.cs`: authenticated GETs return PartDto; `PartRepository.cs:139-141`: supplier and cost fields | Current staff APIs violate the new privacy requirements. Split counter/admin projections. |
| R03 | `SuppliersController.cs:11-28`: ordinary authenticated GETs; `DashboardDtos.cs:16-24`: supplier counts, stock values and PartDto children | Restrict supplier routes; remove private aggregates and nested private fields from counter dashboard. A quantity and stock value can reveal unit cost. |
| R04 | `StockMovementService.cs:72-97`, `PartRepository.cs:114-124` | Transaction and FOR UPDATE exist. Preserve protection, lock all affected SKUs in deterministic order for multi-line operations, and test with separate PostgreSQL connections. |
| R05 | `ApiFactory.cs` uses SQLite; `PostgresModelTests.cs` explicitly opens no server connection | Previous 59 passing tests do not prove PostgreSQL locks, rollback concurrency or search indexes. Retain useful unit tests and add real-server integration tests. |
| R06 | Stock request DTOs/service and schema have no operation/idempotency record | Repeated successful POSTs can duplicate effects. Add operation IDs and request fingerprints before transactional rollout. |
| R07 | `UnitOfWork.cs:21-24,64-70` joins an outer transaction using no-op commit/rollback; `StockMovementService.cs:115` then notifies | A future sale service wrapping stock calls could broadcast before its outer commit, even if that sale later rolls back. Move transaction ownership up; stage notifications for the outer commit. This is a composition hazard, not proof the standalone endpoint already broadcasts before commit. |
| R08 | `providers.tsx` keeps one QueryClient; `auth.tsx` changes users without clearing/cancelling queries | Cross-login cached data can persist. Replace the session query scope, cancel/discard old responses, and reset subscriptions/tokens on logout and switching. Also clear local auth state if logout's HTTP request fails. |
| R09 | Current ledger is append-only by service convention; `AppDbContext.cs` permits tracked updates; movement DB config mainly checks nonzero quantity | Add database/runtime-role protections for committed ledger rows, explicit reversal links and constraints. Created/updated stamps are not a full event audit. |
| R10 | Search uses lower(...).Contains; absent aliases/search-mode tables | Current suffix syntax is not implemented. Add normalized references, explicit modes, relevance ordering and performance tests. |
| R11 | No Sale, CustomerAccount, CashEntry, ReceiptLot or quality-case domain exists | These are new capabilities, not small UI fixes to the existing stock dialog. |
| R12 | `StartupExtensions.cs`: migrations and seeding may run at startup; development config enables them | Production releases need reviewed migrations, pre-migration backups and no demo seed. Do not test against a configured shop database by starting development mode. |

Earlier session checks: 59 backend tests passed; frontend lint and a Webpack production build passed. Default Turbopack failed in this environment. These checks were not rerun for this documentation-only review, and do not establish live workflow correctness. No production database or browser workflow was exercised during this review.

## 4. Catalogue identity and search

Proposed records (descriptive names, not final generated migrations):

| Record | Responsibility |
| --- | --- |
| Sku | Internal immutable ID; brand, manufacturer number, description, unit/pack definition, attributes, active status. |
| Reference | Raw value, normalized search value, number system, issuing brand where known, visibility, optional private supplier scope. |
| SkuReference | Many-to-many link with source, confirmation state and optional fitment/variant restriction. |
| SkuRelationship | From/to SKU, relationship type, applicability/restrictions, evidence, verifier/date, status and revision history. |
| Application / SkuApplication | Optional structured vehicle/engine/position/year facts and free-text limitations; source retained. |
| SupplierOffer | Supplier-to-SKU link, private supplier code, package/unit and notes. No single supplier field on the SKU. |
| CostObservation | Dated supplier quote, PDF list price, actual receipt cost, or legacy sticker observation, with source and tax/currency basis. |

Supersession is directional. A verified symmetric alternative is represented without implying a globally interchangeable group; conditions still apply. A manufacturer-number supersession claim can reference two reference records even before all aftermarket SKU mappings are verified. Never fabricate missing SKU equivalences. Withdrawn evidence remains in history.

Multiple references matching the same SKU yield one search result with multiple match explanations. A number shared by several SKUs returns the candidates, not the first arbitrary row. Similarity in digits, shape or vehicle family alone never verifies substitution.

Search defaults:

1. Preserve raw input and leading zeros; uppercase recognized Latin number strings. Do not silently remove OE letter prefixes such as A. Keep an approved spacing/hyphen-normalized search form without changing displayed identity.
2. Apply identical, versioned normalization to stored references and queries. Separator-insensitive matches are search candidates, not merge instructions. Ambiguous normalization collisions remain visible.
3. Rank exact (within known scope first), prefix, suffix, then other contained matches, with stable tie-breaking. Respect explicit `~/12582` suffix mode and provide a visible Ends with option.
4. Resolve staff visibility before matching, ranking or counting. A private supplier code must not act as an information oracle in public results.
5. One/two-character queries use bounded exact/prefix search and ask for more characters for broad contains search. No fuzzy automatic replacement of a part number.
6. Parameterize queries; treat `%`/`_` literally unless a documented mode intentionally supports patterns. Limit results and load detailed offers/history only on selection.
7. Exact/suitable prefix B-tree indexes plus pg_trgm GIN for substring/suffix candidates. Add reverse-prefix index only if measured suffix workloads justify it. Benchmark generated SQL and EXPLAIN plans on the actual chosen PostgreSQL version/collation.
8. Seed a disposable benchmark with 20,000 SKUs, realistic multiple references and substantial movement history. Measure concurrent search and sales on shop-class hardware. Initial target: normal first-page searches under 300 ms at the API at p95; this is a test target, not a measured claim.

Avoid N+1 queries: one bounded list projection with stock summary, separate detail query for selected SKU. Avoid joining references directly into a stock SUM that multiplies movement rows. Database capacity is ample for the proposed scale, but relevance, query shape and hardware still require measurement.

## 5. Stock condition, provenance and counts

Inventory entries carry SKU, quantity, condition, actor, operation/document link, recorded timestamp, business occurrence time and optional receipt/quality-case provenance. Internal conditions initially: Sellable and Quarantined. Physical on hand sums held conditions; available sums Sellable. No physical storage-unit dimension is required.

A quarantine operation posts -3 Sellable and +3 Quarantined atomically, connected to a QualityCase. A later supplier return removes Quarantined stock only when goods actually leave. Marking a return as planned must not prematurely reduce physical stock. Release requires an authorized decision and a reason. Customer returns awaiting inspection default to Quarantined.

Receipt lines preserve supplier, time, quantity, currency and cost evidence. Add identifiable lot/batch labels when actually available. For mixed shelves, provenance can be Known, Assumed or Unknown; a cost-allocation assumption is separate from the asserted physical source. Never generate a supplier claim merely because FIFO assigned an old receipt to a sale. A quality case can link to a known receipt or list uncertain sources for admin review.

Return quantities cannot exceed the original sold quantity minus prior committed returns; lock the relevant sale line so simultaneous returns also respect the limit. No-receipt exceptions need admin review and an explicit legacy/unknown source. A documented historic sale may legitimately be returned before it exists in the new system; do not invent a linked new-system sale.

Stock knowledge is separate from movement arithmetic:

- Not counted: no reliable baseline. Display unknown total even if movements since onboarding exist; do not show those deltas as the complete stock.
- Counted: timestamp and baseline plus subsequent movements give the book quantity, including a meaningful zero.
- Needs recount: discrepancy/exception has been recorded; preserve the book movements and expose uncertainty.

A count uses a short per-SKU operational pause and a version checkpoint. No long database lock while somebody walks around counting. If movements occur after the checkpoint, require reconciliation/recount before posting the baseline correction. Count changes are adjustments with actor/reason, never edits to old movements.

Counted-stock default: no oversell. An unfinished cart does not reserve stock; server validation at completion decides. Owner prefers unavailable items blocked but sometimes procures directly for a customer. Provide an explicit received-for-customer operation before/with selling; never manufacture a receipt merely to bypass stock. Unknown quantities remain unknown until counted. Any future discrepancy override needs a deliberate policy, reason and atomic operation. Quarantine cannot be bypassed. Quick-add can record a clearly provisional SKU, not pretend it is verified.

Owner confirmed sealed 1L/4L/5L oil containers, individually sold spark plugs from boxes of ten, and hoses sold by length. Model bottles as distinct packaged SKUs, plugs as individual pieces (ten received from a box), and cut hose in metres with validated precision/increments. Proposed numeric(18,3) quantities require whole numbers for discrete SKUs; fractions are permitted only for measured SKUs. Current prototype integer movements do not yet support cut hose. A boxed kit does not secretly decrement components.

## 6. Atomic operations, retries and audit

Keep a modular .NET application and one PostgreSQL database. Catalogue, inventory, sales, customer accounts and cash are internal modules. Do not introduce microservices or separate databases for this shop.

Every sale/receipt/payment/refund/exchange/correction command has:

- A stable client-generated operation ID, preserved when retrying; a hash of the canonical request; authenticated actor and timestamps.
- A database unique constraint on operation scope and ID. Same ID and same payload returns the committed result after authorization. Same ID and different payload is a conflict. Do not store a privileged serialized response that can later be replayed to a less-privileged user.
- One outer database transaction covering documents, all stock legs, all customer-account legs, all cash legs, audit events and the operation outcome.
- Deterministic lock order across resource types and sorted IDs. Lock SKUs, return source lines and relevant accounts when enforcing their invariants. Keep transactions short; handle deadlocks with bounded whole-operation retries using the same operation ID.
- Database-generated internal document numbers; never MAX()+1. Gaps after aborted transactions are permissible for internal references. Statutory numbering, if required, is a separate rule to verify.
- After-commit best-effort notifications; clients tolerate duplicate/missing refresh hints and refetch on reconnect, focus and periodic refresh. No durable outbox for these hints in the pilot. Financial/external side effects requiring guaranteed delivery will need a durable mechanism later.

Posting services must not invoke independently committing stock or money services. Refactor the current nested transaction behavior before composing it into sales. Rollback of the outer operation must leave no stock, debt, cash or notification effect.

One key prevents a retried request, not two separately created sales with different keys. Shared carts use a server draft ID and single completion constraint. Independent carts require human review if suspected duplicates; identical amounts/items are not proof of duplication.

Documents transition Draft -> Posted -> reversed/corrected by linked documents. Posted line descriptions, brand, quantity, prices and customer display details are snapshots, so future catalogue edits do not rewrite a receipt. Editing a quote has no ledger effects. Converting one quote/draft twice is prevented by a constraint. Printing/reprinting uses the existing posted document; printing failure never posts another sale.

Committed ledger/audit rows are immutable to the runtime DB role. Use schema constraints/triggers/privileges plus application guards, with a separate migration role. This protects against application mistakes; a database administrator can still alter data, so do not claim cryptographic tamper-proofing.

## 7. Money and customer accounts

Use exact decimal arithmetic (C# decimal and PostgreSQL numeric); never binary floating-point for authoritative money calculations. Transmit amounts/rates as decimal strings to the browser, validate precision/range server-side, and calculate final totals on the server. UI previews must use the same documented rounding policy.

Proposed precisions: cost/unit price numeric(20,6), exchange rates numeric(20,10), posted USD/EUR settlements at two decimal places and physical LBP tenders at whole-pound increments for the initial cash UI. Stored original cost precision is retained. Postgres numeric precision and C# decimal range must both be validated. Explicit rounding defaults to nearest with ties away from zero; confirm expected supplier/receipt rounding before launch. Any difference between accepted cash and settlement is a visible rounding/adjustment entry, not an unrecorded loss.

Label input rates in everyday terms:

- 1 USD = 90,000 LBP: USD settlement = LBP / 90,000.
- 1 EUR = 1.17 USD: USD settlement = EUR * 1.17.

Store the entered direction/value and effective conversion used. Positive rates only. Historical conversions are immutable; changing the current safe-valuation rate never changes past sales or debts. Keep original currency, amount, tax basis and source even when a USD comparison is shown.

Cost observations distinguish tax-exclusive, tax-inclusive, exempt/not charged, and unknown. Use dated rate snapshots. Add TVA only to a confirmed exclusive cost. Inclusive cost can be divided by 1 + rate to derive its net component when the rate is known. Do not assume input tax recovery or calculate statutory profit from these observations. Optional discounts/freight remain explicit; do not call a list price a fully landed cost.

USD customer account entries are append-only causes: verified opening balance, sale charge, payment credit, return credit, payout/reversal and correction. Balance is derived, not a manually overwritten total. A sale can have several tenders and an unpaid remainder; cash/debt are outcomes, not incompatible document types.

For named customers, post full sale charge and payment credit together, leaving the unpaid remainder. For anonymous fully paid sales, retain tenders on the sale without creating a fake customer account. A remaining debt needs a named customer. Overpayments are returned as explicit change or, with agreement, kept as customer credit; never silently discarded or capped. Existing customer credit can fund a new sale without creating a second cash entry.

Returns create credit at the original sold price for the accepted returned quantity. An exchange links that return to a new sale at its explicitly entered price, settling any difference. It is not a free-form stock adjustment. Refund disposition is explicit: reduce debt, retain customer credit, cash payout, or a combination. Do not both reduce debt and pay out the same refund twice.

Refund currency and rate are selected and recorded independently of the original tender. Display original tender(s) and historical rate(s) for context. A returned USD 10 sale paid with 900,000 LBP does not authorize the app to choose a new 100,000 LBP/USD rate or decide which party bears the difference. Employees may process routine linked returns/refunds without an admin PIN. Never automatically suggest refunding more credit than the linked return creates without a separately approved adjustment.

Partial refunds use cumulative allocation against the original posted rounded line total. Three units at USD 0.335 posted as USD 1.01 must refund exactly USD 1.01 in total, never three independent USD 0.34 refunds. Test successive partial returns and simultaneous requests.

Supplier payables remain deferred. A received order may be unpaid and has no immediate cash effect. Paying USD 500 to a supplier records USD 500 cash-out, even if the app does not calculate what remains owed. Supplier payments, stock receipt and cost observation are related but different events.

Any cost snapshot attached to a sale for later analysis must say whether it is verified, estimated from a receipt allocation, legacy, or unknown. Correcting a costing assumption adds a revision, not an edit to the historical sale. Do not present latest supplier list price as the actual cost of old stock.

## 8. Cash and daily reconciliation

First model: one pooled cash account per currency representing the shop's available physical cash. Counting must cover the same physical scope consistently. There is no employee-level ownership inferred merely from who entered a payment. Optional later safe/till transfers must have paired entries.

Cash events include opening amount, customer tender, change given, debt payment, refund, supplier payment, expense, personal withdrawal, charity, currency exchange, correction and counted discrepancy. Owner confirmed cash and Whish digital only. Whish has a separate non-cash account and must never increase the physical safe. No OMT, cheque or bank UI is needed.

Example: USD 10 sale, receive LBP 1,000,000 at 90,000 LBP/USD, give LBP 100,000 change. Physical cash entries: +1,000,000 and -100,000 LBP; USD sale settled: 10. Mixed-currency change has its own explicit conversion and settlement values.

Opening cash is counted, not assumed zero. Show native balances first. The estimated USD total is labelled with valuation rates and their timestamp; it is not exchangeable cash guaranteed by the app. Prevent unexplained negative physical cash; recording a legitimate discrepancy requires an authorized correction.

Reconciliation stores expected amount, actual count, checkpoint and difference per currency. Any later entry after the count checkpoint is handled explicitly; do not overwrite the past close. Store timestamps in UTC/timestamptz, and derive business-day boundaries in Asia/Beirut, including timezone changes. Distinguish occurred-at from recorded-at for paper recovery. Late entries retain the original operator and a reason; closed periods get linked corrections.

Expected liquidity remains conditional on recording all cash movements. Software cannot infer an unrecorded family withdrawal or sale. The UI must distinguish expected cash and last physically counted cash.

## 9. Permissions and counter UX

Use dedicated counter response types and private admin routes/projections. Private purchase tables are never joined into counter responses. Role restrictions cover search, sorts, filters, result counts, dashboard values, receipt renderers, import documents, errors, history and SignalR. Tests check forbidden values and nested fields, not only a top-level property name.

Public catalogue references can be many-to-many; private supplier codes are visible only to admins. A staff search by a private reference must neither return its match nor change result ordering/counts. Supplier-specific notes must not be copied into public stock reasons or receipts.

| Action | Family admin | Employee default |
| --- | --- | --- |
| Search public catalogue, see stock and verified application notes | Yes | Yes |
| Enter normal sale price, debt sale, customer payment; see accounts | Yes | Yes; flexible family prices |
| Print an existing customer-safe document | Yes | Yes |
| Receive purchases with supplier and cost information | Yes | No; may submit a separate blind quantity observation |
| Flag suspected defective stock / open quality report | Yes | Yes; separate permission from refunding or releasing |
| Post stock adjustment, confirm opening count, release quarantine | Yes | No |
| Routine linked return/refund/exchange | Yes | Yes; no admin PIN interruption |
| Exceptional/unlinked financial reversal | Yes | Explicit exception policy required before release |
| Supplier records, costs, import review, private cash movements | Yes | No |
| Edit permissions, opening debt/cash, migrate data | Yes | No |

Use an admin counter-view toggle that defaults to hiding private fields even for the owner, because a customer can look at an admin screen. Revealing costs is a deliberate action, hidden again after inactivity or switching users. This visual protection supplements server authorization and does not make a physical sticker secret.

Individual sessions, visible current user, and fast lock/user-switch flow help avoid shared identities. A PIN unlock, if added, must be rate-limited and scoped to an already enrolled device/session, not a replacement for unrestricted remote authentication. Clear old query caches, pending auth work and subscriptions on switching/logout; private responses use no-store and no persistent offline cache. Test account disable/revocation and authenticated hub reconnection behavior.

Counter screen: focused search; number, brand, variant/pack and stock confidence together; highlighted matched reference; copy-number action; customer-safe prices only. Admin cost panel clearly separates last actual purchase from newest supplier quote, showing date/currency/TVA/source. One selected SKU detail loads richer history.

Sale completion has an editable cart, explicit USD line prices, tenders, change and unpaid balance. Quick one-item sale is possible but still performs the same atomic server command. Busy/retry state prevents accidental resubmission; ambiguous network outcomes show Check transaction status, not Make a new sale. Return UI asks quantity, reason, condition and disposition together.

## 10. Import, migration and rollout

Begin with one supplier PDF and a small sample when provided. No Excel import is required. Keep original private document, fingerprint, supplier/date, page and row source, raw extracted strings and reviewed mappings. Do not claim extraction accuracy until real samples have been tested.

Staging handles scans/OCR, comma/decimal separators, pack quantities, missing currency/TVA, dates, repeated rows, bad numbers and possible duplicates. Missing information stays unknown. Never infer actual purchase, compatibility, stock quantity or tax status solely from a catalogue list.

Admin reviews proposed creates/links/cost observations, conflicts and rejected rows before commitment. The document hash plus supplier/import scope and row provenance protects reimports; revised catalogues need explicit revision handling. Import stock/cash movement counts must remain zero. Deduplicating a reference does not automatically merge SKUs or different brand variants.

Backfill existing application data conservatively: retain stable identifiers and history; brand can be Unknown; the old UnitPrice becomes a legacy cost observation of uncertain origin rather than a fabricated dated receipt. Existing quantities are opening book data needing validation. Use additive migrations and reconciliation queries before retiring old columns. Never drop and reseed the shop database.

Proposed delivery sequence:

1. **Foundation and small catalogue pilot:** fix privacy/session isolation; new SKU/reference/cost model; unknown-stock state; staged one-supplier import; search benchmark; audit and tested backup/restore before real records become relied upon. Family can search and count alongside paper.
2. **Complete transaction pilot:** receiving, sellable/quarantined stock, sales/tenders/customer accounts, returns/exchanges/refunds, cash events/counting, idempotency and PostgreSQL race tests. Pilot perhaps 30-100 representative real SKUs and two concurrent users after pending rules are settled. Include a hose issue, mixed currencies, debt and a partial refund.
3. **Broader shop adoption:** gradual catalogue/count onboarding, opening customer balances reviewed, more import layouts, reorder list, receipts, operating/recovery guide and family training. Do not count supplier-catalogue-only entries as empty shelves needing reorders. Track Stocked / Reference-only / Special-order intent per SKU for reorder eligibility.
4. **Later:** supplier payables, warranty, selected public product exports, remote/mobile access and accounting integration when daily records are dependable.

Local deployment uses the existing .NET API/PostgreSQL/Next frontend. Prefer one browser origin for UI/API via deployment routing; PostgreSQL listens only where the application needs it, not to guest devices. Use a reviewed supported runtime/dependency baseline at release, HTTPS configuration, service auto-start and an installer/update routine. Removing runtime Google-font fetching is a deployment consideration; the shop UI must load without internet. A static frontend export or Vite migration is evaluated only if it materially simplifies packaging and passes existing route/auth tests.

Backup proposal: hourly local snapshots while trading and a daily encrypted copy on a separate device/off-site destination, queued retries when offline, visible last-success time and retention limits. This implies up to one hour of transaction loss unless augmented by more frequent/WAL-based recovery. It is a proposed target, not an achieved guarantee. Copying snapshots only to the same disk is insufficient. Verify restore to a separate database/machine, restore attachment/source documents consistently, protect keys separately, and document who can recover when the developer is absent. Take a backup before every live migration.

A host power failure stops writes. Do not promise offline multi-device sales synchronization in version one. Use numbered paper fallback entries and a controlled recovery screen with original occurrence time, actor and duplicate review. Test restoring and reconciling transactions recorded after the backup; retained client operation IDs/document references help identify overlap. Service restart/UPS and maintenance timing belong to deployment, not a claim that Windows will never reboot.

## 11. Required failure tests before transactional use

Run provider-specific tests on an isolated real PostgreSQL database, using Testcontainers if available or an explicitly configured disposable instance. Never silently substitute SQLite for this suite or connect to the shop database.

| Case | Required outcome |
| --- | --- |
| Two connections sell the last counted unit | One normal sale commits; the other receives a conflict. No unauthorized negative balance. |
| Multi-line sale has one unavailable line | No lines, stock, customer-account or cash effects commit. |
| Double-click, concurrent duplicate, retry after response loss | Exactly one operation and one set of effects; original authorized result is recoverable. |
| Reuse idempotency ID with modified payload | Conflict; no second effect. |
| Return the same sold unit concurrently | At most the remaining returnable quantity commits. |
| Database failure between stock and tender creation | Entire business transaction rolls back. |
| Outer rollback after stock staging | No published change; notification happens only after outer commit. |
| Broker/hub unavailable or app dies after commit | Committed sale persists; refresh catches up; replay does not duplicate sale. |
| Partial debt sale, partial payment, credit return, payout | Customer account and native cash agree without double credit. |
| LBP tender with LBP/USD change, EUR purchase, later rate change | Original amounts/rates unchanged; rounded settlement and native cash reconcile. |
| Inclusive/exclusive/unknown TVA | No double addition, fabricated tax or silent loss of uncertainty. |
| Quarantine 3 of 10, then sell 7 | 3 physical quarantined remain; selling an eighth normally fails. |
| Untraceable returned item | Provenance remains unknown; no fabricated supplier liability. |
| Count while another user sells | Checkpoint mismatch requires reconciliation, not silent overwrite. |
| Same PDF imported again / normalized references collide | No duplicate transaction/stock; collision reviewed; variants remain distinct. |
| Employee routes, search by private reference, events, exports, errors | No purchase/supplier information or inference via restricted filters. |
| Admin logout then employee login, including in-flight responses | No stale private data or late old-session refresh survives. |
| Document printing fails | Sale remains posted exactly once and can be reprinted. |
| Direct runtime-role ledger UPDATE/DELETE | Rejected; correction requires linked entries. |
| Beirut business-day boundary and recovered paper entry | Correct reporting day; recorded-at history preserved. |
| Restore from encrypted backup onto clean instance | Login, documents, ledgers, source attachments and reconciliation verified within agreed recovery target. |

Run existing unit/HTTP suites as regressions; add browser scenarios for actual counter/return flows and access switching. Benchmark search with multiple references per SKU to catch duplicated sums and heavy projections. No test suite justifies claiming all risks are eliminated.

## 12. Confirmed follow-up decisions and remaining release gates

Owner answered the consolidated questions on 10 October 2026:

1. Availability normally enforced; customer-specific procurement supported explicitly, not blanket force-sell.
2. Keep family selling prices flexible; no compulsory minimum-price feature in this pilot.
3. Employees may perform routine linked returns and refunds without admin PIN interruptions.
4. Oil containers are whole packaged units; spark plugs individual pieces even when purchased in boxes; some hose is sold by metre.
5. Cash and Whish digital; keep Whish outside the safe balance. Customer phone remains optional.

Additional safeguards before live transactions:

- Defect holds immediately remove identified stock from availability, with recorded confirmation of physical separation/tagging. Staff can hold; only authorized release restores sellability. Software cannot distinguish mixed faulty/good hoses on the shelf.
- Customers have immutable IDs and optional distinguishing labels (garage, area). Warn about possible duplicates, show identity context before payments, never auto-merge by name/phone, and correct wrong-account payments through linked reversals/reassignment.
- Preserve actual tender, explicitly agreed USD credit and rate/rounding adjustment. If 850,000 LBP is accepted to close USD 10, confirm that settlement rather than silently leaving debt or rewriting the default rate.
- Recovery loss tolerance still requires owner agreement before go-live. An hourly or 15-minute dump can lose every committed unprinted sale since that dump. Test recovery of this failure window, not only paper sales after an outage. Lower loss requires an independently stored continuous recovery solution; no backup frequency is a zero-loss promise.
- No live migration, supplier PDF import or real money processing has been performed in this implementation slice.

## 13. Technical sources checked

Implementation checkpoint: 92 backend tests passed including real PostgreSQL migrations, a concurrent
last-unit stock-out race, API privacy and reference authorization tests. Five client session tests,
TypeScript, ESLint and a Webpack production build passed. No browser visual inspection was performed
for the new reference form. Benchmark data: 20,000 SKUs, 20,000 references and 100,000 movements;
warm search p95 approximately 12–27 ms, quantity sort approximately 108 ms on this development host.
These measurements exclude HTTP/browser latency. Correlated reference searches initially measured
240–320 ms and were replaced with indexed reference-ID set queries. Count plus page stays two queries.

Still pending implementation: unknown-stock/counting state, supplier offers and cost history,
measured quantities, imports, quarantine, sales/idempotency, cash/Whish/customer ledgers and tested
backup recovery. Current `UnitPrice`/single supplier remain legacy admin-only catalogue fields.
Generated migrations have not been applied to the shop database. Runtime Npgsql automatic retries
were disabled because the existing services own explicit transactions and lack replay idempotency.

Later checkpoint (11 October 2026): reference identity is distinct from broad search normalization;
product/reference links can be deactivated and restored with required reasons, actors, timestamps and
optimistic revisions. Raw stock-out requires a reason and remains explicitly labelled as inventory-only.
A populated previous-schema migration test preserves product/movement IDs, quantities and links without
inventing old audit events. Explicit logout now persists a local restoration block before attempting the
network call and signals other tabs; a successful explicit login is the only action that clears it.
Admins start in customer-safe mode: private catalogue routes/caches are only used after deliberate reveal,
and are cleared on hide, inactivity, backgrounding or session change. The latest isolated run passed 100
backend tests and seven client request/session tests, plus TypeScript, ESLint and a production build.
Actual multi-tab browser automation is still pending.

Controlled-brand checkpoint (11 October 2026): products now retain their historical uppercase brand
snapshot while also linking to a canonical brand ID. Brand names normalize case and repeated whitespace;
known alternate spellings are unique aliases. The management screen warns on compact/expanded duplicate
candidates and part entry uses a brand selector instead of free text. The migration promotes existing
non-empty brands and preserves part/movement IDs. A populated-schema upgrade test verifies this path.
Brand reads are available to counter users; create, alias and activation changes are admin-only. The
activation endpoints for parts, suppliers and users were also aligned with the frontend's JSON PATCH
contract. The isolated validation run passed 105 backend tests, seven client tests, TypeScript, ESLint
and a Webpack production build. The disposable PostgreSQL container was removed afterward.

Stock idempotency checkpoint (11 October 2026): stock-in, raw stock-out and adjustment commands now
require a client-generated operation ID. Each committed movement stores that ID plus a SHA-256 payload
fingerprint. The server takes a transaction-scoped PostgreSQL advisory lock for that user/operation,
then the existing part-row lock. An exact retry returns the original movement without another stock
effect or duplicate live notification; reusing the ID with changed details returns a conflict. The stock
dialog keeps its ID stable across an uncertain response. A two-connection PostgreSQL test proves two
concurrent duplicate submissions produce one movement and the same result. Legacy movement rows remain
valid with null operation IDs. The complete isolated suite now passes 108 backend tests; the seven client
tests, TypeScript, ESLint and Webpack production build also pass.

The configured normal database connection appears remote (likely an old Supabase project). An authorized
read-only connection attempt failed before inspection, so its migrations and data remain unknown. It was
not migrated, seeded or modified. Local PostgreSQL is the intended shop primary, but changing connection
configuration and migrating real data require a separate backup/recovery deployment step.

- PostgreSQL pg_trgm: supports indexed LIKE/ILIKE without a left-anchored pattern; patterns without extractable trigrams may require a full-index scan. This supports testing GIN for both contains and suffix searches, rather than assuming every suffix search needs another index: https://www.postgresql.org/docs/current/pgtrgm.html
- PostgreSQL explicit locking: FOR UPDATE protection lasts through the transaction; locking order and deadlock handling matter when operations span several resources: https://www.postgresql.org/docs/current/explicit-locking.html
- PostgreSQL numeric types: exact numeric storage is appropriate for monetary values, with deliberately chosen precision and rounding: https://www.postgresql.org/docs/current/datatype-numeric.html
- PostgreSQL backup and restore: logical dumps, filesystem backups and continuous archiving have different recovery properties. The final backup choice must meet the measured recovery target: https://www.postgresql.org/docs/current/backup.html

These sources support technical choices. They do not establish Lebanese tax/invoice obligations, the physical identity of mixed stock, or the owner's preferred business policies.
