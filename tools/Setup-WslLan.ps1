param(
    [Parameter(Mandatory = $true)][string]$WslAddress,
    [Parameter(Mandatory = $true)][int]$Port
)
$ErrorActionPreference = 'Stop'
$rule = 'RL Survival WSL LAN TCP ' + $Port
$current = (netsh interface portproxy show v4tov4) -join "`n"
$mappingExists = $current -match "0\.0\.0\.0\s+$Port\s+$([regex]::Escape($WslAddress))\s+$Port"
$firewallExists = Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue
if ($mappingExists -and $firewallExists) { exit 0 }
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -WslAddress `"$WslAddress`" -Port $Port"
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait
    } catch {
        Write-Error 'Approve the Windows admin prompt to enable LAN access to the WSL relay.'
        exit 1
    }
    exit 0
}
if ($current -match "0\.0\.0\.0\s+$Port\s+\S+\s+$Port") { netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=$Port | Out-Null }
netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=$Port connectaddress=$WslAddress connectport=$Port | Out-Null
if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $Port -RemoteAddress LocalSubnet -Profile Any -Action Allow | Out-Null
}
