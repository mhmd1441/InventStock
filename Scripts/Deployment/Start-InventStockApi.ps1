[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ApiRoot,
    [Parameter(Mandatory = $true)][string]$EnvironmentFile,
    [ValidateRange(1024, 65535)][int]$Port = 5100
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $EnvironmentFile -PathType Leaf)) { throw "Private API environment file not found: $EnvironmentFile" }
$apiExecutable = Join-Path $ApiRoot 'InventStock.Api.exe'
if (-not (Test-Path -LiteralPath $apiExecutable -PathType Leaf)) { throw "Published API executable not found: $apiExecutable" }

$settings = @{}
foreach ($line in Get-Content -LiteralPath $EnvironmentFile) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }
    $separator = $trimmed.IndexOf('=')
    if ($separator -lt 1) { throw 'Every non-comment API environment line must be NAME=VALUE.' }
    $settings[$trimmed.Substring(0, $separator).Trim()] = $trimmed.Substring($separator + 1)
}

$required = @(
    'INVENTSTOCK_ConnectionStrings__DefaultConnection',
    'INVENTSTOCK_Jwt__SigningKey',
    'INVENTSTOCK_Cors__AllowedOrigins__0'
)
foreach ($name in $required) {
    if (-not $settings.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($settings[$name])) { throw "Required API setting is missing: $name" }
}
foreach ($safeSetting in @('INVENTSTOCK_Seed__IncludeDemoData','INVENTSTOCK_Database__MigrateOnStartup','INVENTSTOCK_TransactionRelease__Enabled')) {
    if ($settings.ContainsKey($safeSetting) -and $settings[$safeSetting] -ine 'false') { throw "$safeSetting must be false in the initial production deployment." }
    $settings[$safeSetting] = 'false'
}

$settings['ASPNETCORE_ENVIRONMENT'] = 'Production'
$settings['ASPNETCORE_URLS'] = "http://127.0.0.1:$Port"
foreach ($entry in $settings.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process') }

Set-Location -LiteralPath $ApiRoot
& $apiExecutable
exit $LASTEXITCODE
