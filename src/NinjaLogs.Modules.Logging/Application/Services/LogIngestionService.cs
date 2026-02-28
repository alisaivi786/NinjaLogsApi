using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class LogIngestionService(ILogIngestionQueue queue) : ILogIngestionService
{
    private readonly ILogIngestionQueue _queue = queue;

    public Task IngestAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        return _queue.EnqueueAsync(logEvent, cancellationToken).AsTask();
    }
}
