#Requires -Version 5.1
#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "$env:ProgramFiles\PlexPrerollManager",
    [string]$DataPath = "$env:ProgramData\PlexPrerollManager"
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
    Write-Host "✗ .NET 9.0 not found or too old (found: $dotnetVersion)" -ForegroundColor Red
    Write-Host ""
    Write-Host "PLEXPREROLLMANAGER REQUIRES .NET 9.0 RUNTIME" -ForegroundColor Yellow
    Write-Host "This application is built with .NET 9.0 and cannot run without it." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Download and install .NET 9.0 from:" -ForegroundColor Cyan
    Write-Host "https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Choose the 'ASP.NET Core Runtime 9.0.x' download for Windows." -ForegroundColor Gray
    Write-Host ""
    Write-Host "After installation:" -ForegroundColor Yellow
    Write-Host "1. Restart PowerShell (or open a new PowerShell window)" -ForegroundColor Yellow
    Write-Host "2. Run this installer again" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "✓ .NET $dotnetVersion found" -ForegroundColor Green

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

Write-Host ""
Write-Host "✓ Installation complete!" -ForegroundColor Green
Write-Host "Web Interface: http://localhost:8089" -ForegroundColor Cyan
Write-Host "Install Path: $InstallPath" -ForegroundColor Gray
Write-Host "Data Path: $DataPath" -ForegroundColor Gray
