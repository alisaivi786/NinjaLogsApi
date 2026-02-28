using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Application.Services;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.File;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Repositories;

namespace NinjaLogs.Api.Configuration;

public static class LoggingStorageServiceCollectionExtensions
{
    public static IServiceCollection AddLoggingStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value);
        services.AddSingleton<StorageRuntimeMetrics>();
        services.AddSingleton<ILogIngestionQueue, DurableSpoolIngestionQueue>();
        services.AddSingleton<ILogQueryPlanner, DefaultLogQueryPlanner>();
        services.AddSingleton<IngestionProtectionService>();
        services.AddSingleton<IngestionQuotaCoordinator>();
        services.AddSingleton<LogDataSanitizer>();
        services.AddHostedService<QueuedLogWriterBackgroundService>();
        services.AddHostedService<StorageRetentionBackgroundService>();
        services.AddScoped<ILogIngestionService, LogIngestionService>();
        services.AddScoped<ILogQueryService, LogQueryService>();
        services.AddScoped<DeadLetterReplayService>();

        StorageOptions storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
        string provider = storage.GetNormalizedProvider();

        switch (provider)
        {
            case "file":
                services.AddScoped<ILogStorage, FileLogStorage>();
                services.AddSingleton<ILogIndexStrategy>(_ =>
                    new ProviderLogIndexStrategy("file",
                    [
                        "TimestampUtc scan",
                        "In-memory filter evaluation"
                    ]));
                break;
            case "sqlite":
                services.AddScoped<SqliteLogEventRepository>();
                services.AddScoped<ILogStorage, SqliteLogStorage>();
                services.AddSingleton<ILogIndexStrategy>(_ =>
                    new ProviderLogIndexStrategy("sqlite",
                    [
                        "idx_logs_timestamp(TimestampUtc)",
                        "idx_logs_level(Level)",
                        "idx_logs_service(ServiceName)",
                        "idx_logs_trace(TraceId)",
                        "idx_logs_correlation(CorrelationId)"
                    ]));
                break;
            case "segmentedfile":
                services.AddScoped<ILogStorage, SegmentedFileLogStorage>();
                services.AddSingleton<ILogIndexStrategy>(_ =>
                    new ProviderLogIndexStrategy("segmentedfile",
                    [
                        "Segment manifest index",
                        "Sequential segment scan",
                        "Per-segment append offset tracking"
                    ]));
                break;
            case "sqlserver":
                services.AddScoped<SqlServerLogEventRepository>();
                services.AddScoped<ILogStorage, SqlServerLogStorage>();
                services.AddSingleton<ILogIndexStrategy>(_ =>
                    new ProviderLogIndexStrategy("sqlserver",
                    [
                        "IX_Logs_TimestampUtc(TimestampUtc DESC)",
                        "IX_Logs_Level(Level)",
                        "IX_Logs_ServiceName(ServiceName)",
                        "IX_Logs_TraceId(TraceId)",
                        "IX_Logs_CorrelationId(CorrelationId)"
                    ]));
                break;
            case "postgresql":
                services.AddScoped<PostgresLogEventRepository>();
                services.AddScoped<ILogStorage, PostgresLogStorage>();
                services.AddSingleton<ILogIndexStrategy>(_ =>
                    new ProviderLogIndexStrategy("postgresql",
                    [
                        "Planned: btree(TimestampUtc, Level, ServiceName)",
                        "Planned: gin(PropertiesJson)"
                    ]));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported storage provider '{storage.Provider}'. Supported values: File, SQLite, SegmentedFile, SqlServer, PostgreSQL.");
        }

        return services;
    }
}
