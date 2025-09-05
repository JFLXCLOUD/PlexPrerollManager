# PlexPrerollManager

[![Version](https://img.shields.io/badge/version-2.2.0-blue.svg)](https://github.com/JFLXCLOUD/PlexPrerollManager/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)

A professional Windows application for managing Plex Media Server preroll videos with automated scheduling, category management, and usage statistics.

## ✨ Features

- 🎬 **Preroll Management**: Upload and organize preroll videos by category
- 🔐 **Secure Authentication**: Multiple authentication methods (Plex.tv OAuth, Direct Token)
- ⏰ **Automated Scheduling**: Set up recurring schedules for category switching
- 📊 **Usage Statistics**: Track play counts, watch time, and performance metrics
- 💾 **Backup & Restore**: Full backup system with metadata preservation
- 🖥️ **Web Interface**: Clean, modern dashboard for easy management
- 🔧 **Windows Service**: Runs as a background service with automatic startup

## 🚀 Quick Start

### Installation (2 minutes)

1. Download the latest release from [GitHub Releases](https://github.com/JFLXCLOUD/PlexPrerollManager/releases)
2. Extract the ZIP file to any folder
3. Right-click `INSTALL.bat` and select "Run as administrator"
4. Follow the installation prompts
5. Open http://localhost:8089 in your browser

### First Setup (5 minutes)

1. **Authenticate with Plex.tv**: Click "Connect to Plex" and authorize the application
2. **Upload Prerolls**: Add video files and organize them into categories
3. **Activate Category**: Choose which category should play before movies
4. **Create Schedules** (optional): Set up automated category switching

## 📋 Requirements

- Windows 10/11 (64-bit)
- [.NET 9.0 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- Plex Media Server (local network access)
- Administrator privileges for installation

## 🏗️ Architecture

```
PlexPrerollManager/
├── Controllers/          # Web API endpoints
├── Models/              # Data models and DTOs
├── Services/            # Business logic and integrations
├── web/                 # Web interface files
├── install.ps1          # Professional installer
└── PlexPrerollManager.csproj
```

## 📖 Documentation

- [Installation Guide](INSTALLATION.md)
- [Quick Start Guide](QUICK_START.md)
- [API Documentation](Controllers/)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Plex Media Server](https://www.plex.tv/) for their excellent media platform
- [.NET Community](https://dotnet.microsoft.com/) for the framework
- All contributors and users

## 📞 Support

- 🐛 [GitHub Issues](https://github.com/JFLXCLOUD/PlexPrerollManager/issues)
- 📖 [Documentation](INSTALLATION.md)
- 🐛 [Bug Reports](https://github.com/JFLXCLOUD/PlexPrerollManager/issues/new?template=bug_report.md)

---

**Made with ❤️ for the Plex community**