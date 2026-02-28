using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile;
using System.Text.Json;

namespace NinjaLogs.IntegrationTests;

public sealed class SegmentedFileLogStorageIntegrationTests
{
    [Fact]
    public async Task AppendAndQuery_ShouldCreateManifestRotateSegmentsAndReturnResults()
    {
        string dataDir = ResolveApiSegmentDataDirectory();
        Directory.CreateDirectory(dataDir);

        StorageOptions options = new()
        {
            Provider = "SegmentedFile",
            SegmentedFile = new SegmentedFileStorageOptions
            {
                DataDirectory = dataDir,
                SegmentMaxBytes = 500,
                ManifestFileName = "manifest.json"
            }
        };

        SegmentedFileLogStorage storage = new(options);
        string traceId = $"TRACE-SEG-{Guid.NewGuid():N}";

        for (int i = 0; i < 12; i++)
        {
            await storage.AppendAsync(new LogEvent(
                TimestampUtc: DateTime.UtcNow,
                Level: i % 2 == 0 ? LogLevel.Information : LogLevel.Error,
                Message: $"Segment event {i} {new string('x', 90)}",
                ServiceName: "SegmentSvc",
                Environment: "Test",
                Exception: null,
                PropertiesJson: "{\"kind\":\"segment\"}",
                TraceId: i == 11 ? traceId : null,
                StatusCode: i % 2 == 0 ? 200 : 500));
        }

        PagedResult<LogEvent> result = await storage.QueryAsync(new LogQuery(
            TraceId: traceId,
            Page: 1,
            PageSize: 10));

        LogEvent only = Assert.Single(result.Items);
        Assert.Equal(traceId, only.TraceId);

        string manifestPath = Path.Combine(dataDir, "manifest.json");
        Assert.True(System.IO.File.Exists(manifestPath));
        Assert.True(Directory.EnumerateFiles(dataDir, "segment-*.dat").Count() > 1);
    }

    private static string ResolveApiSegmentDataDirectory()
    {
        string apiDir = FindApiProjectDirectory()
            ?? throw new InvalidOperationException("Could not locate src/NinjaLogs.Api directory from test runtime.");

        string devConfigPath = Path.Combine(apiDir, "appsettings.Development.json");
        string baseConfigPath = Path.Combine(apiDir, "appsettings.json");
        string dataRelative = ReadSegmentDataDirectoryFromFile(devConfigPath) ?? ReadSegmentDataDirectoryFromFile(baseConfigPath) ?? "data";

        return Path.GetFullPath(Path.Combine(apiDir, dataRelative));
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

    private static string? ReadSegmentDataDirectoryFromFile(string path)
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

        if (storage.TryGetProperty("SegmentedFile", out JsonElement segmented) &&
            segmented.TryGetProperty("DataDirectory", out JsonElement dataDir) &&
            dataDir.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(dataDir.GetString()))
        {
            return dataDir.GetString();
        }

        return null;
    }
}
