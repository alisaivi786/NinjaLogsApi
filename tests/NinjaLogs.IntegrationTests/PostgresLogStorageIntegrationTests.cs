using System.Text.Json;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;

namespace NinjaLogs.IntegrationTests;

public sealed class PostgresLogStorageIntegrationTests
{
    [Fact]
    public async Task InsertAndQuery_ShouldWork_WhenConnectionStringIsConfigured()
    {
        string? connectionString = ResolvePostgresConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        StorageOptions options = new()
        {
            Provider = "PostgreSQL",
            Connections = new StorageConnectionOptions
            {
                PostgreSQL = connectionString
            }
        };

        PostgresLogEventRepository repository = new(options);

        string traceId = $"TRACE-PG-{Guid.NewGuid():N}";
        LogEvent expected = new(
            TimestampUtc: DateTime.UtcNow,
            Level: LogLevel.Warning,
            Message: "PostgreSQL integration test event",
            ServiceName: "PgService",
            Environment: "Test",
            Exception: null,
            PropertiesJson: "{\"source\":\"integration\"}",
            TraceId: traceId,
            CorrelationId: $"CORR-{Guid.NewGuid():N}",
            RequestMethod: "GET",
            StatusCode: 200);

        await repository.InsertAsync(expected);

        PagedResult<LogEvent> result = await repository.SearchAsync(new LogQuery(
            TraceId: traceId,
            Page: 1,
            PageSize: 10));

        Assert.True(result.TotalCount >= 1);
        LogEvent log = Assert.Single(result.Items);
        Assert.Equal(expected.Message, log.Message);
        Assert.Equal(expected.TraceId, log.TraceId);
        Assert.Equal(expected.ServiceName, log.ServiceName);
    }

    private static string? ResolvePostgresConnectionString()
    {
        string? apiProjectDir = FindApiProjectDirectory();
        if (string.IsNullOrWhiteSpace(apiProjectDir))
        {
            return null;
        }

        string devConfigPath = Path.Combine(apiProjectDir, "appsettings.Development.json");
        string baseConfigPath = Path.Combine(apiProjectDir, "appsettings.json");

        string? fromDev = ReadPostgresConnectionFromFile(devConfigPath);
        if (!string.IsNullOrWhiteSpace(fromDev))
        {
            return fromDev.Trim();
        }

        string? fromBase = ReadPostgresConnectionFromFile(baseConfigPath);
        return string.IsNullOrWhiteSpace(fromBase) ? null : fromBase.Trim();
    }

    private static string? FindApiProjectDirectory()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "NinjaLogs.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string? ReadPostgresConnectionFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using FileStream stream = File.OpenRead(path);
        using JsonDocument doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("Storage", out JsonElement storage))
        {
            return null;
        }

        if (storage.TryGetProperty("Connections", out JsonElement connections) &&
            connections.TryGetProperty("PostgreSQL", out JsonElement pg) &&
            pg.ValueKind == JsonValueKind.String)
        {
            return pg.GetString();
        }

        return null;
    }
}
