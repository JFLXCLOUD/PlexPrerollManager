@echo off
REM PlexPrerollManager Installer Builder
REM This script builds a professional Windows installer using Inno Setup

REM Change to the script's directory
cd /d "%~dp0"

echo.
echo ========================================
echo  PlexPrerollManager Installer Builder
echo ========================================
echo.
echo Current directory: %CD%
echo.

REM Check if Inno Setup is installed
echo Checking for Inno Setup Compiler...
iscc >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Inno Setup Compiler (iscc) not found in PATH!
    echo.
    echo Checking common installation locations...

    REM Check common Inno Setup installation paths
    set "ISCC_EXE="
    if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    ) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
        set "ISCC_EXE=C:\Program Files\Inno Setup 6\ISCC.exe"
    ) else if exist "C:\Program Files (x86)\Inno Setup 5\ISCC.exe" (
        set "ISCC_EXE=C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
    ) else if exist "C:\Program Files\Inno Setup 5\ISCC.exe" (
        set "ISCC_EXE=C:\Program Files\Inno Setup 5\ISCC.exe"
    )

    if defined ISCC_EXE (
        echo Found Inno Setup at: %ISCC_EXE%
        echo Adding to PATH for this session...
        set "PATH=%PATH%;%ISCC_EXE:~0,-8%"
    ) else (
        echo.
        echo ========================================
        echo  INNO SETUP NOT FOUND!
        echo ========================================
        echo.
        echo Inno Setup Compiler (iscc) is required to build the installer.
        echo.
        echo Please install Inno Setup:
        echo.
        echo Option 1 - Direct Download:
        echo 1. Go to: https://jrsoftware.org/isinfo.php
        echo 2. Download and install Inno Setup
        echo 3. Make sure it's added to your system PATH
        echo.
        echo Option 2 - Chocolatey (if you have it installed):
        echo choco install innosetup
        echo.
        echo After installation, run this script again.
        echo.
        echo ========================================
        echo  Script completed. Press any key to exit...
        echo ========================================
        pause >nul
        exit /b 1
    )
) else (
    echo Found Inno Setup Compiler in PATH.
)
echo.

REM Build the application first
echo.
echo Building PlexPrerollManager...
echo Command: dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
echo.

dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo  BUILD FAILED!
    echo ========================================
    echo.
    echo The .NET build failed. This could be due to:
    echo - Missing .NET 9.0 SDK
    echo - Compilation errors in the code
    echo - Missing dependencies
    echo.
    echo Please check the error messages above and fix any issues.
    echo.
    pause
    exit /b 1
)
echo.
echo Build completed successfully!

REM Create installer
echo.
echo ========================================
echo  Creating Professional Installer
echo ========================================
echo.
echo Command: iscc installer.iss
echo.

iscc installer.iss
if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo  INSTALLER CREATION FAILED!
    echo ========================================
    echo.
    echo The installer creation failed. This could be due to:
    echo - Missing installer.iss file
    echo - Syntax errors in the installer script
    echo - Missing publish directory
    echo - Permission issues
    echo.
    echo Please check the error messages above and ensure:
    echo 1. installer.iss exists in the current directory
    echo 2. The publish directory contains the built application
    echo 3. Inno Setup is properly installed
    echo.
    pause
    exit /b 1
)

REM Verify installer was created
if exist "installer\PlexPrerollManager-Setup-2.0.0.exe" (
    for %%A in ("installer\PlexPrerollManager-Setup-2.0.0.exe") do set "installer_size=%%~zA"
    echo.
    echo ========================================
    echo  INSTALLER CREATED SUCCESSFULLY!
    echo ========================================
    echo.
    echo Files created:
    echo   installer\PlexPrerollManager-Setup-2.0.0.exe (!installer_size! bytes)
    echo.
    echo This is a professional Windows installer that:
    echo - Includes .NET runtime (no separate download needed)
    echo - Creates desktop and start menu shortcuts
    echo - Installs as Windows service (optional)
    echo - Provides uninstaller through Windows Add/Remove Programs
    echo - Professional installation wizard
    echo.
    echo Users can now install with a single EXE file!
    echo.
    echo Next steps:
    echo 1. Test the installer locally
    echo 2. Upload to GitHub Releases
    echo 3. Share with users
    echo.
) else (
    echo.
    echo ========================================
    echo  WARNING: Installer file not found!
    echo ========================================
    echo.
    echo The build completed but the installer file was not created.
    echo Please check the installer directory and error messages above.
    echo.
)

echo.
echo ========================================
echo  Script completed. Press any key to exit...
echo ========================================
pause >nul