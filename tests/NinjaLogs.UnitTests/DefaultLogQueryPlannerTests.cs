using NinjaLogs.Modules.Logging.Application.Services;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.UnitTests;

public sealed class DefaultLogQueryPlannerTests
{
    [Fact]
    public void Normalize_ShouldTrimAndClamp_AndNormalizeUtc()
    {
        DefaultLogQueryPlanner planner = new();

        DateTime localFrom = new(2026, 2, 28, 22, 0, 0, DateTimeKind.Local);
        DateTime localTo = new(2026, 2, 28, 10, 0, 0, DateTimeKind.Local);
        LogQuery input = new(
            FromUtc: localFrom,
            ToUtc: localTo,
            Level: LogLevel.Error,
            ServiceName: "  Billing  ",
            Environment: "  Prod  ",
            TraceId: "  T-1  ",
            CorrelationId: "  C-1  ",
            RequestId: "  R-1  ",
            RequestMethod: " post ",
            StatusCode: 500,
            SearchText: "  failed  ",
            Page: 0,
            PageSize: 9999);

        LogQuery normalized = planner.Normalize(input);

        Assert.Equal(1, normalized.Page);
        Assert.Equal(500, normalized.PageSize);
        Assert.Equal("Billing", normalized.ServiceName);
        Assert.Equal("Prod", normalized.Environment);
        Assert.Equal("T-1", normalized.TraceId);
        Assert.Equal("C-1", normalized.CorrelationId);
        Assert.Equal("R-1", normalized.RequestId);
        Assert.Equal("POST", normalized.RequestMethod);
        Assert.Equal("failed", normalized.SearchText);
        Assert.NotNull(normalized.FromUtc);
        Assert.NotNull(normalized.ToUtc);
        Assert.Equal(DateTimeKind.Utc, normalized.FromUtc!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, normalized.ToUtc!.Value.Kind);
        Assert.True(normalized.FromUtc <= normalized.ToUtc);
    }

    [Fact]
    public void Normalize_ShouldApplyDefaults_ForMissingValues()
    {
        DefaultLogQueryPlanner planner = new();

        LogQuery normalized = planner.Normalize(new LogQuery(Page: -1, PageSize: -1));

        Assert.Equal(1, normalized.Page);
        Assert.Equal(100, normalized.PageSize);
        Assert.Null(normalized.ServiceName);
        Assert.Null(normalized.RequestMethod);
    }
}
