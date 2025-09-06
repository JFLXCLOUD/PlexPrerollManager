using System;

namespace Nexroll.Models
{
    public class DailyStats
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int TotalPlays { get; set; } = 0;
        public int TotalWatchTime { get; set; } = 0; // in seconds
        public int UniquePrerolls { get; set; } = 0;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}