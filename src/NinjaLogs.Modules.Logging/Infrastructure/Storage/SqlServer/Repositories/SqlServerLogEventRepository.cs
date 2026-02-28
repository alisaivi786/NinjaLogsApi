using Microsoft.Data.SqlClient;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Persistence;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.Relational.Repositories;
using System.Data;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Repositories;

public sealed class SqlServerLogEventRepository(StorageOptions options) : IRelationalLogEventRepository
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static volatile bool _schemaReady;
    private readonly string _connectionString = ResolveConnectionString(options);

    public async Task InsertAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Logs (
                TimestampUtc, Level, ServiceName, Environment, Message, Exception, PropertiesJson,
                EventId, SourceContext, RequestId, CorrelationId, TraceId, SpanId, UserId, UserName,
                ClientIp, UserAgent, MachineName, Application, Version, RequestPath, RequestMethod,
                StatusCode, DurationMs, RequestHeadersJson, ResponseHeadersJson, RequestBody, ResponseBody
            ) VALUES (
                @TimestampUtc, @Level, @ServiceName, @Environment, @Message, @Exception, @PropertiesJson,
                @EventId, @SourceContext, @RequestId, @CorrelationId, @TraceId, @SpanId, @UserId, @UserName,
                @ClientIp, @UserAgent, @MachineName, @Application, @Version, @RequestPath, @RequestMethod,
                @StatusCode, @DurationMs, @RequestHeadersJson, @ResponseHeadersJson, @RequestBody, @ResponseBody
            );
            """;

        Add(command, "@TimestampUtc", logEvent.TimestampUtc.ToUniversalTime(), SqlDbType.DateTime2);
        Add(command, "@Level", (int)logEvent.Level, SqlDbType.Int);
        Add(command, "@ServiceName", logEvent.ServiceName, SqlDbType.NVarChar);
        Add(command, "@Environment", logEvent.Environment, SqlDbType.NVarChar);
        Add(command, "@Message", logEvent.Message, SqlDbType.NVarChar);
        Add(command, "@Exception", logEvent.Exception, SqlDbType.NVarChar);
        Add(command, "@PropertiesJson", logEvent.PropertiesJson, SqlDbType.NVarChar);
        Add(command, "@EventId", logEvent.EventId, SqlDbType.NVarChar);
        Add(command, "@SourceContext", logEvent.SourceContext, SqlDbType.NVarChar);
        Add(command, "@RequestId", logEvent.RequestId, SqlDbType.NVarChar);
        Add(command, "@CorrelationId", logEvent.CorrelationId, SqlDbType.NVarChar);
        Add(command, "@TraceId", logEvent.TraceId, SqlDbType.NVarChar);
        Add(command, "@SpanId", logEvent.SpanId, SqlDbType.NVarChar);
        Add(command, "@UserId", logEvent.UserId, SqlDbType.NVarChar);
        Add(command, "@UserName", logEvent.UserName, SqlDbType.NVarChar);
        Add(command, "@ClientIp", logEvent.ClientIp, SqlDbType.NVarChar);
        Add(command, "@UserAgent", logEvent.UserAgent, SqlDbType.NVarChar);
        Add(command, "@MachineName", logEvent.MachineName, SqlDbType.NVarChar);
        Add(command, "@Application", logEvent.Application, SqlDbType.NVarChar);
        Add(command, "@Version", logEvent.Version, SqlDbType.NVarChar);
        Add(command, "@RequestPath", logEvent.RequestPath, SqlDbType.NVarChar);
        Add(command, "@RequestMethod", logEvent.RequestMethod, SqlDbType.NVarChar);
        Add(command, "@StatusCode", logEvent.StatusCode, SqlDbType.Int);
        Add(command, "@DurationMs", logEvent.DurationMs, SqlDbType.Float);
        Add(command, "@RequestHeadersJson", logEvent.RequestHeadersJson, SqlDbType.NVarChar);
        Add(command, "@ResponseHeadersJson", logEvent.ResponseHeadersJson, SqlDbType.NVarChar);
        Add(command, "@RequestBody", logEvent.RequestBody, SqlDbType.NVarChar);
        Add(command, "@ResponseBody", logEvent.ResponseBody, SqlDbType.NVarChar);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PagedResult<LogEvent>> SearchAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 100 : Math.Min(query.PageSize, 500);
        int offset = (page - 1) * pageSize;

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);

        List<string> where = [];
        List<SqlParameter> parameters = [];
        BuildFilters(query, where, parameters);
        string whereClause = where.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", where)}";

        int total = await ExecuteCountAsync(connection, whereClause, parameters, cancellationToken);
        IReadOnlyCollection<LogEvent> items = await ExecutePageAsync(connection, whereClause, parameters, pageSize, offset, cancellationToken);
        return new PagedResult<LogEvent>(items, total, page, pageSize);
    }

    private static void BuildFilters(LogQuery query, List<string> where, List<SqlParameter> parameters)
    {
        AddDateFilter(query.FromUtc, ">=", "TimestampUtc", "@fromUtc", where, parameters);
        AddDateFilter(query.ToUtc, "<=", "TimestampUtc", "@toUtc", where, parameters);

        if (query.Level.HasValue)
        {
            where.Add("Level = @level");
            parameters.Add(new SqlParameter("@level", SqlDbType.Int) { Value = (int)query.Level.Value });
        }

        AddEqualsFilter(query.ServiceName, "ServiceName", "@serviceName", where, parameters);
        AddEqualsFilter(query.Environment, "Environment", "@environment", where, parameters);
        AddEqualsFilter(query.TraceId, "TraceId", "@traceId", where, parameters);
        AddEqualsFilter(query.CorrelationId, "CorrelationId", "@correlationId", where, parameters);
        AddEqualsFilter(query.RequestId, "RequestId", "@requestId", where, parameters);
        AddEqualsFilter(query.RequestMethod, "RequestMethod", "@requestMethod", where, parameters);

        if (query.StatusCode.HasValue)
        {
            where.Add("StatusCode = @statusCode");
            parameters.Add(new SqlParameter("@statusCode", SqlDbType.Int) { Value = query.StatusCode.Value });
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            where.Add("""
                (
                    Message LIKE @search OR
                    Exception LIKE @search OR
                    PropertiesJson LIKE @search OR
                    ServiceName LIKE @search OR
                    Environment LIKE @search OR
                    TraceId LIKE @search OR
                    CorrelationId LIKE @search OR
                    RequestId LIKE @search OR
                    RequestPath LIKE @search OR
                    SourceContext LIKE @search OR
                    UserId LIKE @search OR
                    UserName LIKE @search
                )
                """);
            parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar) { Value = $"%{query.SearchText.Trim()}%" });
        }
    }

    private static void AddEqualsFilter(string? value, string column, string name, List<string> where, List<SqlParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        where.Add($"{column} = {name}");
        parameters.Add(new SqlParameter(name, SqlDbType.NVarChar) { Value = value.Trim() });
    }

    private static void AddDateFilter(DateTime? value, string op, string column, string name, List<string> where, List<SqlParameter> parameters)
    {
        if (!value.HasValue)
        {
            return;
        }

        where.Add($"{column} {op} {name}");
        parameters.Add(new SqlParameter(name, SqlDbType.DateTime2) { Value = value.Value.ToUniversalTime() });
    }

    private static async Task<int> ExecuteCountAsync(SqlConnection connection, string whereClause, IEnumerable<SqlParameter> parameters, CancellationToken cancellationToken)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM dbo.Logs{whereClause};";
        foreach (SqlParameter parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.SqlDbType) { Value = parameter.Value });
        }

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task<IReadOnlyCollection<LogEvent>> ExecutePageAsync(
        SqlConnection connection,
        string whereClause,
        IEnumerable<SqlParameter> parameters,
        int pageSize,
        int offset,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                TimestampUtc, Level, ServiceName, Environment, Message, Exception, PropertiesJson,
                EventId, SourceContext, RequestId, CorrelationId, TraceId, SpanId, UserId, UserName,
                ClientIp, UserAgent, MachineName, Application, Version, RequestPath, RequestMethod,
                StatusCode, DurationMs, RequestHeadersJson, ResponseHeadersJson, RequestBody, ResponseBody
            FROM dbo.Logs
            {whereClause}
            ORDER BY TimestampUtc DESC, Id DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        foreach (SqlParameter parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.SqlDbType) { Value = parameter.Value });
        }

        command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = offset });
        command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize });

        List<LogEvent> items = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private static LogEvent Map(SqlDataReader reader)
    {
        DateTime timestamp = reader.GetDateTime(0);
        return new LogEvent(
            DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            (LogLevel)reader.GetInt32(1),
            reader.GetString(4),
            GetString(reader, 2),
            GetString(reader, 3),
            GetString(reader, 5),
            GetString(reader, 6),
            GetString(reader, 7),
            GetString(reader, 8),
            GetString(reader, 9),
            GetString(reader, 10),
            GetString(reader, 11),
            GetString(reader, 12),
            GetString(reader, 13),
            GetString(reader, 14),
            GetString(reader, 15),
            GetString(reader, 16),
            GetString(reader, 17),
            GetString(reader, 18),
            GetString(reader, 19),
            GetString(reader, 20),
            GetString(reader, 21),
            GetInt(reader, 22),
            GetDouble(reader, 23),
            GetString(reader, 24),
            GetString(reader, 25),
            GetString(reader, 26),
            GetString(reader, 27));
    }

    private static string? GetString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static int? GetInt(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    private static double? GetDouble(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    private static void Add(SqlCommand command, string name, object? value, SqlDbType dbType)
    {
        SqlParameter p = command.Parameters.Add(name, dbType);
        p.Value = value ?? DBNull.Value;
    }

    private static string ResolveConnectionString(StorageOptions options)
    {
        string? configured = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : options.Connections.SqlServer;

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Storage:ConnectionString is required for SqlServer provider.");
        }

        return configured.Trim();
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

            await using SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using SqlCommand createTable = connection.CreateCommand();
            createTable.CommandText = SqlServerSchema.CreateLogsTableSql;
            await createTable.ExecuteNonQueryAsync(cancellationToken);

            await using SqlCommand createIndexes = connection.CreateCommand();
            createIndexes.CommandText = SqlServerSchema.CreateIndexesSql;
            await createIndexes.ExecuteNonQueryAsync(cancellationToken);

            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
