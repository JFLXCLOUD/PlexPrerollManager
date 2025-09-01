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
try {
    $dotnetVersion = & dotnet --version 2>$null
    if (-not $dotnetVersion -or $dotnetVersion -lt "9.0") {
        throw ".NET 9.0 required"
    }
    Write-Host "✓ .NET $dotnetVersion found" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET 9.0 not found. Please install from: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red
    exit 1
}

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
    $configJson = '{
  "Plex": {
    "Url": "http://localhost:32400",
    "Token": ""
  },
  "PrerollManager": {
    "PrerollsPath": "' + $DataPath + '\\Prerolls",
    "ConfigPath": "' + $DataPath + '\\config.json"
  }
}'
    $configJson | Out-File -FilePath $configPath -Encoding UTF8
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
