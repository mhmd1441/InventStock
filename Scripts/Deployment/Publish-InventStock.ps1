[CmdletBinding()]
param(
    [string]$SourceRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [Parameter(Mandatory = $true)][string]$ReleasesRoot,
    [Parameter(Mandatory = $true)][uri]$PublicOrigin
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PublicOrigin.Scheme -ne 'https' -or -not [string]::IsNullOrEmpty($PublicOrigin.AbsolutePath.Trim('/'))) {
    throw 'PublicOrigin must be an HTTPS origin without a path, for example https://inventstock.local:8443.'
}

$releaseName = Get-Date -Format 'yyyyMMdd-HHmmss'
$releaseRoot = Join-Path ([IO.Path]::GetFullPath($ReleasesRoot)) $releaseName
if (Test-Path -LiteralPath $releaseRoot) { throw "Release directory already exists: $releaseRoot" }
$apiOutput = Join-Path $releaseRoot 'api'
$frontendOutput = Join-Path $releaseRoot 'frontend'
New-Item -ItemType Directory -Path $apiOutput,$frontendOutput | Out-Null

$apiProject = Join-Path $SourceRoot 'backend\InventStock\InventStock.Api\InventStock.Api.csproj'
$frontendSource = Join-Path $SourceRoot 'frontend'
dotnet publish $apiProject -c Release --no-restore -o $apiOutput
if ($LASTEXITCODE -ne 0) { throw 'API publish failed.' }

Push-Location $frontendSource
$oldApi = $env:NEXT_PUBLIC_API_BASE_URL
$oldRelease = $env:NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED
try {
    $env:NEXT_PUBLIC_API_BASE_URL = $PublicOrigin.GetLeftPart([UriPartial]::Authority)
    $env:NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED = 'false'
    npm ci
    if ($LASTEXITCODE -ne 0) { throw 'Frontend npm ci failed.' }
    npm run build -- --webpack
    if ($LASTEXITCODE -ne 0) { throw 'Frontend production build failed.' }

    Copy-Item -Path (Join-Path $frontendSource '.next\standalone\*') -Destination $frontendOutput -Recurse -Force
    $staticTarget = Join-Path $frontendOutput '.next\static'
    New-Item -ItemType Directory -Force -Path $staticTarget | Out-Null
    Copy-Item -Path (Join-Path $frontendSource '.next\static\*') -Destination $staticTarget -Recurse -Force
    $publicSource = Join-Path $frontendSource 'public'
    if (Test-Path -LiteralPath $publicSource) { Copy-Item -LiteralPath $publicSource -Destination (Join-Path $frontendOutput 'public') -Recurse -Force }
}
finally {
    $env:NEXT_PUBLIC_API_BASE_URL = $oldApi
    $env:NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED = $oldRelease
    Pop-Location
}

Write-Host "Release created without activation: $releaseRoot"
Write-Host 'Transaction routes are compiled OFF. Review, back up, migrate explicitly, then update the Current pointer yourself.'
