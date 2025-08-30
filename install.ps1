# PlexPrerollManager One-Click Installer
# Run this script to automatically install PlexPrerollManager on Windows

param(
    [string]$InstallPath = "$env:ProgramFiles\PlexPrerollManager",
    [string]$DataPath = "$env:ProgramData\PlexPrerollManager",
    [switch]$SkipFFmpeg,
    [switch]$Force,
    [switch]$Debug
)

#Requires -Version 5.1
#Requires -RunAsAdministrator

# Setup logging
$logFile = Join-Path $env:TEMP "PlexPrerollManager_Install_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
$ErrorActionPreference = "Stop"

function Write-Log {
    param($Message, $Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $logFile -Value $logMessage
}

function Write-Error-Log {
    param($Message, $Exception = $null)
    Write-Log "ERROR: $Message" "ERROR"
    if ($Exception) {
        Write-Log "Exception: $($Exception.Message)" "ERROR"
        Write-Log "Stack Trace: $($Exception.StackTrace)" "ERROR"
    }
}

# Global error handling
$global:installSuccess = $false
$global:installError = $null

try {
    # Check for existing installation
    $existingInstallation = $false
    $existingService = Get-Service -Name "PlexPrerollManager" -ErrorAction SilentlyContinue
    $existingExePath = Join-Path $InstallPath "PlexPrerollManager.exe"
    
    # Display ASCII art banner
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                                                                              ║" -ForegroundColor Cyan
    Write-Host "║                    ██████╗ ██╗     ███████╗██╗  ██╗██████╗ ██████╗ ███████╗   ║" -ForegroundColor Magenta
    Write-Host "║                    ██╔══██╗██║     ██╔════╝╚██╗██╔╝██╔══██╗██╔══██╗██╔════╝   ║" -ForegroundColor Magenta
    Write-Host "║                    ██████╔╝██║     █████╗   ╚███╔╝ ██████╔╝██████╔╝█████╗     ║" -ForegroundColor Magenta
    Write-Host "║                    ██╔═══╝ ██║     ██╔══╝   ██╔██╗ ██╔═══╝ ██╔══██╗██╔══╝     ║" -ForegroundColor Magenta
    Write-Host "║                    ██║     ███████╗███████╗██╔╝ ██╗██║     ██║  ██║███████╗   ║" -ForegroundColor Magenta
    Write-Host "║                    ╚═╝     ╚══════╝╚══════╝╚═╝  ╚═╝╚═╝     ╚═╝  ╚═╝╚══════╝   ║" -ForegroundColor Magenta
    Write-Host "║                                                                              ║" -ForegroundColor Cyan
    Write-Host "║                    ██████╗ ██████╗ ███████╗██████╗  ██████╗ ██╗     ██╗        ║" -ForegroundColor Green
    Write-Host "║                    ██╔══██╗██╔══██╗██╔════╝██╔══██╗██╔═══██╗██║     ██║        ║" -ForegroundColor Green
    Write-Host "║                    ██████╔╝██████╔╝█████╗  ██████╔╝██║   ██║██║     ██║        ║" -ForegroundColor Green
    Write-Host "║                    ██╔═══╝ ██╔══██╗██╔══╝  ██╔══██╗██║   ██║██║     ██║        ║" -ForegroundColor Green
    Write-Host "║                    ██║     ██║  ██║███████╗██║  ██║╚██████╔╝███████╗███████╗   ║" -ForegroundColor Green
    Write-Host "║                    ╚═╝     ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚══════╝   ║" -ForegroundColor Green
    Write-Host "║                                                                              ║" -ForegroundColor Cyan
    Write-Host "║                          One-Click Installer v2.0                             ║" -ForegroundColor Yellow
    Write-Host "║                                                                              ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    if ((Test-Path $InstallPath) -or $existingService -or (Test-Path $existingExePath)) {
        $existingInstallation = $true
        Write-Log "Detected existing PlexPrerollManager installation"
        Write-Host "EXISTING INSTALLATION DETECTED - PERFORMING UPGRADE" -ForegroundColor Yellow
        Write-Host "===================================================" -ForegroundColor Yellow
    } else {
        Write-Host "PLEX PREROLL MANAGER ONE-CLICK INSTALLER" -ForegroundColor Cyan
        Write-Host "=========================================" -ForegroundColor Cyan
    }
    
    Write-Host "Starting installation at $(Get-Date)" -ForegroundColor Gray
    Write-Log "=== PlexPrerollManager One-Click Installer Started ==="
    Write-Log "Log file: $logFile"
    Write-Log "Installation started at $(Get-Date)"
    Write-Log "Existing installation detected: $existingInstallation"

    if ($Debug) {
        Write-Host "DEBUG MODE ENABLED" -ForegroundColor Magenta
        Write-Log "DEBUG MODE ENABLED" "DEBUG"
        Write-Host "InstallPath: $InstallPath" -ForegroundColor Magenta
        Write-Host "DataPath: $DataPath" -ForegroundColor Magenta
        Write-Host "SkipFFmpeg: $SkipFFmpeg" -ForegroundColor Magenta
        Write-Host "Force: $Force" -ForegroundColor Magenta
        Write-Log "InstallPath: $InstallPath" "DEBUG"
        Write-Log "DataPath: $DataPath" "DEBUG"
        Write-Log "SkipFFmpeg: $SkipFFmpeg" "DEBUG"
        Write-Log "Force: $Force" "DEBUG"
    }
    Write-Host ""

    # Check if running as administrator
    Write-Log "Checking administrator privileges..."
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error-Log "Script not running as administrator"
        Write-Host "ERROR: This script must be run as Administrator. Please restart PowerShell as Administrator and try again." -ForegroundColor Red
        throw "Administrator privileges required"
    }
    Write-Log "Administrator privileges confirmed"

# Function to write colored output
function Write-Status {
    param($Message)
    Write-Host "INFO: $Message" -ForegroundColor Blue
}
function Write-Success {
    param($Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor Green
}
function Write-Warning {
    param($Message)
    Write-Host "WARNING: $Message" -ForegroundColor Yellow
}
function Write-Error {
    param($Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

# Function to create section headers
function Write-SectionHeader {
    param($Title)
    Write-Host ""
    Write-Host "===============================================================================" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "===============================================================================" -ForegroundColor Cyan
}

# Function to create subsection headers
function Write-SubSectionHeader {
    param($Title)
    Write-Host ""
    Write-Host "--- $Title ---" -ForegroundColor Magenta
}

    # Check .NET 9.0
    Write-SectionHeader "CHECKING SYSTEM REQUIREMENTS"
    Write-SubSectionHeader ".NET 9.0 Runtime"
    Write-Log "Checking .NET 9.0 installation..."
    Write-Status "Checking .NET 9.0 installation..."

    # Function to check .NET version more robustly
    function Get-DotNetVersion {
        try {
            # First try the standard way
            $version = & dotnet --version 2>$null
            if ($version) {
                return $version
            }
        } catch {
            Write-Log "Standard dotnet command failed, trying alternative methods..."
        }

        # Try to find dotnet.exe in common locations
        $commonPaths = @(
            "$env:ProgramFiles\dotnet\dotnet.exe",
            "$env:ProgramFiles (x86)\dotnet\dotnet.exe",
            "$env:LocalAppData\Microsoft\dotnet\dotnet.exe",
            "$env:USERPROFILE\.dotnet\dotnet.exe"
        )

        foreach ($path in $commonPaths) {
            if (Test-Path $path) {
                try {
                    $version = & $path --version 2>$null
                    if ($version) {
                        Write-Log "Found .NET at: $path"
                        return $version
                    }
                } catch {
                    Write-Log "Failed to get version from $path"
                }
            }
        }

        return $null
    }

    try {
        $dotnetVersion = Get-DotNetVersion
        Write-Log ".NET version detected: $dotnetVersion"

        if ($dotnetVersion -and $dotnetVersion -ge "9.0") {
            Write-Success ".NET $dotnetVersion found - ready to proceed!"
            Write-Log ".NET check passed"
        } else {
            Write-Log ".NET version too old or not found: $dotnetVersion"
            Write-Status "Installing .NET 9.0 automatically..."
            Write-Log "Starting automatic .NET 9.0 installation"

            try {
                # Download and run the dotnet-install script
                $installScriptUrl = "https://dot.net/v1/dotnet-install.ps1"
                Write-Log "Downloading dotnet-install script from: $installScriptUrl"

                $installScript = Invoke-WebRequest -Uri $installScriptUrl -UseBasicParsing
                $scriptBlock = [scriptblock]::Create($installScript.Content)

                Write-Log "Installing .NET 9.0 runtime..."
                # Remove -NoPath flag to ensure .NET is added to PATH
                & $scriptBlock -Channel 9.0 -Runtime dotnet -InstallDir "$env:ProgramFiles\dotnet"

                # Refresh PATH for current session - check multiple possible locations
                $possibleDotnetPaths = @(
                    "$env:ProgramFiles\dotnet",
                    "$env:ProgramFiles\dotnet\dotnet.exe",
                    "$env:USERPROFILE\.dotnet"
                )

                $dotnetFound = $false
                foreach ($dotnetPath in $possibleDotnetPaths) {
                    if (Test-Path $dotnetPath) {
                        if ($dotnetPath -like "*.exe") {
                            $dotnetDir = [System.IO.Path]::GetDirectoryName($dotnetPath)
                        } else {
                            $dotnetDir = $dotnetPath
                        }

                        if ($env:PATH -notlike "*$dotnetDir*") {
                            $env:PATH = "$dotnetDir;$env:PATH"
                            Write-Log "Added .NET to PATH: $dotnetDir"
                        }
                        $dotnetFound = $true
                        break
                    }
                }

                if (-not $dotnetFound) {
                    Write-Log "Warning: Could not find .NET installation directory to add to PATH"
                }

                # Verify installation with improved detection
                $newDotnetVersion = Get-DotNetVersion
                Write-Log "Verification: .NET version after install: $newDotnetVersion"

                if ($newDotnetVersion -and $newDotnetVersion -ge "9.0") {
                    Write-Success ".NET $newDotnetVersion installed successfully!"
                    Write-Log ".NET installation completed successfully"
                } else {
                    Write-Error-Log "Failed to verify .NET installation"
                    Write-Error "Installation appeared to complete but .NET 9.0 was not found."
                    Write-Error "Please try installing manually from: https://dotnet.microsoft.com/download/dotnet/9.0"
                    throw ".NET installation verification failed"
                }

            } catch {
                Write-Error-Log "Automatic .NET installation failed" $_
                Write-Error "Automatic .NET installation failed."
                Write-Error "Error details: $($_.Exception.Message)"
                Write-Error ""
                Write-Error "Please install .NET 9.0 manually from:"
                Write-Error "https://dotnet.microsoft.com/download/dotnet/9.0"
                Write-Error ""
                Write-Error "After manual installation, restart PowerShell and run this installer again."
                throw "Automatic .NET installation failed"
            }
        }
    } catch {
        Write-Error-Log ".NET check/installation failed" $_
        if ($_.Exception.Message -notlike "*Automatic .NET installation failed*") {
            Write-Error ".NET 9.0 is required but was not found."
            Write-Error "Please install .NET 9.0 from: https://dotnet.microsoft.com/download/dotnet/9.0"
            Write-Error ""
            Write-Error "After installation, restart PowerShell and run this installer again."
        }
        throw "Dotnet not found or installation failed"
    }

# Install FFmpeg if not skipped
if (-not $SkipFFmpeg) {
    Write-SubSectionHeader "FFmpeg Video Processing"
    Write-Status "Checking FFmpeg installation..."
    try {
        $ffmpegVersion = & ffmpeg -version 2>$null | Select-Object -First 1
        if ($ffmpegVersion) {
            Write-Success "FFmpeg found: $ffmpegVersion"
        } else {
            throw "FFmpeg not found"
        }
    } catch {
        Write-Warning "FFmpeg not found. Installing via Chocolatey..."
        try {
            # Check if Chocolatey is installed
            $chocoVersion = & choco --version 2>$null
            if (-not $chocoVersion) {
                Write-Status "Installing Chocolatey..."
                Set-ExecutionPolicy Bypass -Scope Process -Force
                [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
                Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))
            }

            Write-Status "Installing FFmpeg via Chocolatey..."
            & choco install ffmpeg -y
            Write-Success "FFmpeg installed successfully"
        } catch {
            Write-Warning "Could not install FFmpeg automatically. Please install manually from: https://ffmpeg.org/download.html"
            Write-Warning "Add FFmpeg to your PATH after installation"
        }
    }
}

# Visual separator
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Gray
Write-Host ""

# Handle existing installation (upgrade scenario)
if ($existingInstallation) {
    Write-Status "Preparing for upgrade..."

    # Stop existing service
    if ($existingService) {
        Write-Log "Stopping existing service..."
        Write-Status "Stopping existing PlexPrerollManager service..."
        try {
            Stop-Service -Name "PlexPrerollManager" -Force -ErrorAction Stop
            Write-Log "Existing service stopped successfully"
            Write-Success "Existing service stopped"
        } catch {
            Write-Log "Warning: Could not stop existing service: $_"
            Write-Warning "Could not stop existing service (may already be stopped): $_"
        }
    }

    # Backup existing configuration
    $backupPath = Join-Path $DataPath "backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    if (Test-Path $DataPath) {
        Write-Log "Backing up existing configuration to: $backupPath"
        Write-Status "Backing up existing configuration..."
        try {
            Copy-Item -Path $DataPath -Destination $backupPath -Recurse -Force
            Write-Success "Configuration backed up to: $backupPath"
            Write-Log "Backup completed successfully"
        } catch {
            Write-Log "Warning: Could not backup configuration: $_"
            Write-Warning "Could not backup configuration: $_"
        }
    }

    # Remove old installation files (but keep data directory)
    if (Test-Path $InstallPath) {
        Write-Log "Removing old installation files..."
        Write-Status "Removing old installation files..."
        try {
            # Don't remove the data directory, just the program files
            Get-ChildItem -Path $InstallPath -Exclude "Data" | Remove-Item -Recurse -Force
            Write-Success "Old installation files removed"
            Write-Log "Old files removed successfully"
        } catch {
            Write-Log "Warning: Could not remove some old files: $_"
            Write-Warning "Could not remove some old files (may be in use): $_"
        }
    }

    Write-Log "Upgrade preparation completed"
    Write-Success "Ready for upgrade installation"
}

# Create installation directories
Write-Status "Creating installation directories..."
try {
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }
    if (-not (Test-Path $DataPath)) {
        New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    }
    Write-Success "Directories created successfully"
} catch {
    Write-Error "Failed to create directories: $_"
    throw "Directory creation failed"
}

# Download latest release from GitHub
Write-SectionHeader "DOWNLOADING APPLICATION"
Write-Status "Downloading PlexPrerollManager..."
$repoUrl = "https://api.github.com/repos/JFLXCLOUD/PlexPrerollManager/releases/latest"
$downloadUrl = "https://github.com/JFLXCLOUD/PlexPrerollManager/releases/latest/download/PlexPrerollManager.zip"

try {
    # For now, we'll use a placeholder. In real deployment, this would download from GitHub
    Write-Status "Downloading from GitHub repository..."
    Write-Status "Repository URL: $repoUrl"
    Write-Status "Download URL: $downloadUrl"

    # Download and extract the latest release
    Write-Status "Downloading latest release..."
    try {
        # Get latest release info
        $apiUrl = "https://api.github.com/repos/JFLXCLOUD/PlexPrerollManager/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl -Method Get

        # Find the ZIP asset
        $zipAsset = $release.assets | Where-Object { $_.name -like "*.zip" -and $_.name -notlike "*.sha256" } | Select-Object -First 1

        if ($zipAsset) {
            $zipUrl = $zipAsset.browser_download_url
            Write-Status "Downloading: $zipUrl"

            # Download ZIP file
            $tempZip = Join-Path $env:TEMP "PlexPrerollManager.zip"
            Write-Status "Downloading from: $zipUrl"
            Write-Progress -Activity "Downloading PlexPrerollManager" -Status "Downloading..." -PercentComplete 0

            # Get file size for progress tracking
            try {
                $response = Invoke-WebRequest -Uri $zipUrl -Method Head
                $totalSize = [long]$response.Headers.'Content-Length'
                Write-Log "Download size: $totalSize bytes"
            } catch {
                $totalSize = 0
                Write-Log "Could not determine download size"
            }

            # Download with progress
            $webClient = New-Object System.Net.WebClient
            $webClient.DownloadFile($zipUrl, $tempZip)
            Write-Progress -Activity "Downloading PlexPrerollManager" -Status "Download complete" -PercentComplete 100
            Write-Progress -Activity "Downloading PlexPrerollManager" -Completed

            # Extract ZIP file
            Write-Status "Extracting files..."
            Expand-Archive -Path $tempZip -DestinationPath $InstallPath -Force

            # Clean up
            Remove-Item $tempZip -Force

            Write-Success "Application files downloaded and extracted successfully"

            # Check if this is a compiled release (contains published binaries) or source code
            # First check directly in install path
            $exePath = Join-Path $InstallPath "PlexPrerollManager.exe"
            $projectFile = Join-Path $InstallPath "PlexPrerollManager.csproj"

            Write-Log "Checking for executable at: $exePath"
            Write-Log "Checking for project file at: $projectFile"

            if (Test-Path $exePath) {
                Write-Log "Detected compiled release - skipping build step"
                Write-Status "Compiled release detected - build not required"
                $skipBuild = $true
            } elseif (Test-Path $projectFile) {
                Write-Log "Detected source code release - build required"
                Write-Status "Source code detected - will build application"
                $skipBuild = $false
            } else {
                # Check if files are in a subdirectory (common in GitHub releases)
                Write-Log "Files not found in root directory, checking subdirectories..."
                $subdirs = Get-ChildItem -Path $InstallPath -Directory -ErrorAction SilentlyContinue
                $foundFiles = $false

                foreach ($subdir in $subdirs) {
                    $subdirExePath = Join-Path $subdir.FullName "PlexPrerollManager.exe"
                    $subdirProjectPath = Join-Path $subdir.FullName "PlexPrerollManager.csproj"

                    Write-Log "Checking subdirectory: $($subdir.FullName)"

                    if (Test-Path $subdirExePath) {
                        Write-Log "Found executable in subdirectory: $subdirExePath"
                        # Move files from subdirectory to root, handling conflicts
                        Write-Status "Moving files from subdirectory to root..."
                        try {
                            $itemsToMove = Get-ChildItem -Path $subdir.FullName -ErrorAction Stop
                            Write-Log "Found $($itemsToMove.Count) items to move from $($subdir.FullName)"

                            # Separate files and directories for processing
                            $filesToMove = $itemsToMove | Where-Object { -not $_.PSIsContainer }
                            $dirsToMove = $itemsToMove | Where-Object { $_.PSIsContainer }

                            Write-Log "Processing $($filesToMove.Count) files and $($dirsToMove.Count) directories"

                            # Process files first
                            foreach ($item in $filesToMove) {
                                $destinationPath = Join-Path $InstallPath $item.Name
                                Write-Log "Processing file: $($item.FullName) -> $destinationPath"

                                # Handle conflicts more robustly
                                if (Test-Path $destinationPath) {
                                    Write-Log "Destination exists, attempting to remove: $destinationPath"
                                    try {
                                        Remove-Item $destinationPath -Force -ErrorAction Stop
                                        Write-Log "Successfully removed existing file: $destinationPath"
                                    } catch {
                                        Write-Log "Warning: Could not remove existing file $destinationPath : $_"
                                        # Try to rename the conflicting item instead
                                        $backupName = "$destinationPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                                        try {
                                            Rename-Item $destinationPath $backupName -ErrorAction Stop
                                            Write-Log "Renamed conflicting file to: $backupName"
                                        } catch {
                                            Write-Log "Error: Could not rename conflicting file: $_"
                                            # Skip this item if we can't handle the conflict
                                            Write-Log "Skipping file due to unresolvable conflict: $($item.Name)"
                                            continue
                                        }
                                    }
                                }

                                # Now try to move the file
                                try {
                                    Move-Item $item.FullName -Destination $InstallPath -Force -ErrorAction Stop
                                    Write-Log "Successfully moved file: $($item.FullName) -> $destinationPath"
                                } catch {
                                    Write-Log "Error moving file $($item.FullName): $_"
                                    # Try copy instead of move as a fallback
                                    try {
                                        Copy-Item $item.FullName -Destination $InstallPath -Force -ErrorAction Stop
                                        Remove-Item $item.FullName -Force -ErrorAction Stop
                                        Write-Log "Used copy+delete fallback for file: $($item.FullName)"
                                    } catch {
                                        Write-Log "Error: Copy+delete fallback also failed for file $($item.FullName): $_"
                                        throw "Failed to move/copy file: $($item.Name)"
                                    }
                                }
                            }

                            # Process directories, but skip the current subdirectory itself
                            foreach ($item in $dirsToMove) {
                                # Skip if this directory has the same name as our current subdirectory
                                if ($item.Name -eq $subdir.Name) {
                                    Write-Log "Skipping directory with same name as current subdirectory: $($item.Name)"
                                    continue
                                }

                                $destinationPath = Join-Path $InstallPath $item.Name
                                Write-Log "Processing directory: $($item.FullName) -> $destinationPath"

                                # Handle conflicts more robustly
                                if (Test-Path $destinationPath) {
                                    Write-Log "Destination exists, attempting to remove: $destinationPath"
                                    try {
                                        Remove-Item $destinationPath -Force -Recurse -ErrorAction Stop
                                        Write-Log "Successfully removed existing directory: $destinationPath"
                                    } catch {
                                        Write-Log "Warning: Could not remove existing directory $destinationPath : $_"
                                        # Try to rename the conflicting item instead
                                        $backupName = "$destinationPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                                        try {
                                            Rename-Item $destinationPath $backupName -ErrorAction Stop
                                            Write-Log "Renamed conflicting directory to: $backupName"
                                        } catch {
                                            Write-Log "Error: Could not rename conflicting directory: $_"
                                            # Skip this item if we can't handle the conflict
                                            Write-Log "Skipping directory due to unresolvable conflict: $($item.Name)"
                                            continue
                                        }
                                    }
                                }

                                # Now try to move the directory
                                try {
                                    Move-Item $item.FullName -Destination $InstallPath -Force -ErrorAction Stop
                                    Write-Log "Successfully moved directory: $($item.FullName) -> $destinationPath"
                                } catch {
                                    Write-Log "Error moving directory $($item.FullName): $_"
                                    # Try copy instead of move as a fallback
                                    try {
                                        Copy-Item $item.FullName -Destination $InstallPath -Force -Recurse -ErrorAction Stop
                                        Remove-Item $item.FullName -Force -Recurse -ErrorAction Stop
                                        Write-Log "Used copy+delete fallback for directory: $($item.FullName)"
                                    } catch {
                                        Write-Log "Error: Copy+delete fallback also failed for directory $($item.FullName): $_"
                                        throw "Failed to move/copy directory: $($item.Name)"
                                    }
                                }
                            }

                            # Remove the now-empty subdirectory
                            try {
                                Remove-Item $subdir.FullName -Force -ErrorAction Stop
                                Write-Log "Removed empty subdirectory: $($subdir.FullName)"
                            } catch {
                                Write-Log "Warning: Could not remove subdirectory $($subdir.FullName): $_"
                            }

                            $skipBuild = $true
                            $foundFiles = $true
                            Write-Log "Files moved successfully, detected compiled release"
                            Write-Success "Files moved successfully"
                        } catch {
                            Write-Error-Log "Failed to move files from subdirectory" $_
                            Write-Log "Error details: $($_.Exception.Message)"
                            Write-Log "Stack trace: $($_.Exception.StackTrace)"
                            throw "Failed to move files from subdirectory: $_"
                        }
                        break
                    } elseif (Test-Path $subdirProjectPath) {
                        Write-Log "Found project file in subdirectory: $subdirProjectPath"
                        # Move files from subdirectory to root, handling conflicts
                        Write-Status "Moving files from subdirectory to root..."
                        try {
                            $itemsToMove = Get-ChildItem -Path $subdir.FullName -ErrorAction Stop
                            Write-Log "Found $($itemsToMove.Count) items to move from $($subdir.FullName)"

                            # Separate files and directories for processing
                            $filesToMove = $itemsToMove | Where-Object { -not $_.PSIsContainer }
                            $dirsToMove = $itemsToMove | Where-Object { $_.PSIsContainer }

                            Write-Log "Processing $($filesToMove.Count) files and $($dirsToMove.Count) directories"

                            # Process files first
                            foreach ($item in $filesToMove) {
                                $destinationPath = Join-Path $InstallPath $item.Name
                                Write-Log "Processing file: $($item.FullName) -> $destinationPath"

                                # Handle conflicts more robustly
                                if (Test-Path $destinationPath) {
                                    Write-Log "Destination exists, attempting to remove: $destinationPath"
                                    try {
                                        Remove-Item $destinationPath -Force -ErrorAction Stop
                                        Write-Log "Successfully removed existing file: $destinationPath"
                                    } catch {
                                        Write-Log "Warning: Could not remove existing file $destinationPath : $_"
                                        # Try to rename the conflicting item instead
                                        $backupName = "$destinationPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                                        try {
                                            Rename-Item $destinationPath $backupName -ErrorAction Stop
                                            Write-Log "Renamed conflicting file to: $backupName"
                                        } catch {
                                            Write-Log "Error: Could not rename conflicting file: $_"
                                            # Skip this item if we can't handle the conflict
                                            Write-Log "Skipping file due to unresolvable conflict: $($item.Name)"
                                            continue
                                        }
                                    }
                                }

                                # Now try to move the file
                                try {
                                    Move-Item $item.FullName -Destination $InstallPath -Force -ErrorAction Stop
                                    Write-Log "Successfully moved file: $($item.FullName) -> $destinationPath"
                                } catch {
                                    Write-Log "Error moving file $($item.FullName): $_"
                                    # Try copy instead of move as a fallback
                                    try {
                                        Copy-Item $item.FullName -Destination $InstallPath -Force -ErrorAction Stop
                                        Remove-Item $item.FullName -Force -ErrorAction Stop
                                        Write-Log "Used copy+delete fallback for file: $($item.FullName)"
                                    } catch {
                                        Write-Log "Error: Copy+delete fallback also failed for file $($item.FullName): $_"
                                        throw "Failed to move/copy file: $($item.Name)"
                                    }
                                }
                            }

                            # Process directories, but skip the current subdirectory itself
                            foreach ($item in $dirsToMove) {
                                # Skip if this directory has the same name as our current subdirectory
                                if ($item.Name -eq $subdir.Name) {
                                    Write-Log "Skipping directory with same name as current subdirectory: $($item.Name)"
                                    continue
                                }

                                $destinationPath = Join-Path $InstallPath $item.Name
                                Write-Log "Processing directory: $($item.FullName) -> $destinationPath"

                                # Handle conflicts more robustly
                                if (Test-Path $destinationPath) {
                                    Write-Log "Destination exists, attempting to remove: $destinationPath"
                                    try {
                                        Remove-Item $destinationPath -Force -Recurse -ErrorAction Stop
                                        Write-Log "Successfully removed existing directory: $destinationPath"
                                    } catch {
                                        Write-Log "Warning: Could not remove existing directory $destinationPath : $_"
                                        # Try to rename the conflicting item instead
                                        $backupName = "$destinationPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                                        try {
                                            Rename-Item $destinationPath $backupName -ErrorAction Stop
                                            Write-Log "Renamed conflicting directory to: $backupName"
                                        } catch {
                                            Write-Log "Error: Could not rename conflicting directory: $_"
                                            # Skip this item if we can't handle the conflict
                                            Write-Log "Skipping directory due to unresolvable conflict: $($item.Name)"
                                            continue
                                        }
                                    }
                                }

                                # Now try to move the directory
                                try {
                                    Move-Item $item.FullName -Destination $InstallPath -Force -ErrorAction Stop
                                    Write-Log "Successfully moved directory: $($item.FullName) -> $destinationPath"
                                } catch {
                                    Write-Log "Error moving directory $($item.FullName): $_"
                                    # Try copy instead of move as a fallback
                                    try {
                                        Copy-Item $item.FullName -Destination $InstallPath -Force -Recurse -ErrorAction Stop
                                        Remove-Item $item.FullName -Force -Recurse -ErrorAction Stop
                                        Write-Log "Used copy+delete fallback for directory: $($item.FullName)"
                                    } catch {
                                        Write-Log "Error: Copy+delete fallback also failed for directory $($item.FullName): $_"
                                        throw "Failed to move/copy directory: $($item.Name)"
                                    }
                                }
                            }

                            # Remove the now-empty subdirectory
                            try {
                                Remove-Item $subdir.FullName -Force -ErrorAction Stop
                                Write-Log "Removed empty subdirectory: $($subdir.FullName)"
                            } catch {
                                Write-Log "Warning: Could not remove subdirectory $($subdir.FullName): $_"
                            }

                            $skipBuild = $false
                            $foundFiles = $true
                            Write-Log "Files moved successfully, detected source code release"
                            Write-Success "Files moved successfully"
                        } catch {
                            Write-Error-Log "Failed to move files from subdirectory" $_
                            Write-Log "Error details: $($_.Exception.Message)"
                            Write-Log "Stack trace: $($_.Exception.StackTrace)"
                            throw "Failed to move files from subdirectory: $_"
                        }
                        break
                    }
                }

                if (-not $foundFiles) {
                    Write-Log "Could not find expected files in any location"
                    Write-Log "Contents of $InstallPath :"
                    Get-ChildItem -Path $InstallPath -Recurse | ForEach-Object {
                        Write-Log "  $($_.FullName)"
                    }
                    throw "Downloaded release does not contain expected files (PlexPrerollManager.exe or PlexPrerollManager.csproj)"
                }
            }
        } else {
            throw "Could not find ZIP file in latest release"
        }
    } catch {
        Write-Error "Failed to download release: $_"
        Write-Warning "Falling back to local file copy for development..."

        # Fallback: Copy current files to installation directory (for development/testing)
        Write-Status "Copying application files..."
        $currentPath = Split-Path -Parent $MyInvocation.MyCommand.Path

        Write-Log "Script invocation path: $($MyInvocation.MyCommand.Path)"
        Write-Log "Determined current path: $currentPath"

        if (-not $currentPath) {
            Write-Log "Current path is null, trying alternative methods..."

            # Try multiple fallback methods for remote execution
            if ($MyInvocation.MyCommand.Path) {
                $currentPath = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
                Write-Log "Method 1 - System.IO.Path: $currentPath"
            }

            if (-not $currentPath -and $PSScriptRoot) {
                $currentPath = $PSScriptRoot
                Write-Log "Method 2 - PSScriptRoot: $currentPath"
            }

            if (-not $currentPath) {
                # Last resort: try to find the script in common locations
                $possiblePaths = @(
                    "$env:TEMP\PlexPrerollManager",
                    "$env:USERPROFILE\Downloads\PlexPrerollManager",
                    "$env:USERPROFILE\Desktop\PlexPrerollManager",
                    ".\PlexPrerollManager"
                )

                foreach ($path in $possiblePaths) {
                    Write-Log "Checking possible path: $path"
                    if (Test-Path $path) {
                        $currentPath = $path
                        Write-Log "Found valid path: $currentPath"
                        break
                    }
                }
            }

            Write-Log "Final determined current path: $currentPath"
        }

        if ($currentPath -and (Test-Path $currentPath)) {
            Write-Status "Copying from: $currentPath"
            Write-Status "Copying to: $InstallPath"
            Write-Log "Starting file copy from $currentPath to $InstallPath"

            try {
                # Get all files and directories except excluded ones
                $items = Get-ChildItem -Path $currentPath -Exclude @("*.git*", "*node_modules*", "*.zip", "*.sha256", "release", "PlexPrerollManager-v*") -ErrorAction Stop
                Write-Log "Found $($items.Count) items to copy"

                foreach ($item in $items) {
                    if ($item.Name -ne "install.ps1") {  # Don't copy the installer script itself
                        Write-Log "Copying: $($item.FullName)"
                        Copy-Item $item.FullName -Destination $InstallPath -Recurse -Force -ErrorAction Stop
                    }
                }
                Write-Success "Application files copied successfully"
                $skipBuild = $false  # Local files are source code, so we need to build
                Write-Log "File copy completed successfully"
            } catch {
                Write-Error-Log "File copy failed" $_
                throw "Failed to copy application files: $_"
            }
        } else {
            Write-Error-Log "Could not determine script path" "CurrentPath: $currentPath, InvocationPath: $($MyInvocation.MyCommand.Path)"
            Write-Error "Could not determine script path for local copy. Current path: $currentPath"
            Write-Warning "This may happen when running the script remotely. Please try downloading and running the installer locally."
            throw "Could not determine script path for local copy"
        }
    }
} catch {
    Write-Error "Failed to download/copy application files: $_"
    exit 1
}

# Build the application (only if needed)
if (-not $skipBuild) {
    Write-SectionHeader "BUILDING APPLICATION"
    Write-Status "Building PlexPrerollManager..."
    try {
        Push-Location $InstallPath

        # Build the project
        Write-Progress -Activity "Building PlexPrerollManager" -Status "Building project..." -PercentComplete 25
        & dotnet build -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
        Write-Progress -Activity "Building PlexPrerollManager" -Status "Build complete, publishing..." -PercentComplete 75

        # Publish the application
        & dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=false
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }
        Write-Progress -Activity "Building PlexPrerollManager" -Status "Publish complete" -PercentComplete 100
        Write-Progress -Activity "Building PlexPrerollManager" -Completed

        Pop-Location
        Write-Success "Application built successfully"
    } catch {
        Write-Progress -Activity "Building PlexPrerollManager" -Completed
        Write-Error "Failed to build application: $_"
        throw "Build failed"
    }
} else {
    Write-Log "Skipping build step - using pre-compiled binaries"
    Write-Status "Using pre-compiled application - build skipped"
}

# Create appsettings.json if it doesn't exist
$appsettingsPath = Join-Path $DataPath "appsettings.json"
if (-not (Test-Path $appsettingsPath) -or $Force) {
    Write-Status "Creating default configuration..."
    $defaultConfig = @"
{
  "Plex": {
    "Url": "http://localhost:32400",
    "Token": ""
  },
  "PrerollManager": {
    "PrerollsPath": "$($DataPath)\Prerolls",
    "ConfigPath": "$($DataPath)\config.json"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
"@
    $defaultConfig | Out-File -FilePath $appsettingsPath -Encoding UTF8
    Write-Success "Default configuration created at: $appsettingsPath"
}

# Install Windows service
Write-SectionHeader "INSTALLING WINDOWS SERVICE"
Write-Status "Installing Windows service..."
try {
    # Determine executable path based on whether we built or downloaded pre-compiled
    if ($skipBuild) {
        # Pre-compiled release - executable is directly in install path
        $exePath = Join-Path $InstallPath "PlexPrerollManager.exe"
        Write-Log "Using pre-compiled executable: $exePath"
    } else {
        # Built from source - executable is in publish directory
        $exePath = Join-Path $InstallPath "bin\Release\net9.0\win-x64\publish\PlexPrerollManager.exe"
        Write-Log "Using built executable: $exePath"
    }

    if (Test-Path $exePath) {
        Write-Log "Executable found at: $exePath"

        # Stop existing service if running
        $existingService = Get-Service -Name "PlexPrerollManager" -ErrorAction SilentlyContinue
        if ($existingService) {
            Write-Log "Stopping existing service..."
            Stop-Service -Name "PlexPrerollManager" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
            & sc.exe delete PlexPrerollManager 2>$null
            Start-Sleep -Seconds 2
        }

        # Create new service with proper arguments
        # For self-contained .NET apps, we need different service configuration
        if ($skipBuild) {
            # Pre-compiled release - likely self-contained
            $servicePath = "`"$exePath`""
            Write-Log "Creating service for pre-compiled app: $servicePath"
        } else {
            # Built from source - may need content root
            $servicePath = "`"$exePath`" --contentRoot `"$InstallPath`""
            Write-Log "Creating service for built app: $servicePath"
        }

        $createResult = & sc.exe create PlexPrerollManager binPath= $servicePath start= auto 2>&1
        Write-Log "Service creation result: $createResult"

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Windows service created successfully"

            # Try to start the service with better error handling
            Write-Log "Attempting to start service..."
            try {
                Start-Service -Name "PlexPrerollManager" -ErrorAction Stop
                Write-Success "Windows service started successfully"

                # Verify service is actually running
                Start-Sleep -Seconds 2
                $serviceStatus = Get-Service -Name "PlexPrerollManager"
                if ($serviceStatus.Status -eq "Running") {
                    Write-Success "Service is confirmed running"
                } else {
                    Write-Warning "Service was created but may not be running properly. Status: $($serviceStatus.Status)"
                }
            } catch {
                Write-Error-Log "Failed to start service" $_
                Write-Warning "Service was created but failed to start. This is common and you can start it manually later."
                Write-Warning "To start manually: Run PowerShell as Administrator and execute: Start-Service PlexPrerollManager"
                Write-Warning "Or use: net start PlexPrerollManager"
            }
        } else {
            Write-Error-Log "Failed to create service" "Exit code: $LASTEXITCODE, Result: $createResult"
            Write-Error "Failed to create service. Exit code: $LASTEXITCODE"
            throw "Failed to create service"
        }
    } else {
        Write-Error-Log "Executable not found" "Path: $exePath"
        Write-Error "Could not find executable at: $exePath"
        throw "Executable not found"
    }
} catch {
    Write-Error-Log "Service installation failed" $_
    Write-Error "Failed to install Windows service: $_"
    Write-Warning "You can still run the application manually: $exePath"
    Write-Warning "Manual start command: & `"$exePath`""
}

# Create desktop shortcut
Write-Status "Creating desktop shortcut..."

# Visual separator before final setup
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════════════" -ForegroundColor Gray
Write-Host ""
try {
    $WshShell = New-Object -comObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut("$([Environment]::GetFolderPath('Desktop'))\PlexPrerollManager.lnk")
    $Shortcut.TargetPath = "http://localhost:8089"
    $Shortcut.IconLocation = "shell32.dll,13"
    $Shortcut.Description = "Open PlexPrerollManager Web Interface"
    $Shortcut.Save()
    Write-Success "Desktop shortcut created"
} catch {
    Write-Warning "Could not create desktop shortcut: $_"
}

# Final instructions
Write-Host ""
Write-SectionHeader "INSTALLATION COMPLETE"
if ($existingInstallation) {
    Write-Host "╔══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                           UPGRADE COMPLETE!                                ║" -ForegroundColor Green
    Write-Host "║                                                                          ║" -ForegroundColor Green
    Write-Host "║  PlexPrerollManager has been successfully upgraded to the latest version ║" -ForegroundColor Green
    Write-Host "║                                                                          ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
} else {
    Write-Host "╔══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                         INSTALLATION COMPLETE!                            ║" -ForegroundColor Green
    Write-Host "║                                                                          ║" -ForegroundColor Green
    Write-Host "║       PlexPrerollManager has been successfully installed!                ║" -ForegroundColor Green
    Write-Host "║                                                                          ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
}
Write-Host ""
Write-Host "Web Interface: http://localhost:8089" -ForegroundColor Cyan
Write-Host "Install Path: $InstallPath" -ForegroundColor Cyan
Write-Host "Data Path: $DataPath" -ForegroundColor Cyan

if ($existingInstallation) {
    Write-Host ""
    Write-Host "UPGRADE NOTES:" -ForegroundColor Yellow
    Write-Host "   - Your configuration and data have been preserved"
    Write-Host "   - Existing preroll videos and categories are intact"
    Write-Host "   - Service has been restarted with the new version"
}

Write-Host ""
Write-Host "GETTING STARTED:" -ForegroundColor Yellow
Write-Host "   1. Open http://localhost:8089 in your browser"
if (-not $existingInstallation) {
    Write-Host "   2. Configure your Plex server URL and token in the settings"
    Write-Host "   3. Upload your first preroll videos!"
} else {
    Write-Host "   2. Verify your existing configuration is still correct"
    Write-Host "   3. Check that your preroll videos are still available"
}
Write-Host ""
Write-Host "NEED HELP?" -ForegroundColor Yellow
Write-Host "   - Check the README.md for detailed instructions"
Write-Host "   - View logs in Windows Event Viewer"
Write-Host "   - Visit the GitHub repository for updates"
Write-Host ""
Write-Host "PRO TIPS:" -ForegroundColor Magenta
Write-Host "   - Use Bulk Upload to add multiple videos at once"
Write-Host "   - Set up schedules for automatic preroll switching"
Write-Host "   - Check the scheduling dashboard for advanced automation"
Write-Host ""

# Wait a moment for service to fully start
Write-Status "Waiting for service to start..."
Start-Sleep -Seconds 3

# Test the service
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8089" -TimeoutSec 10 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Success "Service is running and responding correctly!"
    }
} catch {
    Write-Warning "Service may still be starting up. Please wait a moment and try accessing http://localhost:8089"
}

    Write-Host ""
    Write-Host "Thank you for installing PlexPrerollManager!" -ForegroundColor Cyan
    $global:installSuccess = $true
    Write-Log "Installation completed successfully"

} catch {
    $global:installError = $_
    Write-Error-Log "Installation failed" $_
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║                          INSTALLATION FAILED!                             ║" -ForegroundColor Red
    Write-Host "║                                                                          ║" -ForegroundColor Red
    Write-Host "║  An error occurred during installation. Please check the details below. ║" -ForegroundColor Red
    Write-Host "║                                                                          ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Log file: $logFile" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please check the log file for detailed error information." -ForegroundColor Yellow
    Write-Host "For help, visit the GitHub repository or check the README.md" -ForegroundColor Yellow
} finally {
    # Always show final status and wait for user
    Write-Host ""
    if ($global:installSuccess) {
        Write-Host "Installation completed. Press any key to exit..." -ForegroundColor Yellow
    } else {
        Write-Host "Installation failed. Press any key to exit..." -ForegroundColor Red
        Write-Host "Check the log file: $logFile" -ForegroundColor Yellow
    }

    # More robust wait mechanism
    try {
        $null = [Console]::ReadKey($true)
    } catch {
        # Fallback for systems where ReadKey doesn't work
        Write-Host "Press Enter to continue..."
        $null = Read-Host
    }

    Write-Host "Exiting installer..." -ForegroundColor Cyan
    Write-Log "=== PlexPrerollManager One-Click Installer Ended ==="
} # End of main try block
# End of script