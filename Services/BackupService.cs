using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;

namespace PlexPrerollManager.Services
{
    public class BackupService
    {
        private readonly IConfiguration _configuration;
        private readonly string _backupsPath;

        public BackupService(IConfiguration configuration)
        {
            _configuration = configuration;
            _backupsPath = Path.Combine(AppContext.BaseDirectory, "Backups");
            Directory.CreateDirectory(_backupsPath);
        }

        public async Task<(bool Success, string Message, string? FilePath)> CreateBackupAsync()
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
                var backupFileName = $"PlexPrerollManager_Backup_{timestamp}.zip";
                var backupFilePath = Path.Combine(_backupsPath, backupFileName);

                Console.WriteLine($"[DEBUG] Creating backup: {backupFileName}");

                using var archive = ZipFile.Open(backupFilePath, ZipArchiveMode.Create);

                // Backup configuration files
                await BackupConfigurationAsync(archive);

                // Backup preroll files
                await BackupPrerollsAsync(archive);

                // Backup schedules
                await BackupSchedulesAsync(archive);

                // Backup usage statistics database
                await BackupDatabaseAsync(archive);

                // Create backup metadata
                var metadata = new
                {
                    Version = "2.2.0",
                    CreatedAt = DateTime.UtcNow,
                    BackupType = "Full",
                    Categories = await GetCategoryCountAsync(),
                    TotalVideos = await GetTotalVideoCountAsync(),
                    Schedules = await GetScheduleCountAsync()
                };

                var metadataEntry = archive.CreateEntry("backup_metadata.json");
                using var metadataStream = metadataEntry.Open();
                using var writer = new StreamWriter(metadataStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(metadata, Formatting.Indented));

                Console.WriteLine($"[DEBUG] Backup created successfully: {backupFilePath}");
                return (true, $"Backup created: {backupFileName}", backupFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create backup: {ex.Message}");
                return (false, $"Backup failed: {ex.Message}", null);
            }
        }

        public async Task<List<object>> GetBackupsAsync()
        {
            try
            {
                if (!Directory.Exists(_backupsPath))
                {
                    return new List<object>();
                }

                var backupFiles = Directory.GetFiles(_backupsPath, "*.zip");
                var backups = new List<object>();

                foreach (var file in backupFiles)
                {
                    var fileInfo = new FileInfo(file);
                    var metadata = await GetBackupMetadataAsync(file);

                    backups.Add(new
                    {
                        FilePath = file,
                        FileName = fileInfo.Name,
                        Timestamp = fileInfo.CreationTime,
                        FileSize = fileInfo.Length,
                        Version = metadata?.Version ?? "Unknown",
                        CategoryCount = metadata?.Categories ?? 0,
                        TotalVideos = metadata?.TotalVideos ?? 0,
                        BackupType = metadata?.BackupType ?? "Unknown"
                    });
                }

                return backups.OrderByDescending(b => ((DateTime)b.GetType().GetProperty("Timestamp")!.GetValue(b)!)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to get backups: {ex.Message}");
                return new List<object>();
            }
        }

        public async Task<(bool Success, string Message)> RestoreBackupAsync(string backupPath)
        {
            try
            {
                if (!System.IO.File.Exists(backupPath))
                {
                    return (false, "Backup file not found");
                }

                Console.WriteLine($"[DEBUG] Restoring backup: {backupPath}");

                // Create temporary restore directory
                var tempPath = Path.Combine(Path.GetTempPath(), $"PlexPrerollManager_Restore_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempPath);

                try
                {
                    // Extract backup
                    ZipFile.ExtractToDirectory(backupPath, tempPath);

                    // Restore configuration
                    await RestoreConfigurationAsync(tempPath);

                    // Restore prerolls
                    await RestorePrerollsAsync(tempPath);

                    // Restore schedules
                    await RestoreSchedulesAsync(tempPath);

                    // Restore database
                    await RestoreDatabaseAsync(tempPath);

                    Console.WriteLine($"[DEBUG] Backup restored successfully");
                    return (true, "Backup restored successfully");
                }
                finally
                {
                    // Clean up temporary directory
                    if (Directory.Exists(tempPath))
                    {
                        Directory.Delete(tempPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to restore backup: {ex.Message}");
                return (false, $"Restore failed: {ex.Message}");
            }
        }

        private async Task BackupConfigurationAsync(ZipArchive archive)
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (System.IO.File.Exists(configPath))
                {
                    var entry = archive.CreateEntry("config/appsettings.json");
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(configPath);
                    await fileStream.CopyToAsync(entryStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to backup configuration: {ex.Message}");
            }
        }

        private async Task BackupPrerollsAsync(ZipArchive archive)
        {
            try
            {
                var prerollsPath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                if (Directory.Exists(prerollsPath))
                {
                    await BackupDirectoryAsync(archive, prerollsPath, "prerolls");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to backup prerolls: {ex.Message}");
            }
        }

        private async Task BackupSchedulesAsync(ZipArchive archive)
        {
            try
            {
                var schedulesPath = Path.Combine(AppContext.BaseDirectory, "schedules.json");
                if (System.IO.File.Exists(schedulesPath))
                {
                    var entry = archive.CreateEntry("schedules.json");
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(schedulesPath);
                    await fileStream.CopyToAsync(entryStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to backup schedules: {ex.Message}");
            }
        }

        private async Task BackupDatabaseAsync(ZipArchive archive)
        {
            try
            {
                var dbPath = _configuration.GetConnectionString("DefaultConnection")?.Replace("Data Source=", "");
                if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                {
                    var entry = archive.CreateEntry("database/plexprerollmanager.db");
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(dbPath);
                    await fileStream.CopyToAsync(entryStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to backup database: {ex.Message}");
            }
        }

        private async Task BackupDirectoryAsync(ZipArchive archive, string sourcePath, string archivePath)
        {
            var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                var entryPath = $"{archivePath}/{relativePath.Replace('\\', '/')}";
                
                var entry = archive.CreateEntry(entryPath);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(file);
                await fileStream.CopyToAsync(entryStream);
            }
        }

        private async Task<dynamic?> GetBackupMetadataAsync(string backupPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(backupPath);
                var metadataEntry = archive.GetEntry("backup_metadata.json");
                
                if (metadataEntry != null)
                {
                    using var stream = metadataEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to read backup metadata: {ex.Message}");
            }
            
            return null;
        }

        private async Task RestoreConfigurationAsync(string tempPath)
        {
            var configSource = Path.Combine(tempPath, "config", "appsettings.json");
            var configTarget = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            
            if (System.IO.File.Exists(configSource))
            {
                File.Copy(configSource, configTarget, true);
                Console.WriteLine("[DEBUG] Configuration restored");
            }
        }

        private async Task RestorePrerollsAsync(string tempPath)
        {
            var prerollsSource = Path.Combine(tempPath, "prerolls");
            var prerollsTarget = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
            
            if (Directory.Exists(prerollsSource))
            {
                if (Directory.Exists(prerollsTarget))
                {
                    Directory.Delete(prerollsTarget, true);
                }
                
                CopyDirectory(prerollsSource, prerollsTarget);
                Console.WriteLine("[DEBUG] Prerolls restored");
            }
        }

        private async Task RestoreSchedulesAsync(string tempPath)
        {
            var schedulesSource = Path.Combine(tempPath, "schedules.json");
            var schedulesTarget = Path.Combine(AppContext.BaseDirectory, "schedules.json");
            
            if (System.IO.File.Exists(schedulesSource))
            {
                File.Copy(schedulesSource, schedulesTarget, true);
                Console.WriteLine("[DEBUG] Schedules restored");
            }
        }

        private async Task RestoreDatabaseAsync(string tempPath)
        {
            var dbSource = Path.Combine(tempPath, "database", "plexprerollmanager.db");
            var dbTarget = _configuration.GetConnectionString("DefaultConnection")?.Replace("Data Source=", "");
            
            if (System.IO.File.Exists(dbSource) && !string.IsNullOrEmpty(dbTarget))
            {
                var dbDir = Path.GetDirectoryName(dbTarget);
                if (!string.IsNullOrEmpty(dbDir))
                {
                    Directory.CreateDirectory(dbDir);
                }
                
                File.Copy(dbSource, dbTarget, true);
                Console.WriteLine("[DEBUG] Database restored");
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var targetSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, targetSubDir);
            }
        }

        private async Task<int> GetCategoryCountAsync()
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                return Directory.Exists(basePath) ? Directory.GetDirectories(basePath).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetTotalVideoCountAsync()
        {
            try
            {
                var basePath = _configuration["PrerollManager:PrerollsPath"] ?? Path.Combine(AppContext.BaseDirectory, "Prerolls");
                if (!Directory.Exists(basePath)) return 0;

                return Directory.GetDirectories(basePath)
                    .Sum(dir => Directory.GetFiles(dir, "*.*")
                        .Count(f => IsVideoFile(f)));
            }
            catch
            {
                return 0;
            }
        }

        private bool IsVideoFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };
            return videoExtensions.Contains(extension);
        }

        private async Task<int> GetScheduleCountAsync()
        {
            try
            {
                var schedulesPath = Path.Combine(AppContext.BaseDirectory, "schedules.json");
                if (!System.IO.File.Exists(schedulesPath)) return 0;

                var json = await System.IO.File.ReadAllTextAsync(schedulesPath);
                var schedules = JsonConvert.DeserializeObject<List<object>>(json);
                return schedules?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}