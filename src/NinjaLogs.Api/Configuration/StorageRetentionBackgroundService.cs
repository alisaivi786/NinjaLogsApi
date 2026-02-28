using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public sealed class StorageRetentionBackgroundService(StorageOptions storageOptions, ILogger<StorageRetentionBackgroundService> logger) : BackgroundService
{
    private readonly StorageOptions _storageOptions = storageOptions;
    private readonly ILogger<StorageRetentionBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_storageOptions.Retention.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunRetentionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention execution failed.");
            }

            int minutes = Math.Max(1, _storageOptions.Retention.CleanupIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }

    private async Task RunRetentionAsync(CancellationToken cancellationToken)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _storageOptions.Retention.RetainDays));
        string provider = _storageOptions.GetNormalizedProvider();

        switch (provider)
        {
            case "file":
                CleanupDirectoryByWriteTime(Path.GetFullPath(_storageOptions.LogsDirectory), "*.ndjson", cutoff);
                break;
            case "segmentedfile":
                CleanupDirectoryByWriteTime(Path.GetFullPath(_storageOptions.SegmentedFile.DataDirectory), "segment-*.dat", cutoff);
                break;
            case "sqlite":
                await CleanupSqliteAsync(cutoff, cancellationToken);
                break;
            case "sqlserver":
                await CleanupSqlServerAsync(cutoff, cancellationToken);
                break;
            case "postgresql":
                await CleanupPostgresAsync(cutoff, cancellationToken);
                break;
        }
    }

    private static void CleanupDirectoryByWriteTime(string dir, string pattern, DateTime cutoffUtc)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
        {
            DateTime lastWrite = File.GetLastWriteTimeUtc(file);
            if (lastWrite < cutoffUtc)
            {
                File.Delete(file);
            }
        }
    }

    private async Task CleanupSqliteAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        string cs = _storageOptions.Connections.SQLite ?? _storageOptions.ConnectionString ?? "";
        if (string.IsNullOrWhiteSpace(cs))
        {
            return;
        }

        await using SqliteConnection connection = new(cs);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Logs WHERE TimestampUtc < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", cutoffUtc.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CleanupSqlServerAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        string cs = _storageOptions.Connections.SqlServer ?? _storageOptions.ConnectionString ?? "";
        if (string.IsNullOrWhiteSpace(cs))
        {
            return;
        }

        await using SqlConnection connection = new(cs);
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM dbo.Logs WHERE TimestampUtc < @cutoff;";
        cmd.Parameters.AddWithValue("@cutoff", cutoffUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CleanupPostgresAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        string cs = _storageOptions.Connections.PostgreSQL ?? _storageOptions.ConnectionString ?? "";
        if (string.IsNullOrWhiteSpace(cs))
        {
            return;
        }

        await using NpgsqlConnection connection = new(cs);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM logs WHERE timestamp_utc < @cutoff;";
        cmd.Parameters.AddWithValue("@cutoff", cutoffUtc);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
