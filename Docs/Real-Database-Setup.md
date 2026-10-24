# Real PostgreSQL 17 database setup

Use this only for a **new empty** local database named `InventStock`. Do not point these commands at `InventStockDev`, a supplier-import copy, Supabase, or a shop database that has not first been backed up and reviewed.

## Prerequisites

- PostgreSQL **17** server and client tools installed on the shop host.
- A dedicated PostgreSQL login and a `pgpass.conf` kept outside this repository. The file format is `host:port:database:user:password`; restrict it to the Windows account running backups. The backup scripts set `PGPASSFILE` only for `pg_dump`/`pg_restore`; no database password appears in a script or log.
- Two backup destinations: a local folder plus either a second drive or a cloud-synced folder. Never put the encryption key in either destination.
- Run PowerShell from the repository root.

## 1. Create and protect the empty database

Edit only values inside angle brackets. Do not put passwords in source files.

```powershell
$pgBin = 'C:\Program Files\PostgreSQL\17\bin'
$pgHost = 'localhost'
$pgPort = 5432
$pgUser = '<inventstock_db_user>'
$env:PGPASSFILE = 'C:\InventStockSecrets\pgpass.conf'

& "$pgBin\createdb.exe" -h $pgHost -p $pgPort -U $pgUser InventStock
if ($LASTEXITCODE -ne 0) { throw 'InventStock was not created. Stop and resolve the PostgreSQL error.' }

New-Item -ItemType Directory -Force -Path 'C:\InventStockBackups','C:\InventStockSecrets' | Out-Null
& "$pgBin\pg_dump.exe" -h $pgHost -p $pgPort -U $pgUser -d InventStock -Fc -f 'C:\InventStockBackups\InventStock-before-migrations.dump'
if ($LASTEXITCODE -ne 0) { throw 'The pre-migration backup failed. Do not continue.' }
```

## 2. Store configuration outside the repository

```powershell
$inventStockConnection = Read-Host 'Paste the InventStock connection string'
$jwtBytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($jwtBytes)
$rng.Dispose()
$inventStockJwt = [Convert]::ToBase64String($jwtBytes)
[Array]::Clear($jwtBytes, 0, $jwtBytes.Length)

Set-Location '.\backend\InventStock\InventStock.Api'
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $inventStockConnection
dotnet user-secrets set 'Jwt:SigningKey' $inventStockJwt
dotnet user-secrets set 'Seed:AdminEmail' '<owner-email>'
dotnet user-secrets set 'Seed:AdminFullName' '<owner-name>'
dotnet user-secrets set 'Seed:AdminPassword' '<unique-strong-owner-password>'
dotnet user-secrets set 'Seed:IncludeDemoData' 'false'
dotnet user-secrets set 'Database:MigrateOnStartup' 'false'
Remove-Variable inventStockJwt
```

Production rejects demo seeding before it can alter a database. Keep `$inventStockConnection` only for the next step, then remove it.

### Role-specific connections

After applying `Scripts/Provision-DatabaseSecurity.sql` to a restored copy, use three distinct logins. The application runs only as `inventstock_app`; migration commands use `inventstock_migrator`; `pg_dump` uses read-only `inventstock_backup`. Keep their passwords in `pgpass.conf` or user-secrets, never in this repository.

```powershell
$appConnection = 'Host=localhost;Port=5432;Database=InventStock;Username=inventstock_app;Password=<kept-outside-repo>'
$migratorConnection = 'Host=localhost;Port=5432;Database=InventStock;Username=inventstock_migrator;Password=<kept-outside-repo>'
$backupUser = 'inventstock_backup'

Set-Location '.\backend\InventStock\InventStock.Api'
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $appConnection
```

From the PostgreSQL server owner account, apply the role script to a **copy** first. It is safe to run twice and moves database ownership to the migrator role so it can create `pg_trgm` during migrations:

```powershell
& 'C:\Program Files\PostgreSQL\17\bin\psql.exe' -h localhost -p 5432 -U postgres -d InventStockCopy -v ON_ERROR_STOP=1 -f '.\Scripts\Provision-DatabaseSecurity.sql'
```

Use `$migratorConnection` only with the explicit EF migration command below. For the one-time first-admin bootstrap, temporarily set `ConnectionStrings:DefaultConnection` to `$migratorConnection`, run the app once with demo data disabled, then immediately set it back to `$appConnection` and remove `Seed:AdminPassword`.

## 3. Explicit migration with a stop check

The target is printed and must be typed exactly before EF is allowed to run. This does not use startup migration.

```powershell
Set-Location '<repository-root>'
$targetDatabase = 'InventStock'
Write-Host "TARGET DATABASE: $targetDatabase" -ForegroundColor Yellow
if ((Read-Host "Type $targetDatabase exactly to apply migrations") -cne $targetDatabase) {
  throw 'Migration cancelled: confirmation did not match.'
}

$env:INVENTSTOCK_CONNECTION = $migratorConnection
dotnet ef database update `
  --project '.\backend\InventStock\InventStock.Infrastructure\InventStock.Infrastructure.csproj' `
  --startup-project '.\backend\InventStock\InventStock.Api\InventStock.Api.csproj' `
  --connection $migratorConnection
if ($LASTEXITCODE -ne 0) { throw 'Migration failed. Restore the pre-migration backup; do not start the app.' }

dotnet ef migrations has-pending-model-changes `
  --project '.\backend\InventStock\InventStock.Infrastructure\InventStock.Infrastructure.csproj' `
  --startup-project '.\backend\InventStock\InventStock.Api\InventStock.Api.csproj'
Remove-Item Env:INVENTSTOCK_CONNECTION
Remove-Variable appConnection,migratorConnection
```

## 4. Create the first owner account, without demo data

The bootstrap is idempotent: it creates baseline roles/cash definitions/categories and the configured owner only when the email does not exist. It never resets an existing password and never logs it.

```powershell
Set-Location '.\backend\InventStock\InventStock.Api'
dotnet user-secrets set 'Database:MigrateOnStartup' 'true'
dotnet run --launch-profile https
# Log in once, then stop with Ctrl+C.
dotnet user-secrets set 'Database:MigrateOnStartup' 'false'
dotnet user-secrets remove 'Seed:AdminPassword'
```

Do not run this if the migration step failed. Confirm `Seed:IncludeDemoData` remains `false` first.

## 5. Encrypted frequent and nightly backups

Create a 64-byte random key **once**. Keep the resulting file in `C:\InventStockSecrets`, ACL it to the dedicated backup Windows account, and keep a separately protected recovery copy (for example in the owner’s password manager). It must never be copied to either backup drive or cloud folder.

```powershell
$keyPath = 'C:\InventStockSecrets\inventstock-backup.key'
$backupKey = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($backupKey)
$rng.Dispose()
[IO.File]::WriteAllBytes($keyPath, $backupKey)
[Array]::Clear($backupKey, 0, $backupKey.Length)
icacls $keyPath /inheritance:r /grant:r "$env:USERNAME`:(R)"
```

`Backup-InventStock.ps1` writes an authenticated AES-256 encrypted `.dump.enc` file, verifies the hash at the second destination, and reports failure if either destination fails. Frequent and nightly generations are retained separately: the defaults are 192 frequent copies (two days at continuous 15-minute intervals) and 30 nightly copies. The plaintext dump exists only briefly while it is encrypted and is deleted on success or failure.

Create three Windows Task Scheduler tasks under a dedicated local Windows account such as `InventStockBackup` (not an administrator). Give that account read access to `C:\InventStockSecrets\pgpass.conf` and `inventstock-backup.key`, read/write access to both backup destinations, and no access to the repository beyond reading the scripts. These settings are documented but **not automated/tested on this machine**:

1. **InventStock frequent backup**: Daily at the shop opening time (example `08:00`), repeat every **15 minutes** for the duration of shop hours (example `12 hours`); “Run whether user is logged on or not”; Configure for Windows 10/Server 2016 or later; enable **Wake the computer to run this task** and **Run task as soon as possible after a scheduled start is missed**; if it fails, restart every 5 minutes up to 3 times; choose **Do not start a new instance**. Highest privileges are not required when the dedicated account has the folder permissions listed above.
   - Program: `powershell.exe`
   - Arguments:
   ```text
   -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\InventStock\Scripts\Backup-InventStock.ps1" -BackupSchedule Frequent -DatabaseName "InventStock" -PgUser "inventstock_backup" -PrimaryBackupRoot "C:\InventStockBackups" -SecondaryBackupRoot "C:\Users\<backup-user>\OneDrive\InventStockBackups" -PgPassFile "C:\InventStockSecrets\pgpass.conf" -EncryptionKeyPath "C:\InventStockSecrets\inventstock-backup.key"
   ```
2. **InventStock nightly backup**: Daily at `01:00`, with the same account, wake, missed-run, retry and non-overlap settings. Use the same arguments except `-BackupSchedule Nightly`.
3. **InventStock shop-hours backup health check**: Daily at opening time, repeated every 15 minutes for the shop-hours duration; same account/settings and “run as soon as possible after a missed start”; Program `powershell.exe`.
   - Arguments:
   ```text
   -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\InventStock\Scripts\Backup-InventStock.ps1" -CheckOnly -MaximumAgeMinutes 30 -PrimaryBackupRoot "C:\InventStockBackups" -SecondaryBackupRoot "C:\Users\<backup-user>\OneDrive\InventStockBackups"
   ```

### Next-morning five-step check

1. Open Task Scheduler and confirm the frequent backup, nightly backup and health-check tasks show the expected *Last Run Time* and *Last Run Result* `0x0`.
2. Open `C:\InventStockBackups\logs\backup-YYYYMMDD.log`; it must end with `Encrypted backup succeeded` and name both destinations.
3. Confirm a new `.dump.enc` file exists in both the local and cloud/second-drive folders and that the filenames match.
4. Run the health check task manually, or run its `-CheckOnly -MaximumAgeMinutes 30` command. It must exit `0`; an older-than-30-minutes warning during shop hours means backup protection is not current.
5. At least quarterly, restore the newest encrypted backup into a new disposable database and compare migrations, part count, customer count, and a few stock totals. A backup that has not restored is not accepted.

Any non-zero result, missing second copy, or cloud-sync conflict means the pilot must be treated as unprotected until corrected.

## 6. Restore after a full PC failure

1. On a replacement PC, install PostgreSQL **17** and PostgreSQL client tools. Create a new PostgreSQL login and a new empty target database such as `InventStockRecovery`; no file from the dead PC is required for this step.
2. Retrieve the encrypted backup from the second drive/cloud and retrieve the separately protected 64-byte `inventstock-backup.key` from the owner’s password manager or other offline key store. Create `C:\InventStockSecrets` and restore the key there. Do **not** place it beside the encrypted backup.
3. Create a new `C:\InventStockSecrets\pgpass.conf` for the replacement PostgreSQL login. It must contain a matching line for the *new target database* as well as any source database you will access. Restrict it to the recovery/backup Windows account.
4. Run the restore script against a copy first:
   ```powershell
   .\Scripts\Restore-InventStockBackup.ps1 `
     -EncryptedBackupPath 'E:\InventStockBackups\InventStock-YYYYMMDD-HHMMSS.dump.enc' `
     -TargetDatabase 'InventStockRecovery' `
     -PgUser '<inventstock_db_user>' `
     -PgPassFile 'C:\InventStockSecrets\pgpass.conf' `
     -EncryptionKeyPath 'C:\InventStockSecrets\inventstock-backup.key'
   ```
5. Verify row counts, a few part numbers, stock totals, and owner login. Only then point the application configuration to the recovered database.

The companion restore script requires typed target confirmation and deletes its temporary plaintext dump even after failure. Never restore over the source database.
