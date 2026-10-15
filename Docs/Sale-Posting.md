# Counter-sale backend posting contract

This is the sale-posting contract, not an operational sales release. A counter UI and linked returns/refunds exist in code; paper-era returns, exchanges, receipts, reconciliation, backup and deployment remain unfinished.

## Amounts and rounding

- The backend posts USD prices per stock base unit (piece or centimeter), at most six decimals, with positive integer quantities. For cut hose the counter enters metres and USD/metre, then converts to whole centimeters and USD/centimeter before posting.
- Each posted line total is rounded once to USD cents, midpoint away from zero. Sale total is the sum of those posted line totals and must equal the caller's confirmed total. For a linked return, compute cumulative refundable cents from the original posted line total and cumulative returned base units, then subtract earlier refunds. For example, a $1.01 line of three pieces refunds $0.34 + $0.33 + $0.34, never $1.02. `ReturnAllocation` implements this rule, and linked-return posting enforces it under original-line database locks.
- `total = received USD settlements - change USD settlements + customer credit used + unpaid debt`.
- Every payment/change explicitly names an active native-currency account, native amount and confirmed two-decimal USD settlement. Native amounts retain up to six decimals, matching the existing cash foundation. USD tender must equal its USD settlement. No current exchange rate is consulted or inferred as authoritative.
- Change is separate from receipt of tender, including when paid from a different currency/account. No implicit change, account credit creation or overpayment is accepted by this operation. Net cash/wallet settlement cannot be negative.
- Anonymous sales require zero customer credit and zero debt. Named sales post a positive full sale charge and a negative net tender settlement to the customer's USD account. Existing negative balances are credit; sale posting consumes requested credit once, after locking that customer. Outstanding debt is stored explicitly on the sale.
- Unknown cost is nullable. A latest actual-purchase observation may be snapshotted privately in its native currency with its tax treatment. It is an estimate, not COGS or a guaranteed USD profit. Quotes and legacy part prices are not substituted.

## Stock and transaction boundaries

- Transaction owner is `SaleService`. Order: operation lock, customer lock (if any), distinct SKU locks in ascending Guid order. Repeated SKU lines are summed before stock validation; each keeps its own price/description snapshot and stock movement.
- Recorded available stock is physical ledger sum minus quarantined ledger sum. Holds are never released by sales. All raw stock/count/hold and receiving writers share the part lock convention.
- Uncounted stock requires physical confirmation plus reason on every affected line, even when book stock is sufficient; its confidence remains unknown unless a shortage is posted.
- If recorded sellable stock is short, every line for that SKU requires physical confirmation and a reason. The sale records the negative book balance without inventing a stock-in or releasing a hold. It marks the part `NeedsRecount`, and another shortage claim for that SKU is blocked until a physical count is recorded under the same part lock. A count checks its expected book balance, adjusts the ledger, and clears the warning in one transaction.
- A SKU with quarantined units cannot use the shortage exception; count or receive verified separate goods before selling. Goods acquired for a customer must first be physically received; a supplier promise is not receipt. Physical confirmation means sellable goods actually in hand, not an order or a promise.
- Header, lines, stock, tenders, native cash movements, customer entries and operation outcome commit in one database transaction. Append-only guards and database constraints protect posted records. Notifications are public refresh hints sent only after commit; failures cannot invalidate the posted sale.
- Operation ID is permanent. The shared operation guard serializes all current stock/hold/receiving/cash/sale commands. Same actor and canonical payload replay the saved immutable result. Changed payload or actor conflicts. Status returns a committed result only to its original actor. Current active-user status and Admin/Staff membership are checked in the database for posting, replay, recovery and payment-account lookup.
- A failed/missing HTTP response must be resolved through `GET /api/sales/operations/{operationId}` and, if retrying, the same ID and exact payload. No automatic request replay.

## Scope remaining

Customer and counter-sale UI were smoke-tested on a disposable database before the later opening-correction, linked-return and suggested-pricing screens. Those later browser flows are not verified. Older-sale lookup, paper-era returns, receipt numbering/printing and reconciliation remain unfinished. No normal database migration is authorized.
