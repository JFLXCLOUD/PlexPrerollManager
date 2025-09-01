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


## Installation

### Option 1: Download Release (Recommended)

1. **Download the latest release** from [GitHub Releases](https://github.com/JFLXCLOUD/PlexPrerollManager/releases)
2. **Extract** the ZIP file to a folder on your computer
3. **Run the installer** as Administrator:
   ```batch
   install.bat          # Full installation with service setup
   ```
4. **Open** http://localhost:8089 in your browser

### Option 2: Local Development Setup

If you want to build from source:

1. **Clone the repository**
   ```bash
   git clone https://github.com/JFLXCLOUD/PlexPrerollManager.git
   cd PlexPrerollManager
   ```

2. **Run the installer** as Administrator:
   ```batch
   install.bat          # Full installation with service setup
   install-simple.bat   # Quick setup (no service)
   ```

3. **Open** http://localhost:8089 in your browser

### From Source Code

1. **Clone the repository**
   ```bash
   git clone https://github.com/JFLXCLOUD/PlexPrerollManager.git
   cd PlexPrerollManager
   ```

2. **Run the installer** (Administrator required for service installation):
   ```batch
   install.bat                    # Full service installation
   install-simple.bat            # Quick setup without service
   ```

### Installation Methods

| Method | Admin Required | Service Setup | Best For |
|--------|---------------|---------------|----------|
| **Release ZIP** + `install.bat` | ✅ Yes | ✅ Auto | **Production Use** |
| **Release ZIP** + `install-simple.bat` | ❌ No | ❌ Manual | **Testing** |
| **Source Code** + `install.bat` | ✅ Yes | ✅ Auto | **Development** |
| **Source Code** + `install-simple.bat` | ❌ No | ❌ Manual | **Development** |

### Creating Releases

To create a new release for distribution:

1. **Update version numbers** in project files if needed
2. **Run the release preparation script**:
   ```batch
   prepare-release.ps1
   ```
3. **Test the generated release** locally
4. **Create a GitHub release** with the generated ZIP files from the `release/` folder
5. **Upload the release assets** and publish

The `prepare-release.ps1` script will:
- Build the application for distribution
- Create optimized release packages
- Generate checksums for verification
- Prepare all necessary files for the release

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

### Finding Your Plex Token
To get your Plex token for authentication:

1. **Open Plex Web App**: Go to your Plex server in a web browser
2. **Sign In**: Make sure you're signed in to your Plex account
3. **Get Token**: Visit this URL in your browser:
   ```
   http://localhost:32400/web/index.html#!/account
   ```
   (Replace `localhost:32400` with your Plex server address if different)

4. **Copy Token**: Look for "X-Plex-Token" in the URL or use browser developer tools:
   - Press `F12` to open developer tools
   - Go to the "Network" tab
   - Refresh the page
   - Look for any request and check the "X-Plex-Token" header

5. **Alternative Method**: Use Plex's token generator:
   - Visit: https://plex.tv/pms/resources.xml?includeHttps=1&X-Plex-Token=YOUR_TOKEN
   - Replace `YOUR_TOKEN` with your actual token to verify it works

**Security Note**: Keep your Plex token secure and don't share it publicly.

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


## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is open source. Feel free to use, modify, and distribute.


## Support

For issues, questions, or feature requests:
1. Check the troubleshooting section
2. Review Windows Event Logs
3. Create an issue with detailed information
4. Check the [GitHub Discussions](https://github.com/JFLXCLOUD/PlexPrerollManager/discussions) for community help


---

**Thank you for using PlexPrerollManager! Enjoy managing your Plex prerolls with style!**

**Star us on GitHub** ⭐ and share with fellow Plex enthusiasts!