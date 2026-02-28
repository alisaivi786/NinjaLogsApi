using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogIngestionQueue
{
    ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
    ValueTask<IngestionQueueItem> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryDequeue(out IngestionQueueItem? item);
    ValueTask AcknowledgeAsync(long sequence, CancellationToken cancellationToken = default);
    int Count { get; }
}
