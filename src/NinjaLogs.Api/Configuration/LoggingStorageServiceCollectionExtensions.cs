using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Application.Services;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.File;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;
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
        services.AddScoped<ILogIngestionService, LogIngestionService>();
        services.AddScoped<ILogQueryService, LogQueryService>();

        StorageOptions storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
        string provider = storage.GetNormalizedProvider();

        switch (provider)
        {
            case "file":
                services.AddScoped<ILogStorage, FileLogStorage>();
                break;
            case "sqlite":
                services.AddScoped<SqliteLogEventRepository>();
                services.AddScoped<ILogStorage, SqliteLogStorage>();
                break;
            case "sqlserver":
                services.AddScoped<SqlServerLogEventRepository>();
                services.AddScoped<ILogStorage, SqlServerLogStorage>();
                break;
            case "postgresql":
                services.AddScoped<PostgresLogEventRepository>();
                services.AddScoped<ILogStorage, PostgresLogStorage>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported storage provider '{storage.Provider}'. Supported values: File, SQLite, SqlServer, PostgreSQL.");
        }

        return services;
    }
}
