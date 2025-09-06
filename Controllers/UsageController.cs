using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PlexPrerollManager.Models;
using PlexPrerollManager.Services;

namespace PlexPrerollManager.Controllers
{
    [ApiController]
    [Route("api/usage")]
    public class UsageController : ControllerBase
    {
        private readonly UsageTrackingService _usageService;

        public UsageController(UsageTrackingService usageService)
        {
            _usageService = usageService;
        }

        /// <summary>
        /// Record when a preroll starts playing
        /// </summary>
        [HttpPost("play")]
        public async Task<IActionResult> RecordPlay([FromBody] PlayRequest request)
        {
            if (string.IsNullOrEmpty(request.PrerollId) || string.IsNullOrEmpty(request.CategoryName))
            {
                return BadRequest("PrerollId and CategoryName are required");
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            await _usageService.RecordPlayStartAsync(
                request.PrerollId,
                request.CategoryName,
                clientIp,
                userAgent,
                request.PlexClientId);

            return Ok();
        }

        /// <summary>
        /// Record when a preroll finishes playing
        /// </summary>
        [HttpPost("complete")]
        public async Task<IActionResult> RecordCompletion([FromBody] CompletionRequest request)
        {
            if (string.IsNullOrEmpty(request.PrerollId))
            {
                return BadRequest("PrerollId is required");
            }

            await _usageService.RecordPlayCompleteAsync(request.PrerollId, request.PlayDuration);
            return Ok();
        }

        /// <summary>
        /// Get usage statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats([FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            try
            {
                // Initialize database if needed
                await _usageService.InitializeDatabaseAsync();

                var stats = await _usageService.GetUsageStatsAsync(period, days);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                // Return empty stats if database issues
                return Ok(new UsageStats { Data = new List<dynamic>() });
            }
        }

        /// <summary>
        /// Get usage statistics for a specific category
        /// </summary>
        [HttpGet("stats/category/{categoryName}")]
        public async Task<IActionResult> GetCategoryStats(string categoryName, [FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            try
            {
                // Initialize database if needed
                await _usageService.InitializeDatabaseAsync();

                var stats = await _usageService.GetCategoryStatsAsync(categoryName, period, days);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                // Return empty stats if database issues
                return Ok(new UsageStats { Data = new List<dynamic>() });
            }
        }

        /// <summary>
        /// Get usage statistics for a specific preroll
        /// </summary>
        [HttpGet("stats/preroll/{prerollId}")]
        public async Task<IActionResult> GetPrerollStats(string prerollId, [FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            try
            {
                // Initialize database if needed
                await _usageService.InitializeDatabaseAsync();

                var stats = await _usageService.GetPrerollStatsAsync(prerollId, period, days);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                // Return empty stats if database issues
                return Ok(new UsageStats { Data = new List<dynamic>() });
            }
        }

        /// <summary>
        /// Get top performing prerolls
        /// </summary>
        [HttpGet("top")]
        public async Task<IActionResult> GetTopPrerolls([FromQuery] int limit = 10, [FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            try
            {
                // Initialize database if needed
                await _usageService.InitializeDatabaseAsync();

                var topPrerolls = await _usageService.GetTopPrerollsAsync(limit, period, days);
                return Ok(topPrerolls);
            }
            catch (Exception ex)
            {
                // Return empty list if database issues
                return Ok(new List<TopPreroll>());
            }
        }

        /// <summary>
        /// Get usage summary
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                // Initialize database if needed
                await _usageService.InitializeDatabaseAsync();

                var stats = await _usageService.GetUsageStatsAsync("daily", 30);

                var totalPlays = 0;
                var totalWatchTime = 0;
                var uniquePrerolls = 0;
                var categories = new HashSet<string>();

                foreach (var item in stats.Data)
                {
                    totalPlays += Convert.ToInt32(item.Plays ?? 0);
                    totalWatchTime += Convert.ToInt32(item.TotalWatchTime ?? 0);
                    uniquePrerolls += Convert.ToInt32(item.UniquePrerolls ?? 0);
                    if (item.CategoryName != null)
                    {
                        categories.Add(item.CategoryName.ToString());
                    }
                }

                var summary = new
                {
                    TotalPlays = totalPlays,
                    TotalWatchTime = totalWatchTime,
                    UniquePrerolls = uniquePrerolls,
                    TotalCategories = categories.Count,
                    AverageDuration = totalPlays > 0 ? totalWatchTime / totalPlays : 0
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                // Return default summary if database issues
                return Ok(new
                {
                    TotalPlays = 0,
                    TotalWatchTime = 0,
                    UniquePrerolls = 0,
                    TotalCategories = 0,
                    AverageDuration = 0
                });
            }
        }
    }
}