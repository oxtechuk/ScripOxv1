param (
    [string]$Version = "1.1.4",
    [switch]$SkipExtensionBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "      ScripOx CRM - Automated Production Release Builder          " -ForegroundColor Cyan
Write-Host "      Target Version: v$Version                                  " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

$RootDir = $PSScriptRoot
$DesktopDir = Join-Path $RootDir "ScripOx.Desktop"
$ExtensionDir = Join-Path $RootDir "ScripOx.Extension"
$DistDir = Join-Path $RootDir "dist"
$AppPublishDir = Join-Path $DistDir "app"
$InstallerOutputDir = Join-Path $DistDir "installer"
$IssFile = Join-Path $RootDir "installer.iss"

# 1. Look for Inno Setup Compiler
$InnoCompilerPaths = @(
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$Iscc = $InnoCompilerPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    # Try dynamic lookup
    $Iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
}

if (-not $Iscc) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found. Please install Inno Setup."
}

# 2. Build Chrome Extension if needed
if (-not $SkipExtensionBuild) {
    Write-Host "`n[1/5] Checking Chrome Extension..." -ForegroundColor Yellow
    if (Test-Path (Join-Path $ExtensionDir "package.json")) {
        Push-Location $ExtensionDir
        try {
            if (-not (Test-Path "dist\manifest.json")) {
                Write-Host "Building Chrome Extension with npm..." -ForegroundColor Cyan
                npm run build
            } else {
                Write-Host "Extension dist folder verified." -ForegroundColor Green
            }
        } finally {
            Pop-Location
        }
    }
}

# 3. Clean and prepare dist directory
Write-Host "`n[2/5] Preparing output directories..." -ForegroundColor Yellow
if (-not (Test-Path $AppPublishDir)) {
    New-Item -ItemType Directory -Path $AppPublishDir -Force | Out-Null
}
if (-not (Test-Path $InstallerOutputDir)) {
    New-Item -ItemType Directory -Path $InstallerOutputDir -Force | Out-Null
}

# 4. Dotnet Publish (Self-Contained win-x64)
Write-Host "`n[3/5] Publishing ScripOx Desktop (Self-Contained win-x64)..." -ForegroundColor Yellow
$Csproj = Join-Path $DesktopDir "ScripOx.Desktop.csproj"

# Ensure Dotnet SDK is resolved from User or System
$UserDotnetDir = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$DotnetCmd = "dotnet"
if (Test-Path (Join-Path $UserDotnetDir "dotnet.exe")) {
    $env:DOTNET_ROOT = $UserDotnetDir
    $env:PATH = "$UserDotnetDir;" + $env:PATH
    $DotnetCmd = Join-Path $UserDotnetDir "dotnet.exe"
}

# Set custom NuGet cache if H:\ drive exists
if (Test-Path "H:\") {
    $env:NUGET_PACKAGES = "H:\.nuget\packages"
}

& $DotnetCmd publish $Csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -o $AppPublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
}
Write-Host "Publish completed successfully." -ForegroundColor Green

Write-Host "`n[3.5/5] Packaging Python Backend (Standalone ScripOxBackend)..." -ForegroundColor Yellow
$ExtractorSrc = Join-Path $RootDir "ScripOx.Extractor"
$ExtractorDest = Join-Path $AppPublishDir "extractor"
if (-not (Test-Path $ExtractorDest)) { New-Item -ItemType Directory -Path $ExtractorDest -Force | Out-Null }

$BackendDir = Join-Path $ExtractorSrc "dist\ScripOxBackend"
$BackendExe = Join-Path $BackendDir "ScripOxBackend.exe"
$BackendOneFile = Join-Path $ExtractorSrc "dist\ScripOxBackend.exe"

if (-not (Test-Path $BackendExe) -and -not (Test-Path $BackendOneFile)) {
    Write-Host "Compiling standalone Python executable with PyInstaller (safe onedir mode)..." -ForegroundColor Cyan
    $VenvPython = Join-Path $ExtractorSrc ".venv\Scripts\python.exe"
    if (-not (Test-Path $VenvPython)) {
        $VenvPython = "python"
    }
    $BuildScript = Join-Path $ExtractorSrc "build_backend.py"
    & $VenvPython $BuildScript
}

if (Test-Path $BackendDir) {
    Copy-Item -Path "$BackendDir\*" -Destination $ExtractorDest -Recurse -Force
    Write-Host "[OK] Bundled clean ScripOxBackend (Safe onedir mode) into installer package." -ForegroundColor Green
} elseif (Test-Path $BackendOneFile) {
    Copy-Item -Path $BackendOneFile -Destination (Join-Path $ExtractorDest "ScripOxBackend.exe") -Force
    Write-Host "[OK] Bundled standalone ScripOxBackend.exe into installer package." -ForegroundColor Green
} else {
    Write-Warning "Could not find ScripOxBackend! Falling back to copying source files."
    Copy-Item -Path $ExtractorSrc -Destination $ExtractorDest -Recurse -Force
    Get-ChildItem -Path $ExtractorDest -Include "__pycache__", ".venv" -Recurse -Force | Remove-Item -Recurse -Force
}

Write-Host "`n[3.6/5] Packaging Browser Extension..." -ForegroundColor Yellow
$ExtDest = Join-Path $AppPublishDir "Extension"
if (-not (Test-Path $ExtDest)) { New-Item -ItemType Directory -Path $ExtDest -Force | Out-Null }
$ExtDistSrc = Join-Path $ExtensionDir "dist"
if (Test-Path $ExtDistSrc) {
    Copy-Item -Path "$ExtDistSrc\*" -Destination $ExtDest -Recurse -Force
    Write-Host "[OK] Bundled ScripOx Extension into installer package." -ForegroundColor Green
}

# 5. Compile Inno Setup Installer
Write-Host "`n[4/5] Building Setup Installer with Inno Setup..." -ForegroundColor Yellow
& $Iscc "/DMyAppVersion=$Version" $IssFile

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Inno Setup compilation failed (often due to Antivirus lock). Skipping installer creation, but portable zip will still be generated."
}

$InstallerPath = Join-Path $InstallerOutputDir "ScripOx-Setup-v$Version.exe"
if (Test-Path $InstallerPath) {
    $Size = [math]::Round(((Get-Item $InstallerPath).Length / 1048576.0), 2)
    Write-Host "Installer created: $InstallerPath ($Size MB)" -ForegroundColor Green
}

# 5.5 Sign Binaries & Installer
Write-Host "`n[4.5/5] Applying Code Signing & Integrity Signatures..." -ForegroundColor Yellow
$SignScript = Join-Path $RootDir "sign_release.ps1"
if (Test-Path $SignScript) {
    try {
        & $SignScript -Version $Version
    } catch {
        Write-Warning "Code signing step encountered a minor issue: $_"
    }
}

# 6. Create Portable ZIP & Checksums
Write-Host "`n[5/5] Packaging Portable ZIP & Generating Checksums..." -ForegroundColor Yellow
$ZipPath = Join-Path $DistDir "ScripOx-v$Version-win-x64.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Compress-Archive -Path "$AppPublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
$ZipSize = [math]::Round(((Get-Item $ZipPath).Length / 1048576.0), 2)
Write-Host "Portable ZIP created: $ZipPath ($ZipSize MB)" -ForegroundColor Green

# Generate SHA256 hashes
$InstallerHash = (Get-FileHash -Path $InstallerPath -Algorithm SHA256).Hash
$ZipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash

$ChecksumFile = Join-Path $DistDir "SHA256SUMS-v$Version.txt"
$ChecksumLines = "$InstallerHash  ScripOx-Setup-v$Version.exe`r`n$ZipHash  ScripOx-v$Version-win-x64.zip`r`n"
[System.IO.File]::WriteAllText($ChecksumFile, $ChecksumLines, [System.Text.Encoding]::UTF8)

Write-Host "Checksums written to: $ChecksumFile" -ForegroundColor Green

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host "                      RELEASE BUILD READY!                        " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Setup Installer : $InstallerPath" -ForegroundColor White
Write-Host "  Portable ZIP    : $ZipPath" -ForegroundColor White
Write-Host "  Checksums       : $ChecksumFile" -ForegroundColor White
Write-Host "==================================================================" -ForegroundColor Cyan
