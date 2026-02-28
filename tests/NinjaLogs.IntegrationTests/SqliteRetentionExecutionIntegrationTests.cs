using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;
using Microsoft.Data.Sqlite;

namespace NinjaLogs.IntegrationTests;

public sealed class SqliteRetentionExecutionIntegrationTests
{
    [Fact]
    public async Task RetentionDeleteQuery_ShouldRemoveOldRows_KeepNewRows()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ninjalogs-retention-{Guid.NewGuid():N}.db");
        StorageOptions options = new()
        {
            Provider = "SQLite",
            Connections = new StorageConnectionOptions
            {
                SQLite = $"Data Source={dbPath}"
            }
        };

        SqliteLogEventRepository repository = new(options);
        DateTime oldTs = DateTime.UtcNow.AddDays(-40);
        DateTime newTs = DateTime.UtcNow;
        await repository.InsertAsync(new LogEvent(oldTs, LogLevel.Error, "old", null, null, null, null));
        await repository.InsertAsync(new LogEvent(newTs, LogLevel.Error, "new", null, null, null, null));

        DateTime cutoff = DateTime.UtcNow.AddDays(-30);
        await using SqliteConnection connection = new($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs WHERE TimestampUtc < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        await cmd.ExecuteNonQueryAsync();

        await using SqliteCommand count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(1) FROM Logs;";
        int total = Convert.ToInt32(await count.ExecuteScalarAsync());
        Assert.Equal(1, total);

        await using SqliteCommand msg = connection.CreateCommand();
        msg.CommandText = "SELECT Message FROM Logs LIMIT 1;";
        string remaining = Convert.ToString(await msg.ExecuteScalarAsync())!;
        Assert.Equal("new", remaining);
    }
}
