using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Text.Json;

namespace NinjaLogs.UnitTests;

public sealed class DeadLetterReplayServiceTests
{
    [Fact]
    public async Task ReplayAsync_ShouldRequeueValidEntries_AndKeepRemaining()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ninjalogs-dlq-{Guid.NewGuid():N}");
        StorageOptions options = new()
        {
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
            string file = Path.Combine(dlqDir, "2026-03-01.ndjson");

            LogEvent e1 = new(DateTime.UtcNow, LogLevel.Error, "a", null, null, null, null);
            LogEvent e2 = new(DateTime.UtcNow, LogLevel.Error, "b", null, null, null, null);
            await File.WriteAllTextAsync(file,
                JsonSerializer.Serialize(e1) + Environment.NewLine +
                JsonSerializer.Serialize(e2) + Environment.NewLine);

            FakeIngestionService ingestion = new();
            DeadLetterReplayService service = new(options, ingestion);

            IReadOnlyCollection<string> files = service.ListFiles();
            Assert.Contains("2026-03-01.ndjson", files);

            int replayed = await service.ReplayAsync("2026-03-01.ndjson", 1);
            Assert.Equal(1, replayed);
            Assert.Single(ingestion.Events);
            Assert.Equal("a", ingestion.Events[0].Message);

            int remaining = File.ReadLines(file).Count(l => !string.IsNullOrWhiteSpace(l));
            Assert.Equal(1, remaining);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeIngestionService : ILogIngestionService
    {
        public List<LogEvent> Events { get; } = [];
        public Task IngestAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(logEvent);
            return Task.CompletedTask;
        }
    }
}
