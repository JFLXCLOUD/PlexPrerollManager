using System.ComponentModel.DataAnnotations;

namespace PlexPrerollManager.Models
{
    public class Schedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string Description { get; set; } = "";
        
        [Required]
        public string CategoryName { get; set; } = "";
        
        [Required]
        public DateTime StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        [Required]
        public ScheduleType Type { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime? LastExecuted { get; set; }
        
        public DateTime? NextExecution { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public string CreatedBy { get; set; } = "System";
    }

    public enum ScheduleType
    {
        OneTime,
        Daily,
        Weekly,
        Monthly,
        Yearly
    }

    public class CreateScheduleRequest
    {
        [Required]
        public string Description { get; set; } = "";
        
        [Required]
        public string CategoryName { get; set; } = "";
        
        [Required]
        public DateTime StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        [Required]
        public ScheduleType Type { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}