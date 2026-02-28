using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.Relational.Repositories;

public interface IRelationalLogEventRepository
{
    Task InsertAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
    Task<PagedResult<LogEvent>> SearchAsync(LogQuery query, CancellationToken cancellationToken = default);
}
