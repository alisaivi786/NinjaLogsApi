using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogQueryService
{
    Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);
}
