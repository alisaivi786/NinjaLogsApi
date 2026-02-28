using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class DefaultLogQueryPlanner : ILogQueryPlanner
{
    public LogQuery Normalize(LogQuery query)
    {
        int page = query.Page <= 0 ? 1 : query.Page;
        int pageSize = query.PageSize <= 0 ? 100 : Math.Min(query.PageSize, 500);

        DateTime? fromUtc = query.FromUtc?.ToUniversalTime();
        DateTime? toUtc = query.ToUtc?.ToUniversalTime();
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return query with
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = page,
            PageSize = pageSize,
            ServiceName = NormalizeText(query.ServiceName),
            Environment = NormalizeText(query.Environment),
            TraceId = NormalizeText(query.TraceId),
            CorrelationId = NormalizeText(query.CorrelationId),
            RequestId = NormalizeText(query.RequestId),
            RequestMethod = NormalizeText(query.RequestMethod)?.ToUpperInvariant(),
            SearchText = NormalizeText(query.SearchText)
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
