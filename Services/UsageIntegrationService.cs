using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PlexPrerollManager.Services;

namespace PlexPrerollManager.Services
{
    /// <summary>
    /// Service for integrating usage tracking into existing preroll activation code
    /// </summary>
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

        /// <summary>
        /// Track when a preroll starts playing
        /// Call this method when a preroll begins playback
        /// </summary>
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
                // Don't throw - we don't want tracking errors to break preroll functionality
            }
        }

        /// <summary>
        /// Track when a preroll completes playing
        /// Call this method when a preroll finishes playback
        /// </summary>
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
                // Don't throw - we don't want tracking errors to break preroll functionality
            }
        }

        /// <summary>
        /// Helper method to get Plex client ID from HTTP context
        /// </summary>
        public static string? GetPlexClientId(HttpContext httpContext)
        {
            // Try to get from headers
            if (httpContext.Request.Headers.TryGetValue("X-Plex-Client-Identifier", out var clientId))
            {
                return clientId.ToString();
            }

            // Try to get from query parameters
            if (httpContext.Request.Query.TryGetValue("clientId", out var queryClientId))
            {
                return queryClientId.ToString();
            }

            return null;
        }
    }
}