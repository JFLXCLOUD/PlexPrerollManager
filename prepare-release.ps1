# PlexPrerollManager Release Preparation Script
# This script prepares the project for GitHub release

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [switch]$SkipBuild,
    [switch]$SkipTests
)

Write-Host "PlexPrerollManager Release Preparation v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Function to write colored output
function Write-Status { param($Message) Write-Host "INFO: $Message" -ForegroundColor Blue }
function Write-Success { param($Message) Write-Host "SUCCESS: $Message" -ForegroundColor Green }
function Write-Warning { param($Message) Write-Host "WARNING: $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "ERROR: $Message" -ForegroundColor Red }

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

# Simple version update - just replace existing version tag
$csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
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
if (Test-Path "release") { Remove-Item "release" -Recurse -Force }

# First try with single file, if that fails, try without
try {
    & dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=false -o "release" 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Single-file publish failed, trying multi-file publish..."
        & dotnet publish -c Release -r win-x64 --self-contained -o "release"
    }
} catch {
    Write-Warning "Advanced publish failed, trying basic publish..."
    & dotnet publish -c Release -o "release"
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
    exit 1
}

if (-not (Test-Path "release")) {
    Write-Error "Release directory was not created!"
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
Write-Status "Copying files to release directory..."
Copy-Item "release\*" $releaseDir -Recurse -ErrorAction SilentlyContinue

# Copy additional files if they exist
$filesToCopy = @(
    "appsettings.json",
    "README.md",
    "install.ps1",
    "install-simple.bat",
    "install.bat",
    "install-preroll-manager.ps1"
)

foreach ($file in $filesToCopy) {
    if (Test-Path $file) {
        Copy-Item $file $releaseDir -ErrorAction SilentlyContinue
    } else {
        Write-Warning "File not found: $file"
    }
}

# Create ZIP archive
Write-Status "Creating ZIP archive..."
try {
    if (Get-Command Compress-Archive -ErrorAction SilentlyContinue) {
        Compress-Archive -Path $releaseDir -DestinationPath $archiveName -Force
    } else {
        Write-Warning "Compress-Archive not available, using alternative method..."
        # Fallback for older PowerShell versions
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::CreateFromDirectory($releaseDir, $archiveName)
    }
    Write-Success "Release archive created: $archiveName"
} catch {
    Write-Error "Failed to create ZIP archive: $_"
    exit 1
}

# Create simple release notes
Write-Status "Creating release notes..."
$releaseNotes = @"
PlexPrerollManager v$Version Release Notes

NEW FEATURES:
- Bulk upload with progress tracking
- Real-time progress bars for uploads
- Enhanced error handling
- Improved platform compatibility

INSTALLATION:
1. Download PlexPrerollManager-v$Version.zip
2. Extract to a folder
3. Run install.bat as Administrator
4. Open http://localhost:8089

For detailed documentation, visit the GitHub repository.
"@

$releaseNotes | Out-File -FilePath "RELEASE_NOTES.md" -Encoding UTF8
Write-Success "Release notes created"

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