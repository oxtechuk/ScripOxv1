<#
.SYNOPSIS
    Automated Code Signing script for ScripOx CRM binaries and installer.
.DESCRIPTION
    Signs ScripOx.exe, ScripOxBackend.exe, and ScripOx-Setup installer using
    either an existing Code Signing PFX certificate or a freshly generated Ox Tech certificate.
.PARAMETER PfxPath
    Optional path to your official Code Signing PFX certificate.
.PARAMETER PfxPassword
    Password for the PFX certificate.
#>
param (
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$Version = "1.1.3"
)

$ErrorActionPreference = "Stop"
$RootDir = $PSScriptRoot
$TimestampServer = "http://timestamp.digicert.com"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "             ScripOx CRM - Authenticode Code Signer               " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

$AppExe = Join-Path $RootDir "dist\app\ScripOx.exe"
$BackendExe = Join-Path $RootDir "dist\app\extractor\ScripOxBackend.exe"
$InstallerExe = Join-Path $RootDir "dist\installer\ScripOx-Setup-v$Version.exe"

# 1. Resolve Certificate
$Cert = $null

if ($PfxPath -and (Test-Path $PfxPath)) {
    Write-Host "Loading certificate from PFX: $PfxPath" -ForegroundColor Cyan
    $SecurePass = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
    $Cert = Get-PfxCertificate -FilePath $PfxPath
} else {
    # Check if a certificate already exists in Cert:\CurrentUser\My
    $Cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -like "*Ox Tech*" } |
            Select-Object -First 1

    if (-not $Cert) {
        Write-Host "No existing Code Signing certificate found. Creating self-signed certificate for Ox Tech..." -ForegroundColor Yellow
        $Cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject "CN=Ox Tech, O=Ox Tech CRM, OU=Software Development, C=UK" `
            -KeySpec Signature `
            -KeyUsage DigitalSignature `
            -FriendlyName "Ox Tech Code Signing Certificate" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(5)

        # Export public certificate for clients
        $ExportDir = Join-Path $RootDir "certificates"
        if (-not (Test-Path $ExportDir)) { New-Item -ItemType Directory -Path $ExportDir -Force | Out-Null }
        $CerPath = Join-Path $ExportDir "OxTech_Public.cer"
        Export-Certificate -Cert $Cert -FilePath $CerPath | Out-Null
        Write-Host "Exported public root certificate to: $CerPath" -ForegroundColor Green
        Write-Host "(Clients can double-click this .cer file -> Install -> Trusted Root to whitelist Ox Tech on their machine)" -ForegroundColor DarkGray
    } else {
        Write-Host "Found existing certificate: $($Cert.Subject)" -ForegroundColor Green
    }
}

if (-not $Cert) {
    Write-Error "Could not obtain a code signing certificate."
}

# 2. Files to sign
$FilesToSign = @($AppExe, $BackendExe, $InstallerExe) | Where-Object { Test-Path $_ }

if ($FilesToSign.Count -eq 0) {
    Write-Warning "No target executable files found in dist/ to sign. Please run build_release.ps1 first."
    exit 0
}

Write-Host "`nSigning target executables..." -ForegroundColor Cyan

foreach ($File in $FilesToSign) {
    Write-Host "  -> Signing: $(Split-Path $File -Leaf)..." -NoNewline
    try {
        $sig = Set-AuthenticodeSignature -FilePath $File -Certificate $Cert -TimestampServer $TimestampServer -HashAlgorithm SHA256
        if ($sig.Status -eq "Valid") {
            Write-Host " [OK - Valid]" -ForegroundColor Green
        } else {
            Write-Host " [Signed - Status: $($sig.Status)]" -ForegroundColor Yellow
        }
    } catch {
        # Fallback without timestamp server if offline
        $sig = Set-AuthenticodeSignature -FilePath $File -Certificate $Cert -HashAlgorithm SHA256
        Write-Host " [OK - Offline Signed]" -ForegroundColor Yellow
    }
}

Write-Host "`nAll existing release binaries successfully signed!" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Cyan
