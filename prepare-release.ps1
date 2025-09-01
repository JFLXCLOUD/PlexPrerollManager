# PlexPrerollManager Release Preparation Script
# This script creates a clean release package with only essential files

param(
    [string]$Version = "v2.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "PlexPrerollManager Release Preparation" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host ""

# Define essential files to include in release
$essentialFiles = @(
    "README.md",
    "install.ps1",
    "install.bat",
    "PlexPrerollManager.csproj",
    "Program.cs",
    "dashboard.html",
    "scheduling-dashboard.html",
    "appsettings.json",
    ".gitignore"
)

# Create release directory
$releaseDir = "PlexPrerollManager-$Version"
if (Test-Path $releaseDir) {
    Write-Host "Removing existing release directory..." -ForegroundColor Gray
    Remove-Item $releaseDir -Recurse -Force
}

Write-Host "Creating release directory..." -ForegroundColor Gray
New-Item -ItemType Directory -Path $releaseDir | Out-Null

# Copy essential files
Write-Host "Copying essential files..." -ForegroundColor Gray
foreach ($file in $essentialFiles) {
    if (Test-Path $file) {
        Write-Host "  Copying: $file" -ForegroundColor Gray
        Copy-Item $file -Destination $releaseDir
    } else {
        Write-Host "  Warning: $file not found" -ForegroundColor Yellow
    }
}

# Create ZIP archive
$zipFile = "$releaseDir.zip"
if (Test-Path $zipFile) {
    Write-Host "Removing existing ZIP file..." -ForegroundColor Gray
    Remove-Item $zipFile -Force
}

Write-Host "Creating ZIP archive..." -ForegroundColor Gray
Compress-Archive -Path "$releaseDir\*" -DestinationPath $zipFile

# Generate checksum
Write-Host "Generating checksum..." -ForegroundColor Gray
$hash = Get-FileHash $zipFile -Algorithm SHA256
$hashFile = "$zipFile.sha256"
$hash.Hash.ToLower() | Out-File -FilePath $hashFile -Encoding UTF8

Write-Host ""
Write-Host "Release preparation complete!" -ForegroundColor Green
Write-Host "Files created:" -ForegroundColor Cyan
Write-Host "  Directory: $releaseDir\" -ForegroundColor White
Write-Host "  Archive: $zipFile" -ForegroundColor White
Write-Host "  Checksum: $hashFile" -ForegroundColor White
Write-Host ""
Write-Host "Release contents:" -ForegroundColor Cyan
Get-ChildItem $releaseDir | ForEach-Object {
    Write-Host "  File: $($_.Name)" -ForegroundColor White
}
Write-Host ""
Write-Host "Ready to upload to GitHub Releases!" -ForegroundColor Green