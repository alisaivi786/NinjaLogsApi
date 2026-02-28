using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;
using System.Text.Json;

namespace NinjaLogs.IntegrationTests;

public sealed class SQLiteLogStorageIntegrationTests
{
    [Fact]
    public async Task InsertAndQuery_ShouldRoundTripExtendedLogFields()
    {
        SqliteLogEventRepository repository = new(CreateSqliteStorageOptionsFromAppSettings());

        string traceId = $"TRACE-IT-{Guid.NewGuid():N}";
        string correlationId = $"CORR-IT-{Guid.NewGuid():N}";

        LogEvent expected = new(
            TimestampUtc: DateTime.UtcNow,
            Level: LogLevel.Error,
            Message: "SQLite integration test event",
            ServiceName: "BillingService",
            Environment: "Test",
            Exception: "System.Exception: test",
            PropertiesJson: "{\"orderId\":\"123\"}",
            EventId: "PAYMENT_FAILED",
            SourceContext: "Billing.Payments",
            RequestId: $"REQ-{Guid.NewGuid():N}",
            CorrelationId: correlationId,
            TraceId: traceId,
            SpanId: "SPAN-IT-1",
            UserId: "user-1",
            UserName: "user@example.com",
            ClientIp: "127.0.0.1",
            UserAgent: "xUnit",
            MachineName: "ci-machine",
            Application: "NinjaLogs.Tests",
            Version: "1.0.0",
            RequestPath: "/api/payments",
            RequestMethod: "POST",
            StatusCode: 500,
            DurationMs: 42.5,
            RequestHeadersJson: "{\"x-request-id\":\"REQ-IT-1\"}",
            ResponseHeadersJson: "{\"content-type\":\"application/json\"}",
            RequestBody: "{\"amount\":10}",
            ResponseBody: "{\"error\":\"failed\"}");

        await repository.InsertAsync(expected);

        PagedResult<LogEvent> result = await repository.SearchAsync(new LogQuery(
            TraceId: traceId,
            CorrelationId: correlationId,
            Page: 1,
            PageSize: 10));

        Assert.True(result.TotalCount >= 1);
        LogEvent actual = Assert.Single(result.Items);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.ServiceName, actual.ServiceName);
        Assert.Equal(expected.TraceId, actual.TraceId);
        Assert.Equal(expected.CorrelationId, actual.CorrelationId);
        Assert.Equal(expected.RequestMethod, actual.RequestMethod);
        Assert.Equal(expected.StatusCode, actual.StatusCode);
        Assert.Equal(expected.PropertiesJson, actual.PropertiesJson);
    }

    [Fact]
    public async Task QueryFilters_ShouldApplyLevelServiceAndStatusCode()
    {
        SqliteLogEventRepository repository = new(CreateSqliteStorageOptionsFromAppSettings());

        string serviceName = $"SvcB-{Guid.NewGuid():N}";
        await repository.InsertAsync(new LogEvent(DateTime.UtcNow, LogLevel.Information, "Info", "SvcA", "Test", null, null, StatusCode: 200));
        await repository.InsertAsync(new LogEvent(DateTime.UtcNow, LogLevel.Error, "Error", serviceName, "Test", null, null, StatusCode: 500));

        PagedResult<LogEvent> filtered = await repository.SearchAsync(new LogQuery(
            Level: LogLevel.Error,
            ServiceName: serviceName,
            StatusCode: 500,
            Page: 1,
            PageSize: 10));

        LogEvent only = Assert.Single(filtered.Items);
        Assert.Equal(LogLevel.Error, only.Level);
        Assert.Equal(serviceName, only.ServiceName);
        Assert.Equal(500, only.StatusCode);
    }

    private static StorageOptions CreateSqliteStorageOptionsFromAppSettings()
    {
        string? apiProjectDir = FindApiProjectDirectory();
        if (string.IsNullOrWhiteSpace(apiProjectDir))
        {
            throw new InvalidOperationException("Could not locate src/NinjaLogs.Api directory from test runtime.");
        }

        string devConfigPath = Path.Combine(apiProjectDir, "appsettings.Development.json");
        string baseConfigPath = Path.Combine(apiProjectDir, "appsettings.json");

        string? connectionString = ReadSqliteConnectionFromFile(devConfigPath) ?? ReadSqliteConnectionFromFile(baseConfigPath);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SQLite connection string not found in appsettings.");
        }

        connectionString = NormalizeSqliteConnectionString(connectionString, apiProjectDir);

        return new StorageOptions
        {
            Provider = "SQLite",
            Connections = new StorageConnectionOptions
            {
                SQLite = connectionString
            }
        };
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

    private static string? ReadSqliteConnectionFromFile(string path)
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

        if (storage.TryGetProperty("ConnectionString", out JsonElement direct) &&
            direct.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(direct.GetString()) &&
            direct.GetString()!.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return direct.GetString();
        }

        if (storage.TryGetProperty("Connections", out JsonElement connections) &&
            connections.TryGetProperty("SQLite", out JsonElement sqlite) &&
            sqlite.ValueKind == JsonValueKind.String)
        {
            return sqlite.GetString();
        }

        return null;
    }

    private static string NormalizeSqliteConnectionString(string connectionString, string apiProjectDir)
    {
        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        string path = connectionString[prefix.Length..].Trim();
        if (Path.IsPathRooted(path))
        {
            return connectionString;
        }

        string fullPath = Path.GetFullPath(Path.Combine(apiProjectDir, path));
        return $"{prefix}{fullPath}";
    }
}
