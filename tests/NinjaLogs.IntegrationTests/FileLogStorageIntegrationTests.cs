using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.File;
using System.Text.Json;

namespace NinjaLogs.IntegrationTests;

public sealed class FileLogStorageIntegrationTests
{
    [Fact]
    public async Task AppendAndQuery_ShouldPersistNdjsonAndReturnFilteredResults()
    {
        string logsDir = ResolveApiLogsDirectory();
        Directory.CreateDirectory(logsDir);

        StorageOptions options = new()
        {
            Provider = "File",
            LogsDirectory = logsDir
        };

        FileLogStorage storage = new(options);
        string traceId = $"TRACE-FILE-{Guid.NewGuid():N}";

        await storage.AppendAsync(new LogEvent(DateTime.UtcNow, LogLevel.Information, "Info event", "SvcA", "Test", null, null));
        await storage.AppendAsync(new LogEvent(DateTime.UtcNow, LogLevel.Error, "Error event", "SvcB", "Test", null, "{\"orderId\":\"1\"}", TraceId: traceId, RequestMethod: "POST", StatusCode: 500));

        PagedResult<LogEvent> result = await storage.QueryAsync(new LogQuery(
            Level: LogLevel.Error,
            TraceId: traceId,
            RequestMethod: "POST",
            StatusCode: 500,
            Page: 1,
            PageSize: 10));

        LogEvent only = Assert.Single(result.Items);
        Assert.Equal("Error event", only.Message);
        Assert.Equal(traceId, only.TraceId);
        Assert.Equal(500, only.StatusCode);
        Assert.True(Directory.EnumerateFiles(logsDir, "*.ndjson").Any());
    }

    private static string ResolveApiLogsDirectory()
    {
        string apiDir = FindApiProjectDirectory()
            ?? throw new InvalidOperationException("Could not locate src/NinjaLogs.Api directory from test runtime.");

        string devConfigPath = Path.Combine(apiDir, "appsettings.Development.json");
        string baseConfigPath = Path.Combine(apiDir, "appsettings.json");
        string logsRelative = ReadLogsDirectoryFromFile(devConfigPath) ?? ReadLogsDirectoryFromFile(baseConfigPath) ?? "logs";

        return Path.GetFullPath(Path.Combine(apiDir, logsRelative));
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

    private static string? ReadLogsDirectoryFromFile(string path)
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

        if (storage.TryGetProperty("LogsDirectory", out JsonElement logsDir) &&
            logsDir.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(logsDir.GetString()))
        {
            return logsDir.GetString();
        }

        return null;
    }
}
