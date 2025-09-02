using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace PlexPrerollManager
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services
            builder.Services.AddControllers();

            var app = builder.Build();

            // Simple API endpoint
            app.MapGet("/api/status", () => "PlexPrerollManager is running!");

            // Serve static files
            app.UseStaticFiles();

            // Serve dashboard
            app.MapGet("/", async ctx =>
            {
                ctx.Response.ContentType = "text/html";
                var htmlPath = Path.Combine(AppContext.BaseDirectory, "dashboard.html");
                if (File.Exists(htmlPath))
                {
                    await ctx.Response.SendFileAsync(htmlPath);
                }
                else
                {
                    await ctx.Response.WriteAsync("<h1>PlexPrerollManager</h1><p>Dashboard not found</p>");
                }
            });

            await app.RunAsync();
        }
    }
}