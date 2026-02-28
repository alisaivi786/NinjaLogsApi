using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface IBulkLogStorage
{
    Task AppendBatchAsync(IReadOnlyList<LogEvent> logs, CancellationToken cancellationToken = default);
}
