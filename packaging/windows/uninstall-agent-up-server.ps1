$ErrorActionPreference = "Stop"

$serviceName = "agent-up-server"
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($existing) {
    Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Write-Host "Agent-Up Server service removed."
} else {
    Write-Host "Agent-Up Server service is not installed."
}
