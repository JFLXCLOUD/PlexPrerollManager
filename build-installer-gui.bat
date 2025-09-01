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

REM Check if publish directory has files
dir /b "publish" >nul 2>&1
if errorlevel 1 (
    echo ERROR: publish directory is empty!
    echo.
    echo The publish directory exists but contains no files.
    echo Please run build-installer.bat again to rebuild the application.
    echo.
    echo Command: build-installer.bat
    echo.
    pause
    exit /b 1
)

REM Check for executable files in publish directory
echo Checking for executable files...
set "exe_found="
for %%f in ("publish\*.exe") do (
    echo Found executable: %%~nf%%~xf
    set "exe_found=1"
)

if not defined exe_found (
    echo ERROR: No executable files found in publish directory!
    echo.
    echo The publish directory exists but doesn't contain any .exe files.
    echo This usually means the build failed or produced unexpected output.
    echo.
    echo Please check the build output for errors and run build-installer.bat again.
    echo.
    echo Contents of publish directory:
    if exist "publish" dir /b "publish"
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
echo Contents of publish directory:
dir /b "publish"
echo.
echo ========================================
echo  Ready for Inno Setup GUI
echo ========================================
echo.
echo IMPORTANT: Make sure Inno Setup Compiler is running from this directory!
echo.
echo Steps to create the installer:
echo 1. Open Inno Setup Compiler
echo 2. File -> Open -> Select 'installer.iss' from this folder
echo 3. Click 'Compile' (F9)
echo 4. The installer will be created in 'installer\' folder
echo.
echo The installer will include:
echo - PlexPrerollManager.exe and dependencies (from publish\)
echo - dashboard.html (web interface)
echo - scheduling-dashboard.html (scheduling interface)
echo - appsettings.json (configuration)
echo.
echo If you get file not found errors, make sure you're opening
echo installer.iss from the correct directory (this project folder).
echo.
pause