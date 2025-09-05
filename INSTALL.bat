@echo off
setlocal enabledelayedexpansion

:: PlexPrerollManager Installation Launcher
:: This batch file launches the PowerShell installer with proper execution policy

title PlexPrerollManager Installer

echo.
echo =================================================================
echo                     PlexPrerollManager                        
echo                    Installation Launcher                      
echo =================================================================
echo.

:: Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Administrator privileges required
    echo.
    echo Please right-click this file and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] Administrator privileges confirmed
echo.

:: Check if PowerShell is available
powershell -Command "Write-Host 'PowerShell available'" >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] PowerShell not found
    echo.
    echo PowerShell is required for installation
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] PowerShell available
echo.

:: Check if install.ps1 exists
if not exist "install.ps1" (
    echo [ERROR] install.ps1 not found
    echo.
    echo Please ensure install.ps1 is in the same directory as this batch file
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] Installation script found
echo.

echo [STEP] Starting PowerShell installer...
echo.

:: Set working directory to script location
cd /d "%~dp0"

:: Launch PowerShell installer with proper execution policy
powershell -ExecutionPolicy Bypass -File "%~dp0install.ps1"

:: Check if installation was successful
if %errorLevel% equ 0 (
    echo.
    echo [SUCCESS] Installation completed successfully!
    echo.
    echo Web Interface: http://localhost:8089
    echo.
) else (
    echo.
    echo [ERROR] Installation failed with error code %errorLevel%
    echo.
    echo Please check the error messages above and try again
    echo.
)

echo Press any key to exit...
pause >nul