using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using PlexPrerollManager.Services;
using System.Diagnostics;

namespace PlexPrerollManager
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                // Configure Serilog for file logging
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlexPrerollManager", "Logs");

                // Fallback to application directory if CommonApplicationData doesn't work
                if (!Directory.Exists(Path.GetDirectoryName(logDirectory)))
                {
                    logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
                    Log.Warning($"CommonApplicationData not accessible, using fallback: {logDirectory}");
                }

                Directory.CreateDirectory(logDirectory); // Ensure directory exists

                var logFilePath = Path.Combine(logDirectory, "plexprerollmanager-.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .WriteTo.Console()
                    .WriteTo.File(
                        logFilePath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                // Immediate test logging
                Log.Information($"Log directory: {logDirectory}");
                Log.Information($"Log file path: {logFilePath}");
                Log.Information($"Log directory exists: {Directory.Exists(logDirectory)}");
                Log.Information("Serilog configured successfully");
                Log.Information("=== LOGGING TEST ===");
                Log.Debug("Debug message test");
                Log.Information("Information message test");
                Log.Warning("Warning message test");
                Log.Error("Error message test");
                Log.Information("=== LOGGING TEST COMPLETE ===");

                var builder = WebApplication.CreateBuilder(args);

                // Use Serilog for logging
                builder.Host.UseSerilog();

                // Log application startup
                Log.Information("=== PLEX PREROLL MANAGER STARTING ===");
                Log.Information($"Application directory: {AppContext.BaseDirectory}");
                Log.Information($"Current directory: {Directory.GetCurrentDirectory()}");
                Log.Information($"Running as service: {!Environment.UserInteractive}");
                Log.Information($"Command line args: {string.Join(" ", args)}");

                // Configure configuration with reload support
                builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                // Configure URLs - respect command line arguments if provided
                var isService = args.Contains("--service") || Environment.UserInteractive == false;

                // Check if URLs were provided via command line
                var urlsArg = args.FirstOrDefault(arg => arg.StartsWith("--urls="));
                if (urlsArg != null)
                {
                    // Use the provided URLs
                    var providedUrl = urlsArg.Replace("--urls=", "");
                    builder.WebHost.UseUrls(providedUrl);
                    Log.Information($"Application starting on {providedUrl} (from command line)");
                }
                else
                {
                    // Use default URLs
                    var url = isService ? "http://*:8089" : "http://localhost:8089";
                    builder.WebHost.UseUrls(url);
                    Log.Information($"Application starting on {url} (Service mode: {isService})");
                }

                // Add Windows service support
                builder.Host.UseWindowsService();

            // Add services
            builder.Services.AddControllers()
                .AddNewtonsoftJson(options =>
                {
                    // Preserve property names as defined in C# (PascalCase)
                    options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
                });

            // Register application services
            builder.Services.AddScoped<UsageTrackingService>();
            builder.Services.AddScoped<ConfigurationService>();
            builder.Services.AddScoped<PlexApiService>();
            builder.Services.AddScoped<SchedulingService>();
            builder.Services.AddScoped<BackupService>();

            // Ensure controllers are discovered
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            Log.Information("Application built successfully, starting web server...");

            // Enable routing for controllers
            app.UseRouting();

            // Serve static files from the web directory
            var webPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), "web");
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webPath)
            });

            // Also serve static files from the application directory (fallback)
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory())
            });

            // Serve uploaded files
            var prerollsPath = builder.Configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(prerollsPath),
                RequestPath = "/files"
            });

            // Add CORS policy for Plex.tv authentication
            app.UseCors(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });

            // Map controller routes
            app.MapControllers();

            // Add heartbeat endpoint for service monitoring
            app.MapGet("/api/heartbeat", () =>
            {
                Log.Information("Heartbeat requested");
                return Results.Ok(new {
                    status = "running",
                    timestamp = DateTime.UtcNow,
                    uptime = "active"
                });
            });

            // Serve oauth.html specifically
            app.MapGet("/oauth.html", async ctx =>
            {
                ctx.Response.ContentType = "text/html";

                // Try multiple possible locations for oauth.html
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "web", "oauth.html"),
                    Path.Combine(AppContext.BaseDirectory, "oauth.html"),
                    Path.Combine(AppContext.BaseDirectory, "wwwroot", "oauth.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oauth.html"),
                    Path.Combine(Directory.GetCurrentDirectory(), "oauth.html")
                };

                string foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        foundPath = path;
                        Console.WriteLine($"[DEBUG] Found oauth.html at: {foundPath}");
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(foundPath))
                {
                    await ctx.Response.SendFileAsync(foundPath);
                    Console.WriteLine($"[DEBUG] Served oauth.html from: {foundPath}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] oauth.html not found in any expected location");
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.WriteAsync("oauth.html not found");
                }
            });

            // Serve scheduling-dashboard.html specifically
            app.MapGet("/scheduling", async ctx =>
            {
                ctx.Response.ContentType = "text/html";

                // Try multiple possible locations for scheduling-dashboard.html
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "web", "scheduling-dashboard.html"),
                    Path.Combine(AppContext.BaseDirectory, "scheduling-dashboard.html"),
                    Path.Combine(AppContext.BaseDirectory, "wwwroot", "scheduling-dashboard.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduling-dashboard.html"),
                    Path.Combine(Directory.GetCurrentDirectory(), "scheduling-dashboard.html")
                };

                string foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(foundPath))
                {
                    await ctx.Response.SendFileAsync(foundPath);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.WriteAsync("scheduling-dashboard.html not found");
                }
            });

            // Serve dashboard with better file location handling
            app.MapGet("/", async ctx =>
            {
                ctx.Response.ContentType = "text/html";

                // Try multiple possible locations for dashboard.html
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "web", "dashboard.html"),
                    Path.Combine(AppContext.BaseDirectory, "dashboard.html"),
                    Path.Combine(AppContext.BaseDirectory, "wwwroot", "dashboard.html"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dashboard.html"),
                    Path.Combine(Directory.GetCurrentDirectory(), "dashboard.html")
                };

                string foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(foundPath))
                {
                    await ctx.Response.SendFileAsync(foundPath);
                }
                else
                {
                    // Fallback: Serve embedded dashboard content
                    var fallbackDashboard = @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>PlexPrerollManager</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }
        .container { max-width: 800px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; text-align: center; }
        .status { background: #e8f5e8; border: 1px solid #4caf50; color: #2e7d32; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .warning { background: #fff3cd; border: 1px solid #ffc107; color: #856404; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .debug { background: #f8f9fa; border: 1px solid #dee2e6; padding: 15px; border-radius: 5px; margin: 20px 0; font-family: monospace; font-size: 12px; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🎬 PlexPrerollManager</h1>
        <div class='status'>
            <strong>✅ Service Status:</strong> Running successfully on port 8089
        </div>
        <div class='warning'>
            <strong>⚠️ Dashboard File:</strong> dashboard.html not found in expected location<br>
            <strong>Solution:</strong> The service is working, but the dashboard file needs to be in the correct location.
        </div>
        <div class='debug'>
            <strong>Debug Information:</strong><br>
            AppContext.BaseDirectory: " + AppContext.BaseDirectory + @"<br>
            AppDomain.CurrentDomain.BaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory + @"<br>
            Directory.GetCurrentDirectory(): " + Directory.GetCurrentDirectory() + @"<br><br>
            <strong>Searched Paths:</strong><br>";

                    foreach (var path in possiblePaths)
                    {
                        fallbackDashboard += path + " - " + (System.IO.File.Exists(path) ? "EXISTS" : "NOT FOUND") + "<br>";
                    }

                    fallbackDashboard += @"
        </div>
        <p><strong>API Endpoints:</strong></p>
        <ul>
            <li><a href='/api/status'>/api/status</a> - Service status</li>
        </ul>
    </div>
</body>
</html>";

                    await ctx.Response.WriteAsync(fallbackDashboard);
                }
            });

                Log.Information("Starting web server...");
                await app.RunAsync();
                Log.Information("Web server stopped");
            }
            catch (Exception ex)
            {
                // Log the error and exit gracefully
                Log.Fatal(ex, "Critical error starting PlexPrerollManager service");
                Console.WriteLine($"Critical error starting PlexPrerollManager service: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Check if it's a port binding error
                if (ex.Message.Contains("address already in use") || ex.InnerException?.Message.Contains("address already in use") != null)
                {
                    Log.Error("Port 8089 is already in use. This usually means:");
                    Log.Error("1. The PlexPrerollManager service is already running");
                    Log.Error("2. Another application is using port 8089");
                    Log.Error("3. Try stopping the service first: net stop PlexPrerollManager");
                    Console.WriteLine("Port 8089 is already in use. The service might already be running.");
                }

                // For Windows service, we need to exit with a non-zero code to indicate failure
                Environment.Exit(1);
            }
            finally
            {
                // Ensure to flush and stop the internal timer/threads before application-exit (Avoid segmentation fault on Linux)
                Log.CloseAndFlush();
            }
        }
    }
}