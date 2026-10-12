# InventStock

> **Shop-development plan:** [Reviewed production design](Docs/Production-Design.md) records the
> owner's workflow, the code-risk review, pending business rules, and the implementation gates.
> It describes planned capabilities; the current application below is still the inventory baseline.

Inventory and parts management for an automotive parts business: a normalized catalogue of parts,
categories and suppliers, an append-only stock ledger with a full audit trail, role-based access
control, and live stock updates pushed to connected clients.

**Stack:** ASP.NET Core Web API (.NET 8) · Next.js 16 (App Router, TypeScript) · PostgreSQL · SignalR · JWT

---

## How stock is modelled

Current stock is **not** stored on the part. Every change writes a row to `StockMovements` with a
**signed** `QuantityChange`, and on-hand stock is `SUM(QuantityChange)` for that part.

| Movement | Sign | Example |
|---|---|---|
| `In` | positive | delivery of 20 units → `+20` |
| `Out` | negative | 5 units issued to a job → `-5` |
| `Adjustment` | either | 2 units damaged → `-2` (reason required) |

Three consequences worth knowing:

- **One aggregate gives the answer.** No separate quantity column to drift out of sync with history.
- **The ledger is append-only.** Mistakes are corrected with a compensating `Adjustment`, never by
  editing or deleting a row, so the audit trail is always complete.
- **Every row names a user and a time**, which is what makes the history useful rather than decorative.

Correctness under concurrency is handled in `StockMovementService`: the part row is locked
(`SELECT … FOR UPDATE`) inside a transaction before stock is read, so two simultaneous stock-outs
cannot both pass the "is there enough?" check. The SignalR broadcast happens **after** commit, so
clients never see stock that was rolled back.

## Architecture

```
backend/InventStock/
├── InventStock.Domain/          entities, enums, invariants — no framework dependencies
├── InventStock.Application/     DTOs, service interfaces, business rules, validators
├── InventStock.Infrastructure/  EF Core, PostgreSQL, Identity, JWT, repositories
├── InventStock.Api/             controllers, SignalR hub, middleware, composition root
└── InventStock.Tests/           domain, service, repository and HTTP-level tests
frontend/                        Next.js App Router client
```

Dependencies point inward only: `Api → Infrastructure → Application → Domain`. `Domain` references
nothing, which is what keeps the business rules testable without a database or a web server.

## Roles

| Capability | Staff | Admin |
|---|:--:|:--:|
| View catalogue, stock and history | ✅ | ✅ |
| Record stock in / out | ✅ | ✅ |
| Post stock **adjustments** | ❌ | ✅ |
| Create and edit parts, categories, suppliers | ❌ | ✅ |
| Manage users | ❌ | ✅ |

Adjustments are the only movement that can write stock off without a corresponding issue, so they are
restricted to administrators.

## Security

- **Access tokens** are short-lived JWTs held in memory by the browser — never in `localStorage`.
- **Refresh tokens** are delivered as `HttpOnly; Secure; SameSite=None` cookies scoped to `/api/auth`,
  so page scripts cannot read them. Only a SHA-256 hash is stored server-side, and each use rotates
  the token. Presenting an already-revoked token is treated as theft and revokes the whole family.
- **Passwords** require 12+ characters with mixed case, a digit and a symbol; accounts lock for
  15 minutes after 5 failed attempts. Login returns the same message for a wrong password, a missing
  account and a disabled account, so the endpoint cannot be used to enumerate users.
- **SignalR** authorises per hub method, not just per connection — a connection-level `[Authorize]`
  alone would let any signed-in member of staff call administrator-only methods.
- **Rate limiting** applies globally and more tightly to the auth endpoints.
- Errors are returned as RFC 7807 problem details; internal exception text is never sent to clients
  outside development.
- The API refuses to start in Production with a missing, short or development JWT signing key.

## Running it locally

### Prerequisites

.NET 8 SDK · Node.js 20+ · a PostgreSQL database

### 1. Backend configuration

Secrets are kept out of the repository. Set them with user secrets:

```bash
cd backend/InventStock/InventStock.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<host>;Port=5432;Database=postgres;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets set "Jwt:SigningKey" "<a random string of at least 32 characters>"
dotnet user-secrets set "Seed:AdminEmail" "admin@yourdomain.com"
dotnet user-secrets set "Seed:AdminPassword" "<a password with 12+ chars, mixed case, digit, symbol>"
```

Environment variables work too, prefixed with `INVENTSTOCK_` (for example
`INVENTSTOCK_Jwt__SigningKey`).

In Development the API applies migrations and seeds roles, the administrator and a small demo
catalogue on startup (`Database:MigrateOnStartup` and `Seed:IncludeDemoData` in
`appsettings.Development.json`). Both default to off everywhere else.

### 2. Run the API

```bash
cd backend/InventStock
dotnet run --project InventStock.Api --launch-profile https
```

The API listens on `https://localhost:7054`. Swagger is served at `/swagger`, and `/health` reports
database connectivity.

### 3. Run the frontend

```bash
cd frontend
cp .env.example .env.local   # already points at https://localhost:7054
npm install
npm run dev
```

Open http://localhost:3000 and sign in with the seeded administrator.

> The refresh cookie is `Secure`, so the API must be reached over HTTPS — use the `https://localhost:…`
> URL from `launchSettings.json`, not the plain HTTP one, or sign-in will not persist across reloads.

### Trusting the development certificate

```bash
dotnet dev-certs https --trust
```

Run this **once**, before starting the API — each run replaces the certificate, and a running Kestrel
keeps serving the one it loaded at startup. If `dotnet dev-certs https --check` reports
`Keyset does not exist`, the store is corrupt: `dotnet dev-certs https --clean`, then trust again.

**Firefox** maintains its own certificate store instead of reading the operating system's, so the
command above is not enough there. Set `security.enterprise_roots.enabled` to `true` in `about:config`
and restart the browser completely. If a certificate warning was ever clicked through for
`localhost:7054`, delete that entry under *Settings → Privacy & Security → View Certificates → Servers*
first — a saved exception overrides later trust decisions. Chromium browsers need neither step.

A browser that does not trust the certificate reports the failure as a CORS error with
`Status code: (null)`; the request never reached the server.

## Tests

```bash
cd backend/InventStock
dotnet test
```

123 tests covering four levels (including the PostgreSQL-only migration and concurrency gate):

- **Domain** — the movement invariants (sign per type, adjustments need a reason, no zero-quantity rows).
- **Services** — insufficient stock is rejected and leaves no row behind, inactive parts reject
  movements, a failed broadcast does not roll back a committed movement.
- **Repositories** — run against real SQLite, so the LINQ must actually translate to SQL: stock
  aggregation, low-stock filtering, case-insensitive search, pagination.
- **HTTP** — the API is started through `WebApplicationFactory` and exercised over real requests:
  anonymous access is rejected, staff cannot create parts or post adjustments, validation returns
  problem details, over-issuing stock returns `409`.

A separate set builds the EF model against the **Npgsql** provider so a mapping that only works on
SQLite fails in the test suite rather than on deployment.

## Database migrations

Catalogue update (10 October 2026): counter responses omit purchase costs and suppliers; admin
reads use `/api/admin/parts` and `/api/admin/dashboard`. Parts now have brand/variant identity and
public many-to-many references. `~/12582` searches number endings; Exact/Prefix/Suffix/Contains
modes are also available. MercedesOE references support their explicit optional-A match rule.

New migrations `CatalogueSearchKeys` and `SkuReferences` were verified on disposable PostgreSQL,
not applied to the configured shop database. Back up and review before applying. `pg_trgm` must
be available to the migration role. Downgrading after adding multiple brands for one number may
fail the old unique-number constraint; use a reviewed restore/migration plan, not a blind downgrade.

`ReferenceIdentityAndAudit` is deliberately forward-only: it separates exact reference identity from
separator-insensitive searching and preserves an audit trail when a link is deactivated/restored. Restore
a reviewed backup instead of attempting to downgrade through that migration.

`ControlledBrands` introduces canonical brand IDs and aliases. It backfills each existing non-empty
brand without changing part IDs or stock movements; new part writes select a brand ID rather than accept
free-text spelling. It has been tested by upgrading a populated previous schema only. It has not been
applied to the configured normal database.

`StockOperationIdempotency` gives stock-in, raw stock-out and adjustments stable operation IDs. A
true retry returns the original movement; changing the payload under the same ID returns a conflict.
Legacy movements remain valid with a null operation ID. This migration is also unapplied outside the
disposable test database.

`RequiredBrandIdentity` makes the controlled brand ID part of SKU identity and provides an explicit
Unknown brand instead of nullable identity. `SessionRevocationIdentity` lets a failed browser logout
later revoke the exact old session without affecting a new employee session. `StockUnitsAndConfidence`
adds fixed base units (`Piece` or `Centimeter`) and auditable physical-count checkpoints. Catalogue rows
start as **Not counted**; an unknown quantity is no longer presented as a verified zero. None of these
migrations has been applied to a normal or shop database.

`SupplierCostHistory` adds private append-only USD/EUR/LBP cost observations per SKU and supplier,
including TVA treatment, evidence source, pack quantity, supplier code and observation date.
`AuditedBrandMerges` adds immutable merge records; merging locks both brands, refuses SKU identity
collisions, reassigns products and usable aliases, and deactivates the old name atomically.

`StockConditionHolds` adds an append-only quarantine ledger. A hold requires confirmation that the
identified goods were physically separated or tagged, immediately removes them from sellable quantity,
and cannot be bypassed by raw stock-out. Staff may hold; only admins may release verified goods.

`AtomicPurchaseReceiving` adds immutable multi-line supplier receipts. Posting a receipt adds all base-unit
stock movements and any known actual-purchase cost observations in one transaction, with an idempotent
operation ID. Package conversions are preserved, unknown cost remains null, and receiving never implies
that the supplier was paid. The admin receiving screen searches catalogue SKUs and supports piece and
centimeter base units.

Optional real PostgreSQL regression tests require `INVENTSTOCK_TEST_POSTGRES` pointing to a
**disposable database named `inventstock_test`**. Tests create and remove a random isolated schema.
Never point this at business data. The benchmark seeds 20,000 parts, 20,000 references and 100,000
movements. On the development machine warm search p95 was approximately 12–27 ms and stock-quantity
sorting 108 ms; these are repository timings, not end-to-end performance guarantees.

Frontend verification: `npm run test:client`, `npm run lint`, `npx tsc --noEmit`, and
`npx next build --webpack`. The client tests cover session switching, refresh races and cancellation.

```bash
cd backend/InventStock
dotnet dotnet-ef migrations add <Name> --project InventStock.Infrastructure --startup-project InventStock.Api --output-dir Data/Migrations
dotnet dotnet-ef database update --project InventStock.Infrastructure --startup-project InventStock.Api
```

The EF tool is pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` once after cloning.

## API surface

| Method | Route | Role |
|---|---|---|
| `POST` | `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout` | anonymous |
| `GET` | `/api/auth/me` | any |
| `POST` | `/api/auth/change-password` | any |
| `GET` | `/api/dashboard` | any |
| `GET` | `/api/admin/dashboard`, `/api/admin/parts` (including detail/by-number/low-stock) | Admin |
| `GET` | `/api/suppliers`, `/api/suppliers/{id}` | Admin |
| `POST` | `/api/admin/receipts` | Admin |
| `GET` | `/api/admin/receipts/{id}`, `/api/admin/receipts/operations/{operationId}` | Admin |
| `GET` | `/api/parts/{id}/references` | any |
| `GET` | `/api/brands` | any |
| `POST`/`PATCH` | `/api/brands`, `/api/brands/{id}/aliases`, `/api/brands/{id}/active` | Admin |
| `POST` | `/api/parts/{id}/references` | Admin |
| `GET` | `/api/parts`, `/api/parts/{id}`, `/api/parts/by-number/{n}`, `/api/parts/low-stock` | any |
| `GET` | `/api/parts/{id}/movements`, `/api/stock-movements` | any |
| `POST` | `/api/parts/{id}/stock/in`, `/api/parts/{id}/stock/out` | any |
| `POST` | `/api/parts/{id}/stock/adjust`, `/api/parts/{id}/stock/count` | Admin |
| `GET` | `/api/stock-operations/{operationId}` | original operation user |
| `POST`/`PUT`/`PATCH` | `/api/parts`, `/api/categories`, `/api/suppliers` | Admin |
| `DELETE` | `/api/categories/{id}` | Admin |
| `GET`/`POST`/`PATCH` | `/api/users` | Admin |
| — | `/hubs/stock` (SignalR) | any |

## Deliberate scope choices

Cut from v1 to keep the core solid: multi-warehouse locations, a purchase-order workflow, and a
charting analytics page. A `LowStockThreshold` per part plus a "needs reorder" filter covers most of
the value of a reorder workflow at a fraction of the complexity.

Barcode is modelled and stored (unique per part) but scanning is deliberately left for a later phase —
lookup by part number is the flow that has to be right first.
