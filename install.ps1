#Requires -Version 5.1

<#
.SYNOPSIS
    One-click installer for Plex Preroll Manager
.DESCRIPTION
    Downloads and installs Plex Preroll Manager with optional .NET runtime installation
.PARAMETER SkipDotNet
    Skip .NET runtime check and installation
.PARAMETER Force
    Force reinstallation even if already installed
#>

param(
    [switch]$SkipDotNet,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Configuration
$AppName = "Plex Preroll Manager"
$RepoOwner = "JFLXCLOUD"
$RepoName = "Nexroll"
$Version = "2.2.0"
$InstallPath = Join-Path $env:ProgramFiles $AppName
$ServiceName = "Nexroll"

function Write-Header {
    Clear-Host
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host "                Plex Preroll Manager Installer                 " -ForegroundColor Cyan
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

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-DotNet {
    Write-Step "Installing .NET 9.0 Runtime..."

    try {
        # Download .NET 9.0 installer
        $dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/8e3e4e7e-9b8e-4e8e-9b8e-4e8e9b8e4e8e/dotnet-runtime-9.0.0-win-x64.exe"
        $installerPath = "$env:TEMP\dotnet-runtime-9.0.0-win-x64.exe"

        Write-Host "Downloading .NET 9.0 Runtime..." -ForegroundColor Gray
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $installerPath -UseBasicParsing

        Write-Host "Installing .NET 9.0 Runtime..." -ForegroundColor Gray
        Start-Process -FilePath $installerPath -ArgumentList "/install /quiet /norestart" -Wait

        # Clean up
        Remove-Item $installerPath -Force

        Write-Success ".NET 9.0 Runtime installed successfully"
    } catch {
        Write-Error "Failed to install .NET 9.0 Runtime: $($_.Exception.Message)"
        Write-Host "Please install .NET 9.0 manually from: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Yellow
        exit 1
    }
}

function Test-DotNet {
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -match "9\.") {
            Write-Success ".NET 9.0 is already installed ($dotnetVersion)"
            return $true
        } else {
            Write-Host ".NET version $dotnetVersion found, but .NET 9.0 is required" -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host ".NET runtime not found" -ForegroundColor Yellow
        return $false
    }
}

function Get-LatestRelease {
    try {
        $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl -Method Get

        $asset = $release.assets | Where-Object { $_.name -match "\.zip$" } | Select-Object -First 1
        if (-not $asset) {
            throw "No ZIP asset found in latest release"
        }

        return @{
            Version = $release.tag_name
            DownloadUrl = $asset.browser_download_url
            FileName = $asset.name
        }
    } catch {
        Write-Error "Failed to get latest release: $($_.Exception.Message)"
        exit 1
    }
}

function Install-Application {
    param([string]$DownloadUrl, [string]$FileName)

    Write-Step "Downloading $AppName..."

    try {
        $tempPath = "$env:TEMP\$FileName"
        Write-Host "Downloading from GitHub..." -ForegroundColor Gray
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $tempPath -UseBasicParsing

        Write-Step "Extracting files..."
        $extractPath = "$env:TEMP\Nexroll_Extract"
        if (Test-Path $extractPath) {
            Remove-Item $extractPath -Recurse -Force
        }

        Expand-Archive -Path $tempPath -DestinationPath $extractPath -Force

        Write-Step "Installing application..."

        # Stop service and kill any running processes before replacing files
        Write-Host "Stopping existing service and processes..." -ForegroundColor Gray

        # Stop the service
        $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($existingService) {
            if ($existingService.Status -eq "Running") {
                Write-Host "Stopping service..." -ForegroundColor Gray
                Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue -Force
            }

            # Delete the service to ensure clean reinstall
            Write-Host "Removing existing service..." -ForegroundColor Gray
            $deleteService = "sc.exe delete `"$ServiceName`""
            cmd.exe /c $deleteService 2>$null | Out-Null

            # Wait for service to be deleted
            $maxWait = 10
            $waitCount = 0
            while ((Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) -and ($waitCount -lt $maxWait)) {
                Start-Sleep -Seconds 1
                $waitCount++
            }
        }

        # Kill any running Nexroll processes
        $processes = Get-Process -Name "Nexroll" -ErrorAction SilentlyContinue
        if ($processes) {
            Write-Host "Terminating running Nexroll processes..." -ForegroundColor Gray
            foreach ($process in $processes) {
                try {
                    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                    Write-Host "Terminated process $($process.Id)" -ForegroundColor Gray
                } catch {
                    Write-Host "Could not terminate process $($process.Id): $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
            Start-Sleep -Seconds 2  # Give processes time to fully terminate
        }

        # Additional cleanup - kill any processes using files in the install directory
        if (Test-Path $InstallPath) {
            $lockedFiles = Get-ChildItem -Path $InstallPath -File -Recurse -ErrorAction SilentlyContinue | Where-Object {
                try {
                    $fileStream = [System.IO.File]::Open($_.FullName, 'Open', 'Read', 'None')
                    $fileStream.Close()
                    $fileStream.Dispose()
                    $false
                } catch {
                    $true
                }
            }

            if ($lockedFiles) {
                Write-Host "Found locked files, attempting to terminate locking processes..." -ForegroundColor Yellow
                # This is a more aggressive approach - kill any process that might be locking our files
                $handlePath = Join-Path $env:TEMP "handle.exe"
                if (Test-Path $handlePath) {
                    foreach ($file in $lockedFiles) {
                        $output = & $handlePath $file.FullName 2>$null
                        $processIds = $output | Select-String -Pattern "pid: (\d+)" | ForEach-Object { $_.Matches.Groups[1].Value } | Select-Object -Unique
                        foreach ($processId in $processIds) {
                            try {
                                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                                Write-Host "Terminated locking process $processId" -ForegroundColor Gray
                            } catch {
                                Write-Host "Could not terminate process $processId" -ForegroundColor Yellow
                            }
                        }
                    }
                }
                Start-Sleep -Seconds 3
            }
        }

        # BACKUP EXISTING CONFIGURATION FILES
        $configBackup = @{}
        $configFiles = @("appsettings.json")

        Write-Host "Backing up existing configuration..." -ForegroundColor Gray
        foreach ($configFile in $configFiles) {
            $configPath = Join-Path $InstallPath $configFile
            if (Test-Path $configPath) {
                try {
                    $configBackup[$configFile] = Get-Content -Path $configPath -Raw
                    Write-Host "Backed up $configFile" -ForegroundColor Gray
                } catch {
                    Write-Host "Warning: Could not backup $configFile" -ForegroundColor Yellow
                }
            }
        }

        if (-not (Test-Path $InstallPath)) {
            New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        }

        # Copy all files with retry logic for locked files
        $files = Get-ChildItem -Path $extractPath
        foreach ($file in $files) {
            $destinationPath = Join-Path $InstallPath $file.Name
            $maxRetries = 3
            $retryCount = 0
            $success = $false

            while (-not $success -and $retryCount -lt $maxRetries) {
                try {
                    if ($file.PSIsContainer) {
                        Copy-Item -Path $file.FullName -Destination $destinationPath -Recurse -Force
                    } else {
                        Copy-Item -Path $file.FullName -Destination $destinationPath -Force
                    }
                    $success = $true
                } catch {
                    $retryCount++
                    if ($retryCount -lt $maxRetries) {
                        Write-Host "File $($file.Name) is locked, retrying in 2 seconds... ($retryCount/$maxRetries)" -ForegroundColor Yellow
                        Start-Sleep -Seconds 2
                    } else {
                        Write-Host "Warning: Could not copy $($file.Name) after $maxRetries attempts" -ForegroundColor Yellow
                        $success = $true  # Continue with other files
                    }
                }
            }
        }

        # RESTORE CONFIGURATION FILES
        Write-Host "Restoring configuration files..." -ForegroundColor Gray
        foreach ($configFile in $configFiles) {
            if ($configBackup.ContainsKey($configFile)) {
                $configPath = Join-Path $InstallPath $configFile
                try {
                    Set-Content -Path $configPath -Value $configBackup[$configFile] -Force
                    Write-Host "Restored $configFile" -ForegroundColor Gray
                } catch {
                    Write-Host "Warning: Could not restore $configFile" -ForegroundColor Yellow
                }
            }
        }

        # Clean up
        Remove-Item $tempPath -Force
        Remove-Item $extractPath -Recurse -Force

        Write-Success "Application installed to $InstallPath"
    } catch {
        Write-Error "Failed to install application: $($_.Exception.Message)"
        exit 1
    }
}

function Install-WindowsService {
    Write-Step "Installing Windows service..."

    try {
        $servicePath = Join-Path $InstallPath "Nexroll.exe"

        if (-not (Test-Path $servicePath)) {
            # Try with .dll extension (framework-dependent deployment)
            $servicePath = Join-Path $InstallPath "Nexroll.dll"
        }

        if (Test-Path $servicePath) {
            # Check if service already exists
            $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($existingService) {
                Write-Host "Removing existing service..." -ForegroundColor Gray
                # Stop the service if running
                Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
                # Delete the existing service
                $deleteService = "sc.exe delete `"$ServiceName`""
                cmd.exe /c $deleteService 2>$null

                # Wait for service to be fully deleted
                $maxWait = 10
                $waitCount = 0
                while ((Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) -and ($waitCount -lt $maxWait)) {
                    Start-Sleep -Seconds 1
                    $waitCount++
                    Write-Host "Waiting for service deletion... ($waitCount/$maxWait)" -ForegroundColor Gray
                }

                if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
                    Write-Host "Warning: Could not delete existing service. Trying to continue..." -ForegroundColor Yellow
                }
            }

            # Create Windows service
            $createService = "sc.exe create `"$ServiceName`" binPath= `"$servicePath`" start= auto"
            cmd.exe /c $createService 2>$null

            # Set service description
            $setDescription = "sc.exe description `"$ServiceName`" `"Plex Preroll Manager - Manages Plex cinema prerolls`""
            cmd.exe /c $setDescription 2>$null

            Write-Success "Windows service installed"
        } else {
            Write-Host "Warning: Service executable not found. Manual start required." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Warning: Failed to create Windows service. You can start the application manually." -ForegroundColor Yellow
    }
}

function Start-Application {
    Write-Step "Starting application..."

    try {
        # Try to start the service
        Start-Service -Name $ServiceName -ErrorAction SilentlyContinue

        # Wait a moment for the service to start
        Start-Sleep -Seconds 3

        # Check if service is running
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq "Running") {
            Write-Success "Service started successfully"
        } else {
            Write-Host "Service not running. You may need to start it manually." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Could not start service automatically. Please start manually." -ForegroundColor Yellow
    }
}

function Show-Completion {
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "                   Installation Complete!                      " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Plex Preroll Manager has been installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Access the web interface at: http://localhost:8089" -ForegroundColor Cyan
    Write-Host "Installation directory: $InstallPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Open http://localhost:8089 in your web browser" -ForegroundColor White
    Write-Host "2. Authenticate with your Plex.tv account" -ForegroundColor White
    Write-Host "3. Configure your Plex server connection" -ForegroundColor White
    Write-Host "4. Upload preroll videos and create categories" -ForegroundColor White
    Write-Host ""
    Write-Host "For help, visit: https://github.com/JFLXCLOUD/Nexroll" -ForegroundColor Gray
}

# Main installation process
try {
    Write-Header

    # Check administrator privileges
    if (-not (Test-Administrator)) {
        Write-Error "Administrator privileges required. Please run as administrator."
        exit 1
    }

    Write-Host "Installing $AppName v$Version" -ForegroundColor Cyan
    Write-Host ""

    # Check .NET installation
    if (-not $SkipDotNet) {
        if (-not (Test-DotNet)) {
            $installDotNet = Read-Host "Install .NET 9.0 Runtime? (Y/N)"
            if ($installDotNet -match "^[Yy]") {
                Install-DotNet
            } else {
                Write-Host "Skipping .NET installation. Make sure .NET 9.0 is installed manually." -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "Skipping .NET check as requested" -ForegroundColor Gray
    }

    # Get latest release
    Write-Step "Checking for latest release..."
    $release = Get-LatestRelease
    Write-Success "Found release $($release.Version)"

    # Check for existing installation
    if ((Test-Path $InstallPath) -and -not $Force) {
        Write-Host "Existing installation found at $InstallPath" -ForegroundColor Yellow
        $reinstall = Read-Host "Reinstall? This will stop the service and replace all files. (Y/N)"
        if ($reinstall -notmatch "^[Yy]") {
            Write-Host "Installation cancelled" -ForegroundColor Yellow
            exit 0
        }
    }

    # Install application
    Install-Application -DownloadUrl $release.DownloadUrl -FileName $release.FileName

    # Install Windows service
    Install-WindowsService

    # Start application
    Start-Application

    # Show completion message
    Show-Completion

} catch {
    Write-Error "Installation failed: $($_.Exception.Message)"
    exit 1
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
