using PlexPrerollManager.Models;
using Newtonsoft.Json;
using System.IO;

namespace PlexPrerollManager.Services
{
    public class SchedulingService
    {
        private readonly string _schedulesPath;
        private readonly PlexApiService _plexApiService;
        private readonly IConfiguration _configuration;

        public SchedulingService(IConfiguration configuration, PlexApiService plexApiService)
        {
            _configuration = configuration;
            _plexApiService = plexApiService;
            _schedulesPath = Path.Combine(AppContext.BaseDirectory, "schedules.json");
        }

        public async Task<List<Schedule>> GetSchedulesAsync()
        {
            try
            {
                if (!System.IO.File.Exists(_schedulesPath))
                {
                    return new List<Schedule>();
                }

                var json = await System.IO.File.ReadAllTextAsync(_schedulesPath);
                return JsonConvert.DeserializeObject<List<Schedule>>(json) ?? new List<Schedule>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load schedules: {ex.Message}");
                return new List<Schedule>();
            }
        }

        public async Task<bool> SaveSchedulesAsync(List<Schedule> schedules)
        {
            try
            {
                var json = JsonConvert.SerializeObject(schedules, Formatting.Indented);
                await System.IO.File.WriteAllTextAsync(_schedulesPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save schedules: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string Message, Schedule? Schedule)> CreateScheduleAsync(CreateScheduleRequest request)
        {
            try
            {
                var schedule = new Schedule
                {
                    Description = request.Description,
                    CategoryName = request.CategoryName,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Type = request.Type,
                    IsActive = request.IsActive,
                    NextExecution = CalculateNextExecution(request.StartDate, request.Type)
                };

                var schedules = await GetSchedulesAsync();
                schedules.Add(schedule);

                var success = await SaveSchedulesAsync(schedules);
                if (success)
                {
                    Console.WriteLine($"[DEBUG] Created schedule: {schedule.Description} for category {schedule.CategoryName}");
                    return (true, "Schedule created successfully", schedule);
                }
                else
                {
                    return (false, "Failed to save schedule", null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error creating schedule: {ex.Message}");
                return (false, $"Failed to create schedule: {ex.Message}", null);
            }
        }

        public async Task<bool> DeleteScheduleAsync(string scheduleId)
        {
            try
            {
                var schedules = await GetSchedulesAsync();
                var schedule = schedules.FirstOrDefault(s => s.Id == scheduleId);
                
                if (schedule == null)
                {
                    return false;
                }

                schedules.Remove(schedule);
                var success = await SaveSchedulesAsync(schedules);
                
                if (success)
                {
                    Console.WriteLine($"[DEBUG] Deleted schedule: {schedule.Description}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error deleting schedule: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Schedule>> GetActiveSchedulesAsync()
        {
            var schedules = await GetSchedulesAsync();
            var now = DateTime.UtcNow;

            return schedules.Where(s => 
                s.IsActive && 
                s.StartDate <= now && 
                (s.EndDate == null || s.EndDate >= now)
            ).ToList();
        }

        public async Task ProcessSchedulesAsync()
        {
            try
            {
                var activeSchedules = await GetActiveSchedulesAsync();
                var now = DateTime.UtcNow;

                foreach (var schedule in activeSchedules)
                {
                    if (schedule.NextExecution <= now)
                    {
                        Console.WriteLine($"[DEBUG] Executing schedule: {schedule.Description}");
                        
                        // Execute the schedule (activate the category)
                        await ExecuteScheduleAsync(schedule);
                        
                        // Update next execution time
                        schedule.LastExecuted = now;
                        schedule.NextExecution = CalculateNextExecution(now, schedule.Type);
                        
                        // If it's a one-time schedule, deactivate it
                        if (schedule.Type == ScheduleType.OneTime)
                        {
                            schedule.IsActive = false;
                        }
                    }
                }

                // Save updated schedules
                await SaveSchedulesAsync(activeSchedules.Concat(
                    (await GetSchedulesAsync()).Where(s => !activeSchedules.Any(a => a.Id == s.Id))
                ).ToList());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error processing schedules: {ex.Message}");
            }
        }

        private async Task ExecuteScheduleAsync(Schedule schedule)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Executing schedule '{schedule.Description}' - activating category '{schedule.CategoryName}'");

                // Get video files for the category
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                var categoryPath = Path.Combine(basePath, schedule.CategoryName);

                if (!Directory.Exists(categoryPath))
                {
                    Console.WriteLine($"[WARNING] Category directory does not exist: {categoryPath}");
                    return;
                }

                var videoFiles = Directory.GetFiles(categoryPath, "*.*")
                    .Where(f => IsVideoFile(f))
                    .ToList();

                if (videoFiles.Count == 0)
                {
                    Console.WriteLine($"[WARNING] No video files found in category: {schedule.CategoryName}");
                    return;
                }

                // Set the preroll using the Plex API service
                var result = await _plexApiService.SetPrerollAsync(schedule.CategoryName, videoFiles);
                
                if (result.Success)
                {
                    Console.WriteLine($"[DEBUG] Successfully executed schedule: {result.Message}");
                }
                else
                {
                    Console.WriteLine($"[WARNING] Schedule execution failed: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error executing schedule: {ex.Message}");
            }
        }

        private bool IsVideoFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
            return videoExtensions.Contains(extension);
        }

        private DateTime? CalculateNextExecution(DateTime baseDate, ScheduleType type)
        {
            return type switch
            {
                ScheduleType.OneTime => null,
                ScheduleType.Daily => baseDate.AddDays(1),
                ScheduleType.Weekly => baseDate.AddDays(7),
                ScheduleType.Monthly => baseDate.AddMonths(1),
                ScheduleType.Yearly => baseDate.AddYears(1),
                _ => null
            };
        }
    }
}