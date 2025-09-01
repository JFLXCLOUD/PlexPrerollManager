@echo off
REM PlexPrerollManager Installer Builder for Inno Setup GUI
REM Run this from Inno Setup Compiler GUI

REM Change to the script's directory
cd /d "%~dp0"

echo.
echo ========================================
echo  Building Installer for Inno Setup GUI
echo ========================================
echo.
echo Current directory: %CD%
echo.

REM Check if publish directory exists
if not exist "publish" (
    echo ERROR: publish directory not found!
    echo.
    echo Please run build-installer.bat first to build the application.
    echo This will create the publish directory with the compiled files.
    echo.
    echo Command: build-installer.bat
    echo.
    pause
    exit /b 1
)

REM Check if required files exist
if not exist "dashboard.html" (
    echo ERROR: dashboard.html not found!
    pause
    exit /b 1
)

if not exist "scheduling-dashboard.html" (
    echo ERROR: scheduling-dashboard.html not found!
    pause
    exit /b 1
)

if not exist "appsettings.json" (
    echo ERROR: appsettings.json not found!
    pause
    exit /b 1
)

echo All required files found!
echo.
echo ========================================
echo  Ready for Inno Setup GUI
echo ========================================
echo.
echo Now you can:
echo 1. Open Inno Setup Compiler
echo 2. Open this file: installer.iss
echo 3. Click Compile
echo.
echo The installer will include:
echo - PlexPrerollManager.exe (from publish\)
echo - dashboard.html
echo - scheduling-dashboard.html
echo - appsettings.json
echo.
pause