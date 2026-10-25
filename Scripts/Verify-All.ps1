[CmdletBinding()]
param([switch] $Benchmark)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$container = 'inventstock-test-postgres'
$allowedDatabase = 'inventstock_test'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logRoot = Join-Path $root "TestResults\verify-all\$stamp"
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Step, [string]$Status, [string]$Log) {
  $results.Add([pscustomobject]@{ Step = $Step; Status = $Status; Log = $Log })
}

function Invoke-Logged([string]$Name, [string]$WorkingDirectory, [scriptblock]$Action) {
  $log = Join-Path $logRoot (($Name -replace '[^A-Za-z0-9_-]', '-') + '.log')
  Push-Location $WorkingDirectory
  try {
    & $Action *>&1 | Tee-Object -FilePath $log
    if ($LASTEXITCODE -ne 0) { throw "$Name exited with code $LASTEXITCODE" }
    Add-Result $Name 'PASS' $log
  } catch {
    $_ | Out-File -FilePath $log -Append
    Add-Result $Name 'FAIL' $log
  } finally { Pop-Location }
}

New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker CLI is required.' }
if (-not (docker info 2>$null)) { throw 'Docker Desktop is not running.' }
$info = docker ps --filter "name=^/$container$" --format '{{.Names}}|{{.Image}}|{{.Ports}}'
if ($info -notmatch '^inventstock-test-postgres\|postgres:17[^|]*\|.*127\.0\.0\.1:55439->5432') {
  throw "Safety refusal: expected '$container', PostgreSQL 17, bound to 127.0.0.1:55439."
}
$password = docker inspect $container --format '{{range .Config.Env}}{{println .}}{{end}}' |
  Where-Object { $_ -like 'POSTGRES_PASSWORD=*' } | ForEach-Object { $_.Substring(18) }
if ([string]::IsNullOrWhiteSpace($password)) { throw 'Disposable container password was not found.' }
$connection = "Host=127.0.0.1;Port=55439;Database=$allowedDatabase;Username=postgres;Password=$password"
if ($connection -notmatch 'Host=127\.0\.0\.1;Port=55439;Database=inventstock_test;') {
  throw 'Safety refusal: verification database is not the fixed disposable target.'
}
$env:INVENTSTOCK_TEST_POSTGRES = $connection
$env:INVENTSTOCK_TEST_MIGRATOR_POSTGRES = $connection

Invoke-Logged 'backend-build' $root { dotnet build backend/InventStock/InventStock.sln --no-restore -m:1 -c Release }
Invoke-Logged 'backend-full-suite' $root { dotnet test backend/InventStock/InventStock.Tests/InventStock.Tests.csproj --no-build --no-restore -m:1 -c Release }
Invoke-Logged 'frontend-client-tests' (Join-Path $root 'frontend') { npm run test:client }
Invoke-Logged 'frontend-lint' (Join-Path $root 'frontend') { npm run lint }
Invoke-Logged 'frontend-typescript' (Join-Path $root 'frontend') { npx tsc --noEmit }
Invoke-Logged 'frontend-production-build' (Join-Path $root 'frontend') { npm run build -- --webpack }

$messy = Join-Path $PSScriptRoot 'Run-MessyImportTests.ps1'
if (Test-Path $messy) { Invoke-Logged 'messy-import' $root { & $messy } }
else { Add-Result 'messy-import' 'SKIPPED' 'Runner not written yet.' }

$browser = Join-Path $PSScriptRoot 'Run-BrowserSmoke.ps1'
Invoke-Logged 'browser-smoke' $root { & $browser }

if ($Benchmark) {
  $benchmarkScript = Join-Path $PSScriptRoot 'Run-SearchBenchmark.ps1'
  if (Test-Path $benchmarkScript) { Invoke-Logged 'hostile-search-benchmark' $root { & $benchmarkScript } }
  else { Add-Result 'hostile-search-benchmark' 'SKIPPED' 'Runner not written yet.' }
} else { Add-Result 'hostile-search-benchmark' 'SKIPPED' 'Use -Benchmark to run.' }

$results | Format-Table -AutoSize
$results | ConvertTo-Json | Set-Content (Join-Path $logRoot 'summary.json')
if ($results.Status -contains 'FAIL') { Write-Host "FAIL: see $logRoot" -ForegroundColor Red; exit 1 }
Write-Host "PASS: see $logRoot" -ForegroundColor Green
