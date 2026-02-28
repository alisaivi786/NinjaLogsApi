using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.File;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;

namespace NinjaLogs.IntegrationTests;

public sealed class ProviderParityContractIntegrationTests
{
    [Fact]
    public async Task File_Segmented_Sqlite_ShouldReturnSameCoreQuerySemantics()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ninjalogs-parity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            ILogStorage file = new FileLogStorage(new StorageOptions
            {
                Provider = "File",
                LogsDirectory = Path.Combine(root, "file")
            });

            ILogStorage segmented = new SegmentedFileLogStorage(new StorageOptions
            {
                Provider = "SegmentedFile",
                SegmentedFile = new SegmentedFileStorageOptions
                {
                    DataDirectory = Path.Combine(root, "segmented"),
                    SegmentMaxBytes = 64 * 1024
                }
            });

            StorageOptions sqliteOptions = new()
            {
                Provider = "SQLite",
                Connections = new StorageConnectionOptions
                {
                    SQLite = $"Data Source={Path.Combine(root, "sqlite", "parity.db")}"
                }
            };
            Directory.CreateDirectory(Path.Combine(root, "sqlite"));
            ILogStorage sqlite = new SqliteLogStorage(new SqliteLogEventRepository(sqliteOptions));

            await SeedAsync(file);
            await SeedAsync(segmented);
            await SeedAsync(sqlite);

            LogQuery query = new(
                FromUtc: DateTime.UtcNow.AddHours(-1),
                ToUtc: DateTime.UtcNow.AddHours(1),
                Level: LogLevel.Error,
                ServiceName: "Billing",
                RequestMethod: "POST",
                StatusCode: 500,
                Page: 1,
                PageSize: 50);

            PagedResult<LogEvent> f = await file.QueryAsync(query);
            PagedResult<LogEvent> s = await segmented.QueryAsync(query);
            PagedResult<LogEvent> q = await sqlite.QueryAsync(query);

            Assert.Equal(f.TotalCount, s.TotalCount);
            Assert.Equal(f.TotalCount, q.TotalCount);
            Assert.Equal(1, f.TotalCount);
            Assert.Equal(f.Items.First().Message, s.Items.First().Message);
            Assert.Equal(f.Items.First().Message, q.Items.First().Message);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task SeedAsync(ILogStorage storage)
    {
        DateTime now = DateTime.UtcNow;
        await storage.AppendAsync(new LogEvent(now, LogLevel.Error, "match", "Billing", "Prod", null, null, RequestMethod: "POST", StatusCode: 500, TraceId: "T1"));
        await storage.AppendAsync(new LogEvent(now, LogLevel.Information, "skip-level", "Billing", "Prod", null, null, RequestMethod: "POST", StatusCode: 500, TraceId: "T2"));
        await storage.AppendAsync(new LogEvent(now, LogLevel.Error, "skip-service", "Identity", "Prod", null, null, RequestMethod: "POST", StatusCode: 500, TraceId: "T3"));
    }
}
