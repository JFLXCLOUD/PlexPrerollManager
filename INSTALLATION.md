# PlexPrerollManager Installation Guide

## 🚀 Quick Installation

### Prerequisites
- **Windows 10/11** (Administrator privileges required)
- **.NET 9.0 Runtime** ([Download here](https://dotnet.microsoft.com/download/dotnet/9.0))
- **PowerShell 5.1+** (included with Windows)

### One-Click Installation
1. **Download** the latest release
2. **Extract** to any folder
3. **Right-click** on `install.ps1` → "Run with PowerShell"
4. **Follow** the installation prompts
5. **Access** the web interface at http://localhost:8089

## 📋 Installation Options

### Standard Installation
```powershell
# Run as Administrator
.\install.ps1
```

### Custom Installation Paths
```powershell
# Custom install and data directories
.\install.ps1 -InstallPath "C:\MyApps\PlexPrerollManager" -DataPath "D:\PlexData"
```

### Clean Installation
```powershell
# Remove all existing data and start fresh
.\install.ps1 -CleanInstall
```

### Files-Only Installation
```powershell
# Install files without creating Windows service
.\install.ps1 -SkipService
```

## 🗂️ Directory Structure

After installation, the following directories are created:

```
📁 C:\Program Files\PlexPrerollManager\     (Installation)
├── PlexPrerollManager.exe                  (Main executable)
├── appsettings.json                        (Configuration)
├── dashboard.html                          (Web interface)
├── oauth.html                             (Authentication callback)
└── [Runtime files...]

📁 C:\ProgramData\PlexPrerollManager\       (Data)
├── 📁 Prerolls\                           (Video categories)
│   ├── 📁 General\
│   ├── 📁 Halloween\
│   ├── 📁 Christmas\
│   └── 📁 [Other categories...]
├── 📁 Backups\                            (System backups)
├── 📁 Logs\                               (Application logs)
├── appsettings.json                       (Main configuration)
├── schedules.json                         (Scheduling data)
└── plexprerollmanager.db                  (Usage statistics)
```

## 🔧 Service Management

### Windows Service Commands
```powershell
# Start service
sc start PlexPrerollManager

# Stop service
sc stop PlexPrerollManager

# Check service status
Get-Service PlexPrerollManager

# View service configuration
sc query PlexPrerollManager
```

### Manual Service Installation
```powershell
# Create service manually
sc create PlexPrerollManager binPath="C:\Program Files\PlexPrerollManager\PlexPrerollManager.exe" start=auto obj=LocalSystem DisplayName="Plex Preroll Manager"

# Set service description
sc description PlexPrerollManager "Manages Plex cinema prerolls with automated scheduling"

# Configure service recovery
sc failure PlexPrerollManager reset=86400 actions=restart/5000/restart/10000/restart/30000
```

## 🌐 Network Access

### Local Access
- **Web Interface**: http://localhost:8089
- **API Endpoints**: http://localhost:8089/api/*

### Network Access
- **LAN Access**: http://[COMPUTER-NAME]:8089
- **Firewall**: Port 8089 is automatically configured

### Firewall Configuration
```powershell
# Manual firewall rule (if needed)
New-NetFirewallRule -DisplayName "PlexPrerollManager HTTP" -Direction Inbound -Protocol TCP -LocalPort 8089 -Action Allow
```

## 🔐 Security Considerations

### Authentication
- **Plex.tv OAuth**: Recommended for secure authentication
- **Direct Token**: Alternative for advanced users
- **Local Access**: Web interface accessible on local network

### Data Protection
- **Configuration**: Stored in protected ProgramData directory
- **Tokens**: Encrypted using Windows DPAPI
- **File Uploads**: Validated for type and size
- **Path Security**: Protected against directory traversal

## 🛠️ Troubleshooting

### Common Issues

#### Service Won't Start
```powershell
# Check service status
Get-Service PlexPrerollManager

# Check Windows Event Log
Get-EventLog -LogName Application -Source PlexPrerollManager -Newest 10

# Manual start for debugging
cd "C:\Program Files\PlexPrerollManager"
.\PlexPrerollManager.exe
```

#### Web Interface Not Accessible
1. **Check service status**: `Get-Service PlexPrerollManager`
2. **Check firewall**: Ensure port 8089 is allowed
3. **Check logs**: Review application logs in `C:\ProgramData\PlexPrerollManager\Logs`
4. **Test locally**: Try http://localhost:8089

#### .NET Runtime Issues
```powershell
# Check .NET version
dotnet --version

# Should show 9.0.x or higher
# If not, download from: https://dotnet.microsoft.com/download/dotnet/9.0
```

#### Permission Issues
- **Run as Administrator**: Required for service installation
- **Data Directory**: Ensure LocalSystem has access to ProgramData
- **Firewall**: May require manual configuration in some environments

### Log Files
- **Application Logs**: `C:\ProgramData\PlexPrerollManager\Logs\plexprerollmanager.log`
- **Windows Event Log**: Application → PlexPrerollManager
- **Service Logs**: Use `Get-EventLog -LogName System -Source "Service Control Manager"`

## 🗑️ Uninstallation

### Complete Removal
```powershell
# Remove everything
.\uninstall.ps1
```

### Keep Data
```powershell
# Remove application but keep prerolls and configuration
.\uninstall.ps1 -KeepData
```

### Force Removal
```powershell
# Remove without confirmation
.\uninstall.ps1 -Force
```

## 📞 Support

### Getting Help
- **GitHub Issues**: [Report bugs and request features](https://github.com/JFLXCLOUD/PlexPrerollManager/issues)
- **Documentation**: Check the README.md for usage instructions
- **Logs**: Always include log files when reporting issues

### Manual Cleanup (if needed)
```powershell
# Stop and remove service
sc stop PlexPrerollManager
sc delete PlexPrerollManager

# Remove directories
Remove-Item "C:\Program Files\PlexPrerollManager" -Recurse -Force
Remove-Item "C:\ProgramData\PlexPrerollManager" -Recurse -Force

# Remove firewall rules
Remove-NetFirewallRule -DisplayName "PlexPrerollManager*"

# Remove shortcuts
Remove-Item "$env:USERPROFILE\Desktop\PlexPrerollManager.lnk" -Force
Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\PlexPrerollManager.lnk" -Force
```

## 🔄 Upgrade Process

### Automatic Upgrade
1. **Download** new version
2. **Run** `install.ps1` (will upgrade existing installation)
3. **Service** automatically restarted with new version

### Manual Upgrade
1. **Stop** service: `sc stop PlexPrerollManager`
2. **Backup** data: Copy `C:\ProgramData\PlexPrerollManager`
3. **Replace** files in `C:\Program Files\PlexPrerollManager`
4. **Start** service: `sc start PlexPrerollManager`

---

**Note**: This installer creates a production-ready installation with proper Windows service integration, security, and management capabilities.