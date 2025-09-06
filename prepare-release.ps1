#Requires -Version 5.1

<#
.SYNOPSIS
    Nexroll Release Preparation Script
.DESCRIPTION
    Prepares a clean, professional release package for distribution
.PARAMETER OutputPath
    Output directory for the release package
.PARAMETER Version
    Version number for the release
#>

param(
    [string]$OutputPath = ".\Release",
    [string]$Version = "2.2.0",
    [switch]$Confirm = $false
)

$ErrorActionPreference = "Stop"

function Write-Header {
    Clear-Host
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host "                          Nexroll                             " -ForegroundColor Cyan
    Write-Host "                   Release Preparation                        " -ForegroundColor Cyan
    Write-Host "                        Version $Version                           " -ForegroundColor Cyan
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host "[STEP] $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function New-ReleaseDirectory {
    Write-Step "Creating release directory..."

    if (Test-Path $OutputPath) {
        try {
            Remove-Item -Path $OutputPath -Recurse -Force
        } catch {
            Write-Host "[WARNING] Could not remove existing release directory: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "[INFO] Please close any applications using files in the Release directory and try again." -ForegroundColor Yellow
            throw "Release directory is locked by another process"
        }
    }

    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Success "Release directory created: $OutputPath"
}

function Publish-Application {
    Write-Step "Publishing application..."

    $publishPath = Join-Path $OutputPath "publish"
    dotnet publish Nexroll.csproj -c Release -o $publishPath --self-contained false

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish application"
        exit 1
    }

    Write-Success "Application published to $publishPath"
}

function Copy-SourceFiles {
    Write-Step "Copying published application..."

    $publishPath = Join-Path $OutputPath "publish"

    # Copy published files
    Get-ChildItem -Path $publishPath | Copy-Item -Destination $OutputPath -Recurse -Force

    # Clean up publish folder
    Remove-Item $publishPath -Recurse -Force

    # Copy additional files
    $includeFiles = @(
        "README.md",
        "RELEASE_NOTES.md",
        "QUICK_START.md",
        "INSTALLATION.md"
    )

    foreach ($file in $includeFiles) {
        if (Test-Path $file) {
            Copy-Item -Path $file -Destination $OutputPath -Force
        }
    }

    # Copy HTML files from web folder
    $htmlFiles = @("dashboard.html", "oauth.html", "scheduling-dashboard.html")
    foreach ($file in $htmlFiles) {
        $sourcePath = Join-Path "web" $file
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $OutputPath -Force
        }
    }

    Write-Success "Published application and files copied"
}

function Copy-InstallationFiles {
    Write-Step "Copying installation files..."
    
    # Installation scripts
    Copy-Item -Path "install.ps1" -Destination $OutputPath -Force
    Copy-Item -Path "uninstall.ps1" -Destination $OutputPath -Force
    Copy-Item -Path "INSTALL.bat" -Destination $OutputPath -Force
    Copy-Item -Path "INSTALLATION.md" -Destination $OutputPath -Force
    
    Write-Success "Installation files copied"
}

function New-ReleaseNotes {
    Write-Step "Creating release notes..."
    
    $releaseNotes = @"
# Plex Preroll Manager v$Version Release Notes

## Major Fixes and Improvements

### RESOLVED ISSUES

#### 1. Plex.tv Authentication - FIXED
- Fixed PIN-based OAuth flow that was failing with "Code not found or expired"
- Corrected PIN ID vs PIN CODE usage in API calls
- Improved authentication polling and timeout handling
- Added proper PIN expiration detection and cleanup

#### 2. Plex Server Integration - IMPLEMENTED
- Added comprehensive Plex API service with multiple preroll setting methods
- Implemented proper Plex server connectivity testing
- Added support for CinemaTrailersPrerollID and library section prerolls
- Integrated preroll management with category activation

#### 3. Configuration Persistence - IMPLEMENTED
- Authentication settings now properly saved to appsettings.json
- Added ConfigurationService for persistent configuration management
- Plex.tv tokens automatically saved after successful authentication
- Support for all authentication methods (token, credentials, API key, Plex.tv)

#### 4. Missing Features - IMPLEMENTED
- Scheduling System: Full implementation with OneTime, Daily, Weekly, Monthly, Yearly schedules
- Backup/Restore: Complete backup system with ZIP-based backups including metadata
- Usage Statistics: Enhanced statistics tracking and reporting
- Error Handling: Comprehensive error handling and user-friendly messages

#### 5. Security Enhancements - IMPLEMENTED
- Input validation with data annotations
- Path traversal protection for file operations
- File type validation for uploads
- Secure token storage using Windows DPAPI
- File name sanitization to prevent security issues

#### 6. Frontend Improvements - ENHANCED
- Improved OAuth callback handling with better error diagnostics
- Enhanced PIN expiration handling
- Better loading states and error feedback
- Multiple parameter source detection for authentication callbacks

## NEW FEATURES

### Service Architecture
- ConfigurationService: Persistent configuration management
- PlexApiService: Comprehensive Plex server communication
- SchedulingService: Automated preroll scheduling
- BackupService: Full backup and restore capabilities

### Enhanced API Endpoints
- /api/plex/auth/start - Start Plex.tv authentication
- /api/plex/auth/check - Check authentication status
- /api/plex/auth/complete - Complete authentication
- /api/plex/auth/diagnose - Authentication diagnostics
- /api/schedules - Schedule management (GET/POST/DELETE)
- /api/backup - Create backups
- /api/backup/restore - Restore from backup
- /api/backups - List available backups

### Professional Installation
- Enhanced PowerShell installer with progress indicators
- Automatic Windows service creation and configuration
- Firewall rule configuration
- Desktop and Start Menu shortcuts
- Comprehensive uninstaller
- Clean build artifact removal

## CURRENT STATUS

FULLY FUNCTIONAL:
- Core Application: Perfect (localhost:8089 loads and works flawlessly)
- Token Authentication: Working (secure server access with tokens)
- Category Management: Working (activate/deactivate categories)
- Plex Server Integration: Implemented with token-based authentication
- Scheduling System: Fully implemented and functional
- Backup/Restore: Complete implementation
- Usage Statistics: Working with charts and analytics
- One-Click Installation: Professional automated setup
- Error Handling: Comprehensive and user-friendly
- Security: Input validation and token protection

## INSTALLATION

### Option 1: One-Click Installation (Recommended)
```powershell
powershell -ExecutionPolicy Bypass -Command "Invoke-Expression (Invoke-RestMethod 'https://raw.githubusercontent.com/JFLXCLOUD/Nexroll/main/install.ps1')"
```

### Option 2: Manual Installation
1. Download the release package from GitHub
2. Extract to any folder
3. Right-click `INSTALL.bat` and select "Run as administrator"
4. Follow installation prompts
5. Access web interface at http://localhost:8089

## NEXT STEPS

1. Configure Plex Token: Enter your Plex token and server URL
2. Upload Prerolls: Add video files and organize them into categories
3. Activate Category: Choose which category should play before movies
4. Create Schedules: Set up automated category switching (optional)
5. Monitor Usage: View statistics and performance metrics
"@

    $releaseNotes | Out-File -FilePath (Join-Path $OutputPath "RELEASE_NOTES.md") -Encoding UTF8
    Write-Success "Release notes created"
}

function New-QuickStartGuide {
    Write-Step "Creating quick start guide..."
    
    $quickStart = @"
# Nexroll Quick Start Guide

## Installation (2 minutes)

1. Right-click INSTALL.bat and select "Run as administrator"
2. Press Y to confirm installation
3. Wait for installation to complete
4. Open http://localhost:8089 in your browser

## First Setup (5 minutes)

### Step 1: Authenticate with Plex.tv
1. Click "Start Authentication" in the Plex.tv section
2. Authorize Nexroll in the browser tab that opens
3. Return to the dashboard - authentication will complete automatically

### Step 2: Upload Prerolls
1. Go to "Upload New Preroll" section
2. Select video files (MP4, MKV, etc.)
3. Choose or create a category (e.g., "Halloween", "Christmas")
4. Click "Upload Video"

### Step 3: Activate Category
1. Find your category in the "Categories" section
2. Click "Activate" on the desired category
3. Verify the category shows as "Active"

## You're Done!

Your Plex server will now use prerolls from the active category before movies.

## Optional: Create Schedules

1. Go to "Quick Schedule" section
2. Set schedule name (e.g., "Halloween Schedule")
3. Choose category and dates
4. Select schedule type (OneTime, Daily, Weekly, etc.)
5. Click "Create Schedule"

## Troubleshooting

- Web interface not loading? Check Windows Firewall for port 8089
- Plex not connecting? Verify Plex server URL in settings
- Authentication failing? Try the authentication process again
- Need help? Check INSTALLATION.md for detailed troubleshooting

Support: https://github.com/JFLXCLOUD/Nexroll/issues
"@

    $quickStart | Out-File -FilePath (Join-Path $OutputPath "QUICK_START.md") -Encoding UTF8
    Write-Success "Quick start guide created"
}

function Remove-DevelopmentFiles {
    Write-Step "Removing development and unnecessary files..."

    # Files and patterns to exclude from release
    $excludePatterns = @(
        "bin", "obj", "publish", "publish-test",
        "*.tmp", "*.log", "*.cache",
        ".vs", ".vscode",
        "corrected_*.html", "corrected_*.txt",
        "install-simple.bat", "build-installer*.bat",
        "installer.iss", "delete", "start", "stop",
        "prepare-release.ps1", "Nexroll_v*.zip"
    )

    foreach ($pattern in $excludePatterns) {
        Get-ChildItem -Path $OutputPath -Filter $pattern -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }

    # Remove specific files that shouldn't be in release
    $excludeFiles = @(
        "AssemblyInfo.cs",
        "IntegrationExamples.cs",
        "USAGE_STATISTICS_BACKEND_README.md",
        "USAGE_STATISTICS_IMPLEMENTATION.md",
        "PROJECT_STATUS.md"
    )

    foreach ($file in $excludeFiles) {
        $filePath = Join-Path $OutputPath $file
        if (Test-Path $filePath) {
            Remove-Item -Path $filePath -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Success "Development and unnecessary files removed"
}

function New-ReleasePackage {
    Write-Step "Creating release package..."

    # Create packages directory if it doesn't exist
    $packagesDir = "packages"
    if (-not (Test-Path $packagesDir)) {
        New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null
    }

    $packageName = "Nexroll_v$Version"
    $packagePath = Join-Path $packagesDir "$packageName.zip"

    if (Test-Path $packagePath) {
        Remove-Item -Path $packagePath -Force
    }

    # Create ZIP package
    Compress-Archive -Path "$OutputPath\*" -DestinationPath $packagePath -CompressionLevel Optimal

    Write-Success "Release package created: $packagePath"

    # Show package contents
    Write-Host ""
    Write-Host "Package Contents:" -ForegroundColor Cyan
    Get-ChildItem -Path $OutputPath | ForEach-Object {
        if ($_.PSIsContainer) {
            Write-Host "   [DIR]  $($_.Name)\" -ForegroundColor Blue
        } else {
            $size = if ($_.Length -gt 1MB) { "{0:N1} MB" -f ($_.Length / 1MB) } else { "{0:N0} KB" -f ($_.Length / 1KB) }
            Write-Host "   [FILE] $($_.Name) ($size)" -ForegroundColor White
        }
    }
}

# Main release preparation process
try {
    Write-Header
    
    Write-Host "Release Parameters:" -ForegroundColor Cyan
    Write-Host "  Version:      $Version" -ForegroundColor White
    Write-Host "  Output Path:  $OutputPath" -ForegroundColor White
    Write-Host ""
    
    if ($Confirm) {
        $confirm = Read-Host "Prepare release package? (Y/N)"
        if ($confirm -notmatch "^[Yy]") {
            Write-Host "Release preparation cancelled" -ForegroundColor Yellow
            exit 0
        }
    }
    
    Write-Host ""
    
    # Preparation steps
    New-ReleaseDirectory
    Publish-Application
    Copy-SourceFiles
    Copy-InstallationFiles
    New-ReleaseNotes
    New-QuickStartGuide
    Remove-DevelopmentFiles
    New-ReleasePackage
    
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "                   Release Package Ready!                     " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "Release Information:" -ForegroundColor Cyan
    Write-Host "   Version:        $Version" -ForegroundColor White
    Write-Host "   Package:        $packagePath" -ForegroundColor White
    Write-Host "   Source Folder:  $OutputPath" -ForegroundColor White
    Write-Host "   Distribution:   Ready for GitHub Releases" -ForegroundColor White
    Write-Host ""
    
    Write-Host "Package Includes:" -ForegroundColor Cyan
    Write-Host "   [OK] Complete application executable" -ForegroundColor White
    Write-Host "   [OK] Professional installer (install.ps1)" -ForegroundColor White
    Write-Host "   [OK] Easy launcher (INSTALL.bat)" -ForegroundColor White
    Write-Host "   [OK] Comprehensive uninstaller" -ForegroundColor White
    Write-Host "   [OK] Installation documentation" -ForegroundColor White
    Write-Host "   [OK] Quick start guide" -ForegroundColor White
    Write-Host "   [OK] Release notes" -ForegroundColor White
    Write-Host ""
    
    Write-Host "Ready for Distribution!" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] Release preparation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")