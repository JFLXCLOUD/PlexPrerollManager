@echo off
REM PlexPrerollManager Installer Builder (Framework-Dependent)
REM This creates a smaller installer that requires .NET to be installed separately

REM Change to the parent directory (project root)
cd /d "%~dp0.."

echo.
echo ========================================
echo  PlexPrerollManager Framework-Dependent Build
echo ========================================
echo.
echo This will create a smaller installer that requires .NET 9.0 to be installed.
echo Users will need to install .NET 9.0 separately if they don't have it.
echo.
echo Press any key to continue...
pause >nul

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
        echo Please install Inno Setup from:
        echo https://jrsoftware.org/isinfo.php
        echo.
        echo Or install via Chocolatey:
        echo choco install innosetup
        echo.
        echo Then add it to your system PATH.
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

REM Build the application (framework-dependent)
echo.
echo Building PlexPrerollManager (Framework-Dependent)...
echo Command: dotnet publish PlexPrerollManager.csproj -c Release -r win-x64 -p:PublishSingleFile=true -o publish-framework
echo.

REM Clean any existing publish directory
if exist "publish-framework" (
    echo Cleaning existing publish-framework directory...
    rd /s /q "publish-framework"
)

dotnet publish PlexPrerollManager.csproj -c Release -r win-x64 -p:PublishSingleFile=true -o publish-framework
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
    echo - Project file not found
    echo.
    echo Please check the error messages above and fix any issues.
    echo.
    echo Current directory contents:
    dir /b
    echo.
    pause
    exit /b 1
)

echo.
echo Build completed successfully!

REM Verify the build output
echo.
echo Checking build output...
if exist "publish-framework" (
    echo Contents of publish-framework directory:
    dir /b "publish-framework"
    echo.
) else (
    echo ERROR: publish-framework directory was not created!
    echo.
    pause
    exit /b 1
)

REM Create installer
echo.
echo ========================================
echo  Creating Framework-Dependent Installer
echo ========================================
echo.
echo Command: iscc /DFrameworkDependent installer-src\installer.iss
echo.

iscc /DFrameworkDependent installer-src\installer.iss
if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo  INSTALLER CREATION FAILED!
    echo ========================================
    echo.
    echo The installer creation failed. This could be due to:
    echo - Missing installer.iss file
    echo - Syntax errors in the installer script
    echo - Missing publish-framework directory
    echo - Permission issues
    echo.
    echo Please check the error messages above and ensure:
    echo 1. installer.iss exists in the current directory
    echo 2. The publish-framework directory contains the built application
    echo 3. Inno Setup is properly installed
    echo.
    pause
    exit /b 1
)

REM Verify installer was created
if exist "installer\PlexPrerollManager-Setup-2.0.0-Framework-Dependent.exe" (
    for %%A in ("installer\PlexPrerollManager-Setup-2.0.0-Framework-Dependent.exe") do set "installer_size=%%~zA"
    echo.
    echo ========================================
    echo  FRAMEWORK-DEPENDENT INSTALLER CREATED!
    echo ========================================
    echo.
    echo Files created:
    echo   installer\PlexPrerollManager-Setup-2.0.0-Framework-Dependent.exe (!installer_size! bytes)
    echo.
    echo This installer:
    echo - Does NOT include .NET runtime (smaller size)
    echo - Requires .NET 9.0 to be installed separately
    echo - Checks for .NET during installation
    echo - Creates desktop and start menu shortcuts
    echo - Installs as Windows service (optional)
    echo - Provides uninstaller through Windows Add/Remove Programs
    echo.
    echo Users will need to install .NET 9.0 from:
    echo https://dotnet.microsoft.com/download/dotnet/9.0
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