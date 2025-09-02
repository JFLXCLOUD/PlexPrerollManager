// =============================================================================
// USAGE STATISTICS INTEGRATION EXAMPLES
// =============================================================================
// Add these examples to your existing preroll activation code
// =============================================================================

/*
1. INJECT USAGE INTEGRATION SERVICE INTO YOUR CONTROLLER
================================================================================

using PlexPrerollManager.Services;

public class YourExistingController : ControllerBase
{
    private readonly UsageIntegrationService _usageIntegration;

    public YourExistingController(UsageIntegrationService usageIntegration)
    {
        _usageIntegration = usageIntegration;
    }

    // ... your existing methods ...
}

================================================================================

2. TRACK PREROLL PLAY START
================================================================================

[HttpPost("activate-preroll")]
public async Task<IActionResult> ActivatePreroll([FromBody] ActivatePrerollRequest request)
{
    try
    {
        // Your existing preroll activation logic
        var preroll = await GetPrerollByIdAsync(request.PrerollId);
        var category = await GetCategoryByNameAsync(request.CategoryName);

        // TRACK THE PLAY START
        await _usageIntegration.TrackPrerollPlayAsync(
            preroll.Id,
            category.Name,
            HttpContext,
            UsageIntegrationService.GetPlexClientId(HttpContext)
        );

        // Your existing activation response
        return Ok(new { success = true, message = "Preroll activated" });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

================================================================================

3. TRACK PREROLL COMPLETION (Optional - for detailed analytics)
================================================================================

[HttpPost("preroll-completed")]
public async Task<IActionResult> MarkPrerollCompleted([FromBody] PrerollCompletedRequest request)
{
    try
    {
        // Track the completion with duration
        await _usageIntegration.TrackPrerollCompleteAsync(
            request.PrerollId,
            request.DurationSeconds
        );

        return Ok();
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

// Request model for completion tracking
public class PrerollCompletedRequest
{
    public string PrerollId { get; set; }
    public int DurationSeconds { get; set; }
}

================================================================================

4. ALTERNATIVE: INTEGRATE INTO EXISTING METHODS
================================================================================

// If you have existing preroll activation methods, just add tracking calls:

public async Task<IActionResult> ExistingActivateMethod(string prerollId, string categoryName)
{
    // Your existing logic here...

    // Add this line to track usage
    await _usageIntegration.TrackPrerollPlayAsync(prerollId, categoryName, HttpContext);

    // Rest of your existing logic...
}

================================================================================

5. SERVICE REGISTRATION IN Program.cs
================================================================================

// Add these lines to your Program.cs or Startup.cs:

builder.Services.AddScoped<UsageTrackingService>();
builder.Services.AddScoped<UsageIntegrationService>();
builder.Services.AddHostedService<DatabaseInitializationService>();

================================================================================

6. JAVASCRIPT INTEGRATION (Frontend)
================================================================================

// Add these calls to your existing frontend preroll activation code:

// When preroll starts
async function trackPrerollStart(prerollId, categoryName) {
    try {
        await fetch('/api/usage/play', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                prerollId: prerollId,
                categoryName: categoryName,
                plexClientId: getPlexClientId()
            })
        });
    } catch (error) {
        console.error('Error tracking preroll start:', error);
    }
}

// When preroll completes
async function trackPrerollComplete(prerollId, duration) {
    try {
        await fetch('/api/usage/complete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                prerollId: prerollId,
                playDuration: duration
            })
        });
    } catch (error) {
        console.error('Error tracking preroll completion:', error);
    }
}

================================================================================

7. DEPENDENCY INJECTION SETUP
================================================================================

// In Program.cs, add the services:

var builder = WebApplication.CreateBuilder(args);

// Add existing services...
builder.Services.AddControllers();

// Add usage statistics services
builder.Services.AddScoped<UsageTrackingService>();
builder.Services.AddScoped<UsageIntegrationService>();
builder.Services.AddHostedService<DatabaseInitializationService>();

var app = builder.Build();

// ... rest of your setup ...

================================================================================

8. DATABASE CONNECTION STRING
================================================================================

// Ensure your appsettings.json has the connection string:

{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=plexprerollmanager.db"
  }
}

================================================================================

9. TESTING THE INTEGRATION
================================================================================

// Test the API endpoints:

// 1. Record a play
POST /api/usage/play
{
    "prerollId": "test-preroll-1",
    "categoryName": "Test Category",
    "plexClientId": "test-client"
}

// 2. Get statistics
GET /api/usage/stats?days=30

// 3. Get top prerolls
GET /api/usage/top?limit=5

================================================================================

10. MONITORING AND LOGGING
================================================================================

// The system includes comprehensive logging. Check your logs for:
// - Successful tracking events
// - Database initialization
// - API endpoint usage
// - Any errors in tracking

================================================================================
*/