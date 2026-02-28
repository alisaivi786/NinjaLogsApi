using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.UnitTests;

public sealed class DurableSpoolIngestionQueueCheckpointTests
{
    [Fact]
    public async Task Acknowledge_ShouldPersistCheckpoint_AndEventuallyCompact()
    {
        string logsDir = Path.Combine(Path.GetTempPath(), $"ninjalogs-queue-{Guid.NewGuid():N}");
        StorageOptions options = new()
        {
            LogsDirectory = logsDir,
            IngestionPipeline = new IngestionPipelineOptions
            {
                QueueCapacity = 2000
            }
        };

        try
        {
            DurableSpoolIngestionQueue queue = new(options);
            int total = 550;
            for (int i = 0; i < total; i++)
            {
                await queue.EnqueueAsync(new LogEvent(DateTime.UtcNow, LogLevel.Information, $"m-{i}", null, null, null, null));
            }

            for (int i = 0; i < total; i++)
            {
                _ = await queue.DequeueAsync();
                await queue.AcknowledgeAsync();
            }

            string checkpoint = Path.Combine(logsDir, "queue", "checkpoint.txt");
            Assert.True(File.Exists(checkpoint));
            string text = (await File.ReadAllTextAsync(checkpoint)).Trim();
            Assert.Equal(total.ToString(), text);

            string spool = Path.Combine(logsDir, "queue", "spool.ndjson");
            Assert.True(File.Exists(spool));
            int remaining = File.ReadLines(spool).Count(l => !string.IsNullOrWhiteSpace(l));
            Assert.True(remaining <= 50);
        }
        finally
        {
            if (Directory.Exists(logsDir))
            {
                Directory.Delete(logsDir, recursive: true);
            }
        }
    }
}
