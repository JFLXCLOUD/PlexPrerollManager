# PlexPrerollManager Release Preparation Script
# This script prepares the project for GitHub release

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [string]$ReleaseNotes = "",

    [switch]$SkipBuild,

    [switch]$SkipTests
)

Write-Host "🚀 PlexPrerollManager Release Preparation" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow

# Function to write colored output
function Write-Status { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Blue }
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }

# Clean previous builds
Write-Status "Cleaning previous builds..."
if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
if (Test-Path "release") { Remove-Item "release" -Recurse -Force }
Write-Success "Clean completed"

# Update version in project file
Write-Status "Updating version in project file..."
$csprojPath = "PlexPrerollManager.csproj"
$csprojContent = Get-Content $csprojPath -Raw

# Update version if it exists, otherwise add it
if ($csprojContent -match '<Version>.*?</Version>') {
    $csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
} else {
    $csprojContent = $csprojContent -replace '<PropertyGroup>', "<PropertyGroup>`n    <Version>$Version</Version>"
}

$csprojContent | Set-Content $csprojPath -Encoding UTF8
Write-Success "Version updated to $Version"

# Build the application
if (-not $SkipBuild) {
    Write-Status "Building application..."
    & dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed!"
        exit 1
    }
    Write-Success "Build completed successfully"
}

# Run tests
if (-not $SkipTests) {
    Write-Status "Running tests..."
    & dotnet test
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed!"
        exit 1
    }
    Write-Success "All tests passed"
}

# Publish self-contained executable
Write-Status "Creating self-contained executable..."
& dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=false -o "release"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
    exit 1
}
Write-Success "Self-contained executable created"

# Create release archive
Write-Status "Creating release archive..."
$releaseDir = "PlexPrerollManager-v$Version"
$archiveName = "$releaseDir.zip"

if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $releaseDir | Out-Null

# Copy files to release directory
Copy-Item "release\*" $releaseDir -Recurse
Copy-Item "appsettings.json" $releaseDir
Copy-Item "README.md" $releaseDir
Copy-Item "install.ps1" $releaseDir
Copy-Item "install-simple.bat" $releaseDir
Copy-Item "install.bat" $releaseDir
Copy-Item "install-preroll-manager.ps1" $releaseDir

# Create ZIP archive
Compress-Archive -Path $releaseDir -DestinationPath $archiveName -Force
Write-Success "Release archive created: $archiveName"

# Generate release notes if not provided
if (-not $ReleaseNotes) {
    Write-Status "Generating release notes..."
    $ReleaseNotes = @"
# PlexPrerollManager v$Version

## What's New

### 🎬 Enhanced Upload Features
- **Bulk Upload**: Upload multiple videos at once with progress tracking
- **Real-time Progress Bars**: Visual progress indicators for uploads
- **Improved Error Handling**: Individual file failures don't stop bulk uploads

### 🔧 Technical Improvements
- **Better Platform Compatibility**: Fixed Windows-specific compilation warnings
- **Enhanced User Interface**: Modern, responsive design improvements
- **Improved Performance**: Optimized file handling and processing

## Installation

### Quick Install (One-Liner)
```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/YOUR_USERNAME/PlexPrerollManager/main/install.ps1'))
```

### Manual Installation
1. Download and extract the release archive
2. Run `install.bat` or `install.ps1` as Administrator
3. Open http://localhost:8089 in your browser

## Features
- ✅ Bulk video upload with progress tracking
- ✅ Advanced scheduling system
- ✅ Video thumbnail generation
- ✅ Windows service integration
- ✅ Modern web interface
- ✅ Real-time status monitoring

## System Requirements
- Windows 10/11
- .NET 9.0 or later
- FFmpeg (automatically installed)

---
**Full documentation available at: https://github.com/YOUR_USERNAME/PlexPrerollManager**
"@
}

# Save release notes
$ReleaseNotes | Out-File -FilePath "RELEASE_NOTES.md" -Encoding UTF8
Write-Success "Release notes generated"

# Create checksums
Write-Status "Generating checksums..."
$hash = Get-FileHash $archiveName -Algorithm SHA256
$checksum = "$($hash.Algorithm) checksum: $($hash.Hash)"
$checksum | Out-File -FilePath "$archiveName.sha256" -Encoding UTF8
Write-Success "Checksums generated"

# Display release information
Write-Host ""
Write-Host "🎉 Release Preparation Complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "📦 Release Archive: $archiveName" -ForegroundColor Cyan
Write-Host "🔐 Checksum: $archiveName.sha256" -ForegroundColor Cyan
Write-Host "📝 Release Notes: RELEASE_NOTES.md" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test the release archive on a clean Windows system"
Write-Host "2. Create a new GitHub release with the archive"
Write-Host "3. Update the install.ps1 script with your GitHub username"
Write-Host "4. Announce the release!"
Write-Host ""
Write-Host "📊 Release Summary:" -ForegroundColor Magenta
Write-Host "• Version: $Version"
Write-Host "• Archive Size: $([math]::Round((Get-Item $archiveName).Length / 1MB, 2)) MB"
Write-Host "• Files Included: $(Get-ChildItem $releaseDir -Recurse | Measure-Object | Select-Object -ExpandProperty Count)"
Write-Host ""

# List files in release
Write-Host "📁 Files in Release:" -ForegroundColor Blue
Get-ChildItem $releaseDir | ForEach-Object {
    Write-Host "  • $($_.Name)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Thank you for releasing PlexPrerollManager v$Version! 🚀" -ForegroundColor Cyan