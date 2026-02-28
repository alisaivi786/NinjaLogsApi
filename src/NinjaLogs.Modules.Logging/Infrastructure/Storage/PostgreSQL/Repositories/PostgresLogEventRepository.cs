using Npgsql;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Persistence;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.Relational.Repositories;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;

public sealed class PostgresLogEventRepository(StorageOptions options) : IRelationalLogEventRepository
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static volatile bool _schemaReady;
    private readonly string _connectionString = ResolveConnectionString(options);

    public async Task InsertAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO logs (
                timestamp_utc, level, service_name, environment, message, exception, properties_json,
                event_id, source_context, request_id, correlation_id, trace_id, span_id, user_id, user_name,
                client_ip, user_agent, machine_name, application, version, request_path, request_method,
                status_code, duration_ms, request_headers_json, response_headers_json, request_body, response_body
            ) VALUES (
                @timestamp_utc, @level, @service_name, @environment, @message, @exception, @properties_json,
                @event_id, @source_context, @request_id, @correlation_id, @trace_id, @span_id, @user_id, @user_name,
                @client_ip, @user_agent, @machine_name, @application, @version, @request_path, @request_method,
                @status_code, @duration_ms, @request_headers_json, @response_headers_json, @request_body, @response_body
            );
            """;

        Add(command, "@timestamp_utc", logEvent.TimestampUtc.ToUniversalTime());
        Add(command, "@level", (int)logEvent.Level);
        Add(command, "@service_name", logEvent.ServiceName);
        Add(command, "@environment", logEvent.Environment);
        Add(command, "@message", logEvent.Message);
        Add(command, "@exception", logEvent.Exception);
        Add(command, "@properties_json", logEvent.PropertiesJson);
        Add(command, "@event_id", logEvent.EventId);
        Add(command, "@source_context", logEvent.SourceContext);
        Add(command, "@request_id", logEvent.RequestId);
        Add(command, "@correlation_id", logEvent.CorrelationId);
        Add(command, "@trace_id", logEvent.TraceId);
        Add(command, "@span_id", logEvent.SpanId);
        Add(command, "@user_id", logEvent.UserId);
        Add(command, "@user_name", logEvent.UserName);
        Add(command, "@client_ip", logEvent.ClientIp);
        Add(command, "@user_agent", logEvent.UserAgent);
        Add(command, "@machine_name", logEvent.MachineName);
        Add(command, "@application", logEvent.Application);
        Add(command, "@version", logEvent.Version);
        Add(command, "@request_path", logEvent.RequestPath);
        Add(command, "@request_method", logEvent.RequestMethod);
        Add(command, "@status_code", logEvent.StatusCode);
        Add(command, "@duration_ms", logEvent.DurationMs);
        Add(command, "@request_headers_json", logEvent.RequestHeadersJson);
        Add(command, "@response_headers_json", logEvent.ResponseHeadersJson);
        Add(command, "@request_body", logEvent.RequestBody);
        Add(command, "@response_body", logEvent.ResponseBody);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertBatchAsync(IReadOnlyList<LogEvent> logs, CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0)
        {
            return;
        }

        if (logs.Count == 1)
        {
            await InsertAsync(logs[0], cancellationToken);
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction tx = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = BuildBatchInsertSql(logs.Count);

        for (int i = 0; i < logs.Count; i++)
        {
            AddBatch(command, i, logs[i]);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<PagedResult<LogEvent>> SearchAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 100 : Math.Min(query.PageSize, 500);
        int offset = (page - 1) * pageSize;

        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);

        List<string> where = [];
        List<NpgsqlParameter> parameters = [];
        BuildFilters(query, where, parameters);
        string whereClause = where.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", where)}";

        int total = await ExecuteCountAsync(connection, whereClause, parameters, cancellationToken);
        IReadOnlyCollection<LogEvent> items = await ExecutePageAsync(connection, whereClause, parameters, pageSize, offset, cancellationToken);
        return new PagedResult<LogEvent>(items, total, page, pageSize);
    }

    private static void BuildFilters(LogQuery query, List<string> where, List<NpgsqlParameter> parameters)
    {
        AddDateFilter(query.FromUtc, ">=", "timestamp_utc", "@from_utc", where, parameters);
        AddDateFilter(query.ToUtc, "<=", "timestamp_utc", "@to_utc", where, parameters);

        if (query.Level.HasValue)
        {
            where.Add("level = @level");
            parameters.Add(new NpgsqlParameter("@level", (int)query.Level.Value));
        }

        AddEqualsFilter(query.ServiceName, "service_name", "@service_name", where, parameters);
        AddEqualsFilter(query.Environment, "environment", "@environment", where, parameters);
        AddEqualsFilter(query.TraceId, "trace_id", "@trace_id", where, parameters);
        AddEqualsFilter(query.CorrelationId, "correlation_id", "@correlation_id", where, parameters);
        AddEqualsFilter(query.RequestId, "request_id", "@request_id", where, parameters);
        AddEqualsFilter(query.RequestMethod, "request_method", "@request_method", where, parameters);

        if (query.StatusCode.HasValue)
        {
            where.Add("status_code = @status_code");
            parameters.Add(new NpgsqlParameter("@status_code", query.StatusCode.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            where.Add("""
                (
                    message ILIKE @search OR
                    exception ILIKE @search OR
                    properties_json ILIKE @search OR
                    service_name ILIKE @search OR
                    environment ILIKE @search OR
                    trace_id ILIKE @search OR
                    correlation_id ILIKE @search OR
                    request_id ILIKE @search OR
                    request_path ILIKE @search OR
                    source_context ILIKE @search OR
                    user_id ILIKE @search OR
                    user_name ILIKE @search
                )
                """);
            parameters.Add(new NpgsqlParameter("@search", $"%{query.SearchText.Trim()}%"));
        }
    }

    private static async Task<int> ExecuteCountAsync(NpgsqlConnection connection, string whereClause, IEnumerable<NpgsqlParameter> parameters, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM logs{whereClause};";
        foreach (NpgsqlParameter parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(parameter.ParameterName, parameter.Value));
        }

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task<IReadOnlyCollection<LogEvent>> ExecutePageAsync(
        NpgsqlConnection connection,
        string whereClause,
        IEnumerable<NpgsqlParameter> parameters,
        int pageSize,
        int offset,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                timestamp_utc, level, service_name, environment, message, exception, properties_json,
                event_id, source_context, request_id, correlation_id, trace_id, span_id, user_id, user_name,
                client_ip, user_agent, machine_name, application, version, request_path, request_method,
                status_code, duration_ms, request_headers_json, response_headers_json, request_body, response_body
            FROM logs
            {whereClause}
            ORDER BY timestamp_utc DESC, id DESC
            LIMIT @limit OFFSET @offset;
            """;

        foreach (NpgsqlParameter parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(parameter.ParameterName, parameter.Value));
        }

        command.Parameters.Add(new NpgsqlParameter("@limit", pageSize));
        command.Parameters.Add(new NpgsqlParameter("@offset", offset));

        List<LogEvent> items = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private static LogEvent Map(NpgsqlDataReader reader)
    {
        DateTime timestamp = reader.GetDateTime(0);
        return new LogEvent(
            DateTime.SpecifyKind(timestamp.ToUniversalTime(), DateTimeKind.Utc),
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

    private static string? GetString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static int? GetInt(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    private static double? GetDouble(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    private static void Add(NpgsqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static void AddBatch(NpgsqlCommand command, int index, LogEvent logEvent)
    {
        string p = $"_{index}";
        Add(command, $"@timestamp_utc{p}", logEvent.TimestampUtc.ToUniversalTime());
        Add(command, $"@level{p}", (int)logEvent.Level);
        Add(command, $"@service_name{p}", logEvent.ServiceName);
        Add(command, $"@environment{p}", logEvent.Environment);
        Add(command, $"@message{p}", logEvent.Message);
        Add(command, $"@exception{p}", logEvent.Exception);
        Add(command, $"@properties_json{p}", logEvent.PropertiesJson);
        Add(command, $"@event_id{p}", logEvent.EventId);
        Add(command, $"@source_context{p}", logEvent.SourceContext);
        Add(command, $"@request_id{p}", logEvent.RequestId);
        Add(command, $"@correlation_id{p}", logEvent.CorrelationId);
        Add(command, $"@trace_id{p}", logEvent.TraceId);
        Add(command, $"@span_id{p}", logEvent.SpanId);
        Add(command, $"@user_id{p}", logEvent.UserId);
        Add(command, $"@user_name{p}", logEvent.UserName);
        Add(command, $"@client_ip{p}", logEvent.ClientIp);
        Add(command, $"@user_agent{p}", logEvent.UserAgent);
        Add(command, $"@machine_name{p}", logEvent.MachineName);
        Add(command, $"@application{p}", logEvent.Application);
        Add(command, $"@version{p}", logEvent.Version);
        Add(command, $"@request_path{p}", logEvent.RequestPath);
        Add(command, $"@request_method{p}", logEvent.RequestMethod);
        Add(command, $"@status_code{p}", logEvent.StatusCode);
        Add(command, $"@duration_ms{p}", logEvent.DurationMs);
        Add(command, $"@request_headers_json{p}", logEvent.RequestHeadersJson);
        Add(command, $"@response_headers_json{p}", logEvent.ResponseHeadersJson);
        Add(command, $"@request_body{p}", logEvent.RequestBody);
        Add(command, $"@response_body{p}", logEvent.ResponseBody);
    }

    private static string BuildBatchInsertSql(int count)
    {
        List<string> rows = new(count);
        for (int i = 0; i < count; i++)
        {
            string p = $"_{i}";
            rows.Add(
                $"(@timestamp_utc{p}, @level{p}, @service_name{p}, @environment{p}, @message{p}, @exception{p}, @properties_json{p}, " +
                $"@event_id{p}, @source_context{p}, @request_id{p}, @correlation_id{p}, @trace_id{p}, @span_id{p}, @user_id{p}, @user_name{p}, " +
                $"@client_ip{p}, @user_agent{p}, @machine_name{p}, @application{p}, @version{p}, @request_path{p}, @request_method{p}, " +
                $"@status_code{p}, @duration_ms{p}, @request_headers_json{p}, @response_headers_json{p}, @request_body{p}, @response_body{p})");
        }

        return $"""
            INSERT INTO logs (
                timestamp_utc, level, service_name, environment, message, exception, properties_json,
                event_id, source_context, request_id, correlation_id, trace_id, span_id, user_id, user_name,
                client_ip, user_agent, machine_name, application, version, request_path, request_method,
                status_code, duration_ms, request_headers_json, response_headers_json, request_body, response_body
            ) VALUES
            {string.Join(",\n", rows)};
            """;
    }

    private static void AddEqualsFilter(string? value, string column, string name, List<string> where, List<NpgsqlParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        where.Add($"{column} = {name}");
        parameters.Add(new NpgsqlParameter(name, value.Trim()));
    }

    private static void AddDateFilter(DateTime? value, string op, string column, string name, List<string> where, List<NpgsqlParameter> parameters)
    {
        if (!value.HasValue)
        {
            return;
        }

        where.Add($"{column} {op} {name}");
        parameters.Add(new NpgsqlParameter(name, value.Value.ToUniversalTime()));
    }

    private static string ResolveConnectionString(StorageOptions options)
    {
        string? configured = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : options.Connections.PostgreSQL;

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Storage:Connections:PostgreSQL is required for PostgreSQL provider.");
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

            await using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using NpgsqlCommand createTable = connection.CreateCommand();
            createTable.CommandText = PostgresSchema.CreateLogsTableSql;
            await createTable.ExecuteNonQueryAsync(cancellationToken);

            await using NpgsqlCommand createIndexes = connection.CreateCommand();
            createIndexes.CommandText = PostgresSchema.CreateIndexesSql;
            await createIndexes.ExecuteNonQueryAsync(cancellationToken);

            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
