[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$FrontendRoot,
    [ValidateRange(1024, 65535)][int]$Port = 3000,
    [string]$NodeExecutable = 'node.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$server = Join-Path $FrontendRoot 'server.js'
if (-not (Test-Path -LiteralPath $server -PathType Leaf)) { throw "Next.js standalone server not found: $server" }
$node = Get-Command $NodeExecutable -ErrorAction Stop
$env:NODE_ENV = 'production'
$env:HOSTNAME = '127.0.0.1'
$env:PORT = $Port.ToString()
Set-Location -LiteralPath $FrontendRoot
& $node.Source $server
exit $LASTEXITCODE
