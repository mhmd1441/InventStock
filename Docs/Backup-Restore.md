# PostgreSQL backup and restore

Use this before importing authoritative data and before every migration of the persistent shop database. Replace the example database names only after reviewing the active connection string. Never restore over the source database.

## Create the backup

```powershell
$backupStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = "C:\InventStockBackups\InventStock-$backupStamp.dump"
New-Item -ItemType Directory -Force -Path 'C:\InventStockBackups'
pg_dump -h localhost -p 5432 -U postgres -d InventStock -Fc -f $backupPath
Get-Item -LiteralPath $backupPath | Select-Object FullName,Length,LastWriteTime
```

Keep an encrypted copy off the shop computer. Do not place dumps inside the repository; `*.dump`, `*.backup`, `*.bak` and `backups/` are ignored.

## Verify by restoring to a separate database

Choose a new disposable name and ensure it is not the source database.

```powershell
createdb -h localhost -p 5432 -U postgres InventStockRestoreCheck
pg_restore -h localhost -p 5432 -U postgres -d InventStockRestoreCheck --no-owner $backupPath
psql -h localhost -p 5432 -U postgres -d InventStockRestoreCheck -c 'SELECT COUNT(*) FROM "Parts";'
psql -h localhost -p 5432 -U postgres -d InventStockRestoreCheck -c 'SELECT COUNT(*) FROM "StockMovements";'
```

Compare part, customer, movement and reference counts with the source. Sample important part IDs and calculated stock totals. A backup is accepted only after this restore succeeds.

## Migration rehearsal

Point a temporary API configuration at the restored database, keep demo seeding disabled, and apply migrations there first. Record migration duration, compare IDs/counts/totals again, and test login/search/counting. Do not switch the normal database until the reviewed backup and rehearsal both pass.

## Verified development check

On 15 October 2026, the custom-format `pg_dump`/`pg_restore` cycle was tested inside the disposable PostgreSQL 17 container using an isolated schema and sentinel row. The schema and dump were removed afterward. This proves the tool chain; it is not a backup of `InventStockDev` or shop data.
