using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.Relational.Repositories;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;

public sealed class PostgresLogEventRepository : IRelationalLogEventRepository
{
    public Task InsertAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Future PostgreSQL insert implementation pending.");
    }

    public Task<PagedResult<LogEvent>> SearchAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Future PostgreSQL query implementation pending.");
    }
}
