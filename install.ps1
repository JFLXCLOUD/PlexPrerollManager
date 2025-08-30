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
    Write-Log "=== PlexPrerollManager One-Click Installer Started ==="
    Write-Log "Log file: $logFile"
    Write-Host "PlexPrerollManager One-Click Installer" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "Starting installation at $(Get-Date)" -ForegroundColor Gray
    Write-Log "Installation started at $(Get-Date)"

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
        Write-Host "❌ This script must be run as Administrator. Please restart PowerShell as Administrator and try again." -ForegroundColor Red
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

    # Check .NET 9.0
    Write-Log "Checking .NET 9.0 installation..."
    Write-Status "Checking .NET 9.0 installation..."
    try {
        $dotnetVersion = & dotnet --version 2>$null
        Write-Log ".NET version detected: $dotnetVersion"
        if ($dotnetVersion -and $dotnetVersion -ge "9.0") {
            Write-Success ".NET $dotnetVersion found"
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
                & $scriptBlock -Channel 9.0 -Runtime dotnet -InstallDir "$env:ProgramFiles\dotnet" -NoPath

                # Refresh PATH for current session
                $dotnetPath = "$env:ProgramFiles\dotnet"
                if (Test-Path $dotnetPath) {
                    $env:PATH = "$dotnetPath;$env:PATH"
                    Write-Log "Added .NET to PATH: $dotnetPath"
                }

                # Verify installation
                $newDotnetVersion = & dotnet --version 2>$null
                Write-Log "Verification: .NET version after install: $newDotnetVersion"

                if ($newDotnetVersion -and $newDotnetVersion -ge "9.0") {
                    Write-Success ".NET $newDotnetVersion installed successfully!"
                    Write-Log ".NET installation completed successfully"
                } else {
                    Write-Error-Log "Failed to verify .NET installation"
                    throw ".NET installation verification failed"
                }

            } catch {
                Write-Error-Log "Automatic .NET installation failed" $_
                Write-Error "Automatic .NET installation failed. Please install manually from: https://dotnet.microsoft.com/download"
                Write-Error "Error details: $($_.Exception.Message)"
                throw "Automatic .NET installation failed"
            }
        }
    } catch {
        Write-Error-Log ".NET check/installation failed" $_
        if ($_.Exception.Message -notlike "*Automatic .NET installation failed*") {
            Write-Error ".NET is not installed. Please install .NET 9.0 from: https://dotnet.microsoft.com/download"
        }
        throw "Dotnet not found or installation failed"
    }

# Install FFmpeg if not skipped
if (-not $SkipFFmpeg) {
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
    exit 1
}

# Download latest release from GitHub
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
            Invoke-WebRequest -Uri $zipUrl -OutFile $tempZip

            # Extract ZIP file
            Write-Status "Extracting files..."
            Expand-Archive -Path $tempZip -DestinationPath $InstallPath -Force

            # Clean up
            Remove-Item $tempZip -Force

            Write-Success "Application files downloaded and extracted successfully"

            # Check if this is a compiled release (contains published binaries) or source code
            $exePath = Join-Path $InstallPath "PlexPrerollManager.exe"
            $projectFile = Join-Path $InstallPath "PlexPrerollManager.csproj"

            if (Test-Path $exePath) {
                Write-Log "Detected compiled release - skipping build step"
                Write-Status "Compiled release detected - build not required"
                $skipBuild = $true
            } elseif (Test-Path $projectFile) {
                Write-Log "Detected source code release - build required"
                Write-Status "Source code detected - will build application"
                $skipBuild = $false
            } else {
                Write-Log "Could not determine release type"
                throw "Downloaded release does not contain expected files"
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
        if ($currentPath -and (Test-Path $currentPath)) {
            Write-Status "Copying from: $currentPath"
            Write-Status "Copying to: $InstallPath"

            # Get all files and directories except excluded ones
            $items = Get-ChildItem -Path $currentPath -Exclude @("*.git*", "*node_modules*", "*.zip", "*.sha256", "release", "PlexPrerollManager-v*")
            foreach ($item in $items) {
                if ($item.Name -ne "install.ps1") {  # Don't copy the installer script itself
                    Copy-Item $item.FullName -Destination $InstallPath -Recurse -Force
                }
            }
            Write-Success "Application files copied successfully"
            $skipBuild = $false  # Local files are source code, so we need to build
        } else {
            throw "Could not determine script path for local copy"
        }
    }
} catch {
    Write-Error "Failed to download/copy application files: $_"
    exit 1
}

# Build the application (only if needed)
if (-not $skipBuild) {
    Write-Status "Building PlexPrerollManager..."
    try {
        Push-Location $InstallPath
        # Build the project
        & dotnet build -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }

        # Publish the application
        & dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=false
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }

        Pop-Location
        Write-Success "Application built successfully"
    } catch {
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
    $defaultConfig = @'
{
  "Plex": {
    "Url": "http://localhost:32400",
    "Token": ""
  },
  "PrerollManager": {
    "PrerollsPath": "' + $DataPath + '\\Prerolls",
    "ConfigPath": "' + $DataPath + '\\config.json"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
'@
    $defaultConfig | Out-File -FilePath $appsettingsPath -Encoding UTF8
    Write-Success "Default configuration created at: $appsettingsPath"
}

# Install Windows service
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
        # Stop existing service if running
        $existingService = Get-Service -Name "PlexPrerollManager" -ErrorAction SilentlyContinue
        if ($existingService) {
            Stop-Service -Name "PlexPrerollManager" -Force -ErrorAction SilentlyContinue
            & sc.exe delete PlexPrerollManager
            Start-Sleep -Seconds 2
        }

        # Create new service
        $servicePath = "`"$exePath --contentRoot $InstallPath`""
        & sc.exe create PlexPrerollManager binPath= $servicePath start= auto | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Windows service created successfully"
        } else {
            Write-Error "Failed to create service. Exit code: $LASTEXITCODE"
            throw "Failed to create service"
        }

        # Start the service
        Start-Service -Name "PlexPrerollManager"
        Write-Success "Windows service started successfully"
    } else {
        Write-Error "Could not find executable at: $exePath"
        exit 1
    }
} catch {
    Write-Error "Failed to install Windows service: $_"
    Write-Warning "You can still run the application manually: $exePath"
}

# Create desktop shortcut
Write-Status "Creating desktop shortcut..."
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
Write-Host "INSTALLATION COMPLETE!" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host ""
Write-Host "Web Interface: http://localhost:8089" -ForegroundColor Cyan
Write-Host "Install Path: $InstallPath" -ForegroundColor Cyan
Write-Host "Data Path: $DataPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "GETTING STARTED:" -ForegroundColor Yellow
Write-Host "1. Open http://localhost:8089 in your browser"
Write-Host "2. Configure your Plex server URL and token in the settings"
Write-Host "3. Upload your first preroll videos!"
Write-Host ""
Write-Host "NEED HELP?" -ForegroundColor Yellow
Write-Host "- Check the README.md for detailed instructions"
Write-Host "- View logs in Windows Event Viewer"
Write-Host "- Visit the GitHub repository for updates"
Write-Host ""
Write-Host "PRO TIPS:" -ForegroundColor Magenta
Write-Host "- Use Bulk Upload to add multiple videos at once"
Write-Host "- Set up schedules for automatic preroll switching"
Write-Host "- Check the scheduling dashboard for advanced automation"
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
    Write-Host "❌ INSTALLATION FAILED!" -ForegroundColor Red
    Write-Host "======================" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Log file: $logFile" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please check the log file for detailed error information." -ForegroundColor Yellow
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
}