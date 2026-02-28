using Microsoft.Data.Sqlite;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Persistence;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.Relational.Repositories;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;

public sealed class SqliteLogEventRepository(StorageOptions options) : IRelationalLogEventRepository
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static volatile bool _schemaReady;
    private readonly string _connectionString = ResolveConnectionString(options);

    public async Task InsertAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Logs (
                TimestampUtc, Level, ServiceName, Environment, Message, Exception, PropertiesJson,
                EventId, SourceContext, RequestId, CorrelationId, TraceId, SpanId, UserId, UserName,
                ClientIp, UserAgent, MachineName, Application, Version, RequestPath, RequestMethod,
                StatusCode, DurationMs, RequestHeadersJson, ResponseHeadersJson, RequestBody, ResponseBody
            ) VALUES (
                $timestampUtc, $level, $serviceName, $environment, $message, $exception, $propertiesJson,
                $eventId, $sourceContext, $requestId, $correlationId, $traceId, $spanId, $userId, $userName,
                $clientIp, $userAgent, $machineName, $application, $version, $requestPath, $requestMethod,
                $statusCode, $durationMs, $requestHeadersJson, $responseHeadersJson, $requestBody, $responseBody
            );
            """;

        command.Parameters.AddWithValue("$timestampUtc", logEvent.TimestampUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$level", (int)logEvent.Level);
        command.Parameters.AddWithValue("$serviceName", (object?)logEvent.ServiceName ?? DBNull.Value);
        command.Parameters.AddWithValue("$environment", (object?)logEvent.Environment ?? DBNull.Value);
        command.Parameters.AddWithValue("$message", logEvent.Message);
        command.Parameters.AddWithValue("$exception", (object?)logEvent.Exception ?? DBNull.Value);
        command.Parameters.AddWithValue("$propertiesJson", (object?)logEvent.PropertiesJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$eventId", (object?)logEvent.EventId ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceContext", (object?)logEvent.SourceContext ?? DBNull.Value);
        command.Parameters.AddWithValue("$requestId", (object?)logEvent.RequestId ?? DBNull.Value);
        command.Parameters.AddWithValue("$correlationId", (object?)logEvent.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$traceId", (object?)logEvent.TraceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$spanId", (object?)logEvent.SpanId ?? DBNull.Value);
        command.Parameters.AddWithValue("$userId", (object?)logEvent.UserId ?? DBNull.Value);
        command.Parameters.AddWithValue("$userName", (object?)logEvent.UserName ?? DBNull.Value);
        command.Parameters.AddWithValue("$clientIp", (object?)logEvent.ClientIp ?? DBNull.Value);
        command.Parameters.AddWithValue("$userAgent", (object?)logEvent.UserAgent ?? DBNull.Value);
        command.Parameters.AddWithValue("$machineName", (object?)logEvent.MachineName ?? DBNull.Value);
        command.Parameters.AddWithValue("$application", (object?)logEvent.Application ?? DBNull.Value);
        command.Parameters.AddWithValue("$version", (object?)logEvent.Version ?? DBNull.Value);
        command.Parameters.AddWithValue("$requestPath", (object?)logEvent.RequestPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$requestMethod", (object?)logEvent.RequestMethod ?? DBNull.Value);
        command.Parameters.AddWithValue("$statusCode", (object?)logEvent.StatusCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", (object?)logEvent.DurationMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$requestHeadersJson", (object?)logEvent.RequestHeadersJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$responseHeadersJson", (object?)logEvent.ResponseHeadersJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$requestBody", (object?)logEvent.RequestBody ?? DBNull.Value);
        command.Parameters.AddWithValue("$responseBody", (object?)logEvent.ResponseBody ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PagedResult<LogEvent>> SearchAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 100 : Math.Min(query.PageSize, 500);
        int offset = (page - 1) * pageSize;

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);

        List<string> where = [];
        List<SqliteParameter> parameters = [];
        BuildFilters(query, where, parameters);

        string whereClause = where.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", where)}";

        int totalCount = await ExecuteCountAsync(connection, whereClause, parameters, cancellationToken);
        IReadOnlyCollection<LogEvent> items = await ExecutePageAsync(connection, whereClause, parameters, pageSize, offset, cancellationToken);

        return new PagedResult<LogEvent>(items, totalCount, page, pageSize);
    }

    private static void BuildFilters(LogQuery query, List<string> where, List<SqliteParameter> parameters)
    {
        AddDateFilter(query.FromUtc, ">=", "TimestampUtc", "$fromUtc", where, parameters);
        AddDateFilter(query.ToUtc, "<=", "TimestampUtc", "$toUtc", where, parameters);

        if (query.Level.HasValue)
        {
            where.Add("Level = $level");
            parameters.Add(new SqliteParameter("$level", (int)query.Level.Value));
        }

        AddEqualsFilter(query.ServiceName, "ServiceName", "$serviceName", where, parameters);
        AddEqualsFilter(query.Environment, "Environment", "$environment", where, parameters);
        AddEqualsFilter(query.TraceId, "TraceId", "$traceId", where, parameters);
        AddEqualsFilter(query.CorrelationId, "CorrelationId", "$correlationId", where, parameters);
        AddEqualsFilter(query.RequestId, "RequestId", "$requestId", where, parameters);
        AddEqualsFilter(query.RequestMethod, "RequestMethod", "$requestMethod", where, parameters);

        if (query.StatusCode.HasValue)
        {
            where.Add("StatusCode = $statusCode");
            parameters.Add(new SqliteParameter("$statusCode", query.StatusCode.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            where.Add("""
                (
                    Message LIKE $search OR
                    Exception LIKE $search OR
                    PropertiesJson LIKE $search OR
                    ServiceName LIKE $search OR
                    Environment LIKE $search OR
                    TraceId LIKE $search OR
                    CorrelationId LIKE $search OR
                    RequestId LIKE $search OR
                    RequestPath LIKE $search OR
                    SourceContext LIKE $search OR
                    UserId LIKE $search OR
                    UserName LIKE $search
                )
                """);
            parameters.Add(new SqliteParameter("$search", $"%{query.SearchText.Trim()}%"));
        }
    }

    private static void AddEqualsFilter(
        string? value,
        string column,
        string parameterName,
        List<string> where,
        List<SqliteParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        where.Add($"{column} = {parameterName}");
        parameters.Add(new SqliteParameter(parameterName, value.Trim()));
    }

    private static void AddDateFilter(
        DateTime? value,
        string op,
        string column,
        string parameterName,
        List<string> where,
        List<SqliteParameter> parameters)
    {
        if (!value.HasValue)
        {
            return;
        }

        where.Add($"{column} {op} {parameterName}");
        parameters.Add(new SqliteParameter(parameterName, value.Value.ToUniversalTime().ToString("O")));
    }

    private static async Task<int> ExecuteCountAsync(
        SqliteConnection connection,
        string whereClause,
        IEnumerable<SqliteParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM Logs{whereClause};";
        foreach (SqliteParameter parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.ParameterName, parameter.Value);
        }

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task<IReadOnlyCollection<LogEvent>> ExecutePageAsync(
        SqliteConnection connection,
        string whereClause,
        IEnumerable<SqliteParameter> parameters,
        int pageSize,
        int offset,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                TimestampUtc, Level, ServiceName, Environment, Message, Exception, PropertiesJson,
                EventId, SourceContext, RequestId, CorrelationId, TraceId, SpanId, UserId, UserName,
                ClientIp, UserAgent, MachineName, Application, Version, RequestPath, RequestMethod,
                StatusCode, DurationMs, RequestHeadersJson, ResponseHeadersJson, RequestBody, ResponseBody
            FROM Logs
            {whereClause}
            ORDER BY TimestampUtc DESC
            LIMIT $limit OFFSET $offset;
            """;

        foreach (SqliteParameter parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.ParameterName, parameter.Value);
        }

        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", offset);

        List<LogEvent> result = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    private static LogEvent Map(SqliteDataReader reader)
    {
        DateTime timestamp = DateTime.TryParse(reader.GetString(0), out DateTime parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : DateTime.UtcNow;

        return new LogEvent(
            timestamp,
            (LogLevel)reader.GetInt32(1),
            reader.GetString(4),
            GetNullableString(reader, 2),
            GetNullableString(reader, 3),
            GetNullableString(reader, 5),
            GetNullableString(reader, 6),
            GetNullableString(reader, 7),
            GetNullableString(reader, 8),
            GetNullableString(reader, 9),
            GetNullableString(reader, 10),
            GetNullableString(reader, 11),
            GetNullableString(reader, 12),
            GetNullableString(reader, 13),
            GetNullableString(reader, 14),
            GetNullableString(reader, 15),
            GetNullableString(reader, 16),
            GetNullableString(reader, 17),
            GetNullableString(reader, 18),
            GetNullableString(reader, 19),
            GetNullableString(reader, 20),
            GetNullableString(reader, 21),
            GetNullableInt(reader, 22),
            GetNullableDouble(reader, 23),
            GetNullableString(reader, 24),
            GetNullableString(reader, 25),
            GetNullableString(reader, 26),
            GetNullableString(reader, 27));
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetNullableInt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static double? GetNullableDouble(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    private static string ResolveConnectionString(StorageOptions options)
    {
        string? configured = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : options.Connections.SQLite;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            string connectionString = configured.Trim();
            const string prefix = "Data Source=";
            if (connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string rawPath = connectionString[prefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(rawPath) && !Path.IsPathRooted(rawPath))
                {
                    string fullPath = Path.GetFullPath(rawPath);
                    string? directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    return $"{prefix}{fullPath}";
                }
            }

            return connectionString;
        }

        string baseDir = string.IsNullOrWhiteSpace(options.LogsDirectory) ? "logs" : options.LogsDirectory;
        string absoluteDir = Path.GetFullPath(baseDir);
        Directory.CreateDirectory(absoluteDir);
        return $"Data Source={Path.Combine(absoluteDir, "ninjalogs.db")}";
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using SqliteConnection connection = new(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqliteCommand createTable = connection.CreateCommand();
            createTable.CommandText = SqliteSchema.CreateLogsTableSql;
            await createTable.ExecuteNonQueryAsync(cancellationToken);

            await using SqliteCommand createIndexes = connection.CreateCommand();
            createIndexes.CommandText = SqliteSchema.CreateIndexesSql;
            await createIndexes.ExecuteNonQueryAsync(cancellationToken);

            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
