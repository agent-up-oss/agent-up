$ErrorActionPreference = "Stop"

$serviceName = "agent-up-server"
$displayName = "Agent-Up Server"
$root = Split-Path -Parent $PSScriptRoot
$serverExe = Join-Path $root "server\AgentUp.Server.exe"

if (-not (Test-Path $serverExe)) {
    throw "Server executable not found at $serverExe"
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
}

New-Service `
    -Name $serviceName `
    -DisplayName $displayName `
    -BinaryPathName "`"$serverExe`" --urls http://127.0.0.1:5000" `
    -StartupType Automatic `
    -Description "Local Agent-Up runtime authority for workspaces, processes, ports, diagnostics, and automation."

Start-Service -Name $serviceName
Write-Host "$displayName installed and started."
