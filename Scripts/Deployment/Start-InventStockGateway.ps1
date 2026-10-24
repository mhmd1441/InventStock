[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CaddyExecutable,
    [Parameter(Mandatory = $true)][string]$Caddyfile,
    [Parameter(Mandatory = $true)][string]$DataRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CaddyExecutable -PathType Leaf)) { throw "Caddy executable not found: $CaddyExecutable" }
if (-not (Test-Path -LiteralPath $Caddyfile -PathType Leaf)) { throw "Caddyfile not found: $Caddyfile" }
New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
$env:CADDY_DATA_DIR = $DataRoot
$env:CADDY_CONFIG_DIR = $DataRoot
& $CaddyExecutable run --config $Caddyfile --adapter caddyfile
exit $LASTEXITCODE
