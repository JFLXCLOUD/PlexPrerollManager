# PlexPrerollManager

A comprehensive web-based management system for Plex cinema prerolls with advanced features like scheduling, video thumbnails, and metadata extraction.

## Features

### 🎬 Video Management
- **Video Upload & Organization**: Upload videos and organize them into categories
- **Bulk Upload**: Upload multiple videos at once with progress tracking
- **Upload Progress Bars**: Real-time progress indicators for individual and bulk uploads
- **Video Thumbnails**: Automatic thumbnail generation for visual preview
- **Video Metadata**: Extract duration, resolution, codecs, and other metadata
- **Video Preview**: Click thumbnails to preview videos in a modal player
- **Bulk Operations**: Select and manage multiple videos at once

### ⏰ Scheduling System
- **Automated Preroll Switching**: Schedule category activations
- **Flexible Scheduling**: One-time, daily, weekly, monthly, or yearly schedules
- **Advanced Scheduling Page**: Dedicated interface for complex schedule management
- **Schedule Templates**: Quick setup for common scenarios

### 🎨 User Interface
- **Modern Dark Theme**: Beautiful, responsive design
- **Category Creation**: Type new category names when uploading
- **Visual Feedback**: Status indicators and progress updates
- **Mobile Responsive**: Works great on tablets and phones

### 🔧 Advanced Features
- **Windows Service**: Runs as a background service
- **Large File Support**: Handles videos up to any size with progress tracking
- **Error Handling**: Comprehensive error handling and user feedback
- **Real-time Updates**: Live status updates and data refresh

## Prerequisites

### Required Software
- **.NET 9.0** or later
- **FFmpeg** (for video processing and thumbnails)

### Installing FFmpeg

#### Windows (Recommended)
1. Download FFmpeg from: https://ffmpeg.org/download.html
2. Choose the Windows build (static version recommended)
3. Extract the ZIP file to a folder (e.g., `C:\ffmpeg`)
4. Add the `bin` folder to your system PATH:
   - Right-click "This PC" → Properties → Advanced system settings
   - Click "Environment Variables"
   - Under "System variables", find "Path" and click "Edit"
   - Add `C:\ffmpeg\bin` to the list
5. Restart your command prompt/terminal
6. Verify installation: `ffmpeg -version`

#### Alternative: Chocolatey (Windows)
```bash
choco install ffmpeg
```

#### Linux
```bash
# Ubuntu/Debian
sudo apt update && sudo apt install ffmpeg

# CentOS/RHEL
sudo yum install ffmpeg

# Arch Linux
sudo pacman -S ffmpeg
```

#### macOS
```bash
# Using Homebrew
brew install ffmpeg

# Or using MacPorts
sudo port install ffmpeg
```

## Quick Installation (One-Liner)

### Windows PowerShell (Recommended)
Run this one-liner in an elevated PowerShell terminal:
```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/JFLXCLOUD/PlexPrerollManager/main/install.ps1'))
```

This will:
- ✅ Download and install PlexPrerollManager
- ✅ Install FFmpeg automatically
- ✅ Set up Windows service
- ✅ Configure everything automatically

### Manual Installation

1. **Clone or download** the project files
2. **Navigate** to the project directory
3. **Run the installation script**:
    ```bash
    install.bat                    # Full service installation (Administrator)
    install-simple.bat            # Quick setup (no admin required)
    ./install-preroll-manager.ps1  # PowerShell service installer
    ./install.ps1                  # One-click PowerShell installer
    ```

### Installation Options

| Method | Admin Required | Service Setup | FFmpeg Install | Best For |
|--------|---------------|---------------|----------------|----------|
| `install.ps1` | ✅ Yes | ✅ Auto | ✅ Auto | **New Users** |
| `install.bat` | ✅ Yes | ✅ Auto | ❌ Manual | **Production** |
| `install-simple.bat` | ❌ No | ❌ Manual | ❌ Manual | **Testing** |
| Manual | ❌ No | ❌ Manual | ❌ Manual | **Development** |

## Usage

### Starting the Application
- **As a Service**: The installer sets up a Windows service that starts automatically
- **Manual Start**: Run `PlexPrerollManager.exe` from the installation directory

### Web Interface
Access the web interface at: `http://localhost:8089`

### Basic Workflow
1. **Choose Upload Mode**: Select Single Upload or Bulk Upload
2. **Upload Videos**:
   - **Single Upload**: Upload one video at a time
   - **Bulk Upload**: Select multiple videos and upload them all at once
3. **Create Categories**: Type new category names or select existing ones
4. **Monitor Progress**: Watch real-time progress bars during uploads
5. **View Thumbnails**: Click on category cards to see video thumbnails
6. **Preview Videos**: Click thumbnails to preview videos
7. **Schedule Categories**: Set up automated category switching
8. **Monitor Status**: Check Plex connection and system status

### Upload Features
- **Single Upload**: Traditional single-file upload with progress tracking
- **Bulk Upload**: Select multiple files and upload them to the same category
- **Progress Tracking**: Real-time progress bars for individual files and overall progress
- **Error Handling**: Individual file failures don't stop the entire upload process
- **File Preview**: See file names and sizes before uploading

## Configuration

### App Settings
Edit `appsettings.json` to configure:
- Plex server URL and token
- Preroll storage paths
- Service settings

### Video Storage
Videos are stored in: `%ProgramData%\PlexPrerollManager\Prerolls\`
Thumbnails are stored in: `%ProgramData%\PlexPrerollManager\Prerolls\.thumbnails\`

## API Endpoints

### Video Management
- `GET /api/status` - System status and statistics
- `GET /api/categories` - List all categories
- `GET /api/prerolls/{category}` - Videos in a category
- `POST /api/upload` - Upload a new video
- `POST /api/categories/{category}/activate` - Activate a category
- `DELETE /api/prerolls/{id}` - Delete a video

### Scheduling
- `GET /api/schedules` - List all schedules
- `POST /api/schedules` - Create a new schedule
- `PUT /api/schedules/{id}` - Update a schedule
- `DELETE /api/schedules/{id}` - Delete a schedule

### Thumbnails
- `GET /api/thumbnails/{filename}` - Serve thumbnail images

## Troubleshooting

### FFmpeg Issues
- **"ffmpeg not found"**: Ensure FFmpeg is installed and in PATH
- **Thumbnail generation fails**: Check FFmpeg permissions and video file access
- **Metadata extraction fails**: Verify video files are not corrupted

### Common Issues
- **Service won't start**: Check Windows Event Viewer for error details
- **Videos not appearing**: Verify file permissions and storage paths
- **Thumbnails not loading**: Check thumbnail directory permissions

### Logs
- Application logs are written to Windows Event Log
- Additional logging available in the console when running manually

## Development

### Building from Source
```bash
# Clone the repository
git clone https://github.com/JFLXCLOUD/PlexPrerollManager.git
cd PlexPrerollManager

# Build the application
dotnet build

# Publish for production
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Preparing a Release
Use the included release preparation script:
```bash
# Prepare a new release
.\prepare-release.ps1 -Version "1.2.0" -ReleaseNotes "Add bulk upload feature"

# This will:
# - Update version numbers
# - Build the application
# - Create release archive
# - Generate checksums
# - Create release notes
```

### Project Structure
```
PlexPrerollManager/
├── Program.cs                    # Main application and API endpoints
├── PlexPrerollManager.csproj     # Project configuration
├── dashboard.html               # Main web interface
├── corrected_dashboard.html     # Enhanced web interface
├── scheduling-dashboard.html    # Advanced scheduling interface
├── appsettings.json             # Configuration settings
├── install.ps1                  # One-click PowerShell installer
├── install.bat                  # Full Windows service installer
├── install-simple.bat           # Quick setup installer
├── prepare-release.ps1          # Release preparation script
├── .gitignore                   # Git ignore rules
└── README.md                    # This documentation
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is open source. Feel free to use, modify, and distribute.

## GitHub Repository Setup

### Initial Setup
1. **Create a new GitHub repository** named `PlexPrerollManager`
2. **Push your code**:
  ```bash
  git init
  git add .
  git commit -m "Initial commit: PlexPrerollManager with bulk upload"
  git branch -M main
  git remote add origin https://github.com/JFLXCLOUD/PlexPrerollManager.git
  git push -u origin main
  ```

3. **Update the installation scripts**:
   - The scripts have been updated with your GitHub username (JFLXCLOUD)
   - Repository URLs are already configured correctly

### Creating Releases
1. **Prepare a release**:
   ```bash
   .\prepare-release.ps1 -Version "1.0.0"
   ```

2. **Create GitHub release**:
   - Go to your repository on GitHub
   - Click "Releases" → "Create a new release"
   - Upload the generated ZIP file
   - Copy release notes from `RELEASE_NOTES.md`

3. **Test the installation** on a clean Windows system

## Support

For issues, questions, or feature requests:
1. Check the troubleshooting section
2. Review Windows Event Logs
3. Create an issue with detailed information
4. Check the [GitHub Discussions](https://github.com/JFLXCLOUD/PlexPrerollManager/discussions) for community help

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Quick Contribution Guide
1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Make your changes and test thoroughly
4. Commit your changes: `git commit -m 'Add amazing feature'`
5. Push to the branch: `git push origin feature/amazing-feature`
6. Open a Pull Request

---

**🎉 Thank you for using PlexPrerollManager! Enjoy managing your Plex prerolls with style! 🎬✨**

**Star us on GitHub** ⭐ and share with fellow Plex enthusiasts!