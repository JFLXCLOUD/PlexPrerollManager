using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.IO;

namespace PlexPrerollManager.Services
{
    public class ConfigurationService
    {
        private readonly IConfigurationRoot _configuration;
        private readonly string _configPath;

        public ConfigurationService(IConfiguration configuration)
        {
            _configuration = (IConfigurationRoot)configuration;

            // Try multiple possible locations for appsettings.json
            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                "appsettings.json"
            };

            foreach (var path in possiblePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    _configPath = path;
                    Console.WriteLine($"[DEBUG] ConfigurationService found config file at: {_configPath}");
                    break;
                }
            }

            if (string.IsNullOrEmpty(_configPath))
            {
                _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                Console.WriteLine($"[DEBUG] ConfigurationService using default config path: {_configPath}");
            }

            Console.WriteLine($"[DEBUG] Configuration file exists: {System.IO.File.Exists(_configPath)}");
        }

        public async Task<bool> UpdatePlexAuthenticationAsync(string authMethod, string? token = null)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ===== UPDATE PLEX AUTHENTICATION STARTED =====");
                Console.WriteLine($"[DEBUG] Method: {authMethod}, HasToken: {!string.IsNullOrEmpty(token)}");
                Console.WriteLine($"[DEBUG] Token value: {token?.Substring(0, Math.Min(20, token.Length))}...");
                Console.WriteLine($"[DEBUG] Config path: {_configPath}");
                Console.WriteLine($"[DEBUG] File exists: {System.IO.File.Exists(_configPath)}");

                // Only support token authentication
                if (authMethod.ToLower() != "token")
                {
                    Console.WriteLine($"[ERROR] Only token authentication is supported. Method: {authMethod}");
                    return false;
                }

                // Read current configuration
                var configJson = await System.IO.File.ReadAllTextAsync(_configPath);
                var config = JsonConvert.DeserializeObject<dynamic>(configJson);

                // Update Plex section
                if (config?.Plex == null)
                {
                    config.Plex = new { };
                }

                // Set token
                config.Plex.Token = token ?? "";
                Console.WriteLine($"[DEBUG] Setting Plex.Token to: {token?.Substring(0, Math.Min(20, token.Length))}...");

                // Clear other auth fields (keep them empty)
                config.Plex.Username = "";
                config.Plex.Password = "";
                config.Plex.ApiKey = "";

                // Write back to file
                var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);

                // Ensure the directory exists
                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"[DEBUG] Created directory: {directory}");
                }

                Console.WriteLine($"[DEBUG] About to write updated JSON to file...");
                Console.WriteLine($"[DEBUG] Updated JSON length: {updatedJson.Length}");
                Console.WriteLine($"[DEBUG] Updated JSON preview: {updatedJson.Substring(0, Math.Min(200, updatedJson.Length))}");

                await System.IO.File.WriteAllTextAsync(_configPath, updatedJson);

                Console.WriteLine($"[DEBUG] Configuration file written successfully to: {_configPath}");

                // Verify the file was written correctly
                var verifyJson = await System.IO.File.ReadAllTextAsync(_configPath);
                Console.WriteLine($"[DEBUG] Verification - file length: {verifyJson.Length}");
                var verifyConfig = JsonConvert.DeserializeObject<dynamic>(verifyJson);
                var verifyToken = verifyConfig?.Plex?.Token?.ToString() ?? "";
                Console.WriteLine($"[DEBUG] Verification - token in file: {verifyToken?.Substring(0, Math.Min(20, verifyToken.Length))}...");

                // Reload configuration to reflect changes in memory
                _configuration.Reload();

                Console.WriteLine($"[DEBUG] Configuration reloaded");

                // Verify the token was saved
                var savedToken = GetPlexToken();
                Console.WriteLine($"[DEBUG] Token after reload: {savedToken?.Substring(0, Math.Min(20, savedToken.Length))}...");
                Console.WriteLine($"[DEBUG] Token saved successfully: {!string.IsNullOrEmpty(savedToken)}");

                Console.WriteLine($"[DEBUG] ===== UPDATE PLEX AUTHENTICATION COMPLETED SUCCESSFULLY =====");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ===== UPDATE PLEX AUTHENTICATION FAILED =====");
                Console.WriteLine($"[ERROR] Failed to update configuration: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                Console.WriteLine($"[ERROR] Inner exception: {ex.InnerException?.Message}");
                return false;
            }
        }

        public async Task<bool> UpdatePlexServerUrlAsync(string url)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Updating Plex server URL: {url}");

                // Read current configuration
                Console.WriteLine($"[DEBUG] Reading current configuration from: {_configPath}");
                var configJson = await System.IO.File.ReadAllTextAsync(_configPath);
                Console.WriteLine($"[DEBUG] Config JSON length: {configJson.Length}");
                var config = JsonConvert.DeserializeObject<dynamic>(configJson);
                Console.WriteLine($"[DEBUG] Config deserialized successfully");

                // Update Plex URL
                if (config?.Plex == null)
                {
                    config.Plex = new { };
                }

                config.Plex.Url = url;

                // Write back to file
                var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                await System.IO.File.WriteAllTextAsync(_configPath, updatedJson);

                // Reload configuration to reflect changes in memory
                _configuration.Reload();

                Console.WriteLine($"[DEBUG] Plex server URL updated and reloaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update Plex server URL: {ex.Message}");
                return false;
            }
        }

        public string GetPlexServerUrl()
        {
            return _configuration["Plex:Url"] ?? "http://localhost:32400";
        }

        public string GetPlexToken()
        {
            // Always read from file directly to ensure we get the latest value
            if (System.IO.File.Exists(_configPath))
            {
                try
                {
                    var configJson = System.IO.File.ReadAllText(_configPath);
                    var config = JsonConvert.DeserializeObject<dynamic>(configJson);
                    var fileToken = config?.Plex?.Token?.ToString() ?? "";
                    Console.WriteLine($"[DEBUG] Token read from file: {fileToken?.Substring(0, Math.Min(20, fileToken.Length))}...");

                    // If we found a token in the file, return it
                    if (!string.IsNullOrEmpty(fileToken))
                    {
                        return fileToken;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Error reading token from file: {ex.Message}");
                }
            }

            // Fallback to configuration if file doesn't exist or token is empty
            var configToken = _configuration["Plex:Token"] ?? "";
            Console.WriteLine($"[DEBUG] Using config token: {configToken?.Substring(0, Math.Min(20, configToken.Length))}...");
            return configToken;
        }

        public string GetPlexApiKey()
        {
            return _configuration["Plex:ApiKey"] ?? "";
        }

        public string GetPlexUsername()
        {
            return _configuration["Plex:Username"] ?? "";
        }

        public string GetPlexPassword()
        {
            return _configuration["Plex:Password"] ?? "";
        }

        public bool HasPlexAuthentication()
        {
            var token = GetPlexToken();
            var hasAuth = !string.IsNullOrEmpty(token);

            Console.WriteLine($"[DEBUG] HasPlexAuthentication: {hasAuth} (Token: {!string.IsNullOrEmpty(token)})");

            return hasAuth;
        }

        public string GetAuthMethod()
        {
            if (!string.IsNullOrEmpty(GetPlexToken())) return "Token";
            return "None";
        }
    }
}