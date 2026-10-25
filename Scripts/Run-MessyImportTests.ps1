[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$connection = $env:INVENTSTOCK_TEST_POSTGRES
if ([string]::IsNullOrWhiteSpace($connection) -or $connection -notmatch 'Host=(127\.0\.0\.1|localhost);Port=55439;Database=inventstock_test;') {
  throw 'Safety refusal: use only the disposable localhost:55439/inventstock_test database.'
}
if ([string]::IsNullOrWhiteSpace($env:INVENTSTOCK_TEST_MIGRATOR_POSTGRES)) {
  $env:INVENTSTOCK_TEST_MIGRATOR_POSTGRES = $connection
}
Push-Location (Join-Path $root 'frontend')
try { npm run test:client; if ($LASTEXITCODE -ne 0) { throw 'Frontend import tests failed.' } }
finally { Pop-Location }
dotnet test (Join-Path $root 'backend\InventStock\InventStock.Tests\InventStock.Tests.csproj') --no-restore -m:1 -c Release --filter 'FullyQualifiedName~SupplierImportApiTests|FullyQualifiedName~PostgresSupplierImportTests'
if ($LASTEXITCODE -ne 0) { throw 'Backend import tests failed.' }
Write-Host 'PASS: messy import parser and reviewed PostgreSQL import tests.' -ForegroundColor Green
