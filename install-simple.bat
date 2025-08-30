@echo off
REM PlexPrerollManager Simple Installer
REM This batch file provides a simple installation option

echo.
echo ============================================
echo  PlexPrerollManager Simple Installer
echo ============================================
echo.

REM Check if .NET is installed
echo Checking .NET installation...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET is not installed.
    echo Please install .NET 9.0 from: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)
echo .NET found!

REM Check if FFmpeg is installed
echo Checking FFmpeg installation...
ffmpeg -version >nul 2>&1
if %errorlevel% neq 0 (
    echo WARNING: FFmpeg not found.
    echo Please install FFmpeg from: https://ffmpeg.org/download.html
    echo Add it to your PATH after installation.
    echo.
    echo Press any key to continue without FFmpeg (limited functionality)...
    pause >nul
) else (
    echo FFmpeg found!
)

REM Build the application
echo.
echo Building PlexPrerollManager...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo Build completed successfully!

REM Create data directory
echo.
echo Creating data directories...
if not exist "%ProgramData%\PlexPrerollManager" mkdir "%ProgramData%\PlexPrerollManager"
if not exist "%ProgramData%\PlexPrerollManager\Prerolls" mkdir "%ProgramData%\PlexPrerollManager\Prerolls"
echo Data directories created!

REM Copy appsettings.json if it doesn't exist
if not exist "%ProgramData%\PlexPrerollManager\appsettings.json" (
    echo Creating default configuration...
    copy "appsettings.json" "%ProgramData%\PlexPrerollManager\appsettings.json" >nul
    echo Default configuration created!
)

echo.
echo ============================================
echo  Installation Complete!
echo ============================================
echo.
echo Your PlexPrerollManager is ready to use!
echo.
echo To start the application:
echo 1. Run: dotnet run --project PlexPrerollManager.csproj
echo 2. Open your browser to: http://localhost:8089
echo.
echo For production use, consider installing as a Windows service.
echo Run install.bat for full service installation.
echo.
echo ============================================
echo  Quick Start Guide
echo ============================================
echo.
echo 1. Start the app: dotnet run
echo 2. Open: http://localhost:8089
echo 3. Configure your Plex server
echo 4. Upload your first preroll videos!
echo 5. Try the new Bulk Upload feature!
echo.
echo Happy preroll managing!
echo.
pause