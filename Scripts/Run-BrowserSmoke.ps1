[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$container = 'inventstock-test-postgres'
$database = 'inventstock_browser_smoke'
$apiUrl = 'http://127.0.0.1:7057'
$webUrl = 'http://127.0.0.1:3107'
$runDir = Join-Path $root 'frontend\test-results\browser-smoke'
$api = $null; $web = $null; $result = $false
$playwright = Join-Path $root 'frontend\node_modules\.bin\playwright.cmd'

function Wait-Http([string] $Url, [int] $Attempts = 80) {
  foreach ($attempt in 1..$Attempts) {
    try { if ((Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { return } } catch { }
    Start-Sleep -Milliseconds 500
  }
  throw "Timed out waiting for $Url"
}

function Assert-PortAvailable([int] $Port, [string] $Name) {
  $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($listener) { throw "$Name port $Port is already in use by process $($listener.OwningProcess). Refusing to target an unknown server." }
}

function Stop-StartedProcessTree($Process) {
  if ($null -eq $Process -or $Process.HasExited) { return }
  # cmd.exe owns dotnet/npm descendants. /T limits cleanup to this runner's own tree.
  & taskkill.exe /PID $Process.Id /T /F 2>$null | Out-Null
}

function Assert-PlaywrightChromium {
  Push-Location (Join-Path $root 'frontend')
  try {
    $browserPath = (& node -e "process.stdout.write(require('@playwright/test').chromium.executablePath())").Trim()
    $nodeExitCode = $LASTEXITCODE
  } finally { Pop-Location }

  if ($nodeExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($browserPath) -or -not (Test-Path $browserPath)) {
    throw 'Playwright Chromium is not installed. Run: Set-Location frontend; npx playwright install chromium; then return to the project root and rerun Scripts\\Run-BrowserSmoke.ps1.'
  }
}

try {
  Write-Host 'SMOKE: Checking the disposable PostgreSQL container...'
  if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker Desktop CLI is required.' }
  if ((docker info 2>$null) -eq $null) { throw 'Docker Desktop is not running.' }
  $containerInfo = docker ps --filter "name=^/$container$" --format '{{.Names}} {{.Ports}}'
  if ($containerInfo -notmatch "^$container .*55439->5432") {
    throw "Refusing to continue: expected reusable PostgreSQL 17 container '$container' mapped to 127.0.0.1:55439."
  }
  $password = (docker inspect $container --format '{{range .Config.Env}}{{println .}}{{end}}' |
    Where-Object { $_ -like 'POSTGRES_PASSWORD=*' } | ForEach-Object { $_.Substring(18) })
  if ([string]::IsNullOrWhiteSpace($password)) { throw 'Disposable container has no usable PostgreSQL password.' }
  if (-not (Test-Path $playwright)) { throw "Playwright is not installed at $playwright. Run npm install in frontend first." }
  Assert-PlaywrightChromium

  New-Item -ItemType Directory -Force -Path $runDir | Out-Null
  Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $runDir 'playwright.log'), (Join-Path $runDir 'playwright.error.log'), (Join-Path $runDir 'failure.log')
  Write-Host 'SMOKE: Creating the disposable browser-test database...'
  docker exec $container psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS $database WITH (FORCE);" | Out-Null
  docker exec $container psql -U postgres -d postgres -c "CREATE DATABASE $database;" | Out-Null
  $connection = "Host=127.0.0.1;Port=55439;Database=$database;Username=postgres;Password=$password"
  $adminEmail = 'browser-smoke-admin@example.test'; $adminPassword = 'BrowserSmoke_2026!'; $staffEmail = 'browser-smoke-staff@example.test'; $staffPassword = 'BrowserSmokeStaff_2026!'
  $apiVars = @{
    'INVENTSTOCK_ConnectionStrings__DefaultConnection' = $connection; 'INVENTSTOCK_Database__MigrateOnStartup' = 'true'
    'INVENTSTOCK_Seed__AdminEmail' = $adminEmail; 'INVENTSTOCK_Seed__AdminFullName' = 'Browser Smoke Admin'; 'INVENTSTOCK_Seed__AdminPassword' = $adminPassword; 'INVENTSTOCK_Seed__IncludeDemoData' = 'true'
    'INVENTSTOCK_Jwt__SigningKey' = 'BrowserSmokeOnlyJwtKey_2026_DoNotUseOutsideDisposableTests'; 'INVENTSTOCK_TransactionRelease__Enabled' = 'true'
    'INVENTSTOCK_Cors__AllowedOrigins__0' = $webUrl; 'ASPNETCORE_ENVIRONMENT' = 'Development'; 'ASPNETCORE_URLS' = $apiUrl
  }
  Assert-PortAvailable 7057 'Disposable API'
  Assert-PortAvailable 3107 'Disposable frontend'
  Write-Host 'SMOKE: Starting the disposable API and applying its test migrations...'
  $apiCommand = (($apiVars.GetEnumerator() | ForEach-Object { "set `"$($_.Key)=$($_.Value)`"" }) -join ' && ') + ' && dotnet run --no-launch-profile'
  $api = Start-Process cmd.exe -ArgumentList '/c', $apiCommand -WorkingDirectory (Join-Path $root 'backend\InventStock\InventStock.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir 'api.log') -RedirectStandardError (Join-Path $runDir 'api.error.log')
  Wait-Http "$apiUrl/health"

  $login = Invoke-RestMethod "$apiUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email=$adminEmail; password=$adminPassword } | ConvertTo-Json)
  $headers = @{ Authorization = "Bearer $($login.accessToken)" }
  Invoke-RestMethod "$apiUrl/api/users" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{ email=$staffEmail; fullName='Browser Smoke Staff'; password=$staffPassword; role='Staff' } | ConvertTo-Json) | Out-Null

  Write-Host 'SMOKE: Starting the disposable frontend...'
  $webCommand = "set `"NEXT_PUBLIC_API_BASE_URL=$apiUrl`" && set `"NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED=true`" && set `"NEXT_DIST_DIR=.next-browser-smoke`" && npm run dev -- --hostname 127.0.0.1 --port 3107"
  $web = Start-Process cmd.exe -ArgumentList '/c', $webCommand -WorkingDirectory (Join-Path $root 'frontend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir 'frontend.log') -RedirectStandardError (Join-Path $runDir 'frontend.error.log')
  Wait-Http "$webUrl/login"

  Push-Location (Join-Path $root 'frontend')
  try {
    $env:PLAYWRIGHT_BASE_URL = $webUrl; $env:PLAYWRIGHT_ADMIN_EMAIL = $adminEmail; $env:PLAYWRIGHT_ADMIN_PASSWORD = $adminPassword
    $env:PLAYWRIGHT_STAFF_EMAIL = $staffEmail; $env:PLAYWRIGHT_STAFF_PASSWORD = $staffPassword
    $env:PLAYWRIGHT_RETURN_PART = 'BRK-1001'; $env:PLAYWRIGHT_LEGACY_RETURN_PART = 'BRK-1001'
    Write-Host 'SMOKE: Running the Playwright transaction pack (test names will print below)...'
    # The local Playwright launcher is a Windows batch file. Calling it through
    # cmd /c CALL prevents its control-flow shim from ending this PowerShell
    # script before Tee can create the durable, live log.
    # Keep the repository config's HTML `open: never` setting. Overriding the
    # reporter here makes Playwright host its report indefinitely on port 9323.
    $playwrightCommand = "call `"$playwright`" test"
    & $env:ComSpec /d /s /c $playwrightCommand 2>&1 | Tee-Object -FilePath (Join-Path $runDir 'playwright.log')
    $playwrightExitCode = $LASTEXITCODE
    if ($playwrightExitCode -ne 0) { throw "Playwright failed with exit code $playwrightExitCode." }
  } finally { Pop-Location }
  $result = $true
} catch {
  $_ | Tee-Object (Join-Path $runDir 'failure.log')
} finally {
  Stop-StartedProcessTree $web
  Stop-StartedProcessTree $api
  # This literal guard makes it impossible for a future edit to drop an arbitrary database.
  if ($database -ne 'inventstock_browser_smoke') { throw 'Safety guard rejected database cleanup.' }
  docker exec $container psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS inventstock_browser_smoke WITH (FORCE);" | Out-Null
}
if ($result) { Write-Host 'PASS: disposable browser smoke completed.' -ForegroundColor Green; exit 0 }
Write-Host "FAIL: see $runDir" -ForegroundColor Red; exit 1
