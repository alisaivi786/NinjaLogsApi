using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class LogQueryService(ILogStorage logStorage) : ILogQueryService
{
    private readonly ILogStorage _logStorage = logStorage;

    public Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _logStorage.QueryAsync(query, cancellationToken);
    }
}
