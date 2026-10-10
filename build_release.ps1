param (
    [string]$Version = "1.0.0",
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
    "C:\Users\Ox Tech\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$Iscc = $InnoCompilerPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $Iscc) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found. Please install Inno Setup 6."
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

# Ensure NuGet cache uses H:\ to avoid filling system drive C:
$env:NUGET_PACKAGES = "H:\.nuget\packages"

& dotnet publish $Csproj `
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

Write-Host "`n[3.5/5] Copying Python Backend (ScripOx.Extractor)..." -ForegroundColor Yellow
$ExtractorSrc = Join-Path $RootDir "ScripOx.Extractor"
$ExtractorDest = Join-Path $AppPublishDir "extractor"
if (Test-Path $ExtractorDest) { Remove-Item $ExtractorDest -Recurse -Force }
# Copy ignoring __pycache__ etc (simple copy for now)
Copy-Item -Path $ExtractorSrc -Destination $ExtractorDest -Recurse -Force
# Clean up pycache inside dist
Get-ChildItem -Path $ExtractorDest -Include "__pycache__" -Recurse -Force | Remove-Item -Recurse -Force

# 5. Compile Inno Setup Installer
Write-Host "`n[4/5] Building Setup Installer with Inno Setup..." -ForegroundColor Yellow
& $Iscc "/DMyAppVersion=$Version" $IssFile

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Inno Setup compilation failed (often due to Antivirus lock). Skipping installer creation, but portable zip will still be generated."
}

$InstallerPath = Join-Path $InstallerOutputDir "ScripOx-Setup-v$Version.exe"
if (Test-Path $InstallerPath) {
    $Size = [math]::Round(((Get-Item $InstallerPath).Length / 1MB), 2)
    Write-Host "Installer created: $InstallerPath ($Size MB)" -ForegroundColor Green
}

# 6. Create Portable ZIP & Checksums
Write-Host "`n[5/5] Packaging Portable ZIP & Generating Checksums..." -ForegroundColor Yellow
$ZipPath = Join-Path $DistDir "ScripOx-v$Version-win-x64.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Compress-Archive -Path "$AppPublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
$ZipSize = [math]::Round(((Get-Item $ZipPath).Length / 1MB), 2)
Write-Host "Portable ZIP created: $ZipPath ($ZipSize MB)" -ForegroundColor Green

# Generate SHA256 hashes
$InstallerHash = (Get-FileHash -Path $InstallerPath -Algorithm SHA256).Hash
$ZipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash

$ChecksumFile = Join-Path $DistDir "SHA256SUMS-v$Version.txt"
@"
$InstallerHash  ScripOx-Setup-v$Version.exe
$ZipHash  ScripOx-v$Version-win-x64.zip
"@ | Out-File -FilePath $ChecksumFile -Encoding utf8

Write-Host "Checksums written to: $ChecksumFile" -ForegroundColor Green

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host "                      RELEASE BUILD READY!                        " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Setup Installer : $InstallerPath" -ForegroundColor White
Write-Host "  Portable ZIP    : $ZipPath" -ForegroundColor White
Write-Host "  Checksums       : $ChecksumFile" -ForegroundColor White
Write-Host "==================================================================" -ForegroundColor Cyan
