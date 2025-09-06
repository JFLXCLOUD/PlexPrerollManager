#Requires -Version 5.1
#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Nexroll Professional Uninstaller
.DESCRIPTION
    Completely removes Nexroll from the system
.PARAMETER KeepData
    Keep user data and configuration files
.PARAMETER Force
    Force removal without confirmation
#>

param(
    [switch]$KeepData,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ServiceName = "Nexroll"
$InstallPath = "$env:ProgramFiles\Nexroll"
$DataPath = "$env:ProgramData\Nexroll"

function Write-Header {
    Clear-Host
    Write-Host "=================================================================" -ForegroundColor Red
    Write-Host "                         Nexroll                             " -ForegroundColor Red
    Write-Host "                   Professional Uninstaller                   " -ForegroundColor Red
    Write-Host "=================================================================" -ForegroundColor Red
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

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Remove-WindowsService {
    Write-Step "Removing Windows service..."
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Write-Step "Stopping service..."
            try {
                Stop-Service -Name $ServiceName -Force
                Write-Success "Service stopped"
            } catch {
                Write-Warning "Could not stop service: $($_.Exception.Message)"
            }
        }
        
        Write-Step "Deleting service..."
        & sc.exe delete $ServiceName | Out-Null
        Write-Success "Service removed"
    } else {
        Write-Success "No service found to remove"
    }
}

function Remove-Processes {
    Write-Step "Stopping any running processes..."
    
    try {
        $processes = Get-Process -Name "Nexroll" -ErrorAction SilentlyContinue
        if ($processes) {
            $processes | Stop-Process -Force
            Write-Success "Stopped $($processes.Count) process(es)"
        } else {
            Write-Success "No running processes found"
        }
    } catch {
        Write-Warning "Could not stop processes: $($_.Exception.Message)"
    }
}

function Remove-InstallationFiles {
    Write-Step "Removing installation files..."
    
    if (Test-Path $InstallPath) {
        try {
            Remove-Item -Path $InstallPath -Recurse -Force
            Write-Success "Installation directory removed: $InstallPath"
        } catch {
            Write-Warning "Could not remove installation directory: $($_.Exception.Message)"
        }
    } else {
        Write-Success "Installation directory not found"
    }
}

function Remove-DataFiles {
    if ($KeepData) {
        Write-Warning "Keeping data files as requested"
        return
    }
    
    Write-Step "Removing data files..."
    
    if (Test-Path $DataPath) {
        try {
            Remove-Item -Path $DataPath -Recurse -Force
            Write-Success "Data directory removed: $DataPath"
        } catch {
            Write-Warning "Could not remove data directory: $($_.Exception.Message)"
        }
    } else {
        Write-Success "Data directory not found"
    }
}

function Remove-Shortcuts {
    Write-Step "Removing shortcuts..."
    
    # Desktop shortcut
    $desktopShortcut = "$env:USERPROFILE\Desktop\Nexroll.lnk"
    if (Test-Path $desktopShortcut) {
        Remove-Item -Path $desktopShortcut -Force
        Write-Success "Desktop shortcut removed"
    }

    # Start Menu shortcut
    $startMenuShortcut = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nexroll.lnk"
    if (Test-Path $startMenuShortcut) {
        Remove-Item -Path $startMenuShortcut -Force
        Write-Success "Start Menu shortcut removed"
    }
}

function Remove-FirewallRules {
    Write-Step "Removing firewall rules..."
    
    try {
        Remove-NetFirewallRule -DisplayName "Nexroll*" -ErrorAction SilentlyContinue
        Write-Success "Firewall rules removed"
    } catch {
        Write-Warning "Could not remove firewall rules: $($_.Exception.Message)"
    }
}

function Show-UninstallSummary {
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "                   Uninstallation Complete!                   " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host ""
    
    if ($KeepData) {
        Write-Host "Data Preserved:" -ForegroundColor Cyan
        Write-Host "   Configuration and prerolls kept at: $DataPath" -ForegroundColor White
        Write-Host ""
    }
    
    Write-Host "Cleanup Summary:" -ForegroundColor Cyan
    Write-Host "   [OK] Windows service removed" -ForegroundColor White
    Write-Host "   [OK] Installation files removed" -ForegroundColor White
    Write-Host "   [OK] Shortcuts removed" -ForegroundColor White
    Write-Host "   [OK] Firewall rules removed" -ForegroundColor White
    if (-not $KeepData) {
        Write-Host "   [OK] Data files removed" -ForegroundColor White
    }
    Write-Host ""
    
    Write-Host "Thank you for using Nexroll!" -ForegroundColor Cyan
}

# Main uninstallation process
try {
    Write-Header
    
    Write-Host "Uninstallation Parameters:" -ForegroundColor Cyan
    Write-Host "  Install Path: $InstallPath" -ForegroundColor White
    Write-Host "  Data Path:    $DataPath" -ForegroundColor White
    Write-Host "  Keep Data:    $KeepData" -ForegroundColor White
    Write-Host ""
    
    if (-not $Force) {
        if ($KeepData) {
            $confirm = Read-Host "Remove Nexroll but keep data files? (Y/N)"
        } else {
            $confirm = Read-Host "Completely remove Nexroll and all data? (Y/N)"
        }

        if ($confirm -notmatch "^[Yy]") {
            Write-Host "Uninstallation cancelled by user" -ForegroundColor Yellow
            exit 0
        }
    }
    
    Write-Host ""
    
    # Uninstallation steps
    Remove-Processes
    Remove-WindowsService
    Remove-InstallationFiles
    Remove-DataFiles
    Remove-Shortcuts
    Remove-FirewallRules
    
    Show-UninstallSummary
    
} catch {
    Write-Host ""
    Write-Host "[ERROR] Uninstallation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")