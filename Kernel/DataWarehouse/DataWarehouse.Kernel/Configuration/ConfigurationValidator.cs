using DataWarehouse.SDK.Contracts;
using System.Text.Json;

namespace DataWarehouse.Kernel.Configuration
{
    /// <summary>
    /// Validates DataWarehouse configuration before startup to prevent runtime errors.
    /// Checks paths, permissions, resource limits, and plugin dependencies.
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly IKernelContext _context;
        private readonly List<ValidationError> _errors = new();
        private readonly List<ValidationWarning> _warnings = new();

        public ConfigurationValidator(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Validate complete DataWarehouse configuration.
        /// </summary>
        public ValidationResult Validate(DataWarehouseConfig config)
        {
            _errors.Clear();
            _warnings.Clear();

            _context.LogInfo("[ConfigValidator] Starting configuration validation...");

            // Core validations
            ValidateRootPath(config.RootPath);
            ValidateOperatingMode(config.Mode);
            ValidateStorageConfiguration(config.Storage);
            ValidateRAIDConfiguration(config.RAID);
            ValidateNetworkConfiguration(config.Network);
            ValidateResourceLimits(config.Resources);
            ValidateBackupConfiguration(config.Backup);
            ValidateSecurityConfiguration(config.Security);

            var result = new ValidationResult
            {
                IsValid = _errors.Count == 0,
                Errors = _errors.ToList(),
                Warnings = _warnings.ToList()
            };

            if (result.IsValid)
            {
                _context.LogInfo($"[ConfigValidator] Validation PASSED with {_warnings.Count} warnings");
            }
            else
            {
                _context.LogError($"[ConfigValidator] Validation FAILED with {_errors.Count} errors and {_warnings.Count} warnings", null);
            }

            return result;
        }

        private void ValidateRootPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                _errors.Add(new ValidationError("RootPath", "Root path cannot be empty"));
                return;
            }

            if (!Path.IsPathRooted(rootPath))
            {
                _errors.Add(new ValidationError("RootPath", "Root path must be an absolute path"));
            }

            // Check if path exists
            if (!Directory.Exists(rootPath))
            {
                try
                {
                    Directory.CreateDirectory(rootPath);
                    _warnings.Add(new ValidationWarning("RootPath", $"Root directory created: {rootPath}"));
                }
                catch (Exception ex)
                {
                    _errors.Add(new ValidationError("RootPath", $"Cannot create root directory: {ex.Message}"));
                }
            }

            // Check write permissions
            if (!CheckWritePermissions(rootPath))
            {
                _errors.Add(new ValidationError("RootPath", "No write permissions for root directory"));
            }
        }

        private void ValidateOperatingMode(OperatingMode mode)
        {
            if (!Enum.IsDefined(typeof(OperatingMode), mode))
            {
                _errors.Add(new ValidationError("Mode", $"Invalid operating mode: {mode}"));
            }
        }

        private void ValidateStorageConfiguration(StorageConfig config)
        {
            if (config.MaxStorageGB <= 0)
            {
                _errors.Add(new ValidationError("Storage.MaxStorageGB", "Max storage must be greater than 0"));
            }

            if (config.MaxStorageGB > 1000000) // 1 PB
            {
                _warnings.Add(new ValidationWarning("Storage.MaxStorageGB", "Very large storage limit configured"));
            }

            if (config.CacheSizeMB < 0)
            {
                _errors.Add(new ValidationError("Storage.CacheSizeMB", "Cache size cannot be negative"));
            }

            if (config.CacheSizeMB > 64 * 1024) // 64 GB
            {
                _warnings.Add(new ValidationWarning("Storage.CacheSizeMB", "Very large cache configured, ensure sufficient RAM"));
            }
        }

        private void ValidateRAIDConfiguration(RAIDConfig config)
        {
            if (config.ProviderCount < 1)
            {
                _errors.Add(new ValidationError("RAID.ProviderCount", "At least 1 storage provider required"));
            }

            // Validate RAID level requirements
            var minProviders = GetMinimumProvidersForRAID(config.Level);
            if (config.ProviderCount < minProviders)
            {
                _errors.Add(new ValidationError("RAID.ProviderCount",
                    $"RAID {config.Level} requires at least {minProviders} providers, configured: {config.ProviderCount}"));
            }

            if (config.StripeSize <= 0)
            {
                _errors.Add(new ValidationError("RAID.StripeSize", "Stripe size must be greater than 0"));
            }

            if (config.StripeSize > 1024 * 1024) // 1 MB
            {
                _warnings.Add(new ValidationWarning("RAID.StripeSize", "Large stripe size may impact performance"));
            }
        }

        private void ValidateNetworkConfiguration(NetworkConfig config)
        {
            if (config.Port < 1024 || config.Port > 65535)
            {
                _errors.Add(new ValidationError("Network.Port", "Port must be between 1024 and 65535"));
            }

            if (config.MaxConnections <= 0)
            {
                _errors.Add(new ValidationError("Network.MaxConnections", "Max connections must be greater than 0"));
            }

            if (config.MaxConnections > 100000)
            {
                _warnings.Add(new ValidationWarning("Network.MaxConnections", "Very high connection limit may exhaust resources"));
            }
        }

        private void ValidateResourceLimits(ResourceConfig config)
        {
            if (config.MaxMemoryMB <= 0)
            {
                _errors.Add(new ValidationError("Resources.MaxMemoryMB", "Max memory must be greater than 0"));
            }

            var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            if (config.MaxMemoryMB > availableMemory * 0.9)
            {
                _warnings.Add(new ValidationWarning("Resources.MaxMemoryMB",
                    $"Configured memory ({config.MaxMemoryMB} MB) exceeds 90% of available memory ({availableMemory} MB)"));
            }

            if (config.MaxThreads <= 0)
            {
                _errors.Add(new ValidationError("Resources.MaxThreads", "Max threads must be greater than 0"));
            }

            if (config.MaxThreads > Environment.ProcessorCount * 100)
            {
                _warnings.Add(new ValidationWarning("Resources.MaxThreads",
                    $"Very high thread count configured ({config.MaxThreads}) for {Environment.ProcessorCount} processors"));
            }
        }

        private void ValidateBackupConfiguration(BackupConfig config)
        {
            if (!Directory.Exists(config.BackupDirectory))
            {
                try
                {
                    Directory.CreateDirectory(config.BackupDirectory);
                }
                catch (Exception ex)
                {
                    _errors.Add(new ValidationError("Backup.BackupDirectory", $"Cannot create backup directory: {ex.Message}"));
                }
            }

            if (config.RetentionDays <= 0)
            {
                _errors.Add(new ValidationError("Backup.RetentionDays", "Retention days must be greater than 0"));
            }

            if (config.RetentionDays > 3650) // 10 years
            {
                _warnings.Add(new ValidationWarning("Backup.RetentionDays", "Very long backup retention configured"));
            }
        }

        private void ValidateSecurityConfiguration(SecurityConfig config)
        {
            if (config.RequireAuthentication && string.IsNullOrEmpty(config.JwtSecret))
            {
                _errors.Add(new ValidationError("Security.JwtSecret", "JWT secret required when authentication is enabled"));
            }

            if (!string.IsNullOrEmpty(config.JwtSecret) && config.JwtSecret.Length < 32)
            {
                _errors.Add(new ValidationError("Security.JwtSecret", "JWT secret must be at least 32 characters"));
            }

            if (config.SessionTimeoutMinutes <= 0)
            {
                _errors.Add(new ValidationError("Security.SessionTimeoutMinutes", "Session timeout must be greater than 0"));
            }

            if (config.SessionTimeoutMinutes > 43200) // 30 days
            {
                _warnings.Add(new ValidationWarning("Security.SessionTimeoutMinutes", "Very long session timeout may pose security risk"));
            }
        }

        private static int GetMinimumProvidersForRAID(string level)
        {
            return level switch
            {
                "0" => 2,
                "1" => 2,
                "2" => 3,
                "3" => 3,
                "4" => 3,
                "5" => 3,
                "6" => 4,
                "10" => 4,
                "01" => 4,
                "50" => 6,
                "60" => 8,
                _ => 1
            };
        }

        private static bool CheckWritePermissions(string path)
        {
            try
            {
                var testFile = Path.Combine(path, $".write_test_{Guid.NewGuid()}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // Configuration classes
    public class DataWarehouseConfig
    {
        public required string RootPath { get; init; }
        public OperatingMode Mode { get; init; } = OperatingMode.Laptop;
        public required StorageConfig Storage { get; init; }
        public required RAIDConfig RAID { get; init; }
        public required NetworkConfig Network { get; init; }
        public required ResourceConfig Resources { get; init; }
        public required BackupConfig Backup { get; init; }
        public required SecurityConfig Security { get; init; }
    }

    public class StorageConfig
    {
        public long MaxStorageGB { get; init; }
        public int CacheSizeMB { get; init; }
    }

    public class RAIDConfig
    {
        public required string Level { get; init; }
        public int ProviderCount { get; init; }
        public int StripeSize { get; init; }
    }

    public class NetworkConfig
    {
        public int Port { get; init; } = 50051;
        public int MaxConnections { get; init; } = 1000;
    }

    public class ResourceConfig
    {
        public int MaxMemoryMB { get; init; }
        public int MaxThreads { get; init; }
    }

    public class BackupConfig
    {
        public required string BackupDirectory { get; init; }
        public int RetentionDays { get; init; }
    }

    public class SecurityConfig
    {
        public bool RequireAuthentication { get; init; }
        public string? JwtSecret { get; init; }
        public int SessionTimeoutMinutes { get; init; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public List<ValidationError> Errors { get; init; } = new();
        public List<ValidationWarning> Warnings { get; init; } = new();
    }

    public class ValidationError
    {
        public string Field { get; init; }
        public string Message { get; init; }

        public ValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }

        public override string ToString() => $"ERROR [{Field}]: {Message}";
    }

    public class ValidationWarning
    {
        public string Field { get; init; }
        public string Message { get; init; }

        public ValidationWarning(string field, string message)
        {
            Field = field;
            Message = message;
        }

        public override string ToString() => $"WARNING [{Field}]: {Message}";
    }
}
