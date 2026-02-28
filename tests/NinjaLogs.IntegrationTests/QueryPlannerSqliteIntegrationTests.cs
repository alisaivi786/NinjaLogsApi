using NinjaLogs.Modules.Logging.Application.Services;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;

namespace NinjaLogs.IntegrationTests;

public sealed class QueryPlannerSqliteIntegrationTests
{
    [Fact]
    public async Task QueryService_ShouldApplyPlannerNormalization_AndReturnExpectedRows()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ninjalogs-planner-{Guid.NewGuid():N}.db");
        StorageOptions options = new()
        {
            Provider = "SQLite",
            Connections = new StorageConnectionOptions
            {
                SQLite = $"Data Source={dbPath}"
            }
        };

        try
        {
            SqliteLogEventRepository repository = new(options);
            SqliteLogStorage storage = new(repository);
            LogQueryService queryService = new(storage, new DefaultLogQueryPlanner());

            DateTime ts = DateTime.UtcNow;
            await storage.AppendAsync(new LogEvent(ts, LogLevel.Error, "A", "Billing", "Prod", null, null, TraceId: "T-42", RequestMethod: "POST", StatusCode: 500));
            await storage.AppendAsync(new LogEvent(ts, LogLevel.Information, "B", "Identity", "Prod", null, null, TraceId: "T-99", RequestMethod: "GET", StatusCode: 200));

            DateTime later = ts.AddMinutes(5);
            DateTime earlier = ts.AddMinutes(-5);
            LogQuery input = new(
                FromUtc: later,
                ToUtc: earlier,
                ServiceName: "  Billing ",
                RequestMethod: " post ",
                TraceId: " T-42 ",
                StatusCode: 500,
                Page: 0,
                PageSize: 9999);

            PagedResult<LogEvent> result = await queryService.QueryAsync(input);

            LogEvent only = Assert.Single(result.Items);
            Assert.Equal("A", only.Message);
            Assert.Equal("Billing", only.ServiceName);
            Assert.Equal("T-42", only.TraceId);
            Assert.Equal(500, only.StatusCode);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
