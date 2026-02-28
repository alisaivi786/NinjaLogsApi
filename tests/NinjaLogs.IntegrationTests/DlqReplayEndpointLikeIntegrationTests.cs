using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Application.Services;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Text.Json;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;

namespace NinjaLogs.IntegrationTests;

public sealed class DlqReplayEndpointLikeIntegrationTests
{
    [Fact]
    public async Task DeadLetterReplayService_ShouldReplayAndShrinkFile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ninjalogs-dlq-it-{Guid.NewGuid():N}");
        StorageOptions options = new()
        {
            Provider = "File",
            LogsDirectory = root,
            IngestionPipeline = new IngestionPipelineOptions
            {
                DeadLetterDirectory = "deadletter"
            }
        };

        try
        {
            string dlqDir = Path.Combine(root, "deadletter");
            Directory.CreateDirectory(dlqDir);
            string fileName = "2026-03-02.ndjson";
            string filePath = Path.Combine(dlqDir, fileName);
            LogEvent e1 = new(DateTime.UtcNow, LogLevel.Error, "x1", null, null, null, null);
            LogEvent e2 = new(DateTime.UtcNow, LogLevel.Error, "x2", null, null, null, null);
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(e1) + Environment.NewLine + JsonSerializer.Serialize(e2) + Environment.NewLine);

            BoundedLogIngestionQueue queue = new();
            LogIngestionService ingestion = new(queue);
            DeadLetterReplayService replay = new(options, ingestion);

            int replayed = await replay.ReplayAsync(fileName, 1);
            Assert.Equal(1, replayed);

            int remaining = File.ReadLines(filePath).Count(l => !string.IsNullOrWhiteSpace(l));
            Assert.Equal(1, remaining);

            LogEvent queued = await queue.DequeueAsync();
            Assert.Equal("x1", queued.Message);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
