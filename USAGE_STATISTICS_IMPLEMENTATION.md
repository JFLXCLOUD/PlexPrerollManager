# Usage Statistics Implementation Guide

## Overview
This guide provides a complete implementation for adding usage statistics to PlexPrerollManager. The feature will track preroll plays, user engagement, and provide detailed analytics.

## Database Schema

### PrerollUsage Table
```sql
CREATE TABLE PrerollUsage (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PrerollId TEXT NOT NULL,
    CategoryName TEXT NOT NULL,
    PlayStartTime DATETIME NOT NULL,
    PlayDuration INTEGER, -- in seconds, NULL if still playing
    ClientIp TEXT,
    UserAgent TEXT,
    PlexClientId TEXT,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PrerollId) REFERENCES Prerolls(Id)
);

-- Indexes for performance
CREATE INDEX idx_preroll_usage_preroll_id ON PrerollUsage(PrerollId);
CREATE INDEX idx_preroll_usage_category ON PrerollUsage(CategoryName);
CREATE INDEX idx_preroll_usage_start_time ON PrerollUsage(PlayStartTime);
CREATE INDEX idx_preroll_usage_date ON PrerollUsage(date(PlayStartTime));
```

### DailyStats Table (for aggregated data)
```sql
CREATE TABLE DailyStats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date DATE NOT NULL,
    CategoryName TEXT NOT NULL,
    TotalPlays INTEGER DEFAULT 0,
    TotalWatchTime INTEGER DEFAULT 0, -- in seconds
    UniquePrerolls INTEGER DEFAULT 0,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(Date, CategoryName)
);
```

## Backend API Implementation

### New API Endpoints

#### 1. Record Preroll Play
```
POST /api/usage/play
Content-Type: application/json

{
    "prerollId": "string",
    "categoryName": "string",
    "clientIp": "string",
    "userAgent": "string",
    "plexClientId": "string"
}
```

#### 2. Record Preroll Completion
```
POST /api/usage/complete
Content-Type: application/json

{
    "prerollId": "string",
    "playDuration": 120
}
```

#### 3. Get Usage Statistics
```
GET /api/usage/stats?period=daily&days=30
GET /api/usage/stats/category/{categoryName}?period=daily&days=30
GET /api/usage/stats/preroll/{prerollId}?period=daily&days=30
```

#### 4. Get Top Prerolls
```
GET /api/usage/top?limit=10&period=daily&days=30
```

#### 5. Get Usage Summary
```
GET /api/usage/summary
```

### Backend Implementation (C#)

#### Usage Tracking Service
```csharp
public class UsageTrackingService
{
    private readonly string _connectionString;

    public UsageTrackingService(string connectionString)
    {
        _connectionString = connectionString;
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
}
```

#### API Controller
```csharp
[ApiController]
[Route("api/usage")]
public class UsageController : ControllerBase
{
    private readonly UsageTrackingService _usageService;

    public UsageController(UsageTrackingService usageService)
    {
        _usageService = usageService;
    }

    [HttpPost("play")]
    public async Task<IActionResult> RecordPlay([FromBody] PlayRequest request)
    {
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

    [HttpPost("complete")]
    public async Task<IActionResult> RecordCompletion([FromBody] CompletionRequest request)
    {
        await _usageService.RecordPlayCompleteAsync(request.PrerollId, request.PlayDuration);
        return Ok();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string period = "daily", [FromQuery] int days = 30)
    {
        var stats = await _usageService.GetUsageStatsAsync(period, days);
        return Ok(stats);
    }

    [HttpGet("top")]
    public async Task<IActionResult> GetTopPrerolls([FromQuery] int limit = 10, [FromQuery] string period = "daily", [FromQuery] int days = 30)
    {
        var topPrerolls = await _usageService.GetTopPrerollsAsync(limit, period, days);
        return Ok(topPrerolls);
    }
}
```

## Frontend Implementation

### Statistics Dashboard Section
Add this to your dashboard.html:

```html
<div class='statistics-section'>
    <h2>📊 Usage Statistics</h2>

    <!-- Statistics Controls -->
    <div class='stats-controls'>
        <select id='statsPeriod'>
            <option value='daily'>Daily</option>
            <option value='weekly'>Weekly</option>
            <option value='monthly'>Monthly</option>
        </select>
        <select id='statsDays'>
            <option value='7'>Last 7 days</option>
            <option value='30'>Last 30 days</option>
            <option value='90'>Last 90 days</option>
        </select>
        <button class='btn' onclick='refreshStats()'>Refresh</button>
    </div>

    <!-- Statistics Cards -->
    <div class='stats-grid' id='statsGrid'>
        <div class='stats-card'>
            <h3>Total Plays</h3>
            <div class='stat-value' id='totalPlays'>0</div>
        </div>
        <div class='stats-card'>
            <h3>Total Watch Time</h3>
            <div class='stat-value' id='totalWatchTime'>0h 0m</div>
        </div>
        <div class='stats-card'>
            <h3>Unique Prerolls</h3>
            <div class='stat-value' id='uniquePrerolls'>0</div>
        </div>
        <div class='stats-card'>
            <h3>Avg. Play Duration</h3>
            <div class='stat-value' id='avgDuration'>0s</div>
        </div>
    </div>

    <!-- Charts Container -->
    <div class='charts-container'>
        <div class='chart-card'>
            <h3>Plays Over Time</h3>
            <canvas id='playsChart'></canvas>
        </div>
        <div class='chart-card'>
            <h3>Top Categories</h3>
            <canvas id='categoriesChart'></canvas>
        </div>
    </div>

    <!-- Top Prerolls Table -->
    <div class='top-prerolls-section'>
        <h3>🏆 Top Performing Prerolls</h3>
        <div class='top-prerolls-table' id='topPrerollsTable'>
            <div class='loading'>Loading top prerolls...</div>
        </div>
    </div>
</div>
```

### JavaScript Implementation
Add this JavaScript to your dashboard:

```javascript
// Statistics variables
let statsData = [];
let statsPeriod = 'daily';
let statsDays = 30;

// Load usage statistics
async function loadUsageStats() {
    try {
        const response = await fetch(`/api/usage/stats?period=${statsPeriod}&days=${statsDays}`);
        const data = await response.json();

        statsData = data.Data || [];
        updateStatsDisplay();
        updateCharts();

    } catch (error) {
        console.error('Error loading usage stats:', error);
        showError('Failed to load usage statistics');
    }
}

// Update statistics display
function updateStatsDisplay() {
    const totalPlays = statsData.reduce((sum, day) => sum + (day.Plays || 0), 0);
    const totalWatchTime = statsData.reduce((sum, day) => sum + (day.TotalWatchTime || 0), 0);
    const uniquePrerolls = new Set(statsData.flatMap(day => day.UniquePrerolls || [])).size;
    const avgDuration = totalPlays > 0 ? Math.round(totalWatchTime / totalPlays) : 0;

    document.getElementById('totalPlays').textContent = totalPlays.toLocaleString();
    document.getElementById('totalWatchTime').textContent = formatDuration(totalWatchTime);
    document.getElementById('uniquePrerolls').textContent = uniquePrerolls;
    document.getElementById('avgDuration').textContent = formatDuration(avgDuration);
}

// Update charts (requires Chart.js)
function updateCharts() {
    updatePlaysChart();
    updateCategoriesChart();
}

function updatePlaysChart() {
    const ctx = document.getElementById('playsChart').getContext('2d');

    const labels = statsData.map(day => new Date(day.Date).toLocaleDateString());
    const playsData = statsData.map(day => day.Plays || 0);

    new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Plays',
                data: playsData,
                borderColor: '#3b82f6',
                backgroundColor: 'rgba(59, 130, 246, 0.1)',
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { display: false }
            },
            scales: {
                y: { beginAtZero: true }
            }
        }
    });
}

function updateCategoriesChart() {
    const ctx = document.getElementById('categoriesChart').getContext('2d');

    // Aggregate by category
    const categoryStats = {};
    statsData.forEach(day => {
        const category = day.CategoryName;
        if (!categoryStats[category]) {
            categoryStats[category] = 0;
        }
        categoryStats[category] += day.Plays || 0;
    });

    const labels = Object.keys(categoryStats);
    const data = Object.values(categoryStats);

    new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: [
                    '#3b82f6', '#10b981', '#f59e0b', '#ef4444',
                    '#8b5cf6', '#06b6d4', '#84cc16', '#f97316'
                ]
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { position: 'bottom' }
            }
        }
    });
}

// Load top prerolls
async function loadTopPrerolls() {
    try {
        const response = await fetch(`/api/usage/top?limit=10&period=${statsPeriod}&days=${statsDays}`);
        const topPrerolls = await response.json();

        const table = document.getElementById('topPrerollsTable');
        if (topPrerolls.length === 0) {
            table.innerHTML = '<p>No preroll usage data available yet.</p>';
            return;
        }

        table.innerHTML = `
            <table>
                <thead>
                    <tr>
                        <th>Rank</th>
                        <th>Preroll</th>
                        <th>Category</th>
                        <th>Plays</th>
                        <th>Total Watch Time</th>
                        <th>Avg. Duration</th>
                    </tr>
                </thead>
                <tbody>
                    ${topPrerolls.map((preroll, index) => `
                        <tr>
                            <td>${index + 1}</td>
                            <td>${preroll.Name}</td>
                            <td>${preroll.CategoryName}</td>
                            <td>${preroll.Plays}</td>
                            <td>${formatDuration(preroll.TotalWatchTime)}</td>
                            <td>${formatDuration(Math.round(preroll.TotalWatchTime / preroll.Plays))}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        `;

    } catch (error) {
        console.error('Error loading top prerolls:', error);
        document.getElementById('topPrerollsTable').innerHTML = '<div class="error">Failed to load top prerolls</div>';
    }
}

// Statistics controls
function refreshStats() {
    statsPeriod = document.getElementById('statsPeriod').value;
    statsDays = parseInt(document.getElementById('statsDays').value);

    loadUsageStats();
    loadTopPrerolls();
}

// Initialize statistics on page load
document.addEventListener('DOMContentLoaded', () => {
    // ... existing initialization code ...

    // Add statistics initialization
    loadUsageStats();
    loadTopPrerolls();

    // Refresh stats every 5 minutes
    setInterval(() => {
        loadUsageStats();
        loadTopPrerolls();
    }, 5 * 60 * 1000);
});
```

## Usage Tracking Integration

### Track Preroll Plays
Add this to your existing preroll activation code:

```javascript
// When a preroll starts playing
async function trackPrerollPlay(prerollId, categoryName) {
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
        console.error('Error tracking preroll play:', error);
    }
}

// When a preroll finishes playing
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
```

## CSS Styling

Add this CSS to your dashboard for the statistics section:

```css
/* Statistics Section */
.statistics-section {
    background: rgba(15, 23, 42, 0.8);
    backdrop-filter: blur(20px);
    border-radius: 16px;
    padding: 30px;
    margin-bottom: 30px;
    box-shadow: 0 12px 24px rgba(0, 0, 0, 0.2);
    border: 1px solid rgba(255, 255, 255, 0.1);
}

.statistics-section h2 {
    color: #f1f5f9;
    font-size: 1.5em;
    font-weight: 600;
    margin-bottom: 20px;
    letter-spacing: -0.01em;
}

/* Statistics Controls */
.stats-controls {
    display: flex;
    gap: 15px;
    margin-bottom: 20px;
    align-items: center;
}

.stats-controls select {
    padding: 8px 12px;
    border: 1px solid rgba(255, 255, 255, 0.2);
    border-radius: 6px;
    background: rgba(30, 41, 59, 0.6);
    color: #f1f5f9;
    font-size: 14px;
}

/* Statistics Grid */
.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin-bottom: 30px;
}

.stats-card {
    background: rgba(30, 41, 59, 0.6);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 12px;
    padding: 20px;
    text-align: center;
    backdrop-filter: blur(10px);
    transition: all 0.3s ease;
}

.stats-card:hover {
    border-color: #3b82f6;
    background: rgba(59, 130, 246, 0.1);
    transform: translateY(-2px);
}

.stats-card h3 {
    color: #94a3b8;
    font-size: 0.9em;
    font-weight: 600;
    margin-bottom: 10px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.stat-value {
    color: #f1f5f9;
    font-size: 2em;
    font-weight: 700;
    margin: 0;
}

/* Charts Container */
.charts-container {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
    gap: 20px;
    margin-bottom: 30px;
}

.chart-card {
    background: rgba(30, 41, 59, 0.6);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 12px;
    padding: 20px;
    backdrop-filter: blur(10px);
}

.chart-card h3 {
    color: #f1f5f9;
    margin-bottom: 15px;
    font-size: 1.1em;
    font-weight: 600;
}

.chart-card canvas {
    max-height: 300px;
}

/* Top Prerolls Table */
.top-prerolls-section h3 {
    color: #f1f5f9;
    margin-bottom: 15px;
    font-size: 1.2em;
    font-weight: 600;
}

.top-prerolls-table table {
    width: 100%;
    border-collapse: collapse;
    background: rgba(30, 41, 59, 0.6);
    border-radius: 8px;
    overflow: hidden;
    border: 1px solid rgba(255, 255, 255, 0.1);
}

.top-prerolls-table th,
.top-prerolls-table td {
    padding: 12px 15px;
    text-align: left;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.top-prerolls-table th {
    background: rgba(51, 65, 85, 0.5);
    color: #f1f5f9;
    font-weight: 600;
    font-size: 0.9em;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.top-prerolls-table td {
    color: #e2e8f0;
    font-size: 0.9em;
}

.top-prerolls-table tr:hover {
    background: rgba(59, 130, 246, 0.05);
}

/* Responsive Design */
@media (max-width: 768px) {
    .stats-grid {
        grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    }

    .charts-container {
        grid-template-columns: 1fr;
    }

    .stats-controls {
        flex-direction: column;
        align-items: stretch;
    }

    .top-prerolls-table {
        overflow-x: auto;
    }
}
```

## Implementation Steps

1. **Database Setup**: Create the `PrerollUsage` and `DailyStats` tables
2. **Backend Services**: Implement the `UsageTrackingService` and API endpoints
3. **Frontend Updates**: Add the statistics dashboard HTML and JavaScript
4. **Usage Tracking**: Integrate play tracking into existing preroll activation code
5. **Styling**: Add the CSS for the statistics interface
6. **Testing**: Test the statistics collection and display

## Dependencies

Add these dependencies to your project:

```xml
<PackageReference Include="Dapper" Version="2.0.123" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="7.0.0" />
```

For charts, include Chart.js in your HTML:
```html
<script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
```

This implementation provides comprehensive usage statistics tracking and visualization for your PlexPrerollManager application.