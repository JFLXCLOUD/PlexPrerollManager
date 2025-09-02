
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
            builder.Services.AddSingleton<IPlexService, PlexService>();
            builder.Services.AddSingleton<IPrerollService, PrerollService>();
            builder.Services.AddHostedService<PrerollManagerWorker>();

            // Configure JSON options to handle enums as strings
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

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
                var prerollService = ctx.RequestServices.GetRequiredService<IPrerollService>();
                await prerollService.ActivateCategoryAsync(category);
                await ctx.Response.WriteAsync("{\"message\":\"Category activated\"}");
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

            app.Logger.LogInformation("Plex Preroll Manager web UI: http://localhost:8089");
            await app.RunAsync();
        }

        // ===== Your original worker =====
        public class PrerollManagerWorker : BackgroundService
        {
            private readonly ILogger<PrerollManagerWorker> _logger;
            private readonly IPrerollService _prerollService;

            public PrerollManagerWorker(ILogger<PrerollManagerWorker> logger, IPrerollService prerollService)
            {
                _logger = logger;
                _prerollService = prerollService;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                _logger.LogInformation("Plex Preroll Manager started at: {time}", DateTimeOffset.Now);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check and execute schedules
                        await _prerollService.CheckAndExecuteSchedulesAsync();

                        // Update active prerolls
                        await _prerollService.UpdateActivePrerollsAsync();

                        _logger.LogInformation("Preroll update completed at: {time}", DateTimeOffset.Now);
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Check every 5 minutes for schedules
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during preroll update");
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                }
            }
        }

        // ===== Interfaces & Services (unchanged, but with ProgramData paths) =====
        public interface IPrerollService
        {
            Task<List<PrerollCategory>> GetCategoriesAsync();
            Task<List<PrerollVideo>> GetPrerollsByCategoryAsync(string category);
            Task<string> GetActiveCategoryAsync();
            Task<int> GetPrerollCountAsync();
            Task<UploadResult> UploadPrerollAsync(IFormFile file, string category, string name);
            Task ActivateCategoryAsync(string category);
            Task<bool> DeletePrerollAsync(string id);
            Task ReorderPrerollAsync(string id, int newOrder);
            Task UpdateActivePrerollsAsync();

            // Scheduling methods
            Task<List<PrerollSchedule>> GetSchedulesAsync();
            Task<PrerollSchedule> CreateScheduleAsync(PrerollSchedule schedule);
            Task<bool> UpdateScheduleAsync(PrerollSchedule schedule);
            Task<bool> DeleteScheduleAsync(string scheduleId);
            Task CheckAndExecuteSchedulesAsync();

            // Thumbnail methods
            string GetThumbnailsPath();

            // Backup/Restore methods
            Task<BackupResult> CreateBackupAsync();
            Task<RestoreResult> RestoreBackupAsync(string backupFilePath);
            Task<List<BackupInfo>> GetAvailableBackupsAsync();

            // Update check methods
            Task<UpdateInfo> CheckForUpdatesAsync();
        }

        public class PrerollService : IPrerollService
        {
            private readonly ILogger<PrerollService> _logger;
            private readonly IConfiguration _configuration;
            private readonly IPlexService _plexService;

            // IMPORTANT: ProgramData so the Windows service account can access these
            private string PrerollsPath => _configuration["PrerollManager:PrerollsPath"]
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlexPrerollManager", "Prerolls");

            private string ConfigPath => _configuration["PrerollManager:ConfigPath"]
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlexPrerollManager", "config.json");

            public PrerollService(ILogger<PrerollService> logger, IConfiguration configuration, IPlexService plexService)
            {
                _logger = logger;
                _configuration = configuration;
                _plexService = plexService;
                EnsureDirectoriesExist();
            }

            private void EnsureDirectoriesExist()
            {
                Directory.CreateDirectory(PrerollsPath);
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            }

            public async Task<List<PrerollCategory>> GetCategoriesAsync()
            {
                var categories = new List<PrerollCategory>();
                var config = await LoadConfigAsync();

                if (Directory.Exists(PrerollsPath))
                {
                    foreach (var categoryDir in Directory.GetDirectories(PrerollsPath))
                    {
                        var categoryName = Path.GetFileName(categoryDir);
                        var videoFiles = Directory.GetFiles(categoryDir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(IsVideoFile).ToArray();

                        // Only include categories that have videos
                        if (videoFiles.Length > 0)
                        {
                            categories.Add(new PrerollCategory
                            {
                                Name = categoryName,
                                PrerollCount = videoFiles.Length,
                                IsActive = config.ActiveCategory == categoryName,
                                LastUpdated = Directory.GetLastWriteTime(categoryDir)
                            });
                        }
                    }
                }
                return categories.OrderBy(c => c.Name).ToList();
            }

            public async Task<List<PrerollVideo>> GetPrerollsByCategoryAsync(string category)
            {
                var result = new List<PrerollVideo>();
                var path = Path.Combine(PrerollsPath, category);
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(IsVideoFile).ToArray();

                    for (int i = 0; i < files.Length; i++)
                    {
                        var video = await ExtractVideoMetadataAsync(files[i]);
                        video.Order = i + 1;
                        result.Add(video);
                    }
                }
                return result.OrderBy(p => p.Order).ToList();
            }

            public async Task<string> GetActiveCategoryAsync()
            {
                var cfg = await LoadConfigAsync();
                return cfg.ActiveCategory ?? "None";
            }

            public Task<int> GetPrerollCountAsync()
            {
                if (!Directory.Exists(PrerollsPath)) return Task.FromResult(0);
                var count = Directory.GetDirectories(PrerollsPath)
                    .SelectMany(dir => Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                    .Count(IsVideoFile);
                return Task.FromResult(count);
            }

            public async Task<UploadResult> UploadPrerollAsync(IFormFile file, string category, string name)
            {
                try
                {
                    if (!IsVideoFile(file.FileName))
                        return new UploadResult { Success = false, Error = "File must be a video format" };

                    var categoryPath = Path.Combine(PrerollsPath, category);
                    Directory.CreateDirectory(categoryPath);

                    var fileName = $"{name}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(categoryPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await file.CopyToAsync(stream);

                    _logger.LogInformation("Uploaded preroll: {name} to {cat}", fileName, category);
                    return new UploadResult { Success = true, FilePath = filePath, Message = "Upload successful" };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading preroll");
                    return new UploadResult { Success = false, Error = ex.Message };
                }
            }

            public async Task ActivateCategoryAsync(string category)
            {
                var cfg = await LoadConfigAsync();
                cfg.ActiveCategory = category;
                cfg.LastActivated = DateTime.Now;
                await SaveConfigAsync(cfg);
                await UpdateActivePrerollsAsync();
                _logger.LogInformation("Activated category: {cat}", category);
            }

            public Task<bool> DeletePrerollAsync(string id)
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(PrerollsPath))
                    {
                        var filePath = Path.Combine(dir, $"{id}.mp4");
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            _logger.LogInformation("Deleted preroll: {id}", id);
                            return Task.FromResult(true);
                        }
                    }
                    return Task.FromResult(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting preroll {id}", id);
                    return Task.FromResult(false);
                }
            }

            public Task ReorderPrerollAsync(string id, int newOrder) => Task.CompletedTask;

            public async Task UpdateActivePrerollsAsync()
            {
                var cfg = await LoadConfigAsync();
                if (string.IsNullOrWhiteSpace(cfg.ActiveCategory)) return;

                var videos = await GetPrerollsByCategoryAsync(cfg.ActiveCategory);
                if (videos.Count == 0) return;

                await _plexService.UpdatePrerollsAsync(videos.Select(v => v.FilePath).ToList());
            }

            // ===== Backup/Restore Methods =====

            public async Task<BackupResult> CreateBackupAsync()
            {
                try
                {
                    var backup = new PrerollBackup
                    {
                        Timestamp = DateTime.Now,
                        Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.1.0",
                        Config = await LoadConfigAsync(),
                        Categories = new List<BackupCategory>()
                    };

                    // Backup category information and video metadata
                    if (Directory.Exists(PrerollsPath))
                    {
                        foreach (var categoryDir in Directory.GetDirectories(PrerollsPath))
                        {
                            var categoryName = Path.GetFileName(categoryDir);
                            var videos = await GetPrerollsByCategoryAsync(categoryName);

                            backup.Categories.Add(new BackupCategory
                            {
                                Name = categoryName,
                                Videos = videos.Select(v => new BackupVideo
                                {
                                    Name = v.Name,
                                    FileSizeBytes = v.FileSizeBytes,
                                    CreatedDate = v.CreatedDate,
                                    Order = v.Order
                                }).ToList()
                            });
                        }
                    }

                    // Save backup to file
                    var backupPath = Path.Combine(ConfigPath.Replace("config.json", ""), "backups");
                    Directory.CreateDirectory(backupPath);

                    var backupFile = Path.Combine(backupPath, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(backupFile, json);

                    _logger.LogInformation("Backup created: {file}", backupFile);
                    return new BackupResult { Success = true, FilePath = backupFile, Message = "Backup created successfully" };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating backup");
                    return new BackupResult { Success = false, Error = ex.Message };
                }
            }

            public async Task<RestoreResult> RestoreBackupAsync(string backupFilePath)
            {
                try
                {
                    if (!File.Exists(backupFilePath))
                        return new RestoreResult { Success = false, Error = "Backup file not found" };

                    var json = await File.ReadAllTextAsync(backupFilePath);
                    var backup = JsonSerializer.Deserialize<PrerollBackup>(json);

                    if (backup == null)
                        return new RestoreResult { Success = false, Error = "Invalid backup file" };

                    // Restore configuration
                    await SaveConfigAsync(backup.Config);

                    // Restore categories and videos (metadata only, not actual files)
                    foreach (var category in backup.Categories)
                    {
                        var categoryPath = Path.Combine(PrerollsPath, category.Name);
                        Directory.CreateDirectory(categoryPath);

                        // Note: This only restores metadata. Actual video files would need to be manually restored
                        _logger.LogInformation("Restored category: {category} with {count} videos", category.Name, category.Videos.Count);
                    }

                    _logger.LogInformation("Backup restored from: {file}", backupFilePath);
                    return new RestoreResult { Success = true, Message = $"Restored {backup.Categories.Count} categories and configuration" };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring backup");
                    return new RestoreResult { Success = false, Error = ex.Message };
                }
            }

            public async Task<List<BackupInfo>> GetAvailableBackupsAsync()
            {
                var backups = new List<BackupInfo>();
                var backupPath = Path.Combine(ConfigPath.Replace("config.json", ""), "backups");

                if (Directory.Exists(backupPath))
                {
                    foreach (var file in Directory.GetFiles(backupPath, "backup_*.json"))
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

            // ===== Update Check Methods =====

            public async Task<UpdateInfo> CheckForUpdatesAsync()
            {
                try
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("PlexPrerollManager/1.1.0");

                    var response = await client.GetAsync("https://api.github.com/repos/JFLXCLOUD/PlexPrerollManager/releases/latest");
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release != null)
                    {
                        var currentVersion = new Version(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.1.0");
                        var latestVersion = new Version(release.TagName.TrimStart('v'));

                        return new UpdateInfo
                        {
                            CurrentVersion = currentVersion.ToString(),
                            LatestVersion = latestVersion.ToString(),
                            IsUpdateAvailable = latestVersion > currentVersion,
                            ReleaseUrl = release.HtmlUrl,
                            ReleaseNotes = release.Body,
                            PublishedAt = release.PublishedAt
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking for updates");
                }

                return new UpdateInfo
                {
                    CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.1.0",
                    LatestVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "2.1.0",
                    IsUpdateAvailable = false
                };
            }

            // ===== Scheduling Methods =====

            public async Task<List<PrerollSchedule>> GetSchedulesAsync()
            {
                var config = await LoadConfigAsync();
                return config.Schedules.OrderBy(s => s.StartDate).ToList();
            }

            public async Task<PrerollSchedule> CreateScheduleAsync(PrerollSchedule schedule)
            {
                var config = await LoadConfigAsync();
                schedule.Id = Guid.NewGuid().ToString();
                schedule.CreatedDate = DateTime.Now;
                config.Schedules.Add(schedule);
                await SaveConfigAsync(config);
                _logger.LogInformation("Created schedule: {desc} for category {cat}", schedule.Description, schedule.CategoryName);
                return schedule;
            }

            public async Task<bool> UpdateScheduleAsync(PrerollSchedule schedule)
            {
                var config = await LoadConfigAsync();
                var existing = config.Schedules.FirstOrDefault(s => s.Id == schedule.Id);
                if (existing == null) return false;

                existing.CategoryName = schedule.CategoryName;
                existing.StartDate = schedule.StartDate;
                existing.EndDate = schedule.EndDate;
                existing.Type = schedule.Type;
                existing.IsActive = schedule.IsActive;
                existing.Description = schedule.Description;

                await SaveConfigAsync(config);
                _logger.LogInformation("Updated schedule: {id}", schedule.Id);
                return true;
            }

            public async Task<bool> DeleteScheduleAsync(string scheduleId)
            {
                var config = await LoadConfigAsync();
                var schedule = config.Schedules.FirstOrDefault(s => s.Id == scheduleId);
                if (schedule == null) return false;

                config.Schedules.Remove(schedule);
                await SaveConfigAsync(config);
                _logger.LogInformation("Deleted schedule: {id}", scheduleId);
                return true;
            }

            public async Task CheckAndExecuteSchedulesAsync()
            {
                var config = await LoadConfigAsync();
                var now = DateTime.Now;
                var schedulesToExecute = new List<PrerollSchedule>();

                foreach (var schedule in config.Schedules.Where(s => s.IsActive))
                {
                    bool shouldExecute = false;

                    switch (schedule.Type)
                    {
                        case ScheduleType.OneTime:
                            shouldExecute = schedule.StartDate <= now && schedule.StartDate > (schedule.LastExecuted ?? DateTime.MinValue);
                            break;

                        case ScheduleType.Daily:
                            shouldExecute = ShouldExecuteRecurring(schedule, now, TimeSpan.FromDays(1));
                            break;

                        case ScheduleType.Weekly:
                            shouldExecute = ShouldExecuteRecurring(schedule, now, TimeSpan.FromDays(7));
                            break;

                        case ScheduleType.Monthly:
                            shouldExecute = ShouldExecuteRecurring(schedule, now, TimeSpan.FromDays(30));
                            break;

                        case ScheduleType.Yearly:
                            shouldExecute = ShouldExecuteRecurring(schedule, now, TimeSpan.FromDays(365));
                            break;
                    }

                    if (shouldExecute)
                    {
                        schedulesToExecute.Add(schedule);
                    }
                }

                foreach (var schedule in schedulesToExecute)
                {
                    try
                    {
                        await ActivateCategoryAsync(schedule.CategoryName);
                        schedule.LastExecuted = now;
                        _logger.LogInformation("Executed schedule: {desc} - activated category {cat}",
                            schedule.Description, schedule.CategoryName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute schedule: {id}", schedule.Id);
                    }
                }

                if (schedulesToExecute.Any())
                {
                    await SaveConfigAsync(config);
                }
            }

            private bool ShouldExecuteRecurring(PrerollSchedule schedule, DateTime now, TimeSpan interval)
            {
                if (schedule.LastExecuted == null)
                {
                    return schedule.StartDate <= now;
                }

                var nextExecution = schedule.LastExecuted.Value.Add(interval);
                return now >= nextExecution && (schedule.EndDate == null || now <= schedule.EndDate.Value);
            }

            private async Task<PrerollConfig> LoadConfigAsync()
            {
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        var json = await File.ReadAllTextAsync(ConfigPath);
                        return JsonSerializer.Deserialize<PrerollConfig>(json) ?? new PrerollConfig();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading config");
                }
                return new PrerollConfig();
            }

            private async Task SaveConfigAsync(PrerollConfig config)
            {
                try
                {
                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(ConfigPath, json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving config");
                }
            }

            private bool IsVideoFile(string path) =>
                new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" }
                .Contains(Path.GetExtension(path).ToLowerInvariant());

            // ===== Video Metadata & Thumbnail Methods =====

            private string ThumbnailsPath => Path.Combine(PrerollsPath, ".thumbnails");

            public string GetThumbnailsPath() => ThumbnailsPath;

            public async Task<PrerollVideo> ExtractVideoMetadataAsync(string filePath)
            {
                var video = new PrerollVideo
                {
                    Id = Path.GetFileNameWithoutExtension(filePath),
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    FilePath = filePath,
                    Category = Path.GetFileName(Path.GetDirectoryName(filePath) ?? ""),
                    FileSizeBytes = new FileInfo(filePath).Length,
                    CreatedDate = File.GetCreationTime(filePath),
                    HasMetadata = false
                };

                try
                {
                    var metadata = await GetVideoMetadataAsync(filePath);
                    if (metadata != null)
                    {
                        video.Duration = metadata.Duration;
                        video.Resolution = metadata.Resolution;
                        video.VideoCodec = metadata.VideoCodec;
                        video.AudioCodec = metadata.AudioCodec;
                        video.FrameRate = metadata.FrameRate;
                        video.Bitrate = metadata.Bitrate;
                        video.HasMetadata = true;

                        // Generate thumbnail
                        video.ThumbnailPath = await GenerateThumbnailAsync(filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract metadata for {file}", filePath);
                }

                return video;
            }

            private async Task<VideoMetadata?> GetVideoMetadataAsync(string filePath)
            {
                try
                {
                    var ffprobePath = FindFfprobeExecutable();
                    if (string.IsNullOrEmpty(ffprobePath))
                    {
                        _logger.LogWarning("ffprobe executable not found. Please ensure FFmpeg is installed and accessible.");
                        return null;
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = ffprobePath,
                        Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process == null) return null;

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var errorOutput = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        _logger.LogWarning("ffprobe failed for {file} (exit code: {code}): {error}", filePath, process.ExitCode, errorOutput);
                        return null;
                    }

                    var metadata = JsonSerializer.Deserialize<FfprobeOutput>(output);
                    return ExtractMetadata(metadata);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running ffprobe on {file}", filePath);
                    return null;
                }
            }

            private VideoMetadata? ExtractMetadata(FfprobeOutput? output)
            {
                if (output?.Streams == null || output.Format == null) return null;

                var videoStream = output.Streams.FirstOrDefault(s => s.CodecType == "video");
                var audioStream = output.Streams.FirstOrDefault(s => s.CodecType == "audio");

                if (videoStream == null) return null;

                var duration = TimeSpan.Zero;
                if (double.TryParse(output.Format.Duration, out var durationSeconds))
                {
                    duration = TimeSpan.FromSeconds(durationSeconds);
                }

                var bitrate = 0L;
                if (long.TryParse(output.Format.BitRate, out var br))
                {
                    bitrate = br;
                }

                return new VideoMetadata
                {
                    Duration = duration,
                    Resolution = $"{videoStream.Width}x{videoStream.Height}",
                    VideoCodec = videoStream.CodecName ?? "",
                    AudioCodec = audioStream?.CodecName ?? "",
                    FrameRate = videoStream.FrameRate ?? 0,
                    Bitrate = bitrate
                };
            }

            private async Task<string> GenerateThumbnailAsync(string videoPath)
            {
                try
                {
                    Directory.CreateDirectory(ThumbnailsPath);

                    var videoName = Path.GetFileNameWithoutExtension(videoPath);
                    var thumbnailPath = Path.Combine(ThumbnailsPath, $"{videoName}.jpg");

                    // Skip if thumbnail already exists
                    if (File.Exists(thumbnailPath))
                    {
                        return $"/api/thumbnails/{videoName}.jpg";
                    }

                    var ffmpegPath = FindFfmpegExecutable();
                    if (string.IsNullOrEmpty(ffmpegPath))
                    {
                        _logger.LogWarning("ffmpeg executable not found. Please ensure FFmpeg is installed and accessible.");
                        return "";
                    }

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-i \"{videoPath}\" -ss 00:00:05 -vframes 1 -q:v 2 -vf scale=320:-1 \"{thumbnailPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process == null) return "";

                    var errorOutput = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && File.Exists(thumbnailPath))
                    {
                        _logger.LogInformation("Generated thumbnail for {file} at {thumb}", videoPath, thumbnailPath);
                        return $"/api/thumbnails/{videoName}.jpg";
                    }
                    else
                    {
                        _logger.LogWarning("ffmpeg failed to generate thumbnail for {file} (exit code: {code}): {error}",
                            videoPath, process.ExitCode, errorOutput);
                        return "";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating thumbnail for {file}", videoPath);
                    return "";
                }
            }

            // ===== Helper Classes =====

            private class FfprobeOutput
            {
                public List<FfprobeStream>? Streams { get; set; }
                public FfprobeFormat? Format { get; set; }
            }

            private class FfprobeStream
            {
                public string? CodecType { get; set; }
                public string? CodecName { get; set; }
                public int? Width { get; set; }
                public int? Height { get; set; }
                public double? FrameRate { get; set; }
            }

            private class FfprobeFormat
            {
                public string? Duration { get; set; }
                public string? BitRate { get; set; }
            }

            private class VideoMetadata
            {
                public TimeSpan Duration { get; set; }
                public string Resolution { get; set; } = "";
                public string VideoCodec { get; set; } = "";
                public string AudioCodec { get; set; } = "";
                public double FrameRate { get; set; }
                public long Bitrate { get; set; }
            }

            // ===== FFmpeg Executable Location Methods =====

            private string? FindFfprobeExecutable()
            {
                // Try common FFmpeg installation paths
                var commonPaths = new[]
                {
                    @"C:\ffmpeg\bin\ffprobe.exe",
                    @"C:\Program Files\FFmpeg\bin\ffprobe.exe",
                    @"C:\Program Files (x86)\FFmpeg\bin\ffprobe.exe",
                    @"C:\tools\ffmpeg\bin\ffprobe.exe",
                    @"C:\Users\" + Environment.UserName + @"\ffmpeg\bin\ffprobe.exe"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        _logger.LogInformation("Found ffprobe at: {path}", path);
                        return path;
                    }
                }

                // Try to find in PATH
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "ffprobe",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            var firstLine = output.Split('\n')[0].Trim();
                            if (File.Exists(firstLine))
                            {
                                _logger.LogInformation("Found ffprobe in PATH at: {path}", firstLine);
                                return firstLine;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors when trying to find in PATH
                }

                _logger.LogWarning("ffprobe executable not found in common locations or PATH");
                return null;
            }

            private string? FindFfmpegExecutable()
            {
                // Try common FFmpeg installation paths
                var commonPaths = new[]
                {
                    @"C:\ffmpeg\bin\ffmpeg.exe",
                    @"C:\Program Files\FFmpeg\bin\ffmpeg.exe",
                    @"C:\Program Files (x86)\FFmpeg\bin\ffmpeg.exe",
                    @"C:\tools\ffmpeg\bin\ffmpeg.exe",
                    @"C:\Users\" + Environment.UserName + @"\ffmpeg\bin\ffmpeg.exe"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        _logger.LogInformation("Found ffmpeg at: {path}", path);
                        return path;
                    }
                }

                // Try to find in PATH
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "ffmpeg",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            var firstLine = output.Split('\n')[0].Trim();
                            if (File.Exists(firstLine))
                            {
                                _logger.LogInformation("Found ffmpeg in PATH at: {path}", firstLine);
                                return firstLine;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors when trying to find in PATH
                }

                _logger.LogWarning("ffmpeg executable not found in common locations or PATH");
                return null;
            }
        }

        public interface IPlexService
        {
            Task<PlexServerStatus> GetServerStatusAsync();
            Task UpdatePrerollsAsync(List<string> prerollPaths);
            Task<PlexConfig> GetPlexConfigAsync();
            Task<bool> UpdatePlexConfigAsync(PlexConfig config);
        }

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

    }
}
