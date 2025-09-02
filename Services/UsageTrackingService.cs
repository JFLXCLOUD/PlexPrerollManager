using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using PlexPrerollManager.Models;

namespace PlexPrerollManager.Services
{
    public class UsageTrackingService
    {
        private readonly string _connectionString;

        public UsageTrackingService(IConfiguration configuration)
        {
            // Get the connection string from appsettings.json
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=plexprerollmanager.db";
        }

        public async Task RecordPlayStartAsync(string prerollId, string categoryName,
            string clientIp, string userAgent, string plexClientId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(@"
                INSERT INTO PrerollUsage (PrerollId, CategoryName, PlayStartTime, ClientIp, UserAgent, PlexClientId)
                VALUES (@PrerollId, @CategoryName, @PlayStartTime, @ClientIp, @UserAgent, @PlexClientId)",
                new {
                    PrerollId = prerollId,
                    CategoryName = categoryName,
                    PlayStartTime = DateTime.UtcNow,
                    ClientIp = clientIp,
                    UserAgent = userAgent,
                    PlexClientId = plexClientId
                });
        }

        public async Task RecordPlayCompleteAsync(string prerollId, int playDuration)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(@"
                UPDATE PrerollUsage
                SET PlayDuration = @PlayDuration
                WHERE PrerollId = @PrerollId AND PlayDuration IS NULL
                ORDER BY PlayStartTime DESC LIMIT 1",
                new { PrerollId = prerollId, PlayDuration = playDuration });
        }

        public async Task<UsageStats> GetUsageStatsAsync(string period = "daily", int days = 30)
        {
            using var connection = new SqliteConnection(_connectionString);

            var startDate = DateTime.UtcNow.AddDays(-days);

            var stats = await connection.QueryAsync<dynamic>(@"
                SELECT
                    DATE(PlayStartTime) as Date,
                    CategoryName,
                    COUNT(*) as Plays,
                    SUM(PlayDuration) as TotalWatchTime,
                    COUNT(DISTINCT PrerollId) as UniquePrerolls
                FROM PrerollUsage
                WHERE PlayStartTime >= @StartDate
                GROUP BY DATE(PlayStartTime), CategoryName
                ORDER BY Date DESC, Plays DESC",
                new { StartDate = startDate });

            return new UsageStats { Data = stats };
        }

        public async Task<IEnumerable<TopPreroll>> GetTopPrerollsAsync(int limit = 10, string period = "daily", int days = 30)
        {
            using var connection = new SqliteConnection(_connectionString);

            var startDate = DateTime.UtcNow.AddDays(-days);

            var topPrerolls = await connection.QueryAsync<TopPreroll>(@"
                SELECT
                    PrerollId as Name,
                    CategoryName,
                    COUNT(*) as Plays,
                    COALESCE(SUM(PlayDuration), 0) as TotalWatchTime
                FROM PrerollUsage
                WHERE PlayStartTime >= @StartDate
                GROUP BY PrerollId, CategoryName
                ORDER BY Plays DESC, TotalWatchTime DESC
                LIMIT @Limit",
                new { StartDate = startDate, Limit = limit });

            return topPrerolls;
        }

        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);

            // Create PrerollUsage table
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS PrerollUsage (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PrerollId TEXT NOT NULL,
                    CategoryName TEXT NOT NULL,
                    PlayStartTime DATETIME NOT NULL,
                    PlayDuration INTEGER,
                    ClientIp TEXT,
                    UserAgent TEXT,
                    PlexClientId TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                )");

            // Create DailyStats table
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS DailyStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date DATE NOT NULL,
                    CategoryName TEXT NOT NULL,
                    TotalPlays INTEGER DEFAULT 0,
                    TotalWatchTime INTEGER DEFAULT 0,
                    UniquePrerolls INTEGER DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(Date, CategoryName)
                )");

            // Create indexes for performance
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_preroll_usage_preroll_id ON PrerollUsage(PrerollId)");
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_preroll_usage_category ON PrerollUsage(CategoryName)");
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_preroll_usage_start_time ON PrerollUsage(PlayStartTime)");
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_preroll_usage_date ON PrerollUsage(date(PlayStartTime))");
        }

        public async Task UpdateDailyStatsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);

            // Update daily stats for the last 30 days
            var startDate = DateTime.UtcNow.AddDays(-30);

            await connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO DailyStats (Date, CategoryName, TotalPlays, TotalWatchTime, UniquePrerolls, UpdatedDate)
                SELECT
                    DATE(PlayStartTime) as Date,
                    CategoryName,
                    COUNT(*) as TotalPlays,
                    COALESCE(SUM(PlayDuration), 0) as TotalWatchTime,
                    COUNT(DISTINCT PrerollId) as UniquePrerolls,
                    CURRENT_TIMESTAMP as UpdatedDate
                FROM PrerollUsage
                WHERE PlayStartTime >= @StartDate
                GROUP BY DATE(PlayStartTime), CategoryName",
                new { StartDate = startDate });
        }
    }
}