using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogIngestionService
{
    Task IngestAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
}
