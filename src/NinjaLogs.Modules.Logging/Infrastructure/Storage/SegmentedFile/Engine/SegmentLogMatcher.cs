using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

public static class SegmentLogMatcher
{
    public static bool Matches(LogEvent log, LogQuery query)
    {
        if (query.FromUtc.HasValue && log.TimestampUtc < query.FromUtc.Value) return false;
        if (query.ToUtc.HasValue && log.TimestampUtc > query.ToUtc.Value) return false;
        if (query.Level.HasValue && log.Level != query.Level.Value) return false;
        if (!Eq(log.ServiceName, query.ServiceName)) return false;
        if (!Eq(log.Environment, query.Environment)) return false;
        if (!Eq(log.TraceId, query.TraceId)) return false;
        if (!Eq(log.CorrelationId, query.CorrelationId)) return false;
        if (!Eq(log.RequestId, query.RequestId)) return false;
        if (!Eq(log.RequestMethod, query.RequestMethod)) return false;
        if (query.StatusCode.HasValue && log.StatusCode != query.StatusCode.Value) return false;

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            string s = query.SearchText.Trim();
            bool hit = Has(log.Message, s) || Has(log.Exception, s) || Has(log.PropertiesJson, s) || Has(log.TraceId, s) || Has(log.CorrelationId, s);
            if (!hit) return false;
        }

        return true;
    }

    private static bool Eq(string? actual, string? expected) =>
        string.IsNullOrWhiteSpace(expected) || string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool Has(string? source, string search) =>
        source?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
