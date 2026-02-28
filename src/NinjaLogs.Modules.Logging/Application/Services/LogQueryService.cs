using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class LogQueryService(ILogStorage logStorage, ILogQueryPlanner queryPlanner) : ILogQueryService
{
    private readonly ILogStorage _logStorage = logStorage;
    private readonly ILogQueryPlanner _queryPlanner = queryPlanner;

    public Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        LogQuery normalized = _queryPlanner.Normalize(query);
        return _logStorage.QueryAsync(normalized, cancellationToken);
    }
}
