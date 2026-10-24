[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][uri]$PublicOrigin,
    [ValidateRange(1024, 65535)][int]$ApiPort = 5100,
    [ValidateRange(1024, 65535)][int]$FrontendPort = 3000,
    [ValidateRange(1, 65535)][int]$PostgresPort = 5432
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$response = Invoke-WebRequest -Uri ([uri]::new($PublicOrigin, '/health')) -UseBasicParsing -TimeoutSec 15
if ($response.StatusCode -ne 200) { throw "Public health check returned HTTP $($response.StatusCode)." }

foreach ($port in @($ApiPort,$FrontendPort,$PostgresPort)) {
    $listeners = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
    foreach ($listener in $listeners) {
        if ($listener.LocalAddress -notin @('127.0.0.1','::1')) { throw "Port $port is exposed on $($listener.LocalAddress); it must remain loopback-only." }
    }
}

Write-Host 'PASS: public HTTPS health is reachable and API/frontend/PostgreSQL listeners are loopback-only.'
