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

function Wait-Http([string] $Url, [int] $Attempts = 80) {
  foreach ($attempt in 1..$Attempts) {
    try { if ((Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { return } } catch { }
    Start-Sleep -Milliseconds 500
  }
  throw "Timed out waiting for $Url"
}

try {
  if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker Desktop CLI is required.' }
  if ((docker info 2>$null) -eq $null) { throw 'Docker Desktop is not running.' }
  $containerInfo = docker ps --filter "name=^/$container$" --format '{{.Names}} {{.Ports}}'
  if ($containerInfo -notmatch "^$container .*55439->5432") {
    throw "Refusing to continue: expected reusable PostgreSQL 17 container '$container' mapped to 127.0.0.1:55439."
  }
  $password = (docker inspect $container --format '{{range .Config.Env}}{{println .}}{{end}}' |
    Where-Object { $_ -like 'POSTGRES_PASSWORD=*' } | ForEach-Object { $_.Substring(18) })
  if ([string]::IsNullOrWhiteSpace($password)) { throw 'Disposable container has no usable PostgreSQL password.' }

  New-Item -ItemType Directory -Force -Path $runDir | Out-Null
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
  $apiCommand = (($apiVars.GetEnumerator() | ForEach-Object { "set `"$($_.Key)=$($_.Value)`"" }) -join ' && ') + ' && dotnet run --no-launch-profile'
  $api = Start-Process cmd.exe -ArgumentList '/c', $apiCommand -WorkingDirectory (Join-Path $root 'backend\InventStock\InventStock.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir 'api.log') -RedirectStandardError (Join-Path $runDir 'api.error.log')
  Wait-Http "$apiUrl/health"

  $login = Invoke-RestMethod "$apiUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email=$adminEmail; password=$adminPassword } | ConvertTo-Json)
  $headers = @{ Authorization = "Bearer $($login.accessToken)" }
  Invoke-RestMethod "$apiUrl/api/users" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{ email=$staffEmail; fullName='Browser Smoke Staff'; password=$staffPassword; role='Employee' } | ConvertTo-Json) | Out-Null

  $webCommand = "set `"NEXT_PUBLIC_API_BASE_URL=$apiUrl`" && set `"NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED=true`" && set `"NEXT_DIST_DIR=.next-browser-smoke`" && npm run dev -- --hostname 127.0.0.1 --port 3107"
  $web = Start-Process cmd.exe -ArgumentList '/c', $webCommand -WorkingDirectory (Join-Path $root 'frontend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDir 'frontend.log') -RedirectStandardError (Join-Path $runDir 'frontend.error.log')
  Wait-Http "$webUrl/login"

  Push-Location (Join-Path $root 'frontend')
  try {
    $env:PLAYWRIGHT_BASE_URL = $webUrl; $env:PLAYWRIGHT_ADMIN_EMAIL = $adminEmail; $env:PLAYWRIGHT_ADMIN_PASSWORD = $adminPassword
    $env:PLAYWRIGHT_STAFF_EMAIL = $staffEmail; $env:PLAYWRIGHT_STAFF_PASSWORD = $staffPassword
    $env:PLAYWRIGHT_RETURN_PART = 'BRK-1001'; $env:PLAYWRIGHT_LEGACY_RETURN_PART = 'BRK-1001'
    npx playwright test *>&1 | Tee-Object (Join-Path $runDir 'playwright.log')
    if ($LASTEXITCODE -ne 0) { throw "Playwright failed with exit code $LASTEXITCODE." }
  } finally { Pop-Location }
  $result = $true
} catch {
  $_ | Tee-Object (Join-Path $runDir 'failure.log')
} finally {
  if ($web) { Stop-Process -Id $web.Id -Force -ErrorAction SilentlyContinue }
  if ($api) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
  # This literal guard makes it impossible for a future edit to drop an arbitrary database.
  if ($database -ne 'inventstock_browser_smoke') { throw 'Safety guard rejected database cleanup.' }
  docker exec $container psql -U postgres -d postgres -c "DROP DATABASE IF EXISTS inventstock_browser_smoke WITH (FORCE);" | Out-Null
}
if ($result) { Write-Host 'PASS: disposable browser smoke completed.' -ForegroundColor Green; exit 0 }
Write-Host "FAIL: see $runDir" -ForegroundColor Red; exit 1
