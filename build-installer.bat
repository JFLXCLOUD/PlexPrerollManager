@echo off
REM PlexPrerollManager Installer Builder
REM This script builds a professional Windows installer using Inno Setup

echo.
echo ========================================
echo  PlexPrerollManager Installer Builder
echo ========================================
echo.

REM Check if Inno Setup is installed
iscc >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Inno Setup Compiler (iscc) not found!
    echo.
    echo Please install Inno Setup from:
    echo https://jrsoftware.org/isinfo.php
    echo.
    echo Or install via Chocolatey:
    echo choco install innosetup
    echo.
    pause
    exit /b 1
)

REM Build the application first
echo Building PlexPrerollManager...
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

REM Create installer
echo.
echo Creating installer...
iscc installer.iss
if %errorlevel% neq 0 (
    echo ERROR: Installer creation failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo  INSTALLER CREATED SUCCESSFULLY!
echo ========================================
echo.
echo Files created:
echo   installer\PlexPrerollManager-Setup-2.0.0.exe
echo.
echo This is a professional Windows installer that:
echo - Includes .NET runtime (no separate download needed)
echo - Creates desktop and start menu shortcuts
echo - Installs as Windows service (optional)
echo - Provides uninstaller
echo.
echo Users can now install with a single EXE file!
echo.
pause