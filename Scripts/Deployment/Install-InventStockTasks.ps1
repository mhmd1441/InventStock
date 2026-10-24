[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$CurrentRoot,
    [Parameter(Mandatory = $true)][string]$EnvironmentFile,
    [Parameter(Mandatory = $true)][string]$CaddyExecutable,
    [Parameter(Mandatory = $true)][string]$Caddyfile,
    [Parameter(Mandatory = $true)][string]$CaddyDataRoot,
    [Parameter(Mandatory = $true)][string]$ServiceAccount,
    [ValidateRange(1024, 65535)][int]$PublicPort = 8443,
    [ValidateRange(1024, 65535)][int]$PostgresPort = 5432
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this installer from an elevated PowerShell window.' }

$scriptsRoot = $PSScriptRoot
$apiScript = Join-Path $scriptsRoot 'Start-InventStockApi.ps1'
$frontendScript = Join-Path $scriptsRoot 'Start-InventStockFrontend.ps1'
$gatewayScript = Join-Path $scriptsRoot 'Start-InventStockGateway.ps1'
foreach ($path in @($EnvironmentFile,$CaddyExecutable,$Caddyfile,$apiScript,$frontendScript,$gatewayScript)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required deployment path does not exist: $path" }
}

$credential = Get-Credential -UserName $ServiceAccount -Message 'Dedicated InventStock task account credentials'
$password = $credential.GetNetworkCredential().Password
$powerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -RestartCount 10 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -StartWhenAvailable
$tasks = @(
    @{ Name='InventStock API'; Arguments="-NoProfile -ExecutionPolicy Bypass -File `"$apiScript`" -ApiRoot `"$CurrentRoot\api`" -EnvironmentFile `"$EnvironmentFile`"" },
    @{ Name='InventStock Frontend'; Arguments="-NoProfile -ExecutionPolicy Bypass -File `"$frontendScript`" -FrontendRoot `"$CurrentRoot\frontend`"" },
    @{ Name='InventStock HTTPS Gateway'; Arguments="-NoProfile -ExecutionPolicy Bypass -File `"$gatewayScript`" -CaddyExecutable `"$CaddyExecutable`" -Caddyfile `"$Caddyfile`" -DataRoot `"$CaddyDataRoot`"" }
)

try {
    foreach ($task in $tasks) {
        $action = New-ScheduledTaskAction -Execute $powerShell -Argument $task.Arguments
        if ($PSCmdlet.ShouldProcess($task.Name, 'Register startup task')) {
            Register-ScheduledTask -TaskName $task.Name -Action $action -Trigger $trigger -Settings $settings -User $credential.UserName -Password $password -Force | Out-Null
        }
    }
    if ($PSCmdlet.ShouldProcess('Windows Firewall', "Allow private LocalSubnet TCP $PublicPort and block inbound PostgreSQL TCP $PostgresPort")) {
        Remove-NetFirewallRule -DisplayName 'InventStock LAN HTTPS' -ErrorAction SilentlyContinue
        Remove-NetFirewallRule -DisplayName 'InventStock Block PostgreSQL LAN' -ErrorAction SilentlyContinue
        New-NetFirewallRule -DisplayName 'InventStock LAN HTTPS' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $PublicPort -Profile Private -RemoteAddress LocalSubnet | Out-Null
        New-NetFirewallRule -DisplayName 'InventStock Block PostgreSQL LAN' -Direction Inbound -Action Block -Protocol TCP -LocalPort $PostgresPort -Profile Any -RemoteAddress Any | Out-Null
    }
}
finally { $password = $null; $credential = $null }

Write-Host 'Startup tasks and firewall rules installed. Reboot testing is still required.'
