using System.Text.Json;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;

namespace NinjaLogs.IntegrationTests;

public sealed class ProviderParityRelationalIntegrationTests
{
    [Fact]
    public async Task Sqlite_SqlServer_Postgres_ShouldMatchCoreSemantics_WhenConfigured()
    {
        string? sqlServer = ReadConnection("SqlServer");
        string? postgres = ReadConnection("PostgreSQL");
        if (string.IsNullOrWhiteSpace(sqlServer) || string.IsNullOrWhiteSpace(postgres))
        {
            return;
        }

        string dbPath = Path.Combine(Path.GetTempPath(), $"ninjalogs-rel-parity-{Guid.NewGuid():N}.db");
        StorageOptions sqliteOptions = new()
        {
            Provider = "SQLite",
            Connections = new StorageConnectionOptions { SQLite = $"Data Source={dbPath}" }
        };
        StorageOptions ssOptions = new()
        {
            Provider = "SqlServer",
            Connections = new StorageConnectionOptions { SqlServer = sqlServer }
        };
        StorageOptions pgOptions = new()
        {
            Provider = "PostgreSQL",
            Connections = new StorageConnectionOptions { PostgreSQL = postgres }
        };

        ILogStorage sqlite = new SqliteLogStorage(new SqliteLogEventRepository(sqliteOptions));
        ILogStorage sqls = new SqlServerLogStorage(new SqlServerLogEventRepository(ssOptions));
        ILogStorage pgs = new PostgresLogStorage(new PostgresLogEventRepository(pgOptions));

        string trace = $"PARITY-{Guid.NewGuid():N}";
        await SeedAsync(sqlite, trace);
        await SeedAsync(sqls, trace);
        await SeedAsync(pgs, trace);

        LogQuery query = new(Level: LogLevel.Error, TraceId: trace, StatusCode: 500, Page: 1, PageSize: 50);
        int a = (await sqlite.QueryAsync(query)).TotalCount;
        int b = (await sqls.QueryAsync(query)).TotalCount;
        int c = (await pgs.QueryAsync(query)).TotalCount;

        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.Equal(1, a);
    }

    private static async Task SeedAsync(ILogStorage storage, string trace)
    {
        DateTime now = DateTime.UtcNow;
        await storage.AppendAsync(new LogEvent(now, LogLevel.Error, "match", "Svc", "Prod", null, null, TraceId: trace, StatusCode: 500));
        await storage.AppendAsync(new LogEvent(now, LogLevel.Information, "skip", "Svc", "Prod", null, null, TraceId: trace, StatusCode: 200));
    }

    private static string? ReadConnection(string key)
    {
        string? apiDir = FindApiDir();
        if (apiDir is null) return null;
        foreach (string file in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            string path = Path.Combine(apiDir, file);
            if (!File.Exists(path)) continue;
            using FileStream stream = File.OpenRead(path);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("Storage", out JsonElement storage) &&
                storage.TryGetProperty("Connections", out JsonElement conns) &&
                conns.TryGetProperty(key, out JsonElement val) &&
                val.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(val.GetString()))
            {
                return val.GetString();
            }
        }
        return null;
    }

    private static string? FindApiDir()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, "src", "NinjaLogs.Api");
            if (Directory.Exists(c)) return c;
            dir = dir.Parent;
        }
        return null;
    }
}
