# Smoke tests Windows binary signing using a temporary self-signed certificate.
# No Azure credentials required. The certificate is created fresh and discarded after each run.
param(
    [Parameter(Mandatory)][ValidateSet("payload","package")][string]$Target,
    [Parameter(Mandatory)][string]$Dir
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$cert = New-SelfSignedCertificate `
    -Subject "CN=Agent-Up Smoke Test" `
    -Type CodeSigning `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddDays(1)

$thumbprint = $cert.Thumbprint
Write-Host "Smoke test: created self-signed cert $thumbprint"

try {
    $signtool = Get-Item "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" `
        -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $signtool) {
        throw "signtool.exe not found under Windows Kits — is the Windows SDK installed on this runner?"
    }
    Write-Host "Using: $signtool"

    $filters = if ($Target -eq "payload") { @("*.exe", "*.dll") } else { @("*.exe", "*.msi") }
    $files = $filters |
        ForEach-Object { Get-ChildItem -Path $Dir -Filter $_ -Recurse -ErrorAction SilentlyContinue } |
        Where-Object { $_ -ne $null }

    if (-not $files) {
        Write-Warning "Smoke test: no signable files found in $Dir with filters $($filters -join ', ')"
        exit 0
    }

    foreach ($file in $files) {
        Write-Host "Smoke test: signing $($file.FullName)"
        & $signtool sign /sha1 $thumbprint /fd SHA256 "$($file.FullName)"
        if ($LASTEXITCODE -ne 0) { throw "signtool sign failed for $($file.FullName)" }
    }

    Write-Host "Smoke test: Windows signing complete ($($files.Count) file(s) signed)"
} finally {
    Remove-Item -Path "Cert:\CurrentUser\My\$thumbprint" -Force -ErrorAction SilentlyContinue
}
