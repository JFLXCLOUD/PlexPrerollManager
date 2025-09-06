using System;

namespace Nexroll.Models
{
    public class PrerollUsage
    {
        public int Id { get; set; }
        public string PrerollId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public DateTime PlayStartTime { get; set; }
        public int? PlayDuration { get; set; } // in seconds, NULL if still playing
        public string? ClientIp { get; set; }
        public string? UserAgent { get; set; }
        public string? PlexClientId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}