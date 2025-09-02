using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlexPrerollManager.Services;

namespace PlexPrerollManager.Services
{
    public class UsageIntegrationService
    {
        private readonly UsageTrackingService _usageService;
        private readonly ILogger<UsageIntegrationService> _logger;

        public UsageIntegrationService(
            UsageTrackingService usageService,
            ILogger<UsageIntegrationService> logger)
        {
            _usageService = usageService;
            _logger = logger;
        }

        public async Task TrackPrerollPlayAsync(
            string prerollId,
            string categoryName,
            HttpContext? httpContext = null,
            string? plexClientId = null)
        {
            try
            {
                var clientIp = httpContext?.Connection.RemoteIpAddress?.ToString();
                var userAgent = httpContext?.Request.Headers["User-Agent"].ToString();

                await _usageService.RecordPlayStartAsync(
                    prerollId,
                    categoryName,
                    clientIp ?? "",
                    userAgent ?? "",
                    plexClientId ?? "");

                _logger.LogInformation("Tracked preroll play: {PrerollId} in category {CategoryName}",
                    prerollId, categoryName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking preroll play: {PrerollId}", prerollId);
            }
        }

        public async Task TrackPrerollCompleteAsync(string prerollId, int durationSeconds)
        {
            try
            {
                await _usageService.RecordPlayCompleteAsync(prerollId, durationSeconds);
                _logger.LogInformation("Tracked preroll completion: {PrerollId} ({Duration}s)",
                    prerollId, durationSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking preroll completion: {PrerollId}", prerollId);
            }
        }

        public static string? GetPlexClientId(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue("X-Plex-Client-Identifier", out var clientId))
            {
                return clientId.ToString();
            }

            if (httpContext.Request.Query.TryGetValue("clientId", out var queryClientId))
            {
                return queryClientId.ToString();
            }

            return null;
        }
    }
}