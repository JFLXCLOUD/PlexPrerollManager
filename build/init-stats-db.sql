-- PlexPrerollManager Usage Statistics Database Initialization
-- This script creates the necessary tables for tracking preroll usage statistics

-- PrerollUsage table: Tracks individual preroll plays
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
);

-- DailyStats table: Aggregated daily statistics
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
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_preroll_usage_preroll_id ON PrerollUsage(PrerollId);
CREATE INDEX IF NOT EXISTS idx_preroll_usage_category ON PrerollUsage(CategoryName);
CREATE INDEX IF NOT EXISTS idx_preroll_usage_start_time ON PrerollUsage(PlayStartTime);
CREATE INDEX IF NOT EXISTS idx_preroll_usage_date ON PrerollUsage(date(PlayStartTime));

-- Insert sample data for testing (optional)
-- INSERT INTO PrerollUsage (PrerollId, CategoryName, PlayStartTime, PlayDuration, ClientIp, UserAgent, PlexClientId)
-- VALUES ('sample-preroll-1', 'Movies', datetime('now', '-1 day'), 30, '192.168.1.100', 'Plex/1.0', 'client-123');

-- INSERT INTO DailyStats (Date, CategoryName, TotalPlays, TotalWatchTime, UniquePrerolls)
-- VALUES (date('now', '-1 day'), 'Movies', 5, 150, 3);