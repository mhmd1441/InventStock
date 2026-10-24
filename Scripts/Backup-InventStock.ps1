[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [string]$DatabaseName = 'InventStock',
    [string]$PgHost = 'localhost',
    [ValidateRange(1, 65535)][int]$PgPort = 5432,
    [string]$PgUser = 'inventstock_backup',
    [string]$PrimaryBackupRoot = 'C:\InventStockBackups',
    [string]$SecondaryBackupRoot = 'E:\InventStockBackups',
    [ValidateSet('Frequent', 'Nightly')][string]$BackupSchedule = 'Frequent',
    [ValidateRange(0, 10000)][int]$KeepLast = 0,
    [ValidateRange(1, 10080)][int]$MaximumAgeMinutes = 1440,
    [string]$PgBin = 'C:\Program Files\PostgreSQL\17\bin',
    [string]$PgDumpExecutable = '',
    [string]$PgPassFile = $env:PGPASSFILE,
    [string]$EncryptionKeyPath = 'C:\InventStockSecrets\inventstock-backup.key'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-SafeBackupRoot([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $root = [IO.Path]::GetPathRoot($full).TrimEnd('\')
    if ($full.Length -le $root.Length) { throw "Backup root '$Path' cannot be a drive root." }
}

function Get-EncryptionKey([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        throw 'EncryptionKeyPath must point to the private 64-byte backup key outside the repository and backup destinations.'
    }
    [byte[]]$key = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path))
    if ($key.Length -ne 64) { throw 'The backup encryption key must contain exactly 64 random bytes.' }
    return $key
}

function Write-BackupLog([string]$Message) {
    $line = "{0:u} {1}" -f (Get-Date), $Message
    Add-Content -LiteralPath $script:logPath -Value $line
    Write-Host $line
}

function Test-LastSuccess {
    if (-not (Test-Path -LiteralPath $script:successPath)) {
        Write-Warning 'InventStock backup has no recorded successful run.'
        return $false
    }
    $recorded = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse((Get-Content -LiteralPath $script:successPath -Raw).Trim(), [ref]$recorded)) {
        Write-Warning 'InventStock backup success marker is invalid.'
        return $false
    }
    if (([DateTimeOffset]::UtcNow - $recorded.ToUniversalTime()) -gt [TimeSpan]::FromMinutes($MaximumAgeMinutes)) {
        Write-Warning "InventStock backup is older than $MaximumAgeMinutes minutes: $($recorded.ToString('u'))."
        return $false
    }
    Write-BackupLog "Last successful backup was $($recorded.ToString('u'))."
    return $true
}

function Get-Hmac([string]$Path, [byte[]]$Key) {
    $hmac = [Security.Cryptography.HMACSHA256]::new($Key)
    $stream = [IO.File]::OpenRead($Path)
    try {
        [byte[]]$buffer = New-Object byte[] 1048576
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            [void]$hmac.TransformBlock($buffer, 0, $read, $null, 0)
        }
        [void]$hmac.TransformFinalBlock([byte[]]@(), 0, 0)
        return $hmac.Hash
    }
    finally { $stream.Dispose(); $hmac.Dispose() }
}

function Protect-BackupFile([string]$InputPath, [string]$OutputPath, [byte[]]$Key) {
    # ISBK2 = magic (5), AES-256-CBC IV (16), ciphertext, HMAC-SHA256 (32).
    [byte[]]$encryptionKey = $Key[0..31]
    [byte[]]$macKey = $Key[32..63]
    $aes = [Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256; $aes.Mode = [Security.Cryptography.CipherMode]::CBC; $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key = $encryptionKey; $aes.GenerateIV()
    $input = [IO.File]::OpenRead($InputPath)
    $output = [IO.File]::Open($OutputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        [byte[]]$magic = [Text.Encoding]::ASCII.GetBytes('ISBK2')
        $output.Write($magic, 0, $magic.Length); $output.Write($aes.IV, 0, $aes.IV.Length)
        $crypto = [Security.Cryptography.CryptoStream]::new($output, $aes.CreateEncryptor(), [Security.Cryptography.CryptoStreamMode]::Write, $true)
        try { $input.CopyTo($crypto); $crypto.FlushFinalBlock() } finally { $crypto.Dispose() }
    }
    finally { $input.Dispose(); $output.Dispose(); $aes.Dispose() }
    [byte[]]$mac = Get-Hmac -Path $OutputPath -Key $macKey
    $append = [IO.File]::Open($OutputPath, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $append.Write($mac, 0, $mac.Length) }
    finally {
        $append.Dispose(); [Array]::Clear($encryptionKey, 0, $encryptionKey.Length); [Array]::Clear($macKey, 0, $macKey.Length)
    }
}

Assert-SafeBackupRoot $PrimaryBackupRoot
Assert-SafeBackupRoot $SecondaryBackupRoot
if ($DatabaseName -notmatch '^[A-Za-z0-9_]+$') { throw 'DatabaseName may contain only letters, digits and underscores.' }
if ([IO.Path]::GetFullPath($PrimaryBackupRoot).TrimEnd('\') -ieq [IO.Path]::GetFullPath($SecondaryBackupRoot).TrimEnd('\')) {
    throw 'PrimaryBackupRoot and SecondaryBackupRoot must be different destinations.'
}
try {
    New-Item -ItemType Directory -Force -Path $PrimaryBackupRoot, $SecondaryBackupRoot | Out-Null
}
catch {
    Write-Warning "InventStock primary or secondary backup destination could not be prepared: $($_.Exception.Message)"
    throw
}
$logRoot = Join-Path $PrimaryBackupRoot 'logs'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$script:logPath = Join-Path $logRoot ("backup-{0}.log" -f (Get-Date -Format 'yyyyMMdd'))
$script:successPath = Join-Path $PrimaryBackupRoot 'last-success-utc.txt'

if ($CheckOnly) { if (-not (Test-LastSuccess)) { exit 1 }; exit 0 }
[void](Test-LastSuccess)
$effectiveKeepLast = if ($KeepLast -gt 0) { $KeepLast } elseif ($BackupSchedule -eq 'Frequent') { 192 } else { 30 }
$pgDump = if ([string]::IsNullOrWhiteSpace($PgDumpExecutable)) { Join-Path $PgBin 'pg_dump.exe' } else { $PgDumpExecutable }
if (-not (Test-Path -LiteralPath $pgDump)) { throw "PostgreSQL 17 pg_dump was not found at '$pgDump'." }
if ([string]::IsNullOrWhiteSpace($PgPassFile) -or -not (Test-Path -LiteralPath $PgPassFile)) { throw 'PgPassFile must point to an existing private pgpass.conf outside the repository.' }
[byte[]]$key = $null
$env:PGPASSFILE = $PgPassFile
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$filePrefix = "$DatabaseName-$($BackupSchedule.ToLowerInvariant())"
$plainPartial = Join-Path $PrimaryBackupRoot "$filePrefix-$stamp.dump.partial"
$primaryFinal = Join-Path $PrimaryBackupRoot "$filePrefix-$stamp.dump.enc"
$primaryPartial = "$primaryFinal.partial"
$secondaryFinal = Join-Path $SecondaryBackupRoot "$filePrefix-$stamp.dump.enc"
$secondaryPartial = "$secondaryFinal.partial"

try {
    $key = Get-EncryptionKey $EncryptionKeyPath
    Write-BackupLog "Starting $BackupSchedule encrypted backup for database '$DatabaseName' on $PgHost`:$PgPort."
    & $pgDump --no-password --host $PgHost --port $PgPort --username $PgUser --dbname $DatabaseName --format custom --file $plainPartial 2>&1 | ForEach-Object { Write-BackupLog $_.ToString() }
    if ($LASTEXITCODE -ne 0) { throw "pg_dump exited with code $LASTEXITCODE." }
    Protect-BackupFile -InputPath $plainPartial -OutputPath $primaryPartial -Key $key
    Remove-Item -LiteralPath $plainPartial -Force
    Move-Item -LiteralPath $primaryPartial -Destination $primaryFinal
    try {
        Copy-Item -LiteralPath $primaryFinal -Destination $secondaryPartial -Force
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $primaryFinal).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $secondaryPartial).Hash) { throw 'Second destination copy hash does not match the primary encrypted backup.' }
        Move-Item -LiteralPath $secondaryPartial -Destination $secondaryFinal
    }
    catch { Write-Warning "InventStock secondary backup destination failed: $($_.Exception.Message)"; throw }
    foreach ($root in @($PrimaryBackupRoot, $SecondaryBackupRoot)) {
        Get-ChildItem -LiteralPath $root -File -Filter "$filePrefix-*.dump.enc" | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip $effectiveKeepLast | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }
    [DateTimeOffset]::UtcNow.ToString('O') | Set-Content -LiteralPath $successPath -NoNewline
    Write-BackupLog "Encrypted backup succeeded: $primaryFinal; verified second destination copy: $secondaryFinal."
}
catch {
    Remove-Item -LiteralPath $plainPartial, $primaryPartial, $secondaryPartial -Force -ErrorAction SilentlyContinue
    Write-Warning "InventStock primary or secondary backup failed: $($_.Exception.Message)"
    Write-BackupLog "BACKUP FAILED: $($_.Exception.Message)"
    throw
}
finally { if ($null -ne $key) { [Array]::Clear($key, 0, $key.Length) } }
