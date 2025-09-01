#Requires -Version 5.1
#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "$env:ProgramFiles\PlexPrerollManager",
    [string]$DataPath = "$env:ProgramData\PlexPrerollManager",
    [switch]$InstallDotNet
)

$ErrorActionPreference = "Stop"

Write-Host "PlexPrerollManager Installer" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan

# Check .NET 9.0
Write-Host "Checking .NET 9.0..." -ForegroundColor Yellow

# Try to find dotnet in common locations
$dotnetFound = $false
$dotnetVersion = $null

# First try direct command
try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($dotnetVersion) {
        $dotnetFound = $true
    }
} catch {
    # Try common installation paths
    $commonPaths = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:ProgramFiles (x86)\dotnet\dotnet.exe",
        "$env:LocalAppData\Microsoft\dotnet\dotnet.exe",
        "$env:USERPROFILE\.dotnet\dotnet.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            try {
                $dotnetVersion = & $path --version 2>$null
                if ($dotnetVersion) {
                    $dotnetFound = $true
                    break
                }
            } catch {
                continue
            }
        }
    }
}

if (-not $dotnetFound -or -not $dotnetVersion -or $dotnetVersion -lt "9.0") {
    if ($InstallDotNet) {
        Write-Host "[INFO] .NET 9.0 not found, installing automatically..." -ForegroundColor Yellow
        Write-Host ""

        try {
            # Download and run the dotnet-install script
            $installScriptUrl = "https://dot.net/v1/dotnet-install.ps1"
            Write-Host "[DOWNLOAD] Downloading dotnet-install script..." -ForegroundColor Gray

            $installScript = Invoke-WebRequest -Uri $installScriptUrl -UseBasicParsing
            $scriptBlock = [scriptblock]::Create($installScript.Content)

            Write-Host "[INSTALL] Installing .NET 9.0 runtime..." -ForegroundColor Gray
            Write-Host "(This may take a few minutes)" -ForegroundColor Gray

            # Install .NET 9.0 runtime (not SDK) to Program Files
            & $scriptBlock -Channel 9.0 -Runtime aspnetcore -InstallDir "$env:ProgramFiles\dotnet" -NoPath

            # Refresh PATH for current session
            $dotnetPaths = @(
                "$env:ProgramFiles\dotnet",
                "$env:ProgramFiles\dotnet\dotnet.exe"
            )

            foreach ($dotnetPath in $dotnetPaths) {
                if (Test-Path $dotnetPath) {
                    if ($env:PATH -notlike "*$dotnetPath*") {
                        $env:PATH = "$dotnetPath;$env:PATH"
                        Write-Host "[OK] Added .NET to PATH: $dotnetPath" -ForegroundColor Green
                    }
                }
            }

            # Verify installation
            Write-Host "[VERIFY] Verifying installation..." -ForegroundColor Gray
            $newDotnetVersion = $null
            try {
                $newDotnetVersion = & dotnet --version 2>$null
            } catch {
                # Try direct path
                try {
                    $newDotnetVersion = & "$env:ProgramFiles\dotnet\dotnet.exe" --version 2>$null
                } catch {
                    # Ignore
                }
            }

            if ($newDotnetVersion -and $newDotnetVersion -ge "9.0") {
                Write-Host "[SUCCESS] .NET $newDotnetVersion installed successfully!" -ForegroundColor Green
                Write-Host ""
            } else {
                Write-Host "[WARNING] .NET installation completed but verification failed" -ForegroundColor Yellow
                Write-Host "Installation may have succeeded, continuing..." -ForegroundColor Yellow
                Write-Host ""
            }

        } catch {
            Write-Host "[ERROR] Automatic .NET installation failed" -ForegroundColor Red
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host ""
            Write-Host "Please install .NET 9.0 manually from:" -ForegroundColor Yellow
            Write-Host "https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
            Write-Host ""
            exit 1
        }
    } else {
        Write-Host "[ERROR] .NET 9.0 not found or too old (found: $dotnetVersion)" -ForegroundColor Red
        Write-Host ""
        Write-Host "PLEXPREROLLMANAGER REQUIRES .NET 9.0 RUNTIME" -ForegroundColor Yellow
        Write-Host "This application is built with .NET 9.0 and cannot run without it." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "OPTIONS:" -ForegroundColor Cyan
        Write-Host "1. Install automatically: Add -InstallDotNet parameter" -ForegroundColor White
        Write-Host "   Example: .\install.ps1 -InstallDotNet" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Install manually from:" -ForegroundColor White
        Write-Host "   https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Choose the 'ASP.NET Core Runtime 9.0.x' download for Windows." -ForegroundColor Gray
        Write-Host ""
        exit 1
    }
}

Write-Host "[OK] .NET $dotnetVersion found" -ForegroundColor Green

# Stop existing service
$service = Get-Service -Name "PlexPrerollManager" -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name "PlexPrerollManager" -Force -ErrorAction SilentlyContinue
    & sc.exe delete PlexPrerollManager 2>$null
}

# Create directories
Write-Host "Creating directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
New-Item -ItemType Directory -Path $DataPath -Force | Out-Null

# Copy files
Write-Host "Copying application files..." -ForegroundColor Yellow
$currentPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$items = Get-ChildItem -Path $currentPath -Exclude @("*.git*", "*.zip", "*.sha256", "release", "PlexPrerollManager-v*", "install.ps1")
foreach ($item in $items) {
    Copy-Item $item.FullName -Destination $InstallPath -Recurse -Force
}

# Build application
Write-Host "Building application..." -ForegroundColor Yellow
Push-Location $InstallPath
& dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o .
Pop-Location

# Create default config
$configPath = Join-Path $DataPath "appsettings.json"
if (-not (Test-Path $configPath)) {
    Write-Host "Creating default configuration..." -ForegroundColor Yellow
    $plexUrl = "http://localhost:32400"
    $prerollsPath = "$DataPath\Prerolls"
    $configFilePath = "$DataPath\config.json"

    # Create JSON content line by line to avoid parsing issues
    $line1 = '{'
    $line2 = '  "Plex": {'
    $line3 = '    "Url": "' + $plexUrl + '",'
    $line4 = '    "Token": ""'
    $line5 = '  },'
    $line6 = '  "PrerollManager": {'
    $line7 = '    "PrerollsPath": "' + $prerollsPath + '",'
    $line8 = '    "ConfigPath": "' + $configFilePath + '"'
    $line9 = '  }'
    $line10 = '}'

    # Write each line separately
    $line1 | Out-File -FilePath $configPath -Encoding UTF8
    $line2 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line3 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line4 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line5 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line6 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line7 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line8 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line9 | Out-File -FilePath $configPath -Append -Encoding UTF8
    $line10 | Out-File -FilePath $configPath -Append -Encoding UTF8
}

# Install service
Write-Host "Installing Windows service..." -ForegroundColor Yellow
$exePath = Join-Path $InstallPath "PlexPrerollManager.exe"
& sc.exe create PlexPrerollManager binPath= "`"$exePath`"" start= auto | Out-Null

# Start service
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name "PlexPrerollManager" -ErrorAction SilentlyContinue

# Verify service started
Start-Sleep -Seconds 3
$serviceStatus = Get-Service -Name "PlexPrerollManager" -ErrorAction SilentlyContinue

if ($serviceStatus -and $serviceStatus.Status -eq "Running") {
    Write-Host "[OK] Service is running" -ForegroundColor Green

    # Test web connectivity
    Write-Host "Testing web interface..." -ForegroundColor Gray
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8089" -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] Web interface is responding" -ForegroundColor Green
        } else {
            Write-Host "[WARNING] Web interface returned status: $($response.StatusCode)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "[WARNING] Could not connect to web interface: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "The service may still be starting up. Please wait a moment and try again." -ForegroundColor Gray
    }
} else {
    Write-Host "[WARNING] Service failed to start or is not running" -ForegroundColor Yellow
    Write-Host "Service Status: $($serviceStatus.Status)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting steps:" -ForegroundColor Cyan
    Write-Host "1. Check Windows Event Viewer for error details" -ForegroundColor White
    Write-Host "2. Try starting manually: Start-Service PlexPrerollManager" -ForegroundColor White
    Write-Host "3. Check if port 8089 is available: netstat -ano | findstr :8089" -ForegroundColor White
    Write-Host "4. Verify .NET installation: dotnet --version" -ForegroundColor White
}

Write-Host ""
Write-Host "[SUCCESS] Installation complete!" -ForegroundColor Green
Write-Host "Web Interface: http://localhost:8089" -ForegroundColor Cyan
Write-Host "Install Path: $InstallPath" -ForegroundColor Gray
Write-Host "Data Path: $DataPath" -ForegroundColor Gray

if ($serviceStatus -and $serviceStatus.Status -eq "Running") {
    Write-Host ""
    Write-Host "NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "1. Open http://localhost:8089 in your browser" -ForegroundColor White
    Write-Host "2. Configure your Plex server settings" -ForegroundColor White
    Write-Host "3. Upload your preroll videos" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "SERVICE ISSUES DETECTED - MANUAL STARTUP REQUIRED:" -ForegroundColor Yellow
    Write-Host "1. Open PowerShell as Administrator" -ForegroundColor White
    Write-Host "2. Run: Start-Service PlexPrerollManager" -ForegroundColor White
    Write-Host "3. Wait 30 seconds, then visit http://localhost:8089" -ForegroundColor White
    Write-Host "4. Check Event Viewer if issues persist" -ForegroundColor White
}
