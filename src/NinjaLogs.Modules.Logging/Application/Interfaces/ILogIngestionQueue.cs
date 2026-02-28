using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogIngestionQueue
{
    ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
    ValueTask<LogEvent> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryDequeue(out LogEvent? logEvent);
    ValueTask AcknowledgeAsync(CancellationToken cancellationToken = default);
    int Count { get; }
}
