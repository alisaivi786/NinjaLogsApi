using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.UnitTests;

public sealed class DurableSpoolIngestionQueueTests
{
    [Fact]
    public async Task ShouldReplayQueuedEvents_AfterReconstruction()
    {
        string logsDir = Path.Combine(Path.GetTempPath(), $"ninjalogs-queue-{Guid.NewGuid():N}");
        StorageOptions options = new()
        {
            LogsDirectory = logsDir,
            IngestionPipeline = new IngestionPipelineOptions
            {
                QueueCapacity = 100
            }
        };

        try
        {
            DurableSpoolIngestionQueue firstQueue = new(options);
            await firstQueue.EnqueueAsync(new LogEvent(DateTime.UtcNow, LogLevel.Error, "persist-me", null, null, null, null));

            DurableSpoolIngestionQueue secondQueue = new(options);
            LogEvent replayed = (await secondQueue.DequeueAsync()).Event;

            Assert.Equal("persist-me", replayed.Message);
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
