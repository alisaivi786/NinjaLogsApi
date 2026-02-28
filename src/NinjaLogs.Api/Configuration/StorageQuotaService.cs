using Microsoft.Data.SqlClient;
using Npgsql;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public sealed class StorageQuotaService(StorageOptions storage, LicensingOptions licensing)
{
    private readonly StorageOptions _storage = storage;
    private readonly LicensingOptions _licensing = licensing;

    public async Task<QuotaCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_storage.Policy.EnforceLicenseTotalQuota)
        {
            return QuotaCheckResult.CreateAllowed(0, 0);
        }

        long maxBytes = _licensing.IsLicensed
            ? _storage.Policy.LicensedMaxTotalBytes
            : _storage.Policy.UnlicensedMaxTotalBytes;

        long currentBytes = await GetCurrentUsageBytesAsync(cancellationToken);
        return currentBytes > maxBytes
            ? QuotaCheckResult.CreateBlocked(currentBytes, maxBytes, _storage.Policy.QuotaExceededStatusCode)
            : QuotaCheckResult.CreateAllowed(currentBytes, maxBytes);
    }

    private Task<long> GetCurrentUsageBytesAsync(CancellationToken cancellationToken)
    {
        string provider = _storage.GetNormalizedProvider();
        return provider switch
        {
            "file" => Task.FromResult(GetDirectorySizeBytes(Path.GetFullPath(_storage.LogsDirectory), "*.ndjson")),
            "segmentedfile" => Task.FromResult(GetDirectorySizeBytes(Path.GetFullPath(_storage.SegmentedFile.DataDirectory), "*")),
            "sqlite" => Task.FromResult(GetSqliteDbFileSizeBytes()),
            "sqlserver" => GetSqlServerTableBytesAsync(cancellationToken),
            "postgresql" => GetPostgresTableBytesAsync(cancellationToken),
            _ => Task.FromResult(0L)
        };
    }

    private long GetSqliteDbFileSizeBytes()
    {
        string? raw = !string.IsNullOrWhiteSpace(_storage.ConnectionString)
            ? _storage.ConnectionString
            : _storage.Connections.SQLite;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        const string prefix = "Data Source=";
        string value = raw.Trim();
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string path = value[prefix.Length..].Trim();
        string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        return System.IO.File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
    }

    private async Task<long> GetSqlServerTableBytesAsync(CancellationToken cancellationToken)
    {
        string? connectionString = !string.IsNullOrWhiteSpace(_storage.ConnectionString)
            ? _storage.ConnectionString
            : _storage.Connections.SqlServer;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return 0;
        }

        await using SqlConnection connection = new(connectionString.Trim());
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(
                DATALENGTH([Message]) +
                ISNULL(DATALENGTH([ServiceName]),0) +
                ISNULL(DATALENGTH([Environment]),0) +
                ISNULL(DATALENGTH([Exception]),0) +
                ISNULL(DATALENGTH([PropertiesJson]),0) +
                ISNULL(DATALENGTH([EventId]),0) +
                ISNULL(DATALENGTH([SourceContext]),0) +
                ISNULL(DATALENGTH([RequestId]),0) +
                ISNULL(DATALENGTH([CorrelationId]),0) +
                ISNULL(DATALENGTH([TraceId]),0) +
                ISNULL(DATALENGTH([SpanId]),0) +
                ISNULL(DATALENGTH([UserId]),0) +
                ISNULL(DATALENGTH([UserName]),0) +
                ISNULL(DATALENGTH([ClientIp]),0) +
                ISNULL(DATALENGTH([UserAgent]),0) +
                ISNULL(DATALENGTH([MachineName]),0) +
                ISNULL(DATALENGTH([Application]),0) +
                ISNULL(DATALENGTH([Version]),0) +
                ISNULL(DATALENGTH([RequestPath]),0) +
                ISNULL(DATALENGTH([RequestMethod]),0) +
                ISNULL(DATALENGTH([RequestHeadersJson]),0) +
                ISNULL(DATALENGTH([ResponseHeadersJson]),0) +
                ISNULL(DATALENGTH([RequestBody]),0) +
                ISNULL(DATALENGTH([ResponseBody]),0) +
                8 + 4 + 4 + 8
            ), 0)
            FROM dbo.Logs;
            """;

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is DBNull or null ? 0 : Convert.ToInt64(result);
    }

    private static long GetDirectorySizeBytes(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path).Length)
            .Sum();
    }

    private async Task<long> GetPostgresTableBytesAsync(CancellationToken cancellationToken)
    {
        string? connectionString = !string.IsNullOrWhiteSpace(_storage.ConnectionString)
            ? _storage.ConnectionString
            : _storage.Connections.PostgreSQL;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return 0;
        }

        await using NpgsqlConnection connection = new(connectionString.Trim());
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(pg_total_relation_size('logs'), 0);";
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is DBNull or null ? 0 : Convert.ToInt64(result);
    }
}

public sealed record QuotaCheckResult(bool Allowed, long CurrentBytes, long MaxBytes, int StatusCode)
{
    public static QuotaCheckResult CreateAllowed(long currentBytes, long maxBytes) => new(true, currentBytes, maxBytes, 200);
    public static QuotaCheckResult CreateBlocked(long currentBytes, long maxBytes, int statusCode) => new(false, currentBytes, maxBytes, statusCode);
}
