-- Run with psql as the PostgreSQL server/database owner, against one COPY first:
-- psql -v ON_ERROR_STOP=1 -d InventStockCopy -f Scripts/Provision-DatabaseSecurity.sql
-- Set passwords afterwards interactively with: \password inventstock_app
-- This script never grants app DDL or DELETE on business data.

BEGIN;
DO $$ BEGIN
  CREATE ROLE inventstock_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE ROLE inventstock_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE ROLE inventstock_backup LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

-- CREATE EXTENSION requires the database owner. Keep this outside the main
-- transaction because ALTER DATABASE cannot run inside a transaction block.
COMMIT;
SELECT format('ALTER DATABASE %I OWNER TO inventstock_migrator', current_database()) \gexec
BEGIN;

DO $$ BEGIN
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO inventstock_app, inventstock_backup', current_database());
END $$;
GRANT USAGE ON SCHEMA public TO inventstock_app, inventstock_backup;
GRANT ALL ON SCHEMA public TO inventstock_migrator;
-- The migrator must own the database/schema for EF DDL and pg_trgm extension creation.
ALTER SCHEMA public OWNER TO inventstock_migrator;
DO $$ DECLARE t record; BEGIN
  FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' LOOP
    EXECUTE format('ALTER TABLE public.%I OWNER TO inventstock_migrator', t.tablename);
  END LOOP;
END $$;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO inventstock_migrator;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO inventstock_migrator;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO inventstock_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO inventstock_app;
-- Identity links/tokens are operational session/role state, not business history.
-- Runtime needs to remove a revoked role, expired login or session token.
DO $$ DECLARE t text; BEGIN
  FOREACH t IN ARRAY ARRAY['AspNetUserRoles','AspNetUserClaims','AspNetUserLogins','AspNetUserTokens','RefreshTokens'] LOOP
    IF to_regclass('public.' || quote_ident(t)) IS NOT NULL THEN
      EXECUTE format('GRANT DELETE ON TABLE public.%I TO inventstock_app', t);
    END IF;
  END LOOP;
END $$;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO inventstock_backup;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO inventstock_backup;

CREATE OR REPLACE FUNCTION public.inventstock_reject_history_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$ BEGIN
  RAISE EXCEPTION 'InventStock history table % is append-only; post a linked correction instead.', TG_TABLE_NAME
    USING ERRCODE = '55000';
END $$;

-- Posted/ledger/history rows are append-only. Corrections are new linked rows, never edits.
DO $$ DECLARE t text; BEGIN
  FOREACH t IN ARRAY ARRAY[
    'BrandMergeEvents','ReferenceLinkEvents','StockMovements','StockHoldMovements',
    'SupplierCostObservations','PurchaseReceipts','PurchaseReceiptLines',
    'SupplierImportBatches','SupplierImportRows','PricingPolicies','CutHoseSellingRates',
    'CashOpeningBalances','CashMovements','OutgoingPayments','OutgoingPaymentReversals',
    'SupplierPayments','SupplierPaymentLines','SupplierPaymentReversals','CashTransfers','CashTransferReversals',
    'CashReconciliations','CashReconciliationCorrections','Sales','SaleLines','SaleTenders','SaleExchanges',
    'CustomerProfileEvents','CustomerAccountEntries','CustomerPayments','CustomerPaymentTenders','CustomerPaymentCorrections',
    'SaleReturns','SaleReturnLines','SaleRefundTenders','LegacySaleReturns','LegacySaleReturnLines','LegacySaleRefundTenders'
  ] LOOP
    IF to_regclass('public.' || quote_ident(t)) IS NOT NULL THEN
      EXECUTE format('REVOKE UPDATE, DELETE, TRUNCATE ON TABLE public.%I FROM inventstock_app', t);
      EXECUTE format('DROP TRIGGER IF EXISTS inventstock_block_mutation ON public.%I', t);
      EXECUTE format('CREATE TRIGGER inventstock_block_mutation BEFORE UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION public.inventstock_reject_history_mutation()', t);
    END IF;
  END LOOP;
END $$;

-- Owner-controlled break-glass: disable a specific trigger in a one-off owner session,
-- record a signed incident note outside the DB, repair, re-enable immediately, then audit.
-- Runtime role never receives ALTER TABLE/TRIGGER privilege.
ALTER DEFAULT PRIVILEGES FOR ROLE inventstock_migrator IN SCHEMA public GRANT SELECT, INSERT, UPDATE ON TABLES TO inventstock_app;
ALTER DEFAULT PRIVILEGES FOR ROLE inventstock_migrator IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO inventstock_app;
ALTER DEFAULT PRIVILEGES FOR ROLE inventstock_migrator IN SCHEMA public GRANT SELECT ON TABLES TO inventstock_backup;
COMMIT;
