# install-preroll-manager.ps1 - PowerShell script to install the Plex Preroll Manager service
param(
    [string]$ServiceName = "PlexPrerollManager",
    [string]$DisplayName = "Plex Preroll Manager",
    [string]$Description = "Manages Plex cinema prerolls by season, holiday, and category",
    [string]$BinaryPath = ".\PlexPrerollManager.exe",
    [string]$PrerollsPath = "C:\PlexPrerolls\Videos",
    [string]$PlexToken = ""
)

# Check if running as administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator. Right-click PowerShell and select 'Run as Administrator'"
    exit 1
}

Write-Host "====================================="
Write-Host "   PLEX PREROLL MANAGER INSTALLER   "
Write-Host "====================================="

try {
    # Create prerolls directory
    Write-Host "Creating prerolls directory: $PrerollsPath"
    New-Item -ItemType Directory -Path $PrerollsPath -Force | Out-Null
    
    # Create category subdirectories
    $categories = @("General", "Halloween", "Christmas", "New Year", "Summer", "Winter", "Spring", "Fall", "Horror", "Comedy", "Action", "Classic")
    foreach ($category in $categories) {
        $categoryPath = Join-Path $PrerollsPath $category
        New-Item -ItemType Directory -Path $categoryPath -Force | Out-Null
        Write-Host "  Created category: $category"
    }

    # Stop the service if it's running
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Stopping existing service..."
        Stop-Service -Name $ServiceName -Force
        Write-Host "Removing existing service..."
        sc.exe delete $ServiceName
        Start-Sleep -Seconds 2
    }

    # Update configuration if Plex token provided
    if (-not [string]::IsNullOrEmpty($PlexToken)) {
        Write-Host "Updating Plex token in configuration..."
        $configPath = "appsettings.json"
        if (Test-Path $configPath) {
            $config = Get-Content $configPath -Raw | ConvertFrom-Json
            $config.Plex.Token = $PlexToken
            $config | ConvertTo-Json -Depth 10 | Set-Content $configPath
            Write-Host "Plex token updated successfully"
        }
    }

    # Create the service
    Write-Host "Installing service: $ServiceName"
    $fullPath = Resolve-Path $BinaryPath
    
    New-Service -Name $ServiceName `
                -BinaryPathName $fullPath `
                -DisplayName $DisplayName `
                -Description $Description `
                -StartupType Automatic

    # Configure service to restart on failure
    sc.exe failure $ServiceName reset= 30 actions= restart/5000/restart/5000/restart/5000

    # Create Windows Firewall rule for web interface
    Write-Host "Creating firewall rule for web interface (port 8080)..."
    try {
        New-NetFirewallRule -DisplayName "Plex Preroll Manager" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow | Out-Null
        Write-Host "Firewall rule created successfully"
    } catch {
        Write-Warning "Could not create firewall rule. You may need to manually allow port 8080."
    }

    # Start the service
    Write-Host "Starting service..."
    Start-Service -Name $ServiceName

    Write-Host ""
    Write-Host "🎬 INSTALLATION COMPLETE! 🎬" -ForegroundColor Green
    Write-Host ""
    Write-Host "📁 Prerolls Directory: $PrerollsPath" -ForegroundColor Cyan
    Write-Host "🌐 Web Interface: http://localhost:8080" -ForegroundColor Cyan
    Write-Host "⚙️  Service Name: $ServiceName" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "QUICK START GUIDE:" -ForegroundColor Yellow
    Write-Host "1. Open your web browser to http://localhost:8080"
    Write-Host "2. Upload video files to different categories (Halloween, Christmas, etc.)"
    Write-Host "3. Click 'Activate' on a category to set it as current prerolls"
    Write-Host "4. Your Plex server will automatically use the selected prerolls!"
    Write-Host ""
    Write-Host "PLEX TOKEN SETUP:" -ForegroundColor Yellow
    if ([string]::IsNullOrEmpty($PlexToken)) {
        Write-Host "To get your Plex token:"
        Write-Host "1. Go to https://app.plex.tv/desktop"
        Write-Host "2. Open browser dev tools (F12)"
        Write-Host "3. Look for 'X-Plex-Token' in network requests"
        Write-Host "4. Run: .\install-preroll-manager.ps1 -PlexToken 'YOUR_TOKEN_HERE'"
    } else {
        Write-Host "✅ Plex token configured successfully"
    }
    Write-Host ""
    Write-Host "SERVICE MANAGEMENT COMMANDS:" -ForegroundColor Yellow
    Write-Host "  Start:     Start-Service -Name $ServiceName"
    Write-Host "  Stop:      Stop-Service -Name $ServiceName"
    Write-Host "  Status:    Get-Service -Name $ServiceName"
    Write-Host "  Logs:      Get-EventLog -LogName Application -Source $ServiceName -Newest 50"
    Write-Host "  Uninstall: sc.exe delete $ServiceName"
    Write-Host ""
}
catch {
    Write-Error "Failed to install service: $($_.Exception.Message)"
    exit 1
}

# Helper functions for managing prerolls
function Get-PlexPrerollStatus {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8080/api/status" -Method Get
        Write-Host "Plex Server: " -NoNewline
        if ($response.PlexConnected) {
            Write-Host "✅ Connected ($($response.PlexServerName))" -ForegroundColor Green
        } else {
            Write-Host "❌ Disconnected" -ForegroundColor Red
        }
        Write-Host "Active Category: $($response.ActiveCategory)"
        Write-Host "Total Prerolls: $($response.TotalPrerolls)"
    } catch {
        Write-Host "❌ Could not connect to Preroll Manager web interface" -ForegroundColor Red
    }
}

function Set-PlexPrerollCategory {
    param([string]$Category)
    try {
        Invoke-RestMethod -Uri "http://localhost:8080/api/categories/$Category/activate" -Method Post
        Write-Host "✅ Activated $Category category" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to activate $Category category" -ForegroundColor Red
    }
}

Write-Host "Additional PowerShell functions available:"
Write-Host "  Get-PlexPrerollStatus    - Check current status"
Write-Host "  Set-PlexPrerollCategory  - Activate a category"
Write-Host ""
Write-Host "Example: Set-PlexPrerollCategory -Category 'Halloween'"