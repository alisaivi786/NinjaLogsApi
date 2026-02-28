using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class LogIngestionService(ILogStorage logStorage) : ILogIngestionService
{
    private readonly ILogStorage _logStorage = logStorage;

    public Task IngestAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        return _logStorage.AppendAsync(logEvent, cancellationToken);
    }
}
