namespace NinjaLogs.Modules.Logging.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "File";

    public string? ConnectionString { get; set; }

    public string LogsDirectory { get; set; } = "logs";

    public StorageConnectionOptions Connections { get; set; } = new();

    public string GetNormalizedProvider() => Provider.Trim().ToLowerInvariant();
}

public sealed class StorageConnectionOptions
{
    public string? SQLite { get; set; }
    public string? SqlServer { get; set; }
    public string? PostgreSQL { get; set; }
}
