# Usage Statistics Backend Implementation Guide

## Overview
This guide provides complete backend implementation for the Usage Statistics feature in PlexPrerollManager v2.2.0.

## Files Created

### Models
- `Models/PrerollUsage.cs` - Database model for individual play events
- `Models/DailyStats.cs` - Database model for aggregated daily statistics
- `Models/UsageStats.cs` - API request/response models

### Services
- `Services/UsageTrackingService.cs` - Core service for database operations
- `Services/UsageIntegrationService.cs` - Helper service for easy integration
- `Services/DatabaseInitializationService.cs` - Database setup and initialization

### Controllers
- `Controllers/UsageController.cs` - REST API endpoints for statistics

### Integration
- `IntegrationExamples.cs` - Complete integration examples and instructions

## Installation Steps

### 1. Add Required NuGet Packages

```bash
dotnet add package Dapper
dotnet add package Microsoft.Data.Sqlite
```

### 2. Add Files to Your Project

Copy all the created files into your project structure:

```
YourProject/
├── Controllers/
│   └── UsageController.cs
├── Models/
│   ├── PrerollUsage.cs
│   ├── DailyStats.cs
│   └── UsageStats.cs
├── Services/
│   ├── UsageTrackingService.cs
│   ├── UsageIntegrationService.cs
│   └── DatabaseInitializationService.cs
└── IntegrationExamples.cs
```

### 3. Register Services in Program.cs

```csharp
using PlexPrerollManager.Services;

var builder = WebApplication.CreateBuilder(args);

// Add existing services...
builder.Services.AddControllers();

// Add usage statistics services
builder.Services.AddScoped<UsageTrackingService>();
builder.Services.AddScoped<UsageIntegrationService>();
builder.Services.AddHostedService<DatabaseInitializationService>();

var app = builder.Build();

// ... rest of your setup ...
```

### 4. Configure Database Connection

Ensure your `appsettings.json` has:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=plexprerollmanager.db"
  }
}
```

## API Endpoints

### Record Preroll Play
```http
POST /api/usage/play
Content-Type: application/json

{
    "prerollId": "string",
    "categoryName": "string",
    "plexClientId": "string"
}
```

### Record Preroll Completion
```http
POST /api/usage/complete
Content-Type: application/json

{
    "prerollId": "string",
    "playDuration": 120
}
```

### Get Usage Statistics
```http
GET /api/usage/stats?period=daily&days=30
GET /api/usage/stats/category/{categoryName}?period=daily&days=30
GET /api/usage/stats/preroll/{prerollId}?period=daily&days=30
```

### Get Top Prerolls
```http
GET /api/usage/top?limit=10&period=daily&days=30
```

### Get Usage Summary
```http
GET /api/usage/summary
```

## Integration Examples

### Basic Integration

```csharp
// In your existing controller
public class YourController : ControllerBase
{
    private readonly UsageIntegrationService _usageIntegration;

    public YourController(UsageIntegrationService usageIntegration)
    {
        _usageIntegration = usageIntegration;
    }

    [HttpPost("activate-preroll")]
    public async Task<IActionResult> ActivatePreroll([FromBody] ActivateRequest request)
    {
        // Your existing logic...

        // Add tracking
        await _usageIntegration.TrackPrerollPlayAsync(
            request.PrerollId,
            request.CategoryName,
            HttpContext
        );

        // Your existing response...
    }
}
```

### Advanced Integration with Completion Tracking

```csharp
[HttpPost("preroll-completed")]
public async Task<IActionResult> MarkCompleted([FromBody] CompletionRequest request)
{
    await _usageIntegration.TrackPrerollCompleteAsync(
        request.PrerollId,
        request.DurationSeconds
    );

    return Ok();
}
```

## Database Schema

The system automatically creates these tables:

### PrerollUsage Table
```sql
CREATE TABLE PrerollUsage (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PrerollId TEXT NOT NULL,
    CategoryName TEXT NOT NULL,
    PlayStartTime DATETIME NOT NULL,
    PlayDuration INTEGER,
    ClientIp TEXT,
    UserAgent TEXT,
    PlexClientId TEXT,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### DailyStats Table
```sql
CREATE TABLE DailyStats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date DATE NOT NULL,
    CategoryName TEXT NOT NULL,
    TotalPlays INTEGER DEFAULT 0,
    TotalWatchTime INTEGER DEFAULT 0,
    UniquePrerolls INTEGER DEFAULT 0,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(Date, CategoryName)
);
```

## Testing

### Test API Endpoints

1. **Record a play:**
```bash
curl -X POST http://localhost:8089/api/usage/play \
  -H "Content-Type: application/json" \
  -d '{"prerollId":"test-1","categoryName":"Test Category"}'
```

2. **Get statistics:**
```bash
curl http://localhost:8089/api/usage/stats?days=30
```

3. **Get top prerolls:**
```bash
curl http://localhost:8089/api/usage/top?limit=5
```

### Verify Database

Check your SQLite database:
```sql
SELECT COUNT(*) FROM PrerollUsage;
SELECT * FROM DailyStats ORDER BY Date DESC LIMIT 5;
```

## Performance Considerations

### Indexes
The system creates optimized indexes for:
- PrerollId lookups
- Category filtering
- Date range queries
- Play start time sorting

### Aggregation
Daily statistics are pre-aggregated for fast dashboard loading.

### Error Handling
All tracking operations include error handling to prevent breaking your main application flow.

## Monitoring

### Logs
Check application logs for:
- Database initialization messages
- Tracking events
- API usage statistics
- Any errors in the tracking system

### Health Checks
The system includes comprehensive error handling and logging for troubleshooting.

## Troubleshooting

### Common Issues

1. **Database not created:**
   - Check connection string in appsettings.json
   - Ensure write permissions to database file location

2. **API returns 404:**
   - Verify controller is registered in Program.cs
   - Check route configuration

3. **Tracking not working:**
   - Check logs for error messages
   - Verify service dependencies are injected correctly

### Debug Mode

Enable detailed logging by adding to appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "PlexPrerollManager.Services.UsageTrackingService": "Debug",
      "PlexPrerollManager.Controllers.UsageController": "Debug"
    }
  }
}
```

## Next Steps

1. **Test the integration** with your existing preroll activation code
2. **Verify frontend connectivity** - ensure the dashboard loads statistics
3. **Monitor performance** - check that tracking doesn't impact response times
4. **Set up automated cleanup** - consider archiving old usage data
5. **Add data export** - optional feature for data analysis

## Support

If you encounter issues:
1. Check the application logs
2. Verify all files are correctly added
3. Test API endpoints individually
4. Review the integration examples
5. Check database permissions and file paths

The usage statistics system is designed to be robust and non-intrusive, so it won't break your existing functionality even if there are issues with the tracking.