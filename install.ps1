#Requires -Version 5.1
#Requires -RunAsAdministrator

<#
.SYNOPSIS
    PlexPrerollManager Professional Installer
.DESCRIPTION
    Installs PlexPrerollManager as a Windows service with proper configuration and cleanup
.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\PlexPrerollManager)
.PARAMETER DataPath
    Data directory (default: C:\ProgramData\PlexPrerollManager)
.PARAMETER CleanInstall
    Perform a clean installation (removes existing data)
.PARAMETER SkipService
    Install files only, don't create Windows service
#>

param(
    [string]$InstallPath = "$env:ProgramFiles\PlexPrerollManager",
    [string]$DataPath = "$env:ProgramData\PlexPrerollManager",
    [switch]$CleanInstall,
    [switch]$SkipService
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Set script root path reliably
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

# Version and branding
$Version = "2.2.0"
$ServiceName = "PlexPrerollManager"
$ServiceDisplayName = "Plex Preroll Manager"
$ServiceDescription = "Manages Plex cinema prerolls with automated scheduling and category management"

function Write-Header {
    Clear-Host
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host "                    PlexPrerollManager                        " -ForegroundColor Cyan
    Write-Host "                   Professional Installer                     " -ForegroundColor Cyan
    Write-Host "                        Version $Version                           " -ForegroundColor Cyan
    Write-Host "=================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message, [string]$Color = "Yellow")
    Write-Host "[STEP] $Message" -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Test-Prerequisites {
    Write-Step "Checking prerequisites..."
    
    # Check .NET 9.0
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if (-not $dotnetVersion -or ([version]$dotnetVersion).Major -lt 9) {
            throw ".NET 9.0 required"
        }
        Write-Success ".NET $dotnetVersion found"
    }
    catch {
        Write-Error ".NET 9.0 not found"
        Write-Host "Please install .NET 9.0 from: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
        exit 1
    }

    # Check PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        Write-Error "PowerShell 5.1 or higher required"
        exit 1
    }
    Write-Success "PowerShell $($PSVersionTable.PSVersion) found"

    # Check admin privileges
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error "Administrator privileges required"
        Write-Host "Please run PowerShell as Administrator" -ForegroundColor Cyan
        exit 1
    }
    Write-Success "Administrator privileges confirmed"
}

function Stop-ExistingService {
    Write-Step "Checking for existing service..."
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Warning "Existing service found: $($service.Status)"
        
        if ($service.Status -eq "Running") {
            Write-Step "Stopping existing service..."
            try {
                Stop-Service -Name $ServiceName -Force -ErrorAction Stop
                Write-Success "Service stopped successfully"
            } catch {
                Write-Warning "Could not stop service: $($_.Exception.Message)"
            }
        }

        Write-Step "Removing existing service..."
        try {
            & sc.exe delete $ServiceName 2>$null
            
            # Wait for service deletion
            $maxWait = 30
            $waited = 0
            while ($waited -lt $maxWait) {
                $serviceCheck = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
                if (-not $serviceCheck) {
                    Write-Success "Service removed successfully"
                    return
                }
                Start-Sleep -Seconds 2
                $waited += 2
                Write-Host "  Waiting for service deletion... ($waited/$maxWait seconds)" -ForegroundColor Gray
            }
            
            Write-Warning "Service deletion may not have completed fully"
        } catch {
            Write-Warning "Could not remove service: $($_.Exception.Message)"
        }
    } else {
        Write-Success "No existing service found"
    }
}

function Stop-ExistingProcesses {
    Write-Step "Stopping any running processes..."
    
    try {
        $processes = Get-Process -Name "PlexPrerollManager" -ErrorAction SilentlyContinue
        if ($processes) {
            $processes | Stop-Process -Force
            Write-Success "Stopped $($processes.Count) running process(es)"
        } else {
            Write-Success "No running processes found"
        }
    } catch {
        Write-Warning "Could not stop processes: $($_.Exception.Message)"
    }
}

function New-InstallationDirectories {
    Write-Step "Creating installation directories..."

    try {
        # Backup existing configuration before clean install
        $configBackup = $null
        $configPath = Join-Path $DataPath "appsettings.json"
        $installConfigPath = Join-Path $InstallPath "appsettings.json"

        if ($CleanInstall) {
            Write-Step "Performing clean installation..."

            # Backup existing config before removing directories
            if (Test-Path $configPath) {
                $configBackup = Get-Content -Path $configPath -Raw
                Write-Host "  Backed up existing configuration from data directory" -ForegroundColor Gray
            } elseif (Test-Path $installConfigPath) {
                $configBackup = Get-Content -Path $installConfigPath -Raw
                Write-Host "  Backed up existing configuration from install directory" -ForegroundColor Gray
            }
        }

        # Create install directory
        if (Test-Path $InstallPath) {
            if ($CleanInstall) {
                Remove-Item -Path $InstallPath -Recurse -Force
            }
        }
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Success "Install directory: $InstallPath"

        # Create data directory
        if (Test-Path $DataPath) {
            if ($CleanInstall) {
                Write-Warning "Removing existing data directory..."
                Remove-Item -Path $DataPath -Recurse -Force
            }
        }
        New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
        New-Item -ItemType Directory -Path "$DataPath\Prerolls" -Force | Out-Null
        New-Item -ItemType Directory -Path "$DataPath\Backups" -Force | Out-Null
        New-Item -ItemType Directory -Path "$DataPath\Logs" -Force | Out-Null
        Write-Success "Data directory: $DataPath"

        # Create web directory for HTML files
        $webPath = Join-Path $InstallPath "web"
        New-Item -ItemType Directory -Path $webPath -Force | Out-Null
        Write-Success "Web directory: $webPath"

        # Return backup for use in configuration
        return $configBackup

    } catch {
        Write-Error "Failed to create directories: $($_.Exception.Message)"
        exit 1
    }
}

function Publish-Application {
    Write-Step "Publishing application..."

    Push-Location $ScriptRoot

    try {
        # Clean previous build
        if (Test-Path "bin") { Remove-Item -Path "bin" -Recurse -Force }
        if (Test-Path "obj") { Remove-Item -Path "obj" -Recurse -Force }
        if (Test-Path "Release") { Remove-Item -Path "Release" -Recurse -Force }

        # Publish with optimizations (disable trimming to prevent assembly loading issues)
        Write-Host "  Publishing optimized release..." -ForegroundColor Gray
        & dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=false -p:WarningLevel=0 -o $InstallPath --verbosity quiet

        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }

        Write-Success "Application published successfully"
    } catch {
        Write-Error "Publish failed: $($_.Exception.Message)"
        exit 1
    } finally {
        Pop-Location
    }
}

function Copy-WebFiles {
    Write-Step "Copying web files..."

    $webPath = Join-Path $InstallPath "web"

    # HTML files to copy
    $htmlFiles = @("dashboard.html", "oauth.html", "scheduling-dashboard.html")

    foreach ($file in $htmlFiles) {
        $sourcePath = Join-Path $ScriptRoot $file
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $webPath -Force
            Write-Host "  Copied: $file" -ForegroundColor Gray
        } else {
            Write-Warning "Source file not found: $sourcePath"
        }
    }

    # Remove HTML files from root install directory (they get published there by dotnet publish)
    foreach ($file in $htmlFiles) {
        $rootFilePath = Join-Path $InstallPath $file
        if (Test-Path $rootFilePath) {
            Remove-Item -Path $rootFilePath -Force
            Write-Host "  Removed from root: $file" -ForegroundColor Gray
        }
    }

    Write-Success "Web files copied to: $webPath"
}

function New-Configuration {
    param([string]$ConfigBackup = $null)

    Write-Step "Creating configuration files..."

    $configPath = Join-Path $DataPath "appsettings.json"
    $dbPath = Join-Path $DataPath "plexprerollmanager.db"
    $prerollsPath = Join-Path $DataPath "Prerolls"

    # Check for existing configuration to preserve token
    $existingToken = ""
    $existingUrl = "http://localhost:32400"
    $existingUsername = ""
    $existingPassword = ""
    $existingApiKey = ""

    # First try to use the backup from clean install
    if ($ConfigBackup) {
        Write-Host "  Using backed up configuration from clean install" -ForegroundColor Gray
        try {
            $existingConfig = $ConfigBackup | ConvertFrom-Json
            Write-Host "  Successfully parsed backed up configuration" -ForegroundColor Gray
        } catch {
            Write-Warning "Could not parse backed up configuration: $($_.Exception.Message)"
            $ConfigBackup = $null
        }
    }

    # If no backup, check for existing config files
    if (-not $ConfigBackup) {
        $installConfigPath = Join-Path $InstallPath "appsettings.json"
        $possibleConfigPaths = @($configPath, $installConfigPath)

        $foundConfigs = @()
        foreach ($path in $possibleConfigPaths) {
            if (Test-Path $path) {
                Write-Host "  Found existing configuration at: $path" -ForegroundColor Gray
                try {
                    $rawJson = Get-Content -Path $path -Raw

                    $testConfig = $rawJson | ConvertFrom-Json
                    $hasToken = $testConfig.Plex.Token -and $testConfig.Plex.Token -ne "" -and $testConfig.Plex.Token.Length -gt 0

                    $foundConfigs += @{
                        Path = $path
                        Config = $testConfig
                        HasToken = $hasToken
                    }

                    Write-Host "  Successfully parsed existing configuration (Token: $(if ($hasToken) { "PRESENT" } else { "EMPTY" }))" -ForegroundColor $(if ($hasToken) { "Green" } else { "Yellow" })
                } catch {
                    Write-Warning "Could not read existing configuration at $path : $($_.Exception.Message)"
                    continue
                }
            }
        }

        # Select the best config (prefer one with token)
        if ($foundConfigs.Count -gt 0) {
            $configWithToken = $foundConfigs | Where-Object { $_.HasToken } | Select-Object -First 1
            if ($configWithToken) {
                $existingConfig = $configWithToken.Config
                Write-Host "  Selected configuration with token from: $($configWithToken.Path)" -ForegroundColor Green
            } else {
                # No config with token, use the first one
                $existingConfig = $foundConfigs[0].Config
                Write-Host "  No configuration with token found, using: $($foundConfigs[0].Path)" -ForegroundColor Yellow
            }
        }
    }

    # Preserve existing Plex settings if config was found
    if ($existingConfig -and $existingConfig.Plex) {

        # Check if token exists with multiple conditions
        $tokenExists = $false
        if ($existingConfig.Plex.PSObject.Properties.Name -contains "Token") {
            if ($existingConfig.Plex.Token -and $existingConfig.Plex.Token -ne "" -and $existingConfig.Plex.Token.Length -gt 0) {
                $existingToken = $existingConfig.Plex.Token
                Write-Host "  ✅ Preserved existing Plex token (length: $($existingToken.Length))" -ForegroundColor Green
                $tokenExists = $true
            }
        }

        if (-not $tokenExists) {
            Write-Host "  ❌ Token property not found or is empty/null" -ForegroundColor Red
        }

        if ($existingConfig.Plex.Url) {
            $existingUrl = $existingConfig.Plex.Url
            Write-Host "  Preserved existing Plex server URL: $existingUrl" -ForegroundColor Gray
        }
        if ($existingConfig.Plex.Username) {
            $existingUsername = $existingConfig.Plex.Username
        }
        if ($existingConfig.Plex.Password) {
            $existingPassword = $existingConfig.Plex.Password
        }
        if ($existingConfig.Plex.ApiKey) {
            $existingApiKey = $existingConfig.Plex.ApiKey
        }
    } elseif ($ConfigBackup) {
        Write-Host "  No valid configuration found in backup - performing fresh installation" -ForegroundColor Yellow
    } else {
        Write-Host "  No existing configuration found - performing fresh installation" -ForegroundColor Yellow
    }

    # Create main configuration
    $config = @{
        "ConnectionStrings" = @{
            "DefaultConnection" = "Data Source=$dbPath"
        }
        "Plex" = @{
            "Url" = $existingUrl
            "Token" = $existingToken
            "Username" = $existingUsername
            "Password" = $existingPassword
            "ApiKey" = $existingApiKey
        }
        "PrerollManager" = @{
            "PrerollsPath" = $prerollsPath
            "ConfigPath" = "$DataPath\config.json"
        }
        "Logging" = @{
            "LogLevel" = @{
                "Default" = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
            "File" = @{
                "Path" = "$DataPath\Logs\plexprerollmanager.log"
                "MaxFileSize" = "10MB"
                "MaxFiles" = 5
            }
        }
    }

    $configJson = $config | ConvertTo-Json -Depth 10
    $configJson | Out-File -FilePath $configPath -Encoding UTF8
    Write-Success "Configuration created: $configPath"

    # Copy configuration to install directory
    $installConfigPath = Join-Path $InstallPath "appsettings.json"
    Copy-Item -Path $configPath -Destination $installConfigPath -Force
    Write-Success "Configuration copied to install directory"

    # Create sample preroll categories (only if they don't exist)
    $categories = @("General", "Halloween", "Christmas", "New Years", "4th of July", "Thanksgiving", "Easter", "Valentines")
    foreach ($category in $categories) {
        $categoryPath = Join-Path $prerollsPath $category
        if (-not (Test-Path $categoryPath)) {
            New-Item -ItemType Directory -Path $categoryPath -Force | Out-Null
        }
    }
    Write-Success "Sample preroll categories created"
}

function Install-WindowsService {
    if ($SkipService) {
        Write-Warning "Skipping Windows service installation"
        return
    }
    
    Write-Step "Installing Windows service..."
    
    $exePath = Join-Path $InstallPath "PlexPrerollManager.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Error "Executable not found: $exePath"
        exit 1
    }
    
    # Verify no existing service
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Error "Service still exists after deletion attempt"
        Write-Host "Manual cleanup required. Run: sc delete $ServiceName" -ForegroundColor Cyan
        exit 1
    }
    
    # Create service
    $createResult = & sc.exe create $ServiceName binPath= "`"$exePath`"" start=auto obj= LocalSystem DisplayName= "`"$ServiceDisplayName`"" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create service: $createResult"
        exit 1
    }
    
    # Set service description
    & sc.exe description $ServiceName "$ServiceDescription" | Out-Null
    
    # Configure service recovery
    & sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    
    Write-Success "Windows service created successfully"
}

function Start-ServiceInstallation {
    if ($SkipService) {
        return
    }
    
    Write-Step "Starting service..."
    
    try {
        Start-Service -Name $ServiceName -ErrorAction Stop
        Start-Sleep -Seconds 5
        
        $serviceStatus = Get-Service -Name $ServiceName
        if ($serviceStatus.Status -eq "Running") {
            Write-Success "Service started successfully"
            
            # Test web interface
            Write-Step "Testing web interface..."
            try {
                $response = Invoke-WebRequest -Uri "http://localhost:8089/api/status" -TimeoutSec 10 -UseBasicParsing
                if ($response.StatusCode -eq 200) {
                    Write-Success "Web interface is responding"
                } else {
                    Write-Warning "Web interface returned status: $($response.StatusCode)"
                }
            } catch {
                Write-Warning "Web interface test failed: $($_.Exception.Message)"
            }
        } else {
            Write-Warning "Service status: $($serviceStatus.Status)"
        }
    } catch {
        Write-Warning "Could not start service: $($_.Exception.Message)"
        Write-Host "You can start it manually with: sc start $ServiceName" -ForegroundColor Cyan
    }
}

function New-DesktopShortcut {
    Write-Step "Creating desktop shortcut..."
    
    try {
        $WshShell = New-Object -comObject WScript.Shell
        $Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\PlexPrerollManager.lnk")
        $Shortcut.TargetPath = "http://localhost:8089"
        $Shortcut.Description = "Plex Preroll Manager Web Interface"
        $Shortcut.IconLocation = Join-Path $InstallPath "icon.ico"
        $Shortcut.Save()
        Write-Success "Desktop shortcut created"
    } catch {
        Write-Warning "Could not create desktop shortcut: $($_.Exception.Message)"
    }
}

function New-StartMenuShortcut {
    Write-Step "Creating Start Menu shortcut..."
    
    try {
        $startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
        $shortcutPath = Join-Path $startMenuPath "PlexPrerollManager.lnk"
        
        $WshShell = New-Object -comObject WScript.Shell
        $Shortcut = $WshShell.CreateShortcut($shortcutPath)
        $Shortcut.TargetPath = "http://localhost:8089"
        $Shortcut.Description = "Plex Preroll Manager Web Interface"
        $Shortcut.IconLocation = Join-Path $InstallPath "icon.ico"
        $Shortcut.Save()
        Write-Success "Start Menu shortcut created"
    } catch {
        Write-Warning "Could not create Start Menu shortcut: $($_.Exception.Message)"
    }
}

function Set-FirewallRules {
    Write-Step "Configuring Windows Firewall..."
    
    try {
        # Remove existing rules
        Remove-NetFirewallRule -DisplayName "PlexPrerollManager*" -ErrorAction SilentlyContinue
        
        # Add new rule for HTTP traffic
        New-NetFirewallRule -DisplayName "PlexPrerollManager HTTP" -Direction Inbound -Protocol TCP -LocalPort 8089 -Action Allow -Profile Any | Out-Null
        Write-Success "Firewall rule created for port 8089"
    } catch {
        Write-Warning "Could not configure firewall: $($_.Exception.Message)"
        Write-Host "You may need to manually allow port 8089 in Windows Firewall" -ForegroundColor Cyan
    }
}

function Remove-BuildArtifacts {
    Write-Step "Cleaning up build artifacts..."

    try {
        # Remove build directories
        $buildPaths = @(
            Join-Path $ScriptRoot "bin",
            Join-Path $ScriptRoot "obj",
            Join-Path $ScriptRoot "publish",
            Join-Path $ScriptRoot "publish-test"
        )
        
        foreach ($path in $buildPaths) {
            if (Test-Path $path) {
                Remove-Item -Path $path -Recurse -Force
                Write-Host "  Removed: $path" -ForegroundColor Gray
            }
        }
        
        # Remove temporary files
        $tempFiles = @("*.tmp", "*.log", "*.cache")
        foreach ($pattern in $tempFiles) {
            Get-ChildItem -Path $ScriptRoot -Filter $pattern -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
        }
        
        Write-Success "Build artifacts cleaned up"
    } catch {
        Write-Warning "Could not clean all build artifacts: $($_.Exception.Message)"
    }
}

function Show-InstallationSummary {
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "                    Installation Complete!                    " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host ""

    Write-Host "Installation Details:" -ForegroundColor Cyan
    Write-Host "   Install Path:    $InstallPath" -ForegroundColor White
    Write-Host "   Data Path:       $DataPath" -ForegroundColor White
    Write-Host "   Version:         $Version" -ForegroundColor White
    Write-Host ""

    Write-Host "Access Information:" -ForegroundColor Cyan
    Write-Host "   Web Interface:   http://localhost:8089" -ForegroundColor White
    Write-Host "   Local Network:   http://$(hostname):8089" -ForegroundColor White
    Write-Host ""

    if (-not $SkipService) {
        Write-Host "Service Management:" -ForegroundColor Cyan
        Write-Host "   Start Service:   sc start $ServiceName" -ForegroundColor White
        Write-Host "   Stop Service:    sc stop $ServiceName" -ForegroundColor White
        Write-Host "   Service Status:  Get-Service $ServiceName" -ForegroundColor White
        Write-Host ""
    }

    Write-Host "Important Directories:" -ForegroundColor Cyan
    Write-Host "   Web Files:       $InstallPath\web" -ForegroundColor White
    Write-Host "   Prerolls:        $DataPath\Prerolls" -ForegroundColor White
    Write-Host "   Backups:         $DataPath\Backups" -ForegroundColor White
    Write-Host "   Logs:            $DataPath\Logs" -ForegroundColor White
    Write-Host ""

    # Check if token was preserved
    $configPath = Join-Path $DataPath "appsettings.json"
    $tokenPreserved = $false
    if (Test-Path $configPath) {
        try {
            $config = Get-Content -Path $configPath -Raw | ConvertFrom-Json
            if ($config.Plex.Token -and $config.Plex.Token -ne "") {
                $tokenPreserved = $true
            }
        } catch {
            # Ignore errors
        }
    }

    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Open web interface: http://localhost:8089" -ForegroundColor White
    if ($tokenPreserved) {
        Write-Host "   2. Your Plex token has been preserved from previous installation" -ForegroundColor White
        Write-Host "   3. Upload preroll videos to categories" -ForegroundColor White
        Write-Host "   4. Activate desired preroll category" -ForegroundColor White
    } else {
        Write-Host "   2. Configure Plex server connection" -ForegroundColor White
        Write-Host "   3. Authenticate with Plex.tv (recommended)" -ForegroundColor White
        Write-Host "   4. Upload preroll videos to categories" -ForegroundColor White
        Write-Host "   5. Activate desired preroll category" -ForegroundColor White
    }
    Write-Host ""
}

# Main installation process
try {
    Write-Header
    
    Write-Host "Installation Parameters:" -ForegroundColor Cyan
    Write-Host "  Install Path: $InstallPath" -ForegroundColor White
    Write-Host "  Data Path:    $DataPath" -ForegroundColor White
    Write-Host "  Clean Install: $CleanInstall" -ForegroundColor White
    Write-Host "  Skip Service:  $SkipService" -ForegroundColor White
    Write-Host ""
    
    # Confirm installation
    $confirm = Read-Host "Continue with installation? (Y/N)"
    if ($confirm -notmatch "^[Yy]") {
        Write-Host "Installation cancelled by user" -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    
    # Installation steps
    Test-Prerequisites
    Stop-ExistingProcesses
    Stop-ExistingService
    $configBackup = New-InstallationDirectories
    Publish-Application
    Copy-WebFiles
    New-Configuration -ConfigBackup $configBackup
    
    if (-not $SkipService) {
        Install-WindowsService
        Set-FirewallRules
        Start-ServiceInstallation
    }
    
    New-DesktopShortcut
    New-StartMenuShortcut
    Remove-BuildArtifacts
    
    Show-InstallationSummary
    
} catch {
    Write-Host ""
    Write-Error "Installation failed: $($_.Exception.Message)"
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
}

Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
