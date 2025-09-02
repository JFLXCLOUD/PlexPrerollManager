@echo off
setlocal enabledelayedexpansion

:: =============================================================================
:: Plex Preroll Manager Installation Script
:: =============================================================================
:: This script installs PlexPrerollManager as a Windows service
:: Run this script as Administrator for proper installation
:: =============================================================================

:: Set console colors for better output
color 0A

:: -------------------------------------------
:: Configuration Variables
:: -------------------------------------------
set "SERVICE_NAME=PlexPrerollManager"
set "SERVICE_DISPLAY_NAME=Plex Preroll Manager"
set "SERVICE_DESCRIPTION=Manages Plex cinema prerolls with web dashboard"
set "PUBLISH_DIR=%~dp0bin\Release\net9.0\win-x64\publish"
set "INSTALL_DIR=C:\Program Files\PlexPrerollManager"
set "DATA_DIR=C:\ProgramData\PlexPrerollManager"
set "LOG_DIR=%DATA_DIR%\logs"
set "WEB_PORT=8089"
set "EXE_PATH=%INSTALL_DIR%\PlexPrerollManager.exe"

:: -------------------------------------------
:: Admin Privilege Check
:: -------------------------------------------
echo.
echo ========================================
echo [CHECK] Checking Administrator Privileges
echo ========================================
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Running as Administrator
) else (
    echo [ERROR] This script must be run as Administrator!
    echo.
    echo Please:
    echo 1. Right-click on install.bat
    echo 2. Select "Run as administrator"
    echo.
    pause
    exit /b 1
)

:: -------------------------------------------
:: Display Installation Info
:: -------------------------------------------
echo.
echo ========================================
echo [PLEX] Plex Preroll Manager Installer
echo ========================================
echo Service Name: %SERVICE_NAME%
echo Install Path: %INSTALL_DIR%
echo Data Path:    %DATA_DIR%
echo Web UI:       http://localhost:%WEB_PORT%
echo ========================================
echo.

:: -------------------------------------------
:: Stop and Remove Existing Service
:: -------------------------------------------
echo [INFO] Checking for existing service...
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% == 0 (
    echo [INFO] Found existing service, stopping and removing...
    sc stop %SERVICE_NAME% >nul 2>&1
    timeout /t 3 /nobreak >nul
    sc delete %SERVICE_NAME% >nul 2>&1
    echo [OK] Existing service removed
) else (
    echo [OK] No existing service found
)

:: -------------------------------------------
:: Clean and Rebuild Application
:: -------------------------------------------
echo.
echo ========================================
echo [BUILD] Building Application
echo ========================================

cd /d "%~dp0"
echo Current Directory: %CD%

:: Clean previous build
if exist "%PUBLISH_DIR%" (
    echo [CLEAN] Cleaning previous build...
    rd /s /q "%PUBLISH_DIR%" >nul 2>&1
)

:: Build the application
echo [BUILD] Building Plex Preroll Manager...
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    echo Please check for compilation errors and try again.
    echo.
    pause
    exit /b 1
)

echo [OK] Build completed successfully

:: -------------------------------------------
:: Install Application Files
:: -------------------------------------------
echo.
echo ========================================
echo [FILES] Installing Application Files
echo ========================================

:: Create installation directory
if exist "%INSTALL_DIR%" (
    echo [REMOVE] Removing old installation...
    rd /s /q "%INSTALL_DIR%" >nul 2>&1
)

echo [CREATE] Creating installation directory...
mkdir "%INSTALL_DIR%" 2>nul

:: Copy dashboard.html to publish directory
echo [INFO] Copying dashboard.html to publish directory...
copy "%~dp0dashboard.html" "%PUBLISH_DIR%\" >nul

:: Copy application files
echo [INFO] Copying application files...
xcopy "%PUBLISH_DIR%\*" "%INSTALL_DIR%\" /e /y /i >nul

if not exist "%EXE_PATH%" (
    echo [ERROR] Failed to copy application files!
    pause
    exit /b 1
)

echo [OK] Application files installed

:: -------------------------------------------
:: Setup Data Directories
:: -------------------------------------------
echo.
echo ========================================
echo [DATA] Setting Up Data Directories
echo ========================================

:: Create data directory
if not exist "%DATA_DIR%" (
    echo [CREATE] Creating data directory...
    mkdir "%DATA_DIR%" 2>nul
)

:: Create logs directory
if not exist "%LOG_DIR%" (
    echo [CREATE] Creating logs directory...
    mkdir "%LOG_DIR%" 2>nul
)

:: Create default config if not exists
if not exist "%DATA_DIR%\config.json" (
    echo [CONFIG] Creating default configuration...
    echo { } > "%DATA_DIR%\config.json"
)

echo [OK] Data directories configured

:: -------------------------------------------
:: Register Windows Service
:: -------------------------------------------
echo.
echo ========================================
echo [SERVICE] Registering Windows Service
echo ========================================

echo [INFO] Creating service: %SERVICE_NAME%

:: Create the service with proper syntax
sc create "%SERVICE_NAME%" ^
    binPath= "\"%EXE_PATH%\"" ^
    DisplayName= "%SERVICE_DISPLAY_NAME%" ^
    start= auto ^
    type= own ^
    error= normal

if errorlevel 1 (
    echo [ERROR] Failed to create service!
    echo Error code: %errorLevel%
    pause
    exit /b 1
)

:: Set service description
sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%"

:: Start the service
echo [START] Starting service...
sc start "%SERVICE_NAME%"

if errorlevel 1 (
    echo [WARNING] Service created but failed to start automatically
    echo You may need to start it manually or check the Event Viewer for errors
) else (
    echo [OK] Service started successfully
)

:: Verify service status
timeout /t 2 /nobreak >nul
sc query "%SERVICE_NAME%" | findstr "STATE" >nul
if errorlevel 1 (
    echo [WARNING] Could not verify service status
) else (
    echo [INFO] Service status verified
)

:: -------------------------------------------
:: Configure Firewall
:: -------------------------------------------
echo.
echo ========================================
echo [FIREWALL] Configuring Firewall
echo ========================================

echo [INFO] Adding firewall rule for port %WEB_PORT%...
netsh advfirewall firewall add rule ^
    name="%SERVICE_NAME%" ^
    dir=in ^
    action=allow ^
    protocol=TCP ^
    localport=%WEB_PORT% ^
    profile=any ^
    description="Allow Plex Preroll Manager web interface" >nul 2>&1

if errorlevel 1 (
    echo [WARNING] Could not add firewall rule automatically
    echo You may need to manually allow port %WEB_PORT% in Windows Firewall
) else (
    echo [OK] Firewall rule added
)

:: -------------------------------------------
:: Installation Complete
:: -------------------------------------------
echo.
echo ========================================
echo [SUCCESS] INSTALLATION COMPLETE!
echo ========================================
echo.
echo [WEB] Web Interface: http://localhost:%WEB_PORT%
echo [SERVICE] Service Name:  %SERVICE_NAME%
echo [FILES] Install Path:  %INSTALL_DIR%
echo [DATA] Data Path:     %DATA_DIR%
echo [NOTE] Logs Path:     %LOG_DIR%
echo.
echo [TOOLS] Service Management Commands:
echo    Start:  sc start %SERVICE_NAME%
echo    Stop:   sc stop %SERVICE_NAME%
echo    Status: sc query %SERVICE_NAME%
echo    Logs:   eventvwr.msc (Windows Logs ^> Application)
echo.
echo [NOTE] Next Steps:
echo 1. Open http://localhost:%WEB_PORT% in your browser
echo 2. Upload some video files to test
echo 3. Configure your Plex token in %DATA_DIR%\config.json
echo.
echo ========================================
echo.

:: Wait for user to see the completion message
timeout /t 5 /nobreak >nul

endlocal
