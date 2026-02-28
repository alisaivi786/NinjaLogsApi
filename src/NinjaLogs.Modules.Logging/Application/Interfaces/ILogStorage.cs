using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogStorage
{
    Task AppendAsync(LogEvent log, CancellationToken cancellationToken = default);
    Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);
}
