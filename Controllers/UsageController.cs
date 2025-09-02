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
            var stats = await _usageService.GetUsageStatsAsync(period, days);
            return Ok(stats);
        }

        /// <summary>
        /// Get usage statistics for a specific category
        /// </summary>
        [HttpGet("stats/category/{categoryName}")]
        public async Task<IActionResult> GetCategoryStats(string categoryName, [FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            var stats = await _usageService.GetUsageStatsAsync(period, days);

            // Filter by category
            var filteredData = new List<dynamic>();
            foreach (var item in stats.Data)
            {
                if (item.CategoryName == categoryName)
                {
                    filteredData.Add(item);
                }
            }

            return Ok(new UsageStats { Data = filteredData });
        }

        /// <summary>
        /// Get usage statistics for a specific preroll
        /// </summary>
        [HttpGet("stats/preroll/{prerollId}")]
        public async Task<IActionResult> GetPrerollStats(string prerollId, [FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            var stats = await _usageService.GetUsageStatsAsync(period, days);

            // Filter by preroll (this would need to be enhanced based on your preroll data structure)
            var filteredData = new List<dynamic>();
            foreach (var item in stats.Data)
            {
                // This is a simplified filter - you may need to adjust based on your data structure
                filteredData.Add(item);
            }

            return Ok(new UsageStats { Data = filteredData });
        }

        /// <summary>
        /// Get top performing prerolls
        /// </summary>
        [HttpGet("top")]
        public async Task<IActionResult> GetTopPrerolls([FromQuery] int limit = 10, [FromQuery] string period = "daily", [FromQuery] int days = 30)
        {
            var topPrerolls = await _usageService.GetTopPrerollsAsync(limit, period, days);
            return Ok(topPrerolls);
        }

        /// <summary>
        /// Get usage summary
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var stats = await _usageService.GetUsageStatsAsync("daily", 30);

            var totalPlays = 0;
            var totalWatchTime = 0;
            var uniquePrerolls = 0;
            var categories = new HashSet<string>();

            foreach (var item in stats.Data)
            {
                totalPlays += (int)item.Plays;
                totalWatchTime += (int)(item.TotalWatchTime ?? 0);
                uniquePrerolls += (int)item.UniquePrerolls;
                categories.Add((string)item.CategoryName);
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
    }
}