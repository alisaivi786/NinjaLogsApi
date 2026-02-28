namespace NinjaLogs.Modules.Logging.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "File";

    public string? ConnectionString { get; set; }

    public string LogsDirectory { get; set; } = "logs";

    public StorageConnectionOptions Connections { get; set; } = new();
    public SegmentedFileStorageOptions SegmentedFile { get; set; } = new();
    public StorageRetentionOptions Retention { get; set; } = new();
    public StoragePolicyOptions Policy { get; set; } = new();

    public string GetNormalizedProvider() => Provider.Trim().ToLowerInvariant();
}

public sealed class StorageConnectionOptions
{
    public string? SQLite { get; set; }
    public string? SqlServer { get; set; }
    public string? PostgreSQL { get; set; }
}

public sealed class SegmentedFileStorageOptions
{
    public string DataDirectory { get; set; } = "data";
    public long SegmentMaxBytes { get; set; } = 256L * 1024 * 1024;
    public string ManifestFileName { get; set; } = "manifest.json";
}

public sealed class StorageRetentionOptions
{
    public bool Enabled { get; set; } = false;
    public int RetainDays { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 60;
}

public sealed class StoragePolicyOptions
{
    public bool EnforceForDatabaseProviders { get; set; } = true;
    public bool RequireRetentionEnabledForDatabaseProviders { get; set; } = true;
    public int UnlicensedMaxRetentionDays { get; set; } = 30;
    public int LicensedMaxRetentionDays { get; set; } = 3650;
    public bool EnforceLicenseTotalQuota { get; set; } = true;
    public long UnlicensedMaxTotalBytes { get; set; } = 500L * 1024 * 1024;
    public long LicensedMaxTotalBytes { get; set; } = 10L * 1024 * 1024 * 1024;
    public int QuotaExceededStatusCode { get; set; } = 429;
    public bool EnforceLicenseSegmentCap { get; set; } = false;
    public long UnlicensedSegmentMaxBytes { get; set; } = 256L * 1024 * 1024;
    public long LicensedSegmentMaxBytes { get; set; } = 1024L * 1024 * 1024;
}
