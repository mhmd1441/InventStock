[CmdletBinding()]
param([string] $OutputPath)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$container = 'inventstock-test-postgres'
if (-not $OutputPath) { $OutputPath = Join-Path $root ("TestResults\search-benchmark-{0}.md" -f (Get-Date -Format 'yyyyMMdd-HHmmss')) }

$info = docker ps --filter "name=^/$container$" --format '{{.Names}}|{{.Image}}|{{.Ports}}'
if ($info -notmatch '^inventstock-test-postgres\|postgres:17[^|]*\|.*127\.0\.0\.1:55439->5432') {
  throw 'Safety refusal: benchmark requires only the disposable PostgreSQL 17 container at 127.0.0.1:55439.'
}
$password = docker inspect $container --format '{{range .Config.Env}}{{println .}}{{end}}' |
  Where-Object { $_ -like 'POSTGRES_PASSWORD=*' } | ForEach-Object { $_.Substring(18) }
if ([string]::IsNullOrWhiteSpace($password)) { throw 'Disposable database password was not found.' }
$connection = "Host=127.0.0.1;Port=55439;Database=inventstock_test;Username=postgres;Password=$password"
$env:INVENTSTOCK_TEST_POSTGRES = $connection
$env:INVENTSTOCK_TEST_MIGRATOR_POSTGRES = $connection
$log = [IO.Path]::ChangeExtension($OutputPath, '.log')
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath -Parent) | Out-Null

dotnet test (Join-Path $root 'backend\InventStock\InventStock.Tests\InventStock.Tests.csproj') `
  --no-restore -m:1 -c Release --filter 'FullyQualifiedName~PostgresCatalogueTests.SearchBenchmark' `
  --logger 'console;verbosity=detailed' *>&1 | Tee-Object -FilePath $log
if ($LASTEXITCODE -ne 0) { throw "Benchmark failed. See $log" }

$matches = Select-String -Path $log -Pattern '^(?<query>.+): p50=(?<p50>[0-9.]+)ms, p95=(?<p95>[0-9.]+)ms, max=(?<max>[0-9.]+)ms$'
$rows = $matches | ForEach-Object {
  "| $($_.Matches[0].Groups['query'].Value) | $($_.Matches[0].Groups['p50'].Value) | $($_.Matches[0].Groups['p95'].Value) | $($_.Matches[0].Groups['max'].Value) |"
}
if (-not $rows) { throw "Benchmark completed but no timing rows were parsed. See $log" }
@(
  '# Hostile search benchmark', '',
  "Generated: $(Get-Date -Format o)",
  'Fixture: 20,000 SKUs, approximately 280,000 references, shared aliases, and 100,000 stock movements.', '',
  '| Query shape | p50 ms | p95 ms | max ms |',
  '|---|---:|---:|---:|'
) + $rows | Set-Content $OutputPath
Write-Host "PASS: benchmark table written to $OutputPath" -ForegroundColor Green
