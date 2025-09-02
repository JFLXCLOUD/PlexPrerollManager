using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlexPrerollManager.Services
{
    /// <summary>
    /// Service to initialize the database and ensure statistics tables exist
    /// </summary>
    public class DatabaseInitializationService : IHostedService
    {
        private readonly UsageTrackingService _usageService;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            UsageTrackingService usageService,
            ILogger<DatabaseInitializationService> logger)
        {
            _usageService = usageService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Initializing usage statistics database...");

                await _usageService.InitializeDatabaseAsync();

                _logger.LogInformation("Usage statistics database initialized successfully");

                // Optionally update daily stats on startup
                await _usageService.UpdateDailyStatsAsync();
                _logger.LogInformation("Daily statistics updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing usage statistics database");
                // Don't throw - we don't want database initialization to crash the app
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Database initialization service stopping");
            return Task.CompletedTask;
        }
    }
}