using System.Net.Http;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;

namespace PlexPrerollManager.Services
{
    public class PlexApiService
    {
        private readonly IConfiguration _configuration;
        private readonly ConfigurationService _configService;

        public PlexApiService(IConfiguration configuration, ConfigurationService configService)
        {
            _configuration = configuration;
            _configService = configService;
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                var plexUrl = _configService.GetPlexServerUrl();
                var token = _configService.GetPlexToken();


                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Plex token not configured");
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // Set headers similar to Tautulli
                client.DefaultRequestHeaders.Add("User-Agent", "PlexPrerollManager/2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Product", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Version", "2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Platform", "Windows");
                client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", "11");

                // Use token for authentication
                client.DefaultRequestHeaders.Add("X-Plex-Token", token);

                var response = await client.GetAsync($"{plexUrl.TrimEnd('/')}/identity");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var serverName = ExtractServerName(content);
                    return (true, serverName);
                }
                else
                {
                    return (false, $"Connection failed: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SetPrerollAsync(string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Setting Plex preroll for category '{categoryName}' with {videoFiles.Count} videos");

                var plexUrl = _configService.GetPlexServerUrl();
                var token = _configService.GetPlexToken();

                if (string.IsNullOrEmpty(plexUrl))
                {
                    return (false, "Plex server URL not configured");
                }

                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Plex token not configured");
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // Set headers similar to Tautulli
                client.DefaultRequestHeaders.Add("User-Agent", "PlexPrerollManager/2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Product", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Version", "2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Platform", "Windows");
                client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", "11");

                // Use token for authentication
                client.DefaultRequestHeaders.Add("X-Plex-Token", token);
                client.DefaultRequestHeaders.Add("Accept", "application/xml");

                // Concatenate all video files with semicolons for random selection
                var prerollId = string.Join(";", videoFiles);
                Console.WriteLine($"[DEBUG] Using concatenated preroll IDs: {prerollId}");

                // Use the first video file for validation and logging
                var firstVideo = videoFiles.FirstOrDefault();
                if (string.IsNullOrEmpty(firstVideo))
                {
                    return (false, "No video files found in category");
                }

                var fileName = Path.GetFileName(firstVideo);
                Console.WriteLine($"[DEBUG] First preroll file: {fileName}");
                Console.WriteLine($"[DEBUG] Total files in category: {videoFiles.Count}");

                // Check if first file exists and get file info
                if (System.IO.File.Exists(firstVideo))
                {
                    var fileInfo = new FileInfo(firstVideo);
                    Console.WriteLine($"[DEBUG] First file size: {fileInfo.Length} bytes");
                    Console.WriteLine($"[DEBUG] First file extension: {fileInfo.Extension}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] First preroll file does not exist: {firstVideo}");
                    return (false, $"Preroll file does not exist: {fileName}");
                }

                // Try multiple methods to set the preroll
                // First try with file path, then with URL if that fails
                var methods = new[]
                {
                    () => TrySetCinemaTrailersPrerollAsync(client, plexUrl, prerollId),
                    () => TrySetPrerollPreferenceAsync(client, plexUrl, prerollId),
                    () => TrySetLibrarySectionPrerollAsync(client, plexUrl, prerollId),
                    () => TrySetGlobalPrerollAsync(client, plexUrl, prerollId),
                    () => TrySetPrerollWithUrlAsync(client, plexUrl, firstVideo),
                    () => TryUploadAndSetPrerollAsync(client, plexUrl, firstVideo)
                };

                foreach (var method in methods)
                {
                    var result = await method();
                    if (result.Success)
                    {
                        Console.WriteLine($"[DEBUG] Successfully set preroll using method: {result.Message}");
                        return (true, $"Successfully set preroll for category '{categoryName}': {result.Message}");
                    }
                    Console.WriteLine($"[DEBUG] Method failed: {result.Message}");
                }

                return (false, "All preroll setting methods failed. Manual configuration may be required.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error setting Plex preroll: {ex.Message}");
                return (false, $"Failed to set preroll: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> TrySetCinemaTrailersPrerollAsync(HttpClient client, string plexUrl, string prerollId)
        {
            try
            {
                Console.WriteLine("[DEBUG] Trying CinemaTrailersPrerollID method");

                var url = $"{plexUrl.TrimEnd('/')}/:/prefs?CinemaTrailersPrerollID={Uri.EscapeDataString(prerollId)}";
                var response = await client.PutAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "CinemaTrailersPrerollID preference");
                }

                var content = await response.Content.ReadAsStringAsync();
                return (false, $"CinemaTrailersPrerollID failed: HTTP {(int)response.StatusCode} - {content}");
            }
            catch (Exception ex)
            {
                return (false, $"CinemaTrailersPrerollID exception: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> TrySetPrerollPreferenceAsync(HttpClient client, string plexUrl, string prerollId)
        {
            try
            {
                Console.WriteLine("[DEBUG] Trying PrerollID preference method");

                var url = $"{plexUrl.TrimEnd('/')}/:/prefs?PrerollID={Uri.EscapeDataString(prerollId)}";
                var response = await client.PutAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "PrerollID preference");
                }

                var content = await response.Content.ReadAsStringAsync();
                return (false, $"PrerollID failed: HTTP {(int)response.StatusCode} - {content}");
            }
            catch (Exception ex)
            {
                return (false, $"PrerollID exception: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> TrySetLibrarySectionPrerollAsync(HttpClient client, string plexUrl, string prerollId)
        {
            try
            {
                Console.WriteLine("[DEBUG] Trying library section preroll method");

                // Get library sections
                var sectionsUrl = $"{plexUrl.TrimEnd('/')}/library/sections";
                var sectionsResponse = await client.GetAsync(sectionsUrl);

                if (!sectionsResponse.IsSuccessStatusCode)
                {
                    return (false, "Failed to get library sections");
                }

                var sectionsContent = await sectionsResponse.Content.ReadAsStringAsync();
                
                // Find movie sections (type="movie")
                var doc = new XmlDocument();
                doc.LoadXml(sectionsContent);
                
                var movieSections = doc.SelectNodes("//Directory[@type='movie']");
                if (movieSections == null || movieSections.Count == 0)
                {
                    return (false, "No movie library sections found");
                }

                // Try to set preroll on each movie section
                foreach (XmlNode section in movieSections)
                {
                    var sectionKey = section.Attributes?["key"]?.Value;
                    if (string.IsNullOrEmpty(sectionKey)) continue;

                    var sectionUrl = $"{plexUrl.TrimEnd('/')}/library/sections/{sectionKey}/prefs?prerollID={Uri.EscapeDataString(prerollId)}";
                    var response = await client.PutAsync(sectionUrl, null);

                    if (response.IsSuccessStatusCode)
                    {
                        var sectionTitle = section.Attributes?["title"]?.Value ?? $"Section {sectionKey}";
                        return (true, $"Library section '{sectionTitle}' preroll");
                    }
                }

                return (false, "Failed to set preroll on any movie library section");
            }
            catch (Exception ex)
            {
                return (false, $"Library section exception: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> TrySetGlobalPrerollAsync(HttpClient client, string plexUrl, string prerollId)
        {
            try
            {
                Console.WriteLine("[DEBUG] Trying global preroll setting method");

                // Try different global preroll preference keys
                var preferenceKeys = new[]
                {
                    "preroll",
                    "Preroll",
                    "trailerID",
                    "TrailerID",
                    "cinemaTrailersPrerollID"
                };

                foreach (var key in preferenceKeys)
                {
                    var url = $"{plexUrl.TrimEnd('/')}/:/prefs?{key}={Uri.EscapeDataString(prerollId)}";
                    var response = await client.PutAsync(url, null);

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, $"Global preference '{key}'");
                    }
                }

                return (false, "All global preference methods failed");
            }
            catch (Exception ex)
            {
                return (false, $"Global preroll exception: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> TrySetPrerollWithUrlAsync(HttpClient client, string plexUrl, string videoFilePath)
        {
            try
            {
                Console.WriteLine("[DEBUG] Trying to set preroll using file URL");

                // Handle multiple files separated by semicolons
                var filePaths = videoFilePath.Split(';');
                var fileUrls = new List<string>();

                foreach (var filePath in filePaths)
                {
                    if (!string.IsNullOrEmpty(filePath.Trim()))
                    {
                        // Convert local file path to HTTP URL that Plex can access
                        var fileName = Path.GetFileName(filePath.Trim());
                        var categoryName = Path.GetFileName(Path.GetDirectoryName(filePath.Trim()));

                        // Create a URL that Plex can access (assuming files are served from /api/files/)
                        var fileUrl = $"http://localhost:8090/api/files/{categoryName}/{fileName}";
                        fileUrls.Add(fileUrl);
                    }
                }

                // Concatenate URLs with semicolons for Plex random selection
                var concatenatedUrls = string.Join(";", fileUrls);
                Console.WriteLine($"[DEBUG] Trying with concatenated file URLs: {concatenatedUrls}");

                // Try different preference keys with the URLs
                var preferenceKeys = new[]
                {
                    "CinemaTrailersPrerollID",
                    "cinemaTrailersPrerollID",
                    "PrerollID",
                    "prerollID",
                    "preroll",
                    "Preroll"
                };

                foreach (var key in preferenceKeys)
                {
                    var url = $"{plexUrl.TrimEnd('/')}/:/prefs?{key}={Uri.EscapeDataString(concatenatedUrls)}";
                    Console.WriteLine($"[DEBUG] Trying URL method with key '{key}': {url}");

                    var response = await client.PutAsync(url, null);
                    var content = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"[DEBUG] URL method response: {(int)response.StatusCode} {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, $"URL-based preroll with '{key}' ({fileUrls.Count} files)");
                    }
                    else if (!string.IsNullOrEmpty(content))
                    {
                        Console.WriteLine($"[DEBUG] Response content: {content.Substring(0, Math.Min(200, content.Length))}");
                    }
                }

                return (false, "URL-based preroll methods failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] URL preroll exception: {ex.Message}");
                return (false, $"URL preroll exception: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> TryUploadAndSetPrerollAsync(HttpClient client, string plexUrl, string videoFilePath)
        {
            try
            {
                Console.WriteLine("[DEBUG] Trying upload and set preroll method");

                if (!System.IO.File.Exists(videoFilePath))
                {
                    return (false, "Video file does not exist for upload");
                }

                // Try to upload the file to Plex's library first
                var fileName = Path.GetFileName(videoFilePath);
                Console.WriteLine($"[DEBUG] Attempting to upload file: {fileName}");

                using var fileStream = System.IO.File.OpenRead(videoFilePath);
                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(fileStream), "file", fileName);

                // Try different upload endpoints
                var uploadEndpoints = new[]
                {
                    $"{plexUrl.TrimEnd('/')}/library/upload",
                    $"{plexUrl.TrimEnd('/')}/library/upload?sectionId=prerolls",
                    $"{plexUrl.TrimEnd('/')}/library/upload?type=18" // Type 18 is trailers
                };

                foreach (var uploadUrl in uploadEndpoints)
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Trying upload to: {uploadUrl}");
                        var uploadResponse = await client.PostAsync(uploadUrl, content);

                        Console.WriteLine($"[DEBUG] Upload response: {(int)uploadResponse.StatusCode} {uploadResponse.StatusCode}");

                        if (uploadResponse.IsSuccessStatusCode)
                        {
                            var uploadContent = await uploadResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"[DEBUG] Upload successful, response: {uploadContent.Substring(0, Math.Min(200, uploadContent.Length))}");

                            // Try to extract the uploaded item's rating key
                            var ratingKey = ExtractRatingKeyFromUploadResponse(uploadContent);
                            if (!string.IsNullOrEmpty(ratingKey))
                            {
                                Console.WriteLine($"[DEBUG] Extracted rating key: {ratingKey}");

                                // Now try to set this as the preroll
                                var setResult = await TrySetRatingKeyPrerollAsync(client, plexUrl, ratingKey);
                                if (setResult.Success)
                                {
                                    return (true, $"Uploaded and set preroll with rating key {ratingKey}");
                                }
                            }
                            else
                            {
                                // If we can't extract rating key, the upload might have worked anyway
                                // Try some common preroll preference keys
                                var fileUrl = $"{plexUrl.TrimEnd('/')}/library/files/{fileName}";
                                var urlResult = await TrySetCinemaTrailersPrerollAsync(client, plexUrl, fileUrl);
                                if (urlResult.Success)
                                {
                                    return (true, "Uploaded file and set as preroll");
                                }
                            }
                        }
                        else
                        {
                            var errorContent = await uploadResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"[DEBUG] Upload failed: {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                        }
                    }
                    catch (Exception uploadEx)
                    {
                        Console.WriteLine($"[DEBUG] Upload exception: {uploadEx.Message}");
                    }
                }

                return (false, "Upload and set preroll failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Upload preroll exception: {ex.Message}");
                return (false, $"Upload preroll exception: {ex.Message}");
            }
        }

        private string ExtractRatingKeyFromUploadResponse(string responseContent)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(responseContent);

                // Look for ratingKey in the response
                var videoNode = doc.SelectSingleNode("//Video");
                if (videoNode?.Attributes?["ratingKey"]?.Value is string ratingKey)
                {
                    return ratingKey;
                }

                // Try other patterns
                var ratingKeyMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"ratingKey=""([^""]+)""");
                if (ratingKeyMatch.Success)
                {
                    return ratingKeyMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to extract rating key: {ex.Message}");
            }

            return null;
        }

        private async Task<(bool Success, string Message)> TrySetRatingKeyPrerollAsync(HttpClient client, string plexUrl, string ratingKey)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Trying to set preroll with rating key: {ratingKey}");

                // Try different preference keys with the rating key
                var preferenceKeys = new[]
                {
                    "CinemaTrailersPrerollID",
                    "cinemaTrailersPrerollID",
                    "PrerollID",
                    "prerollID"
                };

                foreach (var key in preferenceKeys)
                {
                    var url = $"{plexUrl.TrimEnd('/')}/:/prefs?{key}={Uri.EscapeDataString(ratingKey)}";
                    Console.WriteLine($"[DEBUG] Trying rating key method with key '{key}': {url}");

                    var response = await client.PutAsync(url, null);

                    if (response.IsSuccessStatusCode)
                    {
                        return (true, $"Rating key preroll with '{key}'");
                    }
                }

                return (false, "Rating key preroll methods failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Rating key preroll exception: {ex.Message}");
                return (false, $"Rating key preroll exception: {ex.Message}");
            }
        }

        private string ExtractServerName(string xmlContent)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Extracting server name from XML response (first 500 chars): {xmlContent.Substring(0, Math.Min(500, xmlContent.Length))}");

                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                // Try to extract friendly name from MediaContainer
                var mediaContainer = doc.SelectSingleNode("//MediaContainer");
                if (mediaContainer?.Attributes?["friendlyName"]?.Value is string friendlyName && !string.IsNullOrEmpty(friendlyName))
                {
                    Console.WriteLine($"[DEBUG] Found friendlyName in MediaContainer: '{friendlyName}'");
                    return friendlyName;
                }

                // Try to extract name from MediaContainer
                if (mediaContainer?.Attributes?["name"]?.Value is string name && !string.IsNullOrEmpty(name))
                {
                    Console.WriteLine($"[DEBUG] Found name in MediaContainer: '{name}'");
                    return name;
                }

                // Try to extract title from MediaContainer
                if (mediaContainer?.Attributes?["title"]?.Value is string title && !string.IsNullOrEmpty(title))
                {
                    Console.WriteLine($"[DEBUG] Found title in MediaContainer: '{title}'");
                    return title;
                }

                // Try to find Server node
                var serverNode = doc.SelectSingleNode("//Server");
                if (serverNode?.Attributes?["friendlyName"]?.Value is string serverFriendlyName && !string.IsNullOrEmpty(serverFriendlyName))
                {
                    Console.WriteLine($"[DEBUG] Found friendlyName in Server node: '{serverFriendlyName}'");
                    return serverFriendlyName;
                }

                if (serverNode?.Attributes?["name"]?.Value is string serverName && !string.IsNullOrEmpty(serverName))
                {
                    Console.WriteLine($"[DEBUG] Found name in Server node: '{serverName}'");
                    return serverName;
                }

                // Try to find Device node
                var deviceNode = doc.SelectSingleNode("//Device");
                if (deviceNode?.Attributes?["friendlyName"]?.Value is string deviceFriendlyName && !string.IsNullOrEmpty(deviceFriendlyName))
                {
                    Console.WriteLine($"[DEBUG] Found friendlyName in Device node: '{deviceFriendlyName}'");
                    return deviceFriendlyName;
                }

                if (deviceNode?.Attributes?["name"]?.Value is string deviceName && !string.IsNullOrEmpty(deviceName))
                {
                    Console.WriteLine($"[DEBUG] Found name in Device node: '{deviceName}'");
                    return deviceName;
                }

                // Try regex patterns for various XML structures
                var patterns = new[]
                {
                    @"friendlyName=""([^""]+)""",
                    @"name=""([^""]+)""",
                    @"title=""([^""]+)""",
                    @"<Server[^>]*friendlyName=""([^""]+)""",
                    @"<Device[^>]*friendlyName=""([^""]+)""",
                    @"<MediaContainer[^>]*friendlyName=""([^""]+)"""
                };

                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(xmlContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var extractedName = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(extractedName) &&
                            !extractedName.ToLower().Contains("plex media server") &&
                            extractedName.Length > 2 &&
                            extractedName.Length < 100)
                        {
                            Console.WriteLine($"[DEBUG] Found server name using regex pattern '{pattern}': '{extractedName}'");
                            return extractedName;
                        }
                    }
                }

                // Fallback to machine identifier
                if (mediaContainer?.Attributes?["machineIdentifier"]?.Value is string machineId && !string.IsNullOrEmpty(machineId))
                {
                    var fallbackName = $"Plex Server ({machineId.Substring(0, Math.Min(8, machineId.Length))}...)";
                    Console.WriteLine($"[DEBUG] Using machine identifier fallback: '{fallbackName}'");
                    return fallbackName;
                }

                Console.WriteLine("[DEBUG] Could not extract server name, using default");
                return "Plex Media Server";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to extract server name: {ex.Message}");
                return "Plex Media Server";
            }
        }

        public async Task<List<string>> GetLibrarySectionsAsync()
        {
            try
            {
                var plexUrl = _configService.GetPlexServerUrl();
                var token = _configService.GetPlexToken();

                if (string.IsNullOrEmpty(token))
                {
                    return new List<string>();
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // Set headers similar to Tautulli
                client.DefaultRequestHeaders.Add("User-Agent", "PlexPrerollManager/2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Product", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Version", "2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Platform", "Windows");
                client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", "11");

                client.DefaultRequestHeaders.Add("X-Plex-Token", token);

                var response = await client.GetAsync($"{plexUrl.TrimEnd('/')}/library/sections");
                if (!response.IsSuccessStatusCode)
                {
                    return new List<string>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = new XmlDocument();
                doc.LoadXml(content);

                var sections = new List<string>();
                var sectionNodes = doc.SelectNodes("//Directory");
                
                if (sectionNodes != null)
                {
                    foreach (XmlNode section in sectionNodes)
                    {
                        var title = section.Attributes?["title"]?.Value;
                        var type = section.Attributes?["type"]?.Value;
                        var key = section.Attributes?["key"]?.Value;

                        if (!string.IsNullOrEmpty(title))
                        {
                            sections.Add($"{title} ({type}) [Key: {key}]");
                        }
                    }
                }

                return sections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get library sections: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<List<PlexSession>> GetServerSessionsAsync()
        {
            try
            {
                var plexUrl = _configService.GetPlexServerUrl();
                var token = _configService.GetPlexToken();

                if (string.IsNullOrEmpty(token))
                {
                    return new List<PlexSession>();
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // Set headers similar to Tautulli
                client.DefaultRequestHeaders.Add("User-Agent", "PlexPrerollManager/2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Product", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Version", "2.2.0");
                client.DefaultRequestHeaders.Add("X-Plex-Platform", "Windows");
                client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", "11");

                client.DefaultRequestHeaders.Add("X-Plex-Token", token);

                var response = await client.GetAsync($"{plexUrl.TrimEnd('/')}/status/sessions");
                if (!response.IsSuccessStatusCode)
                {
                    return new List<PlexSession>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = new XmlDocument();
                doc.LoadXml(content);

                var sessions = new List<PlexSession>();
                var videoNodes = doc.SelectNodes("//Video");

                if (videoNodes != null)
                {
                    foreach (XmlNode video in videoNodes)
                    {
                        var session = new PlexSession
                        {
                            Title = video.Attributes?["title"]?.Value ?? "Unknown",
                            User = video.SelectSingleNode("User")?.Attributes?["title"]?.Value ?? "Unknown",
                            Player = video.SelectSingleNode("Player")?.Attributes?["title"]?.Value ?? "Unknown",
                            State = video.Attributes?["PlayerState"]?.Value ?? "Unknown",
                            Duration = long.TryParse(video.Attributes?["duration"]?.Value, out var dur) ? dur : 0,
                            ViewOffset = long.TryParse(video.Attributes?["viewOffset"]?.Value, out var offset) ? offset : 0
                        };
                        sessions.Add(session);
                    }
                }

                return sessions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get server sessions: {ex.Message}");
                return new List<PlexSession>();
            }
        }
    }

    public class PlexSession
    {
        public string Title { get; set; } = "";
        public string User { get; set; } = "";
        public string Player { get; set; } = "";
        public string State { get; set; } = "";
        public long Duration { get; set; }
        public long ViewOffset { get; set; }
    }
}