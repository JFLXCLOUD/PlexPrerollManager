using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using PlexPrerollManager.Services;
using PlexPrerollManager.Models;
using System.ComponentModel.DataAnnotations;

namespace PlexPrerollManager.Controllers
{
    public class CategoryDto
    {
        [JsonProperty("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("IsActive")]
        public bool IsActive { get; set; }

        [JsonProperty("PrerollCount")]
        public int PrerollCount { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; } = string.Empty;
    }


    public class RestoreBackupRequest
    {
        public string BackupPath { get; set; } = "";
    }

    public class PlexAuthRequest
    {
        [Required]
        [RegularExpression("^(token|credentials|apikey|plextv|direct)$", ErrorMessage = "Invalid authentication method")]
        public string AuthMethod { get; set; } = "token"; // "token", "credentials", "apikey", "plextv"

        [StringLength(500, ErrorMessage = "Token too long")]
        public string Token { get; set; } = "";

        [StringLength(100, ErrorMessage = "Username too long")]
        public string Username { get; set; } = "";

        [StringLength(200, ErrorMessage = "Password too long")]
        public string Password { get; set; } = "";

        [StringLength(500, ErrorMessage = "API key too long")]
        public string ApiKey { get; set; } = "";

        [StringLength(50, ErrorMessage = "PIN too long")]
        public string PlexTvPin { get; set; } = "";

        [StringLength(50, ErrorMessage = "PIN code too long")]
        public string PlexTvPinCode { get; set; } = ""; // The alphanumeric PIN code
    }

    public class PlexTvAuthResponse
    {
        public string Pin { get; set; } = "";
        public string AuthUrl { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
    }

    public class PlexTvTokenResponse
    {
        public string Token { get; set; } = "";
        public PlexUser User { get; set; } = new PlexUser();
    }

    public class PlexUser
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class PlexAuthCompleteRequest
    {
        public string AuthToken { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
    }

    public class PlexServer
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; }
        public bool Owned { get; set; }
        public string MachineIdentifier { get; set; } = "";
    }

    [ApiController]
    [Route("api")]
    public class PlexController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ConfigurationService _configService;
        private readonly PlexApiService _plexApiService;
        private readonly SchedulingService _schedulingService;
        private readonly BackupService _backupService;
        private static string _activeCategory = "General"; // Default active category

        public PlexController(IConfiguration configuration, ConfigurationService configService, PlexApiService plexApiService, SchedulingService schedulingService, BackupService backupService)
        {
            _configuration = configuration;
            _configService = configService;
            _plexApiService = plexApiService;
            _schedulingService = schedulingService;
            _backupService = backupService;
        }

        private string GetActiveCategory()
        {
            try
            {
                var activeCategoryFile = Path.Combine(AppContext.BaseDirectory, "active_category.txt");
                if (System.IO.File.Exists(activeCategoryFile))
                {
                    var content = System.IO.File.ReadAllText(activeCategoryFile).Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        _activeCategory = content;
                    }
                }
            }
            catch
            {
                // If file read fails, keep default
            }
            return _activeCategory;
        }

        private void SetActiveCategory(string categoryName)
        {
            try
            {
                var activeCategoryFile = Path.Combine(AppContext.BaseDirectory, "active_category.txt");
                System.IO.File.WriteAllText(activeCategoryFile, categoryName);
                _activeCategory = categoryName;
            }
            catch
            {
                // If file write fails, update in-memory only
                _activeCategory = categoryName;
            }
        }

        /// <summary>
        /// Get current Plex server sessions
        /// </summary>
        [HttpGet("plex/sessions")]
        public async Task<IActionResult> GetPlexSessions()
        {
            try
            {
                var sessions = await _plexApiService.GetServerSessionsAsync();

                return Ok(new
                {
                    success = true,
                    sessions = sessions.Select(s => new
                    {
                        title = s.Title,
                        user = s.User,
                        player = s.Player,
                        state = s.State,
                        duration = s.Duration,
                        viewOffset = s.ViewOffset
                    }).ToList(),
                    count = sessions.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get Plex sessions: {ex.Message}");
                return Ok(new
                {
                    success = false,
                    message = $"Failed to get sessions: {ex.Message}",
                    sessions = new List<object>(),
                    count = 0
                });
            }
        }

        /// <summary>
        /// Get user's Plex servers from Plex.tv
        /// </summary>
        [HttpGet("plex/servers")]
        public async Task<IActionResult> GetPlexServers()
        {
            try
            {
                var token = _configService.GetPlexToken();

                if (string.IsNullOrEmpty(token))
                {
                    return Ok(new { success = false, message = "No Plex.tv authentication token found" });
                }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-Plex-Token", token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await client.GetAsync("https://plex.tv/api/v2/resources");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var servers = ParsePlexServers(content);

                    return Ok(new
                    {
                        success = true,
                        servers = servers,
                        count = servers.Count
                    });
                }
                else
                {
                    return Ok(new { success = false, message = $"Failed to get servers: {(int)response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get Plex servers: {ex.Message}");
                return Ok(new
                {
                    success = false,
                    message = $"Failed to get servers: {ex.Message}",
                    servers = new List<object>(),
                    count = 0
                });
            }
        }

        private List<PlexServer> ParsePlexServers(string jsonContent)
        {
            var servers = new List<PlexServer>();

            try
            {
                // Parse JSON response from Plex.tv
                // This would parse the JSON array of server resources
                // For now, return a basic structure
                servers.Add(new PlexServer
                {
                    Name = "Local Plex Server",
                    Address = "http://localhost:32400",
                    Port = 32400,
                    Owned = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to parse server response: {ex.Message}");
            }

            return servers;
        }



        /// <summary>
        /// Start Plex.tv authentication flow
        /// </summary>
        [HttpPost("plex/auth/start")]
        public async Task<IActionResult> StartPlexTvAuth()
        {
            try
            {
                Console.WriteLine("[DEBUG] ===== STARTING PLEX.TV AUTHENTICATION FLOW =====");
                Console.WriteLine($"[DEBUG] Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "9df7f855-bb13-4394-bc67-9a3392c21855");
                client.DefaultRequestHeaders.Add("X-Plex-Product", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Version", "2.2.0");

                var requestBody = new
                {
                    strong = true
                };

                var jsonContent = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                Console.WriteLine($"[DEBUG] Creating PIN with client identifier: 9df7f855-bb13-4394-bc67-9a3392c21855");
                var response = await client.PostAsync("https://plex.tv/api/v2/pins", jsonContent);

                Console.WriteLine($"[DEBUG] PIN creation response status: {(int)response.StatusCode} {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] PIN creation failed: {errorContent}");
                    return Ok(new { success = false, message = $"PIN creation failed: {(int)response.StatusCode} {response.StatusCode}" });
                }

                Console.WriteLine($"[DEBUG] Plex.tv response status: {(int)response.StatusCode} {response.StatusCode}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Plex.tv response content (first 500 chars): {responseContent.Substring(0, Math.Min(500, responseContent.Length))}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        // Parse XML response from Plex.tv
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.LoadXml(responseContent);

                        var pinElement = xmlDoc.SelectSingleNode("/pin");
                        if (pinElement != null)
                        {
                            var pinCode = pinElement.Attributes["code"]?.Value;
                            var pinId = pinElement.Attributes["id"]?.Value;
                            var expiresAt = pinElement.Attributes["expiresAt"]?.Value;

                            if (!string.IsNullOrEmpty(pinCode))
                            {
                                Console.WriteLine($"[DEBUG] Got PIN: {pinCode}, ID: {pinId}, Expires: {expiresAt}");

                                // Generate auth URL similar to Tautulli's format
                                var clientId = "9df7f855-bb13-4394-bc67-9a3392c21855";

                                // Get the actual server URL and port dynamically
                                var request = HttpContext.Request;
                                var scheme = request.Scheme; // http or https
                                var host = request.Host.Host; // IP or hostname
                                var port = request.Host.Port; // Port number

                                // Build the forward URL dynamically
                                var forwardUrl = port.HasValue && port.Value != 80 && port.Value != 443
                                    ? $"{scheme}://{host}:{port}/oauth.html"
                                    : $"{scheme}://{host}/oauth.html";

                                Console.WriteLine($"[DEBUG] Generated forward URL: {forwardUrl}");

                                var contextParams = new[]
                                {
                                    $"clientID={Uri.EscapeDataString(clientId)}",
                                    $"forwardUrl={Uri.EscapeDataString(forwardUrl)}",
                                    $"code={Uri.EscapeDataString(pinCode)}",
                                    $"context%5Bdevice%5D%5Bproduct%5D=PlexPrerollManager",
                                    $"context%5Bdevice%5D%5Bplatform%5D=Windows",
                                    $"context%5Bdevice%5D%5BplatformVersion%5D=11",
                                    $"context%5Bdevice%5D%5Bversion%5D=2.2.0"
                                };

                                var authUrl = $"https://app.plex.tv/auth/#!?{string.Join("&", contextParams)}";

                                return Ok(new {
                                    success = true,
                                    pin = pinCode,
                                    pinId = pinId,
                                    authUrl = authUrl,
                                    expiresAt = expiresAt,
                                    message = "Visit the auth URL to authorize PlexPrerollManager. PIN expires in 5 minutes."
                                });
                            }
                        }

                        Console.WriteLine($"[DEBUG] Failed to parse PIN from XML");
                        return Ok(new { success = false, message = "Failed to extract PIN from Plex.tv response" });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] XML parsing error: {ex.Message}");
                        return Ok(new { success = false, message = $"Failed to parse Plex.tv XML response: {ex.Message}" });
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Plex.tv API error: {responseContent}");
                    return Ok(new { success = false, message = $"Plex.tv API error: {(int)response.StatusCode} {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error starting Plex.tv auth: {ex.Message}");
                return Ok(new { success = false, message = $"Failed to start authentication: {ex.Message}" });
            }
        }

        /// <summary>
        /// Check Plex.tv authentication status
        /// </summary>
        [HttpPost("plex/auth/check")]
        public async Task<IActionResult> CheckPlexTvAuth([FromBody] PlexAuthRequest request)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ===== CHECKING PLEX.TV AUTH STATUS =====");
                Console.WriteLine($"[DEBUG] Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"[DEBUG] Received PIN from frontend: {request.PlexTvPin}");

                if (string.IsNullOrEmpty(request.PlexTvPin))
                {
                    Console.WriteLine($"[DEBUG] PIN is empty or null");
                    return Ok(new { success = false, message = "PIN is required" });
                }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "9df7f855-bb13-4394-bc67-9a3392c21855");
                client.DefaultRequestHeaders.Add("X-Plex-Product", "PlexPrerollManager");
                client.DefaultRequestHeaders.Add("X-Plex-Version", "2.2.0");

                // CRITICAL FIX: The PIN from frontend should be PIN ID for authentication checks
                // Plex.tv API expects PIN ID for authentication checks, not PIN CODE
                var pinId = request.PlexTvPin;
                Console.WriteLine($"[DEBUG] Using PIN ID for authentication check: {pinId}");

                var response = await client.GetAsync($"https://plex.tv/api/v2/pins/{pinId}");

                Console.WriteLine($"[DEBUG] Check auth response status: {(int)response.StatusCode} {response.StatusCode}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Check auth response content (first 500 chars): {responseContent.Substring(0, Math.Min(500, responseContent.Length))}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        // Parse XML response from Plex.tv
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.LoadXml(responseContent);

                        var pinElement = xmlDoc.SelectSingleNode("/pin");
                        if (pinElement != null)
                        {
                            var authToken = pinElement.Attributes["authToken"]?.Value;
                            var userId = pinElement.Attributes["clientIdentifier"]?.Value;

                            if (!string.IsNullOrEmpty(authToken))
                            {
                                Console.WriteLine($"[DEBUG] Got Plex.tv auth token: {authToken.Substring(0, 20)}...");

                                // Get user info using the auth token
                                var userInfo = await GetPlexUserInfo(authToken);

                                // TODO: Save authentication settings to configuration
                                // This should update appsettings.json with the new token
                                Console.WriteLine($"[DEBUG] Authentication successful - token should be saved to configuration");

                                return Ok(new {
                                    success = true,
                                    token = authToken,
                                    user = userInfo,
                                    message = "Successfully authenticated with Plex.tv"
                                });
                            }
                        }

                        Console.WriteLine($"[DEBUG] No auth token in response - authentication not completed yet");
                        return Ok(new { success = false, message = "Authentication not yet completed. Please authorize the app on Plex.tv." });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] XML parsing error in check: {ex.Message}");
                        return Ok(new { success = false, message = $"Failed to parse authentication response: {ex.Message}" });
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Check auth API error: {responseContent}");

                    if (responseContent.Contains("Code not found or expired"))
                    {
                        return Ok(new {
                            success = false,
                            message = "PIN has expired. Please start the authentication process again.",
                            expired = true
                        });
                    }

                    return Ok(new { success = false, message = $"Authentication check failed: {(int)response.StatusCode} {response.StatusCode}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error checking Plex.tv auth: {ex.Message}");
                return Ok(new { success = false, message = $"Failed to check authentication: {ex.Message}" });
            }
        }

        /// <summary>
        /// Complete Plex.tv authentication
        /// </summary>
        [HttpPost("plex/auth/complete")]
        public async Task<IActionResult> CompletePlexAuth([FromBody] PlexAuthCompleteRequest request)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ===== COMPLETEPLEXAUTH ENDPOINT CALLED =====");
                Console.WriteLine($"[DEBUG] ===== RECEIVED PLEX.TV AUTH COMPLETION =====");
                Console.WriteLine($"[DEBUG] Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

                if (string.IsNullOrEmpty(request.AuthToken))
                {
                    Console.WriteLine($"[DEBUG] No authentication token provided in request");
                    return Ok(new { success = false, message = "No authentication token provided" });
                }

                Console.WriteLine($"[DEBUG] Received auth token for user {request.Username}: {request.AuthToken?.Substring(0, Math.Min(20, request.AuthToken.Length))}...");

                // Check current token before saving
                var currentToken = _configService.GetPlexToken();
                Console.WriteLine($"[DEBUG] Current token before save: {currentToken?.Substring(0, Math.Min(20, currentToken?.Length ?? 0))}...");

                // Save the Plex.tv token to configuration
                Console.WriteLine($"[DEBUG] About to call UpdatePlexAuthenticationAsync with token: {request.AuthToken?.Substring(0, Math.Min(20, request.AuthToken?.Length ?? 0))}...");
                var success = await _configService.UpdatePlexAuthenticationAsync("plextv", request.AuthToken);
                Console.WriteLine($"[DEBUG] UpdatePlexAuthenticationAsync returned: {success}");

                if (success)
                {
                    Console.WriteLine($"[DEBUG] Successfully saved Plex.tv token to configuration");

                    // Verify the token was actually saved
                    var newToken = _configService.GetPlexToken();
                    Console.WriteLine($"[DEBUG] Token after save: {newToken?.Substring(0, Math.Min(20, newToken?.Length ?? 0))}...");

                    return Ok(new {
                        success = true,
                        message = $"Authentication completed and saved for user {request.Username}",
                        username = request.Username,
                        userId = request.UserId,
                        tokenSaved = !string.IsNullOrEmpty(newToken)
                    });
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Failed to save token to configuration");
                    return Ok(new {
                        success = false,
                        message = $"Authentication completed for user {request.Username} (configuration save failed)",
                        username = request.Username,
                        userId = request.UserId,
                        warning = "Token not saved to configuration file"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error completing authentication: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                return Ok(new { success = false, message = $"Authentication completion failed: {ex.Message}" });
            }
        }

        private async Task<object> GetPlexUserInfo(string authToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-Plex-Token", authToken);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await client.GetAsync("https://plex.tv/api/v2/user");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Parse the JSON response to get user info
                    // For now, return basic info
                    return new { username = "Plex User", id = "unknown" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error getting user info: {ex.Message}");
            }

            return new { username = "Plex User", id = "unknown" };
        }

        /// <summary>
        /// Get Plex server status
        /// </summary>
        [HttpGet("plex/status")]
        public async Task<IActionResult> GetPlexStatus()
        {
            Console.WriteLine("[DEBUG] GetPlexStatus endpoint called");
            var plexUrl = _configService.GetPlexServerUrl();
            var hasCredentials = _configService.HasPlexAuthentication();

            Console.WriteLine($"[DEBUG] Plex URL: {plexUrl}, Has credentials: {hasCredentials}");

            // Test actual Plex connectivity using the new service
            Console.WriteLine("[DEBUG] About to call TestConnectionAsync");
            var connectionTest = await _plexApiService.TestConnectionAsync();
            Console.WriteLine($"[DEBUG] TestConnectionAsync returned: Success={connectionTest.Success}, Message={connectionTest.Message}");

            var result = Ok(new
            {
                Connected = connectionTest.Success,
                ServerName = connectionTest.Success ? connectionTest.Message : "Local Plex Server",
                Url = plexUrl,
                HasToken = !string.IsNullOrEmpty(_configService.GetPlexToken()),
                HasCredentials = !string.IsNullOrEmpty(_configService.GetPlexUsername()) && !string.IsNullOrEmpty(_configService.GetPlexPassword()),
                HasApiKey = !string.IsNullOrEmpty(_configService.GetPlexApiKey()),
                AuthMethod = _configService.GetAuthMethod(),
                LastTested = DateTime.UtcNow,
                ErrorMessage = connectionTest.Success ? null : connectionTest.Message
            });

            Console.WriteLine("[DEBUG] GetPlexStatus returning response");
            return result;
        }

        private async Task<(bool IsConnected, string ServerName, string ErrorMessage)> TestPlexConnectivity(
            string plexUrl, string token, string username, string password, string apiKey)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // Try different authentication methods
                HttpResponseMessage response = null;

                // Method 1: Token authentication (direct server token or Plex.tv token)
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Add("X-Plex-Token", token);
                    response = await client.GetAsync($"{plexUrl}/identity");
                }
                // Method 2: API Key authentication
                else if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("X-Plex-Token", apiKey);
                    response = await client.GetAsync($"{plexUrl}/identity");
                }
                // Method 3: Username/Password (would need to get token first)
                else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    // For username/password, we'd need to authenticate with Plex.tv first
                    // This is more complex and requires the Plex.tv API
                    return (false, null, "Username/password authentication requires additional setup");
                }
                else
                {
                    // No credentials configured - cannot connect
                    return (false, null, "No Plex authentication credentials configured");
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    // Try to extract server name from response
                    var serverName = "Plex Media Server";
                    var machineId = "";
                    var version = "";

                    try
                    {
                        var content = await response.Content.ReadAsStringAsync();

                        if (content.Contains("<MediaContainer") || content.Contains("<?xml"))
                        {
                        // Debug: Log the raw response to understand the XML structure
                        // Console.WriteLine($"[DEBUG] Plex server response: {content}");

                        // Extract machineIdentifier (fallback)
                        var machineMatch = System.Text.RegularExpressions.Regex.Match(content, @"machineIdentifier=""([^""]+)""");
                        if (machineMatch.Success)
                        {
                            machineId = machineMatch.Groups[1].Value;
                        }

                        // Extract version
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(content, @"version=""([^""]+)""");
                        if (versionMatch.Success)
                        {
                            version = versionMatch.Groups[1].Value;
                        }

                        // Try multiple patterns to extract friendly server name (prioritize this)
                        var friendlyPatterns = new[]
                        {
                            @"friendlyName=""([^""]+)""",
                            @"<MediaContainer[^>]*friendlyName=""([^""]+)""",
                            @"<Server[^>]*friendlyName=""([^""]+)""",
                            @"<Device[^>]*friendlyName=""([^""]+)""",
                            @"name=""([^""]+)""",
                            @"<Server[^>]*name=""([^""]+)""",
                            @"<Device[^>]*name=""([^""]+)""",
                            @"title=""([^""]+)""",
                            @"<MediaContainer[^>]*title=""([^""]+)""",
                            // Additional patterns for different Plex server versions
                            @"<MediaContainer[^>]*machineIdentifier=""([^""]+)""[^>]*name=""([^""]+)""",
                            @"<Server[^>]*machineIdentifier=""([^""]+)""[^>]*name=""([^""]+)""",
                            @"<Device[^>]*machineIdentifier=""([^""]+)""[^>]*name=""([^""]+)""",
                            // Try to match server name in different XML structures
                            @"<MediaContainer[^>]*serverName=""([^""]+)""",
                            @"<Server[^>]*serverName=""([^""]+)""",
                            @"<Device[^>]*serverName=""([^""]+)"""
                        };

                        foreach (var pattern in friendlyPatterns)
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                            {
                                var friendlyName = match.Groups[1].Value.Trim();
                                Console.WriteLine($"[DEBUG] Found friendlyName: '{friendlyName}' using pattern: {pattern}");
                                // Make sure it's not just a generic name and has reasonable length
                                if (!friendlyName.ToLower().Contains("plex media server") &&
                                    !friendlyName.ToLower().Contains("unknown") &&
                                    friendlyName.Length > 2 &&
                                    friendlyName.Length < 100)
                                {
                                    serverName = friendlyName;
                                    Console.WriteLine($"[DEBUG] Using friendlyName: '{serverName}'");
                                    break;
                                }
                            }
                        }

                        // If we still don't have a good name, try to get the host name from the URL
                        if (serverName == "Plex Media Server")
                        {
                            try
                            {
                                var uri = new Uri(plexUrl);
                                var hostName = uri.Host;
                                if (!string.IsNullOrEmpty(hostName) && hostName != "localhost" && hostName != "127.0.0.1")
                                {
                                    serverName = $"{hostName} Plex Server";
                                }
                            }
                            catch
                            {
                                // Ignore URI parsing errors
                            }
                        }

                        // If we got a friendly name, optionally add version
                        if (serverName != "Plex Media Server" && !string.IsNullOrEmpty(version) && version.Length < 10)
                        {
                            serverName = $"{serverName} (v{version})";
                        }
                        // If still generic and we have machineId, use a shortened version
                        else if (serverName == "Plex Media Server" && !string.IsNullOrEmpty(machineId) && machineId.Length > 8)
                        {
                            serverName = $"Plex Server ({machineId.Substring(0, 8)}...)";
                        }
                        }
                    }
                    catch (Exception ex)
                    {
                        // If XML parsing fails, log the error but continue with fallback logic
                        Console.WriteLine($"[ERROR] Failed to parse Plex server response: {ex.Message}");
                        serverName = "Plex Media Server"; // Reset to default on error
                    }

                    // Final fallback - if we still have the generic name
                    if (serverName == "Plex Media Server" && !string.IsNullOrEmpty(machineId))
                    {
                        serverName = $"Plex Server ({machineId.Substring(0, 8)}...)";
                    }

                    // If we still have the generic name, try to get server identity from a different endpoint
                    if (serverName == "Plex Media Server" || serverName.Contains("Plex Server ("))
                    {
                        try
                        {
                            // Try to get server identity from identity endpoint (where friendlyName is located)
                            var identityUrl = $"{plexUrl.TrimEnd('/')}/identity";
                            using var identityClient = new HttpClient();
                            identityClient.DefaultRequestHeaders.Add("X-Plex-Token", token ?? apiKey ?? "");
                            identityClient.DefaultRequestHeaders.Add("Accept", "application/xml");

                            var rootResponse = await identityClient.GetAsync(identityUrl);
                            if (rootResponse.IsSuccessStatusCode)
                            {
                                var rootContent = await rootResponse.Content.ReadAsStringAsync();

                                // Try to extract server name from root response
                                var rootPatterns = new[]
                                {
                                    @"friendlyName=""([^""]+)""",
                                    @"<MediaContainer[^>]*friendlyName=""([^""]+)""",
                                    @"name=""([^""]+)""",
                                    @"<MediaContainer[^>]*name=""([^""]+)"""
                                };

                                foreach (var pattern in rootPatterns)
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(rootContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (match.Success && match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                                    {
                                        var rootName = match.Groups[1].Value.Trim();
                                        if (!rootName.ToLower().Contains("plex media server") &&
                                            !rootName.ToLower().Contains("unknown") &&
                                            rootName.Length > 2 &&
                                            rootName.Length < 100)
                                        {
                                            serverName = rootName;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore root endpoint errors, keep the current serverName
                        }
                    }

                    return (true, serverName, null);
                }
                else
                {
                    var errorMsg = response != null ? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}" : "No response from server";
                    return (false, null, errorMsg);
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Connection failed: {ex.Message}");
            }
        }

        private string GetAuthMethod(string token, string username, string password, string apiKey)
        {
            if (!string.IsNullOrEmpty(token)) return "Token";
            if (!string.IsNullOrEmpty(apiKey)) return "API Key";
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) return "Username/Password";
            return "None";
        }

        private string GetAuthMethod(string token, string username, string password, string apiKey, string plexTvToken)
        {
            if (!string.IsNullOrEmpty(plexTvToken)) return "Plex.tv";
            if (!string.IsNullOrEmpty(token)) return "Token";
            if (!string.IsNullOrEmpty(apiKey)) return "API Key";
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) return "Username/Password";
            return "None";
        }

        /// <summary>
        /// Get main status (used by dashboard)
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var plexUrl = _configuration["Plex:Url"];
            var plexToken = _configuration["Plex:Token"];
            var plexUsername = _configuration["Plex:Username"];
            var plexPassword = _configuration["Plex:Password"];
            var plexApiKey = _configuration["Plex:ApiKey"];

            // Test actual Plex connectivity using the new service
            var connectionTest = await _plexApiService.TestConnectionAsync();

            return Ok(new
            {
                PlexConnected = connectionTest.Success,
                PlexServerName = connectionTest.Success ? connectionTest.Message : "Local Plex Server",
                TotalPrerolls = GetTotalPrerollCount(),
                ActiveCategory = GetActiveCategory(),
                AuthMethod = _configService.GetAuthMethod(),
                PlexErrorMessage = connectionTest.Success ? null : $"Connection failed: {connectionTest.Message}"
            });
        }

        private int GetTotalPrerollCount()
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory!, "Prerolls");
                if (!Directory.Exists(basePath)) return 0;

                return Directory.GetDirectories(basePath)
                    .Sum(dir => Directory.GetFiles(dir, "*.*")
                        .Count(f => IsVideoFile(f)));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Serve preroll files
        /// </summary>
        [HttpGet("files/{category}/{fileName}")]
        public IActionResult GetPrerollFile(string category, string fileName)
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                var filePath = Path.Combine(basePath, category, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound();
                }

                var fileStream = System.IO.File.OpenRead(filePath);
                return File(fileStream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error serving file {fileName}: {ex.Message}");
                return StatusCode(500, "Error serving file");
            }
        }

        /// <summary>
        /// Get categories
        /// </summary>
        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory!, "Prerolls");
                var categories = new List<CategoryDto>();

                // Define all possible categories
                var allCategories = new[]
                {
                    new { Name = "General", Description = "General purpose prerolls" },
                    new { Name = "Halloween", Description = "Halloween themed prerolls" },
                    new { Name = "Christmas", Description = "Christmas holiday prerolls" },
                    new { Name = "New Years", Description = "New Year's celebration prerolls" },
                    new { Name = "4th of July", Description = "Independence Day prerolls" },
                    new { Name = "Thanksgiving", Description = "Thanksgiving holiday prerolls" },
                    new { Name = "Easter", Description = "Easter holiday prerolls" },
                    new { Name = "Valentines", Description = "Valentine's Day prerolls" }
                };

                foreach (var cat in allCategories)
                {
                    var categoryPath = Path.Combine(basePath, cat.Name);
                    var videoCount = 0;

                    if (Directory.Exists(categoryPath))
                    {
                        try
                        {
                            videoCount = Directory.GetFiles(categoryPath, "*.*")
                                .Count(f => IsVideoFile(f));
                        }
                        catch
                        {
                            videoCount = 0;
                        }
                    }

                    // Only include categories that have videos
                    if (videoCount > 0)
                    {
                        categories.Add(new CategoryDto
                        {
                            Name = cat.Name,
                            IsActive = cat.Name == GetActiveCategory(), // Check if this is the active category
                            PrerollCount = videoCount,
                            Description = cat.Description
                        });
                    }
                }

                return Ok(categories);
            }
            catch
            {
                // Return empty list on error
                return Ok(new List<CategoryDto>());
            }
        }

        /// <summary>
        /// Get prerolls by category
        /// </summary>
        [HttpGet("prerolls/{category}")]
        public IActionResult GetPrerollsByCategory(string category)
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory!, "Prerolls");
                var categoryPath = Path.Combine(basePath, category);

                if (!Directory.Exists(categoryPath))
                {
                    return Ok(new List<object>());
                }

                var files = Directory.GetFiles(categoryPath, "*.*")
                    .Where(f => IsVideoFile(f))
                    .Select(f => CreatePrerollObject(f, category))
                    .ToList();

                return Ok(files);
            }
            catch
            {
                return Ok(new List<object>());
            }
        }

        private bool IsVideoFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
            return videoExtensions.Contains(extension);
        }

        private object CreatePrerollObject(string filePath, string category)
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileName(filePath);

            return new
            {
                Id = fileName, // Use filename as ID for now
                Name = Path.GetFileNameWithoutExtension(fileName),
                FilePath = $"/files/{category}/{fileName}", // Relative path for web access
                CategoryName = category,
                FileSizeBytes = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime.ToString("O"),
                HasMetadata = false, // Could be enhanced to read video metadata
                Duration = (long?)null,
                Resolution = (string?)null,
                VideoCodec = (string?)null,
                AudioCodec = (string?)null
            };
        }

        /// <summary>
        /// Activate a category
        /// </summary>
        [HttpPost("categories/{categoryName}/activate")]
        public async Task<IActionResult> ActivateCategory(string categoryName)
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                var categoryPath = Path.Combine(basePath, categoryName);

                // Check if category exists and has videos
                if (!Directory.Exists(categoryPath))
                {
                    return Ok(new { success = false, message = $"Category '{categoryName}' does not exist" });
                }

                var videoFiles = Directory.GetFiles(categoryPath, "*.*")
                    .Where(f => IsVideoFile(f))
                    .ToList();

                if (videoFiles.Count == 0)
                {
                    return Ok(new { success = false, message = $"Category '{categoryName}' has no videos to activate" });
                }

                // Set this category as the active one locally
                SetActiveCategory(categoryName);

                // Attempt to update Plex server preroll settings
                var plexResult = await _plexApiService.SetPrerollAsync(categoryName, videoFiles);

                return Ok(new {
                    success = true,
                    message = $"Category '{categoryName}' activated successfully ({videoFiles.Count} videos)",
                    category = categoryName,
                    videoCount = videoFiles.Count,
                    plexUpdate = new { success = plexResult.Success, message = plexResult.Message }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error activating category '{categoryName}': {ex.Message}");
                return Ok(new { success = false, message = $"Failed to activate category: {ex.Message}" });
            }
        }

        /// <summary>
        /// Update Plex server preroll settings
        /// </summary>
        private async Task<object> UpdatePlexPrerolls(string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Starting Plex preroll update for category '{categoryName}' with {videoFiles.Count} videos");

                var plexUrl = _configuration["Plex:Url"];
                var plexToken = _configuration["Plex:Token"];
                var plexApiKey = _configuration["Plex:ApiKey"];

                Console.WriteLine($"[DEBUG] Plex URL: {plexUrl}, Token configured: {!string.IsNullOrEmpty(plexToken)}, API Key configured: {!string.IsNullOrEmpty(plexApiKey)}");

                if (string.IsNullOrEmpty(plexUrl))
                {
                    Console.WriteLine("[DEBUG] Plex URL not configured");
                    return new { success = false, message = "Plex server URL not configured" };
                }

                if (string.IsNullOrEmpty(plexToken) && string.IsNullOrEmpty(plexApiKey))
                {
                    Console.WriteLine("[DEBUG] Plex authentication not configured");
                    return new { success = false, message = "Plex authentication not configured" };
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // Use token or API key for authentication
                var authToken = plexToken ?? plexApiKey;
                client.DefaultRequestHeaders.Add("X-Plex-Token", authToken);
                client.DefaultRequestHeaders.Add("Accept", "application/xml");

                // Method 1: Try to set preroll preferences via server preferences API
                Console.WriteLine("[DEBUG] Trying to update preroll preferences...");
                var prefsResult = await TryUpdatePrerollPreferences(client, plexUrl, categoryName, videoFiles);
                Console.WriteLine($"[DEBUG] Preferences update result: Success={prefsResult.Success}, Message={prefsResult.Message}");

                if (prefsResult.Success)
                {
                    return new { success = true, message = prefsResult.Message, method = "preferences" };
                }

                // Method 2: Try alternative Plex preroll update methods
                Console.WriteLine("[DEBUG] Trying alternative Plex preroll update methods...");
                var altResult = await TryUpdatePrerollPreferences(client, plexUrl, categoryName, videoFiles);
                Console.WriteLine($"[DEBUG] Alternative result: Success={altResult.Success}, Message={altResult.Message}");

                if (altResult.Success)
                {
                    return new { success = true, message = altResult.Message, method = "alternative" };
                }

                // If both methods fail, return partial success with local activation only
                Console.WriteLine($"[DEBUG] Both methods failed. Prefs error: {prefsResult.Message}, Alt error: {altResult.Message}");
                return new {
                    success = false,
                    message = "Category activated locally, but Plex server update failed. Manual configuration may be required.",
                    localSuccess = true,
                    error = prefsResult.Message ?? altResult.Message
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Plex prerolls: {ex.Message}");
                return new {
                    success = false,
                    message = $"Failed to update Plex server: {ex.Message}",
                    localSuccess = true
                };
            }
        }

        private async Task<(bool Success, string Message)> TryUpdatePrerollPreferences(HttpClient client, string plexUrl, string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Attempting to update Plex preroll settings for category '{categoryName}'");

                // Method 1: Try the endpoints that were likely working in version 2.1.0
                var success = await TryVersion210Endpoints(client, plexUrl, categoryName, videoFiles);
                if (success)
                {
                    return (true, $"Successfully updated Plex preroll settings for category '{categoryName}' using v2.1.0 compatible method");
                }

                // Method 2: Try alternative API patterns
                success = await TryAlternativeApiPatterns(client, plexUrl, categoryName, videoFiles);
                if (success)
                {
                    return (true, $"Successfully updated Plex preroll settings for category '{categoryName}' using alternative API");
                }

                // Method 3: Try to set CinemaTrailersPrerollID preference
                success = await TrySetPrerollPreference(client, plexUrl, categoryName, videoFiles);
                if (success)
                {
                    return (true, $"Successfully updated Plex preroll settings for category '{categoryName}'");
                }

                // Method 4: Try to upload files to Plex's preroll directory
                success = await TryUploadToPlexPrerollDirectory(client, plexUrl, categoryName, videoFiles);
                if (success)
                {
                    return (true, $"Successfully uploaded preroll files to Plex server for category '{categoryName}'");
                }

                return (false, "All Plex preroll update methods failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in TryUpdatePrerollPreferences: {ex.Message}");
                return (false, $"Error updating Plex prerolls: {ex.Message}");
            }
        }

        private async Task<bool> TryVersion210Endpoints(HttpClient client, string plexUrl, string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Trying version 2.1.0 compatible endpoints");

                // Get the first video file as the preroll
                var firstVideo = videoFiles.FirstOrDefault();
                if (string.IsNullOrEmpty(firstVideo))
                {
                    Console.WriteLine($"[DEBUG] No video files found for category '{categoryName}'");
                    return false;
                }

                var fileName = Path.GetFileName(firstVideo);
                var prerollId = firstVideo; // Use full path for Plex preroll ID

                Console.WriteLine($"[DEBUG] Using preroll file: {fileName}, Full path: {prerollId}");

                // Try the exact endpoints that were likely working in 2.1.0
                var endpointsToTry = new[]
                {
                    // Direct preference setting (most likely to work)
                    $"{plexUrl.TrimEnd('/')}/:/prefs?CinemaTrailersPrerollID={Uri.EscapeDataString(prerollId)}",
                    // Alternative preference format
                    $"{plexUrl.TrimEnd('/')}/:/prefs?cinemaTrailersPrerollID={Uri.EscapeDataString(prerollId)}",
                    // Try without CinemaTrailers prefix
                    $"{plexUrl.TrimEnd('/')}/:/prefs?PrerollID={Uri.EscapeDataString(prerollId)}",
                    // Try the old format that might have been used
                    $"{plexUrl.TrimEnd('/')}/:/prefs?preroll={Uri.EscapeDataString(fileName)}",
                    // Try with full file path
                    $"{plexUrl.TrimEnd('/')}/:/prefs?CinemaTrailersPrerollID={Uri.EscapeDataString(firstVideo)}",
                    // Try different parameter names
                    $"{plexUrl.TrimEnd('/')}/:/prefs?prerollID={Uri.EscapeDataString(prerollId)}",
                    $"{plexUrl.TrimEnd('/')}/:/prefs?trailerID={Uri.EscapeDataString(prerollId)}"
                };

                foreach (var endpoint in endpointsToTry)
                {
                    Console.WriteLine($"[DEBUG] Trying v2.1.0 endpoint: {endpoint}");

                    // Try different HTTP methods
                    var methods = new[] { HttpMethod.Put, HttpMethod.Post, HttpMethod.Get };

                    foreach (var method in methods)
                    {
                        try
                        {
                            HttpResponseMessage response;
                            if (method == HttpMethod.Put)
                            {
                                response = await client.PutAsync(endpoint, null);
                            }
                            else if (method == HttpMethod.Post)
                            {
                                var content = new StringContent("");
                                response = await client.PostAsync(endpoint, content);
                            }
                            else // GET
                            {
                                response = await client.GetAsync(endpoint);
                            }

                            Console.WriteLine($"[DEBUG] {method.Method} response: {(int)response.StatusCode} {response.StatusCode}");

                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"[DEBUG] SUCCESS! Used {method.Method} on endpoint: {endpoint}");
                                return true;
                            }
                            else
                            {
                                // Log response for debugging
                                var responseContent = await response.Content.ReadAsStringAsync();
                                Console.WriteLine($"[DEBUG] Response content: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Exception with {method.Method}: {ex.Message}");
                        }
                    }
                }

                // Try a completely different approach - check if we can get current preferences first
                Console.WriteLine($"[DEBUG] Trying to read current preferences first");
                try
                {
                    var prefsUrl = $"{plexUrl.TrimEnd('/')}/:/prefs";
                    var prefsResponse = await client.GetAsync(prefsUrl);
                    Console.WriteLine($"[DEBUG] GET /:/prefs response: {(int)prefsResponse.StatusCode}");

                    if (prefsResponse.IsSuccessStatusCode)
                    {
                        var prefsContent = await prefsResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[DEBUG] Current preferences (first 500 chars): {prefsContent.Substring(0, Math.Min(500, prefsContent.Length))}");

                        // Look for any preroll-related preferences
                        var prerollPrefs = System.Text.RegularExpressions.Regex.Matches(prefsContent, @"(CinemaTrailersPrerollID|cinemaTrailersPrerollID|PrerollID|prerollID)[^>]*value=""([^""]*)""");
                        foreach (System.Text.RegularExpressions.Match match in prerollPrefs)
                        {
                            Console.WriteLine($"[DEBUG] Found preroll preference: {match.Groups[1].Value} = {match.Groups[2].Value}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error reading preferences: {ex.Message}");
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in TryVersion210Endpoints: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryAlternativeApiPatterns(HttpClient client, string plexUrl, string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Trying alternative API patterns");

                // Get the first video file as the preroll
                var firstVideo = videoFiles.FirstOrDefault();
                if (string.IsNullOrEmpty(firstVideo))
                {
                    Console.WriteLine($"[DEBUG] No video files found for category '{categoryName}'");
                    return false;
                }

                var fileName = Path.GetFileName(firstVideo);
                var prerollId = firstVideo; // Use full path for Plex preroll ID

                // Try some alternative API patterns that might work with different Plex versions
                var alternativeEndpoints = new[]
                {
                    // Try different base paths
                    $"{plexUrl.TrimEnd('/')}/prefs?CinemaTrailersPrerollID={Uri.EscapeDataString(prerollId)}",
                    $"{plexUrl.TrimEnd('/')}/preferences?CinemaTrailersPrerollID={Uri.EscapeDataString(prerollId)}",

                    // Try with different parameter formats
                    $"{plexUrl.TrimEnd('/')}/:/prefs?CinemaTrailersPrerollID={prerollId}",
                    $"{plexUrl.TrimEnd('/')}/:/prefs?cinemaTrailersPrerollID={prerollId}",

                    // Try with JSON body instead of query parameters
                    $"{plexUrl.TrimEnd('/')}/:/prefs"
                };

                foreach (var endpoint in alternativeEndpoints)
                {
                    Console.WriteLine($"[DEBUG] Trying alternative endpoint: {endpoint}");

                    // For the JSON body approach
                    if (endpoint.Contains("/:/prefs") && !endpoint.Contains("="))
                    {
                        var jsonBody = $"{{\"CinemaTrailersPrerollID\":\"{prerollId}\"}}";
                        var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                        var response = await client.PutAsync(endpoint, content);
                        Console.WriteLine($"[DEBUG] PUT with JSON body response: {(int)response.StatusCode}");

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[DEBUG] SUCCESS! Used JSON body on endpoint: {endpoint}");
                            return true;
                        }
                    }
                    else
                    {
                        // Try different methods for query parameter endpoints
                        var methods = new[] { HttpMethod.Put, HttpMethod.Post };

                        foreach (var method in methods)
                        {
                            HttpResponseMessage response;
                            if (method == HttpMethod.Put)
                            {
                                response = await client.PutAsync(endpoint, null);
                            }
                            else
                            {
                                var content = new StringContent("");
                                response = await client.PostAsync(endpoint, content);
                            }

                            Console.WriteLine($"[DEBUG] {method.Method} response: {(int)response.StatusCode}");

                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"[DEBUG] SUCCESS! Used {method.Method} on endpoint: {endpoint}");
                                return true;
                            }
                        }
                    }
                }

                // Try one more approach - check if we need to use a different authentication method
                Console.WriteLine($"[DEBUG] Trying with different authentication headers");
                try
                {
                    var testClient = new HttpClient();
                    // Try without X-Plex-Token header
                    var testUrl = $"{plexUrl.TrimEnd('/')}/:/prefs?CinemaTrailersPrerollID={Uri.EscapeDataString(prerollId)}";
                    var testResponse = await testClient.PutAsync(testUrl, null);
                    Console.WriteLine($"[DEBUG] Request without X-Plex-Token: {(int)testResponse.StatusCode}");

                    if (testResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[DEBUG] SUCCESS! No authentication required for this endpoint");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error testing without auth: {ex.Message}");
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in TryAlternativeApiPatterns: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TrySetPrerollPreference(HttpClient client, string plexUrl, string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Trying to set cinema trailers preroll settings");

                // Get the first video file as the preroll
                var firstVideo = videoFiles.FirstOrDefault();
                if (string.IsNullOrEmpty(firstVideo))
                {
                    Console.WriteLine($"[DEBUG] No video files found for category '{categoryName}'");
                    return false;
                }

                // Get just the filename for the preroll ID
                var fileName = Path.GetFileName(firstVideo);
                var prerollId = firstVideo; // Use full path for Plex preroll ID

                Console.WriteLine($"[DEBUG] Setting preroll ID to: {prerollId}");

                // Try the preference keys that were working in version 2.1.0
                var preferenceKeys = new[]
                {
                    // Keys that were likely working in 2.1.0
                    "CinemaTrailersPrerollID",
                    "cinemaTrailersPrerollID",
                    // Try without the CinemaTrailers prefix
                    "PrerollID",
                    "prerollID",
                    // Try different variations
                    "CinemaTrailersPreroll",
                    "cinemaTrailersPreroll",
                    // Try the key that might have been used in older versions
                    "preroll",
                    "Preroll"
                };

                foreach (var prefKey in preferenceKeys)
                {
                    // Try both PUT and POST methods
                    var methods = new[] { HttpMethod.Put, HttpMethod.Post };

                    foreach (var method in methods)
                    {
                        var setPrefsUrl = $"{plexUrl.TrimEnd('/')}/:/prefs?{prefKey}={Uri.EscapeDataString(prerollId)}";
                        Console.WriteLine($"[DEBUG] Trying {method.Method} with preference key '{prefKey}' to URL: {setPrefsUrl}");

                        HttpResponseMessage setResponse;
                        if (method == HttpMethod.Put)
                        {
                            setResponse = await client.PutAsync(setPrefsUrl, null);
                        }
                        else
                        {
                            var content = new StringContent("");
                            setResponse = await client.PostAsync(setPrefsUrl, content);
                        }

                        Console.WriteLine($"[DEBUG] {method.Method} response for {prefKey}: {(int)setResponse.StatusCode} {setResponse.StatusCode}");

                        if (setResponse.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[DEBUG] Successfully set {prefKey} to {prerollId} using {method.Method}");
                            return true;
                        }
                        else
                        {
                            // Log the response content for debugging
                            var responseContent = await setResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"[DEBUG] Response content: {responseContent}");
                        }
                    }
                }

                // Try the endpoints that were likely working in version 2.1.0
                Console.WriteLine($"[DEBUG] Trying library management endpoints");

                // Try to get library sections
                var sectionsUrl = $"{plexUrl.TrimEnd('/')}/library/sections";
                var sectionsResponse = await client.GetAsync(sectionsUrl);

                if (sectionsResponse.IsSuccessStatusCode)
                {
                    var sectionsContent = await sectionsResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] Library sections response: {sectionsContent.Substring(0, Math.Min(300, sectionsContent.Length))}");

                    // Look for any section that might handle prerolls
                    var sectionMatches = System.Text.RegularExpressions.Regex.Matches(sectionsContent, @"key=""([^""]+)""[^>]*type=""([^""]+)""");
                    foreach (System.Text.RegularExpressions.Match match in sectionMatches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            var sectionKey = match.Groups[1].Value;
                            var sectionType = match.Groups[2].Value;

                            Console.WriteLine($"[DEBUG] Found section: key={sectionKey}, type={sectionType}");

                            // Try to set preroll on this section
                            var setPrerollUrl = $"{plexUrl.TrimEnd('/')}/library/sections/{sectionKey}?prerollID={Uri.EscapeDataString(prerollId)}";
                            Console.WriteLine($"[DEBUG] Trying section preroll URL: {setPrerollUrl}");

                            var prerollResponse = await client.PutAsync(setPrerollUrl, null);
                            Console.WriteLine($"[DEBUG] Section preroll response: {(int)prerollResponse.StatusCode}");

                            if (prerollResponse.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"[DEBUG] Successfully set preroll via section {sectionKey}");
                                return true;
                            }
                        }
                    }
                }

                // Try the old cinema trailers endpoint as fallback
                Console.WriteLine($"[DEBUG] Trying cinema trailers endpoint");
                var cinemaUrl = $"{plexUrl.TrimEnd('/')}/library/sections/?type=18"; // Type 18 is trailers
                var cinemaResponse = await client.GetAsync(cinemaUrl);

                if (cinemaResponse.IsSuccessStatusCode)
                {
                    var content = await cinemaResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] Cinema trailers endpoint response: {content.Substring(0, Math.Min(200, content.Length))}");

                    // Try to set preroll on the first available section
                    var sectionMatch = System.Text.RegularExpressions.Regex.Match(content, @"key=""([^""]+)""");
                    if (sectionMatch.Success)
                    {
                        var sectionKey = sectionMatch.Groups[1].Value;
                        var setPrerollUrl = $"{plexUrl.TrimEnd('/')}/library/sections/{sectionKey}/preroll?prerollID={Uri.EscapeDataString(prerollId)}";

                        Console.WriteLine($"[DEBUG] Trying section preroll URL: {setPrerollUrl}");
                        var prerollResponse = await client.PutAsync(setPrerollUrl, null);

                        if (prerollResponse.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"[DEBUG] Successfully set preroll via section endpoint");
                            return true;
                        }
                    }
                }

                Console.WriteLine($"[DEBUG] All preference setting methods failed");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error setting preroll preference: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryUploadToPlexPrerollDirectory(HttpClient client, string plexUrl, string categoryName, List<string> videoFiles)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Trying to upload files to Plex preroll directory");

                // Try to upload files to Plex's library
                // This is a fallback method that may work for some Plex configurations

                foreach (var videoFile in videoFiles)
                {
                    if (!System.IO.File.Exists(videoFile))
                    {
                        Console.WriteLine($"[DEBUG] Video file does not exist: {videoFile}");
                        continue;
                    }

                    Console.WriteLine($"[DEBUG] Uploading file: {Path.GetFileName(videoFile)}");

                    using var fileStream = System.IO.File.OpenRead(videoFile);
                    using var content = new MultipartFormDataContent();
                    content.Add(new StreamContent(fileStream), "file", Path.GetFileName(videoFile));

                    // Try different upload endpoints
                    var uploadEndpoints = new[]
                    {
                        $"{plexUrl.TrimEnd('/')}/library/upload",
                        $"{plexUrl.TrimEnd('/')}/library/upload?category={Uri.EscapeDataString(categoryName)}",
                        $"{plexUrl.TrimEnd('/')}/library/upload?sectionId=prerolls"
                    };

                    foreach (var endpoint in uploadEndpoints)
                    {
                        try
                        {
                            Console.WriteLine($"[DEBUG] Trying upload to: {endpoint}");
                            var response = await client.PostAsync(endpoint, content);

                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"[DEBUG] Successfully uploaded {Path.GetFileName(videoFile)}");
                                return true;
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] Upload failed: HTTP {(int)response.StatusCode}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DEBUG] Upload exception: {ex.Message}");
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error uploading to Plex: {ex.Message}");
                return false;
            }
        }

        private async Task<(bool Success, string Message)> TryUploadPrerollsToPlex(HttpClient client, string plexUrl, string categoryName, List<string> videoFiles)
        {
            try
            {
                // Try to upload files to Plex's media directory
                // This is a fallback method that may not work on all Plex installations

                var uploadUrl = $"{plexUrl.TrimEnd('/')}/library/upload";

                foreach (var videoFile in videoFiles)
                {
                    if (!System.IO.File.Exists(videoFile))
                        continue;

                    using var fileStream = System.IO.File.OpenRead(videoFile);
                    using var content = new MultipartFormDataContent();
                    content.Add(new StreamContent(fileStream), "file", Path.GetFileName(videoFile));

                    // Try to upload to a preroll-specific section
                    // This may require creating a specific library section for prerolls
                    var specificUploadUrl = $"{uploadUrl}?sectionId=prerolls&category={Uri.EscapeDataString(categoryName)}";

                    var response = await client.PostAsync(specificUploadUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        // If specific upload fails, try general upload
                        response = await client.PostAsync(uploadUrl, content);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return (false, $"Failed to upload preroll file {Path.GetFileName(videoFile)}: HTTP {(int)response.StatusCode}");
                    }
                }

                return (true, $"Successfully uploaded {videoFiles.Count} preroll files to Plex server");
            }
            catch (Exception ex)
            {
                return (false, $"Error uploading prerolls: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete a preroll
        /// </summary>
        [HttpDelete("prerolls/{prerollId}")]
        public IActionResult DeletePreroll(string prerollId)
        {
            try
            {
                // Input validation and sanitization
                if (string.IsNullOrEmpty(prerollId))
                {
                    return Ok(new { success = false, message = "Preroll ID is required" });
                }

                // Sanitize the preroll ID to prevent path traversal attacks
                var sanitizedId = SanitizeFileName(prerollId);
                if (sanitizedId != prerollId)
                {
                    Console.WriteLine($"[WARNING] Sanitized preroll ID from '{prerollId}' to '{sanitizedId}'");
                }

                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");

                // Find the file in all category directories
                string? filePath = null;
                string? categoryName = null;

                foreach (var categoryDir in Directory.GetDirectories(basePath))
                {
                    var category = Path.GetFileName(categoryDir);
                    var potentialPath = Path.Combine(categoryDir, sanitizedId);

                    if (System.IO.File.Exists(potentialPath))
                    {
                        // Security check: ensure the file is within the expected directory
                        var fullPath = Path.GetFullPath(potentialPath);
                        var fullBasePath = Path.GetFullPath(basePath);
                        
                        if (fullPath.StartsWith(fullBasePath))
                        {
                            filePath = potentialPath;
                            categoryName = category;
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"[SECURITY] Attempted path traversal blocked: {potentialPath}");
                            return Ok(new { success = false, message = "Invalid file path" });
                        }
                    }
                }

                if (filePath is null)
                {
                    return Ok(new { success = false, message = "File not found" });
                }

                // Delete the file
                System.IO.File.Delete(filePath!);
                Console.WriteLine($"[DEBUG] Deleted preroll: {sanitizedId} from category {categoryName}");

                return Ok(new { success = true, message = $"File '{sanitizedId}' deleted from category '{categoryName!}'" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error deleting preroll {prerollId}: {ex.Message}");
                return Ok(new { success = false, message = "Failed to delete file" });
            }
        }

        /// <summary>
        /// Get schedules
        /// </summary>
        [HttpGet("schedules")]
        public async Task<IActionResult> GetSchedules()
        {
            try
            {
                var schedules = await _schedulingService.GetSchedulesAsync();
                return Ok(schedules);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error getting schedules: {ex.Message}");
                return Ok(new List<object>());
            }
        }

        /// <summary>
        /// Create a schedule
        /// </summary>
        [HttpPost("schedules")]
        public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Description) || string.IsNullOrEmpty(request.CategoryName))
                {
                    return Ok(new { Success = false, Message = "Description and category name are required" });
                }

                var result = await _schedulingService.CreateScheduleAsync(request);
                
                return Ok(new {
                    Success = result.Success,
                    Message = result.Message,
                    Schedule = result.Schedule
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error creating schedule: {ex.Message}");
                return Ok(new { Success = false, Message = "Failed to create schedule" });
            }
        }

        /// <summary>
        /// Delete a schedule
        /// </summary>
        [HttpDelete("schedules/{scheduleId}")]
        public async Task<IActionResult> DeleteSchedule(string scheduleId)
        {
            try
            {
                var success = await _schedulingService.DeleteScheduleAsync(scheduleId);
                
                return Ok(new {
                    Success = success,
                    Message = success ? "Schedule deleted successfully" : "Schedule not found"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error deleting schedule: {ex.Message}");
                return Ok(new { Success = false, Message = "Failed to delete schedule" });
            }
        }

        /// <summary>
        /// Upload files
        /// </summary>
        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)] // 100MB limit
        public async Task<IActionResult> UploadFiles()
        {
            try
            {
                // Ensure we can read the form data
                if (!Request.HasFormContentType)
                {
                    return Ok(new {
                        success = false,
                        message = "Invalid request format. Expected multipart/form-data.",
                        totalFiles = 0,
                        successfulUploads = 0
                    });
                }

                var form = await Request.ReadFormAsync();
                var files = form.Files;
                var category = form["category"].ToString();
                var totalFiles = files.Count;
                var successfulUploads = 0;

                // Input validation
                if (string.IsNullOrEmpty(category))
                {
                    return Ok(new {
                        success = false,
                        message = "Category is required",
                        totalFiles = totalFiles,
                        successfulUploads = successfulUploads
                    });
                }

                // Sanitize category name
                category = SanitizeFileName(category);

                if (totalFiles == 0)
                {
                    return Ok(new {
                        success = false,
                        message = "No files provided",
                        totalFiles = totalFiles,
                        successfulUploads = successfulUploads
                    });
                }

                // Create category directory if it doesn't exist
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory!, "Prerolls");
                var categoryPath = Path.Combine(basePath, category);

                try
                {
                    Directory.CreateDirectory(categoryPath);
                }
                catch (Exception dirEx)
                {
                    Console.WriteLine($"[ERROR] Failed to create directory {categoryPath}: {dirEx.Message}");
                    return Ok(new {
                        success = false,
                        message = $"Failed to create directory: {dirEx.Message}",
                        totalFiles = totalFiles,
                        successfulUploads = successfulUploads
                    });
                }

                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        try
                        {
                            // Validate file type
                            if (!IsVideoFile(file.FileName))
                            {
                                Console.WriteLine($"[WARNING] Skipping non-video file: {file.FileName}");
                                continue;
                            }

                            // Validate file size (100MB limit)
                            if (file.Length > 104857600)
                            {
                                Console.WriteLine($"[WARNING] Skipping oversized file: {file.FileName} ({file.Length} bytes)");
                                continue;
                            }

                            var originalFileName = SanitizeFileName(Path.GetFileName(file.FileName));
                            var filePath = Path.Combine(categoryPath, originalFileName);

                            // Handle file name conflicts by adding a number suffix
                            var counter = 1;
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                            var extension = Path.GetExtension(originalFileName);

                            while (System.IO.File.Exists(filePath))
                            {
                                var newFileName = $"{fileNameWithoutExt}_{counter}{extension}";
                                filePath = Path.Combine(categoryPath, newFileName);
                                counter++;
                            }

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            Console.WriteLine($"[DEBUG] Successfully uploaded: {Path.GetFileName(filePath)}");
                            successfulUploads++;
                        }
                        catch (Exception fileEx)
                        {
                            Console.WriteLine($"[ERROR] Failed to upload file {file.FileName}: {fileEx.Message}");
                            continue;
                        }
                    }
                }

                return Ok(new {
                    success = successfulUploads > 0,
                    message = successfulUploads > 0
                        ? $"Successfully uploaded {successfulUploads} of {totalFiles} files to category '{category}'"
                        : "No files were uploaded",
                    totalFiles = totalFiles,
                    successfulUploads = successfulUploads
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Upload failed: {ex.Message}");
                return Ok(new {
                    success = false,
                    message = $"Upload failed: {ex.Message}",
                    totalFiles = 0,
                    successfulUploads = 0
                });
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters from file/folder names
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Limit length and trim whitespace
            return sanitized.Trim().Substring(0, Math.Min(sanitized.Length, 100));
        }

        /// <summary>
        /// Create backup
        /// </summary>
        [HttpPost("backup")]
        public async Task<IActionResult> CreateBackup()
        {
            try
            {
                var result = await _backupService.CreateBackupAsync();
                
                return Ok(new {
                    Success = result.Success,
                    Message = result.Message,
                    FilePath = result.FilePath,
                    Error = result.Success ? null : result.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error creating backup: {ex.Message}");
                return Ok(new {
                    Success = false,
                    Error = $"Backup failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Restore backup
        /// </summary>
        [HttpPost("backup/restore")]
        public async Task<IActionResult> RestoreBackup([FromBody] RestoreBackupRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.BackupPath))
                {
                    return Ok(new {
                        Success = false,
                        Error = "Backup path is required"
                    });
                }

                var result = await _backupService.RestoreBackupAsync(request.BackupPath);
                
                return Ok(new {
                    Success = result.Success,
                    Message = result.Message,
                    Error = result.Success ? null : result.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error restoring backup: {ex.Message}");
                return Ok(new {
                    Success = false,
                    Error = $"Restore failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get backups
        /// </summary>
        [HttpGet("backups")]
        public async Task<IActionResult> GetBackups()
        {
            try
            {
                var backups = await _backupService.GetBackupsAsync();
                return Ok(backups);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error getting backups: {ex.Message}");
                return Ok(new List<object>());
            }
        }





        /// <summary>
        /// Update Plex token
        /// </summary>
        [HttpPost("plex/auth")]
        public async Task<IActionResult> UpdatePlexToken([FromBody] UpdateTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return Ok(new { success = false, message = "Token is required" });
                }

                // Save token to configuration
                var success = await _configService.UpdatePlexAuthenticationAsync("token", request.Token);

                if (success)
                {
                    return Ok(new {
                        success = true,
                        message = "Plex token updated successfully",
                        authMethod = "token"
                    });
                }
                else
                {
                    return Ok(new { success = false, message = "Failed to save token" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error updating Plex token: {ex.Message}");
                return Ok(new { success = false, message = "Failed to update token" });
            }
        }

        /// <summary>
        /// Update Plex server URL
        /// </summary>
        [HttpPost("plex/server")]
        public async Task<IActionResult> UpdatePlexServer([FromBody] UpdateServerRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Url))
                {
                    return Ok(new { success = false, message = "Server URL is required" });
                }

                // Save server URL to configuration
                var success = await _configService.UpdatePlexServerUrlAsync(request.Url);

                if (success)
                {
                    return Ok(new {
                        success = true,
                        message = "Plex server URL updated successfully",
                        serverUrl = request.Url
                    });
                }
                else
                {
                    return Ok(new { success = false, message = "Failed to save server URL" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error updating Plex server URL: {ex.Message}");
                return Ok(new { success = false, message = "Failed to update server URL" });
            }
        }

        public class UpdateTokenRequest
        {
            public string Token { get; set; } = "";
        }

        public class UpdateServerRequest
        {
            public string Url { get; set; } = "";
        }

        public class TestPrerollRequest
        {
            public string TestFile { get; set; } = "";
        }

        public class TestMethodInfo
        {
            public string Name { get; set; } = "";
            public string PreferenceKey { get; set; } = "";
        }


        /// <summary>
        /// Test Plex connection
        /// </summary>
        [HttpPost("plex/test")]
        public IActionResult TestPlexConnection()
        {
            try
            {
                var plexUrl = _configuration["Plex:Url"];
                var plexToken = _configuration["Plex:Token"];
                var plexUsername = _configuration["Plex:Username"];
                var plexPassword = _configuration["Plex:Password"];
                var plexApiKey = _configuration["Plex:ApiKey"];

                // Basic connectivity test
                if (string.IsNullOrEmpty(plexUrl))
                {
                    return Ok(new { success = false, message = "Plex server URL is not configured" });
                }

                var hasCredentials = !string.IsNullOrEmpty(plexToken) ||
                                    (!string.IsNullOrEmpty(plexUsername) && !string.IsNullOrEmpty(plexPassword)) ||
                                    !string.IsNullOrEmpty(plexApiKey);

                if (!hasCredentials)
                {
                    return Ok(new { success = false, message = "No Plex authentication credentials configured" });
                }

                // TODO: Implement actual Plex API connectivity test
                return Ok(new {
                    success = true,
                    message = "Plex connection test completed",
                    serverUrl = plexUrl,
                    authMethod = GetAuthMethod(plexToken!, plexUsername!, plexPassword!, plexApiKey!)
                });
            }
            catch
            {
                return Ok(new { success = false, message = "Connection test failed" });
            }
        }

        /// <summary>
        /// Simple debug test endpoint
        /// </summary>
        [HttpGet("plex/debug/test")]
        public IActionResult DebugTest()
        {
            return Ok(new {
                success = true,
                message = "Debug endpoint is working",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Test oauth.html accessibility
        /// </summary>
        [HttpGet("plex/debug/oauth-test")]
        public IActionResult TestOauthHtml()
        {
            try
            {
                var request = HttpContext.Request;
                var scheme = request.Scheme;
                var host = request.Host.Host;
                var port = request.Host.Port;

                var oauthUrl = port.HasValue && port.Value != 80 && port.Value != 443
                    ? $"{scheme}://{host}:{port}/oauth.html"
                    : $"{scheme}://{host}/oauth.html";

                var oauthFilePath = Path.Combine(AppContext.BaseDirectory, "oauth.html");
                var fileExists = System.IO.File.Exists(oauthFilePath);

                return Ok(new {
                    success = true,
                    message = "OAuth.html test info",
                    oauthUrl = oauthUrl,
                    oauthFileExists = fileExists,
                    oauthFilePath = oauthFilePath,
                    serverHost = host,
                    serverPort = port,
                    serverScheme = scheme,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Ok(new {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Debug configuration test endpoint
        /// </summary>
        [HttpGet("plex/debug/config")]
        public IActionResult DebugConfig()
        {
            try
            {
                var token = _configService.GetPlexToken();
                var hasAuth = _configService.HasPlexAuthentication();
                var authMethod = _configService.GetAuthMethod();

                return Ok(new {
                    success = true,
                    message = "Configuration debug info",
                    tokenPresent = !string.IsNullOrEmpty(token),
                    tokenLength = token?.Length ?? 0,
                    tokenPreview = token?.Substring(0, Math.Min(20, token?.Length ?? 0)),
                    hasAuthentication = hasAuth,
                    authMethod = authMethod,
                    serverUrl = _configService.GetPlexServerUrl(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Ok(new {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }




        /// <summary>
        /// Check for updates
        /// </summary>
        [HttpGet("updates/check")]
        public IActionResult CheckForUpdates()
        {
            return Ok(new {
                IsUpdateAvailable = false,
                CurrentVersion = "2.2.0",
                LatestVersion = "2.2.0"
            });
        }

        /// <summary>
        /// Test Plex preroll integration (debug endpoint)
        /// </summary>
        [HttpPost("test/plex-integration")]
        public async Task<IActionResult> TestPlexIntegration()
        {
            try
            {
                Console.WriteLine("[DEBUG] Testing Plex integration...");

                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                var testCategoryPath = Path.Combine(basePath, "General");

                if (!Directory.Exists(testCategoryPath))
                {
                    return Ok(new { success = false, message = "Test category 'General' does not exist" });
                }

                var videoFiles = Directory.GetFiles(testCategoryPath, "*.*")
                    .Where(f => IsVideoFile(f))
                    .ToList();

                if (videoFiles.Count == 0)
                {
                    return Ok(new { success = false, message = "No video files in test category" });
                }

                Console.WriteLine($"[DEBUG] Found {videoFiles.Count} video files for testing");

                var result = await UpdatePlexPrerolls("General", videoFiles);

                return Ok(new {
                    success = true,
                    message = "Plex integration test completed",
                    result = result
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Test failed: {ex.Message}");
                return Ok(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Debug test individual preroll setting methods
        /// </summary>
        [HttpPost("debug/test-preroll-methods")]
        public async Task<IActionResult> DebugTestPrerollMethods([FromBody] TestPrerollRequest request)
        {
            try
            {
                Console.WriteLine("[DEBUG] Testing individual preroll setting methods...");

                var plexUrl = _configService.GetPlexServerUrl();
                var token = _configService.GetPlexToken();

                if (string.IsNullOrEmpty(plexUrl) || string.IsNullOrEmpty(token))
                {
                    return Ok(new { success = false, message = "Plex URL or token not configured" });
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

                client.DefaultRequestHeaders.Add("X-Plex-Token", token);
                client.DefaultRequestHeaders.Add("Accept", "application/xml");

                var testResults = new List<object>();
                var testFile = request?.TestFile ?? "C:\\test\\video.mp4"; // Use a test file path

                // Test different methods
                var testMethods = new[]
                {
                    new TestMethodInfo { Name = "CinemaTrailersPrerollID", PreferenceKey = "CinemaTrailersPrerollID" },
                    new TestMethodInfo { Name = "PrerollID", PreferenceKey = "PrerollID" },
                    new TestMethodInfo { Name = "cinemaTrailersPrerollID", PreferenceKey = "cinemaTrailersPrerollID" },
                    new TestMethodInfo { Name = "prerollID", PreferenceKey = "prerollID" },
                    new TestMethodInfo { Name = "preroll", PreferenceKey = "preroll" }
                };

                foreach (var method in testMethods)
                {
                    try
                    {
                        var result = await TestPreferenceMethod(client, plexUrl, method.PreferenceKey, testFile);
                        testResults.Add(new
                        {
                            method = method.Name,
                            success = result.Success,
                            statusCode = result.StatusCode,
                            response = result.Response?.Substring(0, Math.Min(200, result.Response?.Length ?? 0))
                        });
                    }
                    catch (Exception ex)
                    {
                        testResults.Add(new
                        {
                            method = method.Name,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                return Ok(new {
                    success = true,
                    message = "Preroll method testing completed",
                    testFile = testFile,
                    results = testResults
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Debug test failed: {ex.Message}");
                return Ok(new { success = false, message = ex.Message });
            }
        }

        private async Task<(bool Success, int StatusCode, string Response)> TestPreferenceMethod(HttpClient client, string plexUrl, string preferenceKey, string value)
        {
            try
            {
                var url = $"{plexUrl.TrimEnd('/')}/:/prefs?{preferenceKey}={Uri.EscapeDataString(value)}";
                Console.WriteLine($"[DEBUG] Testing {preferenceKey} with URL: {url}");

                var response = await client.PutAsync(url, null);
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"[DEBUG] {preferenceKey} response: {(int)response.StatusCode} {response.StatusCode}");

                return (response.IsSuccessStatusCode, (int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] {preferenceKey} exception: {ex.Message}");
                return (false, 0, ex.Message);
            }
        }

    }
}