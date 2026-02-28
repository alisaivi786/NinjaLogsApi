using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL;

public sealed class PostgresLogStorage(PostgresLogEventRepository repository) : ILogStorage, IBulkLogStorage
{
    private readonly PostgresLogEventRepository _repository = repository;

    public Task AppendAsync(LogEvent log, CancellationToken cancellationToken = default)
    {
        return _repository.InsertAsync(log, cancellationToken);
    }

    public Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.SearchAsync(query, cancellationToken);
    }

    public Task AppendBatchAsync(IReadOnlyList<LogEvent> logs, CancellationToken cancellationToken = default)
    {
        return _repository.InsertBatchAsync(logs, cancellationToken);
    }
}
