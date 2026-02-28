using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

namespace NinjaLogs.UnitTests;

public sealed class SegmentLogMatcherTests
{
    [Fact]
    public void Matches_ShouldReturnTrue_WhenAllFiltersMatch()
    {
        LogEvent log = new(
            TimestampUtc: new DateTime(2026, 02, 28, 12, 0, 0, DateTimeKind.Utc),
            Level: LogLevel.Error,
            Message: "Payment failed",
            ServiceName: "BillingService",
            Environment: "Production",
            Exception: "System.Exception: failed",
            PropertiesJson: "{\"orderId\":\"123\"}",
            TraceId: "TRACE-1",
            CorrelationId: "CORR-1",
            RequestId: "REQ-1",
            RequestMethod: "POST",
            StatusCode: 500);

        LogQuery query = new(
            FromUtc: new DateTime(2026, 02, 28, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 02, 28, 23, 59, 59, DateTimeKind.Utc),
            Level: LogLevel.Error,
            ServiceName: "BillingService",
            Environment: "Production",
            TraceId: "TRACE-1",
            CorrelationId: "CORR-1",
            RequestId: "REQ-1",
            RequestMethod: "POST",
            StatusCode: 500,
            SearchText: "failed");

        bool matched = SegmentLogMatcher.Matches(log, query);

        Assert.True(matched);
    }

    [Fact]
    public void Matches_ShouldReturnFalse_WhenServiceMismatches()
    {
        LogEvent log = new(
            TimestampUtc: DateTime.UtcNow,
            Level: LogLevel.Information,
            Message: "OK",
            ServiceName: "SvcA",
            Environment: "Test",
            Exception: null,
            PropertiesJson: null);

        LogQuery query = new(ServiceName: "SvcB");

        Assert.False(SegmentLogMatcher.Matches(log, query));
    }

    [Fact]
    public void Matches_ShouldReturnFalse_WhenSearchTextNotFound()
    {
        LogEvent log = new(
            TimestampUtc: DateTime.UtcNow,
            Level: LogLevel.Warning,
            Message: "Disk usage warning",
            ServiceName: "Infra",
            Environment: "Prod",
            Exception: null,
            PropertiesJson: "{\"disk\":\"80\"}");

        LogQuery query = new(SearchText: "payment");

        Assert.False(SegmentLogMatcher.Matches(log, query));
    }
}
