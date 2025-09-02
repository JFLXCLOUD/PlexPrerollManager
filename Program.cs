using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

// Import our custom namespaces
using PlexPrerollManager.Controllers;
using PlexPrerollManager.Services;
using PlexPrerollManager.Models;

namespace PlexPrerollManager
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ApplicationName = "PlexPrerollManager"
            });

            // Run as Windows Service (like Sonarr/Radarr)
            if (OperatingSystem.IsWindows())
            {
                builder.Host.UseWindowsService(options => options.ServiceName = "PlexPrerollManager");
            }

            // Configure URLs to listen on 8089 (accessible from LAN) - using configuration
            builder.Configuration["Kestrel:Endpoints:Http:Url"] = "http://*:8089";
            builder.Configuration["Kestrel:Limits:MaxRequestBodySize"] = "null"; // Disable limit completely
            builder.Configuration["Kestrel:Limits:MaxRequestBufferSize"] = "null"; // Disable limit completely
            builder.Configuration["Kestrel:Limits:RequestHeadersTimeout"] = "00:30:00"; // 30 minutes
            builder.Configuration["Kestrel:Limits:KeepAliveTimeout"] = "00:30:00"; // 30 minutes
            builder.Configuration["Kestrel:Limits:MinRequestBodyDataRate:BytesPerSecond"] = "1024"; // 1KB/s minimum
            builder.Configuration["Kestrel:Limits:MinRequestBodyDataRate:GracePeriod"] = "00:30:00"; // 30 minutes grace period

            // Configure form options for large file uploads
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue; // Maximum possible
                options.ValueLengthLimit = int.MaxValue; // Maximum for form values
                options.MultipartHeadersLengthLimit = int.MaxValue; // Maximum for headers
                options.MultipartBoundaryLengthLimit = int.MaxValue; // Maximum for boundary
                options.BufferBodyLengthLimit = long.MaxValue; // Buffer entire body
                options.MemoryBufferThreshold = 104857600; // 100MB memory buffer before using disk
                options.MultipartBodyLengthLimit = long.MaxValue; // No limit on multipart body
            });

            // Configure request size limits
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = null; // Unlimited
                options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
                options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 1024, gracePeriod: TimeSpan.FromMinutes(30));
            });

            // Logging (console + Windows Event Log)
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            // Add Windows Event Log only on Windows platforms
            if (OperatingSystem.IsWindows())
            {
                builder.Logging.AddEventLog();
            }

            // Services & DI
            builder.Services.AddHttpClient("", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30); // 30 minute timeout for large file uploads
            });

            // Add our custom services
            builder.Services.AddScoped<UsageTrackingService>();
            builder.Services.AddScoped<UsageIntegrationService>();
            builder.Services.AddHostedService<DatabaseInitializationService>();

            // Configure JSON options to handle enums as strings
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Add MVC and Controllers
            builder.Services.AddControllers();

            var app = builder.Build();

            // ---- UI ----
            app.MapGet("/", async ctx =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                // Try web subdirectory first (installer location), then fallback to root
                var htmlPath = Path.Combine(AppContext.BaseDirectory, "web", "dashboard.html");
                if (!File.Exists(htmlPath))
                {
                    htmlPath = Path.Combine(AppContext.BaseDirectory, "dashboard.html");
                }

                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    await ctx.Response.WriteAsync(html);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.WriteAsync("Dashboard not found");
                }
            });

            app.MapGet("/scheduling", async ctx =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                // Try web subdirectory first (installer location), then fallback to root
                var htmlPath = Path.Combine(AppContext.BaseDirectory, "web", "scheduling-dashboard.html");
                if (!File.Exists(htmlPath))
                {
                    htmlPath = Path.Combine(AppContext.BaseDirectory, "scheduling-dashboard.html");
                }

                if (File.Exists(htmlPath))
                {
                    var html = await File.ReadAllTextAsync(htmlPath);
                    await ctx.Response.WriteAsync(html);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.WriteAsync("Scheduling dashboard not found");
                }
            });

            // Favicon to prevent 404 errors
            app.MapGet("/favicon.ico", () => Task.CompletedTask);

            // Thumbnail endpoint
            app.MapGet("/api/thumbnails/{filename}", async (HttpContext ctx, string filename) =>
            {
                try
                {
                    var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                    var thumbnailPath = Path.Combine(prerollService.GetThumbnailsPath(), filename);

                    if (File.Exists(thumbnailPath))
                    {
                        ctx.Response.ContentType = "image/jpeg";
                        await ctx.Response.SendFileAsync(thumbnailPath);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                        await ctx.Response.WriteAsync("Thumbnail not found");
                    }
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"Error serving thumbnail: {ex.Message}");
                }
            });

            // ---- API ----
            app.MapGet("/api/status", async ctx =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var plexService = ctx.RequestServices.GetRequiredService<IPlexService>();

                var plexStatus = await plexService.GetServerStatusAsync();
                var activeCategory = await prerollService.GetActiveCategoryAsync();
                var totalPrerolls = await prerollService.GetPrerollCountAsync();

                var status = new
                {
                    PlexConnected = plexStatus.Connected,
                    PlexServerName = plexStatus.ServerName,
                    ActiveCategory = activeCategory,
                    TotalPrerolls = totalPrerolls,
                    LastUpdated = DateTime.Now
                };

                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(status));
            });

            app.MapGet("/api/categories", async ctx =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var categories = await prerollService.GetCategoriesAsync();
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(categories));
            });

            app.MapGet("/api/prerolls/{category}", async (HttpContext ctx, string category) =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var prerolls = await prerollService.GetPrerollsByCategoryAsync(category);
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(prerolls));
            });

            app.MapPost("/api/upload", async ctx =>
            {
                try
                {
                    var form = await ctx.Request.ReadFormAsync();
                    var files = form.Files;
                    var category = form["category"].FirstOrDefault() ?? "General";

                    if (files.Count == 0)
                    {
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.WriteAsync("{\"Success\":false,\"Error\":\"No files uploaded\"}");
                        return;
                    }

                    var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                    var results = new List<UploadResult>();
                    var fileIndex = 0;

                    foreach (var file in files)
                    {
                        if (file.Length > 0)
                        {
                            var name = form[$"name_{fileIndex}"].FirstOrDefault() ?? file.FileName ?? "Unknown";
                            var result = await prerollService.UploadPrerollAsync(file, category, name);
                            results.Add(result);
                            fileIndex++;
                        }
                    }

                    var overallSuccess = results.All(r => r.Success);
                    var response = new
                    {
                        Success = overallSuccess,
                        Results = results,
                        TotalFiles = files.Count,
                        SuccessfulUploads = results.Count(r => r.Success),
                        Message = overallSuccess ? "All files uploaded successfully" : $"{results.Count(r => r.Success)} of {files.Count} files uploaded successfully"
                    };

                    ctx.Response.ContentType = "application/json";
                    ctx.Response.StatusCode = overallSuccess ? 200 : 207; // 207 = Multi-Status
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(response));
                }
                catch (Exception ex)
                {
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"Success\":false,\"Error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapPost("/api/categories/{category}/activate", async (HttpContext ctx, string category) =>
            {
                try
                {
                    var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                    var usageService = ctx.RequestServices.GetRequiredService<UsageIntegrationService>();

                    // Get prerolls in the category to track them
                    var prerolls = await prerollService.GetPrerollsByCategoryAsync(category);

                    // Track each preroll play
                    foreach (var preroll in prerolls)
                    {
                        await usageService.TrackPrerollPlayAsync(
                            preroll.Id,
                            category,
                            ctx,
                            UsageIntegrationService.GetPlexClientId(ctx));
                    }

                    // Activate the category
                    await prerollService.ActivateCategoryAsync(category);

                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync("{\"message\":\"Category activated and usage tracked\"}");
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapDelete("/api/prerolls/{id}", async (HttpContext ctx, string id) =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var result = await prerollService.DeletePrerollAsync(id);

                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { success = result }));
            });

            app.MapPost("/api/prerolls/{id}/reorder", async (HttpContext ctx, string id) =>
            {
                using var sr = new StreamReader(ctx.Request.Body);
                var body = await sr.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                var newOrder = Convert.ToInt32(data?["order"]);

                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                await prerollService.ReorderPrerollAsync(id, newOrder);
                await ctx.Response.WriteAsync("{\"message\":\"Order updated\"}");
            });

            // ===== Scheduling API Endpoints =====

            app.MapGet("/api/schedules", async ctx =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var schedules = await prerollService.GetSchedulesAsync();
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(schedules));
            });

            app.MapPost("/api/schedules", async ctx =>
            {
                try
                {
                    using var sr = new StreamReader(ctx.Request.Body);
                    var body = await sr.ReadToEndAsync();

                    // Log the incoming JSON for debugging
                    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Received schedule JSON: {body}", body);

                    // Create custom options with enum converter for this specific deserialization
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());

                    var schedule = JsonSerializer.Deserialize<PrerollSchedule>(body, options);

                    if (schedule == null)
                    {
                        logger.LogWarning("Failed to deserialize schedule JSON");
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Invalid schedule data\"}");
                        return;
                    }

                    logger.LogInformation("Deserialized schedule: Category={category}, Type={type}, Description={desc}",
                        schedule.CategoryName, schedule.Type, schedule.Description);

                    var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                    var createdSchedule = await prerollService.CreateScheduleAsync(schedule);

                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(createdSchedule, options));
                }
                catch (Exception ex)
                {
                    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Error creating schedule");
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapPut("/api/schedules/{id}", async (HttpContext ctx, string id) =>
            {
                try
                {
                    using var sr = new StreamReader(ctx.Request.Body);
                    var body = await sr.ReadToEndAsync();

                    // Log the incoming JSON for debugging
                    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Received schedule update JSON: {body}", body);

                    // Create custom options with enum converter for this specific deserialization
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());

                    var schedule = JsonSerializer.Deserialize<PrerollSchedule>(body, options);

                    if (schedule == null)
                    {
                        logger.LogWarning("Failed to deserialize schedule update JSON");
                        ctx.Response.StatusCode = 400;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Invalid schedule data\"}");
                        return;
                    }

                    schedule.Id = id;
                    logger.LogInformation("Updating schedule: {id}, Category={category}, Type={type}",
                        id, schedule.CategoryName, schedule.Type);

                    var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                    var success = await prerollService.UpdateScheduleAsync(schedule);

                    if (success)
                    {
                        logger.LogInformation("Successfully updated schedule: {id}", id);
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync(JsonSerializer.Serialize(schedule, options));
                    }
                    else
                    {
                        logger.LogWarning("Schedule not found for update: {id}", id);
                        ctx.Response.StatusCode = 404;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"error\":\"Schedule not found\"}");
                    }
                }
                catch (Exception ex)
                {
                    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Error updating schedule {id}", id);
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapDelete("/api/schedules/{id}", async (HttpContext ctx, string id) =>
            {
                try
                {
                    var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                    var success = await prerollService.DeleteScheduleAsync(id);

                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { success }));
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            // ===== Backup/Restore API Endpoints =====

            app.MapPost("/api/backup", async ctx =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var result = await prerollService.CreateBackupAsync();

                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = result.Success ? 200 : 500;
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
            });

            app.MapPost("/api/backup/restore", async ctx =>
            {
                using var sr = new StreamReader(ctx.Request.Body);
                var body = await sr.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                var backupPath = data?["backupPath"];

                if (string.IsNullOrEmpty(backupPath))
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("{\"error\":\"Backup path is required\"}");
                    return;
                }

                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var result = await prerollService.RestoreBackupAsync(backupPath);

                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = result.Success ? 200 : 500;
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
            });

            app.MapGet("/api/backups", async ctx =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var backups = await prerollService.GetAvailableBackupsAsync();

                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(backups));
            });

            // ===== Update Check API Endpoints =====

            app.MapGet("/api/updates/check", async ctx =>
            {
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                var updateInfo = await prerollService.CheckForUpdatesAsync();

                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(updateInfo));
            });

            // ===== Plex Configuration API Endpoints =====

            app.MapGet("/api/plex/config", async ctx =>
            {
                var plexService = ctx.RequestServices.GetRequiredService<IPlexService>();
                var config = await plexService.GetPlexConfigAsync();

                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(config));
            });

            app.MapPost("/api/plex/config", async ctx =>
            {
                try
                {
                    using var sr = new StreamReader(ctx.Request.Body);
                    var body = await sr.ReadToEndAsync();
                    var config = JsonSerializer.Deserialize<PlexConfig>(body);

                    if (config == null)
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.WriteAsync("{\"error\":\"Invalid configuration data\"}");
                        return;
                    }

                    var plexService = ctx.RequestServices.GetRequiredService<IPlexService>();
                    var success = await plexService.UpdatePlexConfigAsync(config);

                    if (success)
                    {
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("{\"message\":\"Plex configuration updated successfully\"}");
                    }
                    else
                    {
                        ctx.Response.StatusCode = 500;
                        await ctx.Response.WriteAsync("{\"error\":\"Failed to update Plex configuration\"}");
                    }
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            // ===== Usage Statistics API Endpoints =====

            app.MapPost("/api/usage/play", async ctx =>
            {
                try
                {
                    using var sr = new StreamReader(ctx.Request.Body);
                    var body = await sr.ReadToEndAsync();
                    var request = JsonSerializer.Deserialize<PlayRequest>(body);

                    if (request == null || string.IsNullOrEmpty(request.PrerollId) || string.IsNullOrEmpty(request.CategoryName))
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.WriteAsync("{\"error\":\"PrerollId and CategoryName are required\"}");
                        return;
                    }

                    var usageService = ctx.RequestServices.GetRequiredService<UsageIntegrationService>();
                    await usageService.TrackPrerollPlayAsync(
                        request.PrerollId,
                        request.CategoryName,
                        ctx,
                        request.PlexClientId);

                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync("{\"message\":\"Play recorded\"}");
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapPost("/api/usage/complete", async ctx =>
            {
                try
                {
                    using var sr = new StreamReader(ctx.Request.Body);
                    var body = await sr.ReadToEndAsync();
                    var request = JsonSerializer.Deserialize<CompletionRequest>(body);

                    if (request == null || string.IsNullOrEmpty(request.PrerollId))
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.WriteAsync("{\"error\":\"PrerollId is required\"}");
                        return;
                    }

                    var usageService = ctx.RequestServices.GetRequiredService<UsageIntegrationService>();
                    await usageService.TrackPrerollCompleteAsync(request.PrerollId, request.PlayDuration);

                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync("{\"message\":\"Completion recorded\"}");
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapGet("/api/usage/stats", async ctx =>
            {
                try
                {
                    var period = ctx.Request.Query["period"].FirstOrDefault() ?? "daily";
                    var days = int.TryParse(ctx.Request.Query["days"].FirstOrDefault(), out var d) ? d : 30;

                    var usageService = ctx.RequestServices.GetRequiredService<UsageTrackingService>();
                    var stats = await usageService.GetUsageStatsAsync(period, days);
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(stats));
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.MapGet("/api/usage/top", async ctx =>
            {
                try
                {
                    var limit = int.TryParse(ctx.Request.Query["limit"].FirstOrDefault(), out var l) ? l : 10;
                    var period = ctx.Request.Query["period"].FirstOrDefault() ?? "daily";
                    var days = int.TryParse(ctx.Request.Query["days"].FirstOrDefault(), out var d) ? d : 30;

                    var usageService = ctx.RequestServices.GetRequiredService<UsageTrackingService>();
                    var topPrerolls = await usageService.GetTopPrerollsAsync(limit, period, days);
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(topPrerolls));
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync($"{{\"error\":\"Server error: {ex.Message}\"}}");
                }
            });

            app.Run();
        }
    }

    // ===== Models =====
    public class PrerollCategory
    {
        public string Name { get; set; } = "";
        public int PrerollCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PrerollVideo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Category { get; set; } = "";
        public int Order { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime CreatedDate { get; set; }

        // Video metadata fields
        public TimeSpan? Duration { get; set; }
        public string Resolution { get; set; } = "";
        public string VideoCodec { get; set; } = "";
        public string AudioCodec { get; set; } = "";
        public double FrameRate { get; set; }
        public long Bitrate { get; set; }
        public string ThumbnailPath { get; set; } = "";
        public bool HasMetadata { get; set; }
    }

    public class PrerollConfig
    {
        public string? ActiveCategory { get; set; }
        public DateTime? LastActivated { get; set; }
        public Dictionary<string, DateTime> ScheduledActivations { get; set; } = new();
        public bool AutoScheduleEnabled { get; set; } = true;
        public List<PrerollSchedule> Schedules { get; set; } = new();
    }

    public class PrerollSchedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CategoryName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public ScheduleType Type { get; set; } = ScheduleType.OneTime;
        public bool IsActive { get; set; } = true;
        public string Description { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastExecuted { get; set; }
    }

    public enum ScheduleType
    {
        OneTime,
        Daily,
        Weekly,
        Monthly,
        Yearly
    }

    public class UploadResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    public class PlexServerStatus
    {
        public bool Connected { get; set; }
        public string ServerName { get; set; } = "";
        public string Version { get; set; } = "";
        public string? Error { get; set; }
    }

    public class PlexConfig
    {
        public string Url { get; set; } = "http://localhost:32400";
        public string Token { get; set; } = "";
    }

    // ===== Backup/Restore Models =====

    public class PrerollBackup
    {
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = "";
        public PrerollConfig Config { get; set; } = new();
        public List<BackupCategory> Categories { get; set; } = new();
    }

    public class BackupCategory
    {
        public string Name { get; set; } = "";
        public List<BackupVideo> Videos { get; set; } = new();
    }

    public class BackupVideo
    {
        public string Name { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime CreatedDate { get; set; }
        public int Order { get; set; }
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    public class BackupInfo
    {
        public string FilePath { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = "";
        public int CategoryCount { get; set; }
        public int TotalVideos { get; set; }
        public long FileSize { get; set; }
    }

    // ===== Update Check Models =====

    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public bool IsUpdateAvailable { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? ReleaseNotes { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("body")]
        public string Body { get; set; } = "";

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }
    }

    // ===== Usage Statistics Models =====

    public class PrerollUsage
    {
        public int Id { get; set; }
        public string PrerollId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime PlayStartTime { get; set; }
        public int? PlayDuration { get; set; }
        public string? ClientIp { get; set; }
        public string? UserAgent { get; set; }
        public string? PlexClientId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class DailyStats
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int TotalPlays { get; set; } = 0;
        public int TotalWatchTime { get; set; } = 0;
        public int UniquePrerolls { get; set; } = 0;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }

    public class UsageStats
    {
        public IEnumerable<dynamic> Data { get; set; } = new List<dynamic>();
    }

    public class TopPreroll
    {
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int Plays { get; set; }
        public int TotalWatchTime { get; set; }
    }

    public class PlayRequest
    {
        public string PrerollId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? PlexClientId { get; set; }
    }

    public class CompletionRequest
    {
        public string PrerollId { get; set; } = string.Empty;
        public int PlayDuration { get; set; }
    }

    // ===== Interfaces =====
    public interface IPlexService
    {
        Task<PlexServerStatus> GetServerStatusAsync();
        Task UpdatePrerollsAsync(List<string> prerollPaths);
        Task<PlexConfig> GetPlexConfigAsync();
        Task<bool> UpdatePlexConfigAsync(PlexConfig config);
    }

    public interface IPrerollService
    {
        Task<List<PrerollCategory>> GetCategoriesAsync();
        Task<List<PrerollVideo>> GetPrerollsByCategoryAsync(string category);
        Task<bool> ActivateCategoryAsync(string category);
        Task<string> GetActiveCategoryAsync();
        Task<int> GetPrerollCountAsync();
        Task<UploadResult> UploadPrerollAsync(IFormFile file, string category, string name);
        Task<bool> DeletePrerollAsync(string id);
        Task ReorderPrerollAsync(string id, int newOrder);
        Task<List<PrerollSchedule>> GetSchedulesAsync();
        Task<PrerollSchedule> CreateScheduleAsync(PrerollSchedule schedule);
        Task<bool> UpdateScheduleAsync(PrerollSchedule schedule);
        Task<bool> DeleteScheduleAsync(string id);
        Task<BackupResult> CreateBackupAsync();
        Task<RestoreResult> RestoreBackupAsync(string backupPath);
        Task<List<BackupInfo>> GetAvailableBackupsAsync();
        Task<UpdateInfo> CheckForUpdatesAsync();
        string GetThumbnailsPath();
    }

    // ===== Services =====
    public class PlexService : IPlexService
    {
        private readonly HttpClient _http;
        private readonly ILogger<PlexService> _logger;
        private readonly IConfiguration _cfg;
        private PlexConfig? _plexConfig;

        private string PlexUrl => _plexConfig?.Url ?? _cfg["Plex:Url"] ?? "http://localhost:32400";
        private string PlexToken => _plexConfig?.Token ?? _cfg["Plex:Token"] ?? "";

        public PlexService(HttpClient http, ILogger<PlexService> logger, IConfiguration cfg)
        {
            _http = http;
            _logger = logger;
            _cfg = cfg;
            LoadPlexConfigAsync().Wait(); // Load config synchronously on startup
        }

        public async Task<PlexServerStatus> GetServerStatusAsync()
        {
            try
            {
                var url = $"{PlexUrl}/";
                if (!string.IsNullOrEmpty(PlexToken)) url += $"?X-Plex-Token={PlexToken}";
                var res = await _http.GetAsync(url);

                if (res.IsSuccessStatusCode)
                {
                    var xml = await res.Content.ReadAsStringAsync();
                    var doc = XDocument.Parse(xml);
                    var root = doc.Element("MediaContainer");
                    return new PlexServerStatus
                    {
                        Connected = true,
                        ServerName = root?.Attribute("friendlyName")?.Value ?? "Plex Server",
                        Version = root?.Attribute("version")?.Value ?? "Unknown"
                    };
                }

                return new PlexServerStatus { Connected = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Plex server");
                return new PlexServerStatus { Connected = false, Error = ex.Message };
            }
        }

        public async Task UpdatePrerollsAsync(List<string> prerollPaths)
        {
            try
            {
                if (prerollPaths.Count == 0)
                {
                    await ClearPrerollsAsync();
                    _logger.LogInformation("No prerolls to set, cleared.");
                    return;
                }

                var urls = prerollPaths.Select(p => Path.IsPathRooted(p) ? p : p).ToList();
                var prerollString = string.Join(";", urls);

                var url = $"{PlexUrl}/:/prefs?CinemaTrailersPrerollID={Uri.EscapeDataString(prerollString)}";
                if (!string.IsNullOrEmpty(PlexToken)) url += $"&X-Plex-Token={PlexToken}";

                var res = await _http.PutAsync(url, null);
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Updated Plex prerolls ({count})", prerollPaths.Count);
                }
                else
                {
                    _logger.LogWarning("Prefs PUT failed: {code}. Trying form PUT…", res.StatusCode);
                    await UpdatePrerollsViaPreferencesAsync(prerollString);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Plex prerolls");
            }
        }

        private async Task UpdatePrerollsViaPreferencesAsync(string prerollString)
        {
            try
            {
                var url = $"{PlexUrl}/:/prefs";
                if (!string.IsNullOrEmpty(PlexToken)) url += $"?X-Plex-Token={PlexToken}";

                var form = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string,string>("CinemaTrailersPrerollID", prerollString)
                });

                var res = await _http.PutAsync(url, form);
                if (res.IsSuccessStatusCode)
                    _logger.LogInformation("Updated Plex prerolls via preferences API");
                else
                    _logger.LogError("Failed updating via preferences API: {code}", res.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in preferences update");
            }
        }

        private async Task ClearPrerollsAsync()
        {
            try
            {
                var url = $"{PlexUrl}/:/prefs?CinemaTrailersPrerollID=";
                if (!string.IsNullOrEmpty(PlexToken)) url += $"&X-Plex-Token={PlexToken}";
                await _http.PutAsync(url, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing prerolls");
            }
        }

        public async Task<PlexConfig> GetPlexConfigAsync()
        {
            // Ensure config is loaded
            if (_plexConfig == null)
            {
                await LoadPlexConfigAsync();
            }

            return new PlexConfig
            {
                Url = PlexUrl,
                Token = PlexToken
            };
        }

        public async Task<bool> UpdatePlexConfigAsync(PlexConfig config)
        {
            try
            {
                // Store Plex config in a custom file since ASP.NET Core config is read-only at runtime
                await SavePlexConfigToCustomFileAsync(config);
                _logger.LogInformation("Plex configuration updated: {url}", config.Url);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Plex configuration");
                return false;
            }
        }

        private async Task LoadPlexConfigAsync()
        {
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                              "PlexPrerollManager", "plex-config.json");

                if (File.Exists(configPath))
                {
                    var json = await File.ReadAllTextAsync(configPath);
                    _plexConfig = JsonSerializer.Deserialize<PlexConfig>(json) ?? new PlexConfig();
                    _logger.LogInformation("Loaded Plex config from custom file");
                }
                else
                {
                    _plexConfig = null; // Use ASP.NET Core config
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading Plex config from custom file, using default config");
                _plexConfig = null; // Use ASP.NET Core config
            }
        }

        private async Task SavePlexConfigToCustomFileAsync(PlexConfig config)
        {
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                              "PlexPrerollManager", "plex-config.json");
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Plex config to custom file");
            }
        }
    }

    public class PrerollService : IPrerollService
    {
        private readonly string _dataPath;
        private readonly string _prerollsPath;
        private readonly string _configPath;
        private readonly string _thumbnailsPath;

        public PrerollService(IConfiguration configuration)
        {
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlexPrerollManager");
            _prerollsPath = Path.Combine(_dataPath, "Prerolls");
            _configPath = Path.Combine(_dataPath, "config.json");
            _thumbnailsPath = Path.Combine(_prerollsPath, ".thumbnails");

            // Ensure directories exist
            Directory.CreateDirectory(_dataPath);
            Directory.CreateDirectory(_prerollsPath);
            Directory.CreateDirectory(_thumbnailsPath);
        }

        public async Task<List<PrerollCategory>> GetCategoriesAsync()
        {
            var categories = new List<PrerollCategory>();

            if (Directory.Exists(_prerollsPath))
            {
                var categoryDirs = Directory.GetDirectories(_prerollsPath);
                foreach (var categoryDir in categoryDirs)
                {
                    var categoryName = Path.GetFileName(categoryDir);
                    var videoFiles = Directory.GetFiles(categoryDir, "*.*")
                        .Where(f => new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" }.Contains(Path.GetExtension(f).ToLower()))
                        .ToArray();

                    categories.Add(new PrerollCategory
                    {
                        Name = categoryName,
                        PrerollCount = videoFiles.Length,
                        LastUpdated = Directory.GetLastWriteTime(categoryDir)
                    });
                }
            }

            return categories;
        }

        public async Task<List<PrerollVideo>> GetPrerollsByCategoryAsync(string category)
        {
            var categoryPath = Path.Combine(_prerollsPath, category);
            var videos = new List<PrerollVideo>();

            if (Directory.Exists(categoryPath))
            {
                var videoFiles = Directory.GetFiles(categoryPath, "*.*")
                    .Where(f => new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" }.Contains(Path.GetExtension(f).ToLower()))
                    .OrderBy(f => Path.GetFileName(f))
                    .ToArray();

                for (int i = 0; i < videoFiles.Length; i++)
                {
                    var file = videoFiles[i];
                    var fileInfo = new FileInfo(file);
                    var thumbnailPath = Path.Combine(_thumbnailsPath, $"{Path.GetFileNameWithoutExtension(file)}.jpg");

                    videos.Add(new PrerollVideo
                    {
                        Id = Path.GetFileNameWithoutExtension(file),
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        Category = category,
                        Order = i,
                        FileSizeBytes = fileInfo.Length,
                        CreatedDate = fileInfo.CreationTime,
                        ThumbnailPath = File.Exists(thumbnailPath) ? $"/api/thumbnails/{Path.GetFileName(thumbnailPath)}" : ""
                    });
                }
            }

            return videos;
        }

        public async Task<bool> ActivateCategoryAsync(string category)
        {
            try
            {
                var config = await LoadConfigAsync();
                config.ActiveCategory = category;
                config.LastActivated = DateTime.Now;
                await SaveConfigAsync(config);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetActiveCategoryAsync()
        {
            var config = await LoadConfigAsync();
            return config.ActiveCategory ?? "";
        }

        public async Task<int> GetPrerollCountAsync()
        {
            var categories = await GetCategoriesAsync();
            return categories.Sum(c => c.PrerollCount);
        }

        public async Task<UploadResult> UploadPrerollAsync(IFormFile file, string category, string name)
        {
            try
            {
                var categoryPath = Path.Combine(_prerollsPath, category);
                Directory.CreateDirectory(categoryPath);

                var fileName = $"{name}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(categoryPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return new UploadResult
                {
                    Success = true,
                    FilePath = filePath,
                    Message = $"Successfully uploaded {fileName}"
                };
            }
            catch (Exception ex)
            {
                return new UploadResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<bool> DeletePrerollAsync(string id)
        {
            try
            {
                // Find the file in all categories
                var categories = await GetCategoriesAsync();
                foreach (var category in categories)
                {
                    var videos = await GetPrerollsByCategoryAsync(category.Name);
                    var video = videos.FirstOrDefault(v => v.Id == id);
                    if (video != null)
                    {
                        if (File.Exists(video.FilePath))
                        {
                            File.Delete(video.FilePath);
                        }

                        // Delete thumbnail if it exists
                        var thumbnailPath = Path.Combine(_thumbnailsPath, $"{id}.jpg");
                        if (File.Exists(thumbnailPath))
                        {
                            File.Delete(thumbnailPath);
                        }

                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task ReorderPrerollAsync(string id, int newOrder)
        {
            // This is a simplified implementation
            // In a real application, you'd want to update the order in a database
            await Task.CompletedTask;
        }

        public async Task<List<PrerollSchedule>> GetSchedulesAsync()
        {
            var config = await LoadConfigAsync();
            return config.Schedules;
        }

        public async Task<PrerollSchedule> CreateScheduleAsync(PrerollSchedule schedule)
        {
            var config = await LoadConfigAsync();
            config.Schedules.Add(schedule);
            await SaveConfigAsync(config);
            return schedule;
        }

        public async Task<bool> UpdateScheduleAsync(PrerollSchedule schedule)
        {
            var config = await LoadConfigAsync();
            var existing = config.Schedules.FirstOrDefault(s => s.Id == schedule.Id);
            if (existing != null)
            {
                var index = config.Schedules.IndexOf(existing);
                config.Schedules[index] = schedule;
                await SaveConfigAsync(config);
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteScheduleAsync(string id)
        {
            var config = await LoadConfigAsync();
            var schedule = config.Schedules.FirstOrDefault(s => s.Id == id);
            if (schedule != null)
            {
                config.Schedules.Remove(schedule);
                await SaveConfigAsync(config);
                return true;
            }
            return false;
        }

        public async Task<BackupResult> CreateBackupAsync()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupDir = Path.Combine(_dataPath, "backups");
                Directory.CreateDirectory(backupDir);

                var backupPath = Path.Combine(backupDir, $"backup_{timestamp}.json");

                var categories = await GetCategoriesAsync();
                var backup = new PrerollBackup
                {
                    Timestamp = DateTime.Now,
                    Version = "2.2.0",
                    Config = await LoadConfigAsync(),
                    Categories = new List<BackupCategory>()
                };

                foreach (var category in categories)
                {
                    var videos = await GetPrerollsByCategoryAsync(category.Name);
                    backup.Categories.Add(new BackupCategory
                    {
                        Name = category.Name,
                        Videos = videos.Select(v => new BackupVideo
                        {
                            Name = v.Name,
                            FileSizeBytes = v.FileSizeBytes,
                            CreatedDate = v.CreatedDate,
                            Order = v.Order
                        }).ToList()
                    });
                }

                var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(backupPath, json);

                return new BackupResult
                {
                    Success = true,
                    FilePath = backupPath,
                    Message = $"Backup created successfully at {backupPath}"
                };
            }
            catch (Exception ex)
            {
                return new BackupResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<RestoreResult> RestoreBackupAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Error = "Backup file not found"
                    };
                }

                var json = await File.ReadAllTextAsync(backupPath);
                var backup = JsonSerializer.Deserialize<PrerollBackup>(json);

                if (backup == null)
                {
                    return new RestoreResult
                    {
                        Success = false,
                        Error = "Invalid backup file format"
                    };
                }

                // Restore configuration
                await SaveConfigAsync(backup.Config);

                // Note: In a real implementation, you'd restore the video files as well
                // For now, we just restore the configuration

                return new RestoreResult
                {
                    Success = true,
                    Message = "Configuration restored successfully"
                };
            }
            catch (Exception ex)
            {
                return new RestoreResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<List<BackupInfo>> GetAvailableBackupsAsync()
        {
            var backups = new List<BackupInfo>();
            var backupDir = Path.Combine(_dataPath, "backups");

            if (Directory.Exists(backupDir))
            {
                var backupFiles = Directory.GetFiles(backupDir, "backup_*.json");
                foreach (var file in backupFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var backup = JsonSerializer.Deserialize<PrerollBackup>(json);

                        if (backup != null)
                        {
                            backups.Add(new BackupInfo
                            {
                                FilePath = file,
                                Timestamp = backup.Timestamp,
                                Version = backup.Version,
                                CategoryCount = backup.Categories.Count,
                                TotalVideos = backup.Categories.Sum(c => c.Videos.Count),
                                FileSize = new FileInfo(file).Length
                            });
                        }
                    }
                    catch
                    {
                        // Skip invalid backup files
                    }
                }
            }

            return backups.OrderByDescending(b => b.Timestamp).ToList();
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                // This is a simplified implementation
                // In a real application, you'd check GitHub API for releases
                return new UpdateInfo
                {
                    CurrentVersion = "2.2.0",
                    LatestVersion = "2.2.0",
                    IsUpdateAvailable = false,
                    ReleaseNotes = "This is the latest version."
                };
            }
            catch
            {
                return new UpdateInfo
                {
                    CurrentVersion = "2.2.0",
                    LatestVersion = "2.2.0",
                    IsUpdateAvailable = false
                };
            }
        }

        public string GetThumbnailsPath()
        {
            return _thumbnailsPath;
        }

        private async Task<PrerollConfig> LoadConfigAsync()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = await File.ReadAllTextAsync(_configPath);
                    return JsonSerializer.Deserialize<PrerollConfig>(json) ?? new PrerollConfig();
                }
            }
            catch
            {
                // Ignore errors and return default config
            }

            return new PrerollConfig();
        }

        private async Task SaveConfigAsync(PrerollConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }

    // ===== Usage Statistics Services =====

    public class UsageTrackingService
    {
        private readonly string _connectionString;
        private readonly ILogger<UsageTrackingService> _logger;

        public UsageTrackingService(IConfiguration configuration, ILogger<UsageTrackingService> logger)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               "Data Source=C:\\ProgramData\\PlexPrerollManager\\plexprerollmanager.db";
        }

        public async Task RecordPlayStartAsync(string prerollId, string categoryName, string clientIp, string userAgent, string? plexClientId)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.ExecuteAsync(@"
                    INSERT INTO PrerollUsage (PrerollId, CategoryName, PlayStartTime, ClientIp, UserAgent, PlexClientId)
                    VALUES (@PrerollId, @CategoryName, @PlayStartTime, @ClientIp, @UserAgent, @PlexClientId)",
                    new
                    {
                        PrerollId = prerollId,
                        CategoryName = categoryName,
                        PlayStartTime = DateTime.UtcNow,
                        ClientIp = clientIp,
                        UserAgent = userAgent,
                        PlexClientId = plexClientId
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording play start for preroll {PrerollId}", prerollId);
            }
        }

        public async Task RecordPlayCompleteAsync(string prerollId, int durationSeconds)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.ExecuteAsync(@"
                    UPDATE PrerollUsage
                    SET PlayDuration = @Duration
                    WHERE PrerollId = @PrerollId AND PlayDuration IS NULL
                    ORDER BY PlayStartTime DESC
                    LIMIT 1",
                    new { PrerollId = prerollId, Duration = durationSeconds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording play completion for preroll {PrerollId}", prerollId);
            }
        }

        public async Task<UsageStats> GetUsageStatsAsync(string period, int days)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var startDate = DateTime.UtcNow.AddDays(-days);

                var query = period switch
                {
                    "daily" => @"
                        SELECT
                            DATE(PlayStartTime) as Date,
                            CategoryName,
                            COUNT(*) as Plays,
                            COALESCE(SUM(PlayDuration), 0) as TotalWatchTime,
                            COUNT(DISTINCT PrerollId) as UniquePrerolls
                        FROM PrerollUsage
                        WHERE PlayStartTime >= @StartDate
                        GROUP BY DATE(PlayStartTime), CategoryName
                        ORDER BY Date DESC",
                    "weekly" => @"
                        SELECT
                            strftime('%Y-%W', PlayStartTime) as Date,
                            CategoryName,
                            COUNT(*) as Plays,
                            COALESCE(SUM(PlayDuration), 0) as TotalWatchTime,
                            COUNT(DISTINCT PrerollId) as UniquePrerolls
                        FROM PrerollUsage
                        WHERE PlayStartTime >= @StartDate
                        GROUP BY strftime('%Y-%W', PlayStartTime), CategoryName
                        ORDER BY Date DESC",
                    "monthly" => @"
                        SELECT
                            strftime('%Y-%m', PlayStartTime) as Date,
                            CategoryName,
                            COUNT(*) as Plays,
                            COALESCE(SUM(PlayDuration), 0) as TotalWatchTime,
                            COUNT(DISTINCT PrerollId) as UniquePrerolls
                        FROM PrerollUsage
                        WHERE PlayStartTime >= @StartDate
                        GROUP BY strftime('%Y-%m', PlayStartTime), CategoryName
                        ORDER BY Date DESC",
                    _ => throw new ArgumentException("Invalid period")
                };

                var data = await connection.QueryAsync(query, new { StartDate = startDate });
                return new UsageStats { Data = data };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage stats");
                return new UsageStats { Data = new List<dynamic>() };
            }
        }

        public async Task<IEnumerable<TopPreroll>> GetTopPrerollsAsync(int limit, string period, int days)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

                var startDate = DateTime.UtcNow.AddDays(-days);

                var query = @"
                    SELECT
                        PrerollId as Name,
                        CategoryName,
                        COUNT(*) as Plays,
                        COALESCE(SUM(PlayDuration), 0) as TotalWatchTime
                    FROM PrerollUsage
                    WHERE PlayStartTime >= @StartDate
                    GROUP BY PrerollId, CategoryName
                    ORDER BY Plays DESC, TotalWatchTime DESC
                    LIMIT @Limit";

                var topPrerolls = await connection.QueryAsync<TopPreroll>(query,
                    new { StartDate = startDate, Limit = limit });

                return topPrerolls;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top prerolls");
                return new List<TopPreroll>();
            }
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

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

                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_preroll_usage_preroll_id ON PrerollUsage(PrerollId)");
                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_preroll_usage_category ON PrerollUsage(CategoryName)");
                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_preroll_usage_start_time ON PrerollUsage(PlayStartTime)");
                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_preroll_usage_date ON PrerollUsage(date(PlayStartTime))");

                _logger.LogInformation("Usage statistics database initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing usage statistics database");
            }
        }

        public async Task UpdateDailyStatsAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);

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

                _logger.LogInformation("Daily statistics updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating daily statistics");
            }
        }
    }

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

                await _usageService.UpdateDailyStatsAsync();
                _logger.LogInformation("Daily statistics updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing usage statistics database");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Database initialization service stopping");
            return Task.CompletedTask;
        }
    }
}