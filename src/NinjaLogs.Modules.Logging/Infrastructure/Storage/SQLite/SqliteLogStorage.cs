using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;

public sealed class SqliteLogStorage(SqliteLogEventRepository repository) : ILogStorage
{
    private readonly SqliteLogEventRepository _repository = repository;

    public Task AppendAsync(LogEvent log, CancellationToken cancellationToken = default)
    {
        return _repository.InsertAsync(log, cancellationToken);
    }

    public Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.SearchAsync(query, cancellationToken);
    }
}
