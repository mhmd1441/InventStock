[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EncryptedBackupPath,
    [Parameter(Mandatory = $true)][string]$TargetDatabase,
    [string]$PgHost = 'localhost',
    [ValidateRange(1, 65535)][int]$PgPort = 5432,
    [string]$PgUser = 'inventstock',
    [string]$PgBin = 'C:\Program Files\PostgreSQL\17\bin',
    [string]$PgRestoreExecutable = '',
    [string]$PgPassFile = $env:PGPASSFILE,
    [string]$EncryptionKeyPath = 'C:\InventStockSecrets\inventstock-backup.key',
    [switch]$DisposableTestOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EncryptionKey([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) { throw 'EncryptionKeyPath must point to the private 64-byte backup key outside the repository and backup destinations.' }
    [byte[]]$key = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path))
    if ($key.Length -ne 64) { throw 'The backup encryption key must contain exactly 64 random bytes.' }
    return $key
}

function Unprotect-BackupFile([string]$InputPath, [string]$OutputPath, [byte[]]$Key) {
    [byte[]]$all = [IO.File]::ReadAllBytes($InputPath)
    $headerLength = 21; $tagLength = 32
    if ($all.Length -le ($headerLength + $tagLength) -or [Text.Encoding]::ASCII.GetString($all, 0, 5) -ne 'ISBK2') { throw 'The backup is not a valid InventStock encrypted backup.' }
    [byte[]]$encryptionKey = $Key[0..31]; [byte[]]$macKey = $Key[32..63]
    $mac = [Security.Cryptography.HMACSHA256]::new($macKey)
    try {
        $protectedLength = $all.Length - $tagLength
        [byte[]]$actualTag = $mac.ComputeHash($all, 0, $protectedLength)
        [byte[]]$expectedTag = $all[($protectedLength)..($all.Length - 1)]
        $difference = 0
        for ($index = 0; $index -lt $actualTag.Length; $index++) {
            $difference = $difference -bor ($actualTag[$index] -bxor $expectedTag[$index])
        }
        if ($difference -ne 0) { throw 'Encrypted backup authentication failed. Do not restore this file.' }
        [byte[]]$iv = $all[5..20]; [byte[]]$ciphertext = $all[$headerLength..($protectedLength - 1)]
        $aes = [Security.Cryptography.Aes]::Create()
        try {
            $aes.KeySize = 256; $aes.Mode = [Security.Cryptography.CipherMode]::CBC; $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7; $aes.Key = $encryptionKey; $aes.IV = $iv
            [byte[]]$plain = $aes.CreateDecryptor().TransformFinalBlock($ciphertext, 0, $ciphertext.Length)
            try { [IO.File]::WriteAllBytes($OutputPath, $plain) } finally { [Array]::Clear($plain, 0, $plain.Length) }
        }
        finally { $aes.Dispose() }
    }
    finally { $mac.Dispose(); [Array]::Clear($encryptionKey, 0, $encryptionKey.Length); [Array]::Clear($macKey, 0, $macKey.Length); [Array]::Clear($all, 0, $all.Length) }
}

if (-not (Test-Path -LiteralPath $EncryptedBackupPath)) { throw "Encrypted backup '$EncryptedBackupPath' was not found." }
if ([string]::IsNullOrWhiteSpace($PgPassFile) -or -not (Test-Path -LiteralPath $PgPassFile)) { throw 'PgPassFile must point to an existing private pgpass.conf outside the repository.' }
$pgRestore = if ([string]::IsNullOrWhiteSpace($PgRestoreExecutable)) { Join-Path $PgBin 'pg_restore.exe' } else { $PgRestoreExecutable }
if (-not (Test-Path -LiteralPath $pgRestore)) { throw "PostgreSQL 17 pg_restore was not found at '$pgRestore'." }
if ($DisposableTestOnly) {
    if ($env:INVENTSTOCK_DISPOSABLE_RESTORE_TEST -ne 'true' -or $TargetDatabase -notmatch '^inventstock_') {
        throw 'DisposableTestOnly requires INVENTSTOCK_DISPOSABLE_RESTORE_TEST=true and an inventstock_ disposable target database.'
    }
    $confirmation = $TargetDatabase
}
else {
    $confirmation = Read-Host "Type the target database name exactly to restore into '$TargetDatabase'"
}
if ($confirmation -cne $TargetDatabase) { throw 'Restore cancelled: target database confirmation did not match.' }
$temporaryDump = Join-Path ([IO.Path]::GetTempPath()) ("InventStock-restore-{0}.dump" -f [Guid]::NewGuid())
[byte[]]$key = $null
try {
    $key = Get-EncryptionKey $EncryptionKeyPath
    Unprotect-BackupFile -InputPath $EncryptedBackupPath -OutputPath $temporaryDump -Key $key
    $env:PGPASSFILE = $PgPassFile
    & $pgRestore -w -h $PgHost -p $PgPort -U $PgUser -d $TargetDatabase --clean --if-exists --no-owner $temporaryDump
    if ($LASTEXITCODE -ne 0) { throw "pg_restore exited with code $LASTEXITCODE." }
    Write-Host "Restore completed into '$TargetDatabase'. Verify counts and log in before using it."
}
finally {
    Remove-Item -LiteralPath $temporaryDump -Force -ErrorAction SilentlyContinue
    if ($null -ne $key) { [Array]::Clear($key, 0, $key.Length) }
}
