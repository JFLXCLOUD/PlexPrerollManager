using System.Collections.Generic;

namespace Nexroll.Models
{
    public class UsageStats
    {
        public IEnumerable<dynamic> Data { get; set; } = new List<dynamic>();
    }

    public class TopPreroll
    {
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int Plays { get; set; }
        public int TotalWatchTime { get; set; }
    }

    public class PlayRequest
    {
        public string PrerollId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? PlexClientId { get; set; }
    }

    public class CompletionRequest
    {
        public string PrerollId { get; set; } = string.Empty;
        public int PlayDuration { get; set; }
    }
}