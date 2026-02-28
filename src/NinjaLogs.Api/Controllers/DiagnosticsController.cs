using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Data;
using System.Text;

namespace NinjaLogs.Api.Controllers;

[Route("api/v1.0/diagnostics")]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(StorageOptions storageOptions) : BaseApiController
{
    private readonly StorageOptions _storageOptions = storageOptions;

    [HttpGet("storage")]
    public IActionResult GetStorage()
    {
        return Ok(new
        {
            provider = _storageOptions.Provider,
            connectionString = _storageOptions.ConnectionString,
            logsDirectory = _storageOptions.LogsDirectory,
            currentDirectory = Directory.GetCurrentDirectory(),
            baseDirectory = AppContext.BaseDirectory
        });
    }

    [HttpGet("query-plan")]
    public async Task<IActionResult> GetQueryPlan(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? level,
        [FromQuery] string? serviceName,
        [FromQuery] string? environment,
        [FromQuery] string? traceId,
        [FromQuery] string? correlationId,
        [FromQuery] string? requestId,
        [FromQuery] string? requestMethod,
        [FromQuery] int? statusCode,
        [FromQuery] string? searchText,
        CancellationToken cancellationToken = default)
    {
        string provider = _storageOptions.GetNormalizedProvider();

        return provider switch
        {
            "sqlite" => Ok(await GetSqlitePlanAsync(fromUtc, toUtc, level, serviceName, environment, traceId, correlationId, requestId, requestMethod, statusCode, searchText, cancellationToken)),
            "sqlserver" => Ok(await GetSqlServerPlanAsync(fromUtc, toUtc, level, serviceName, environment, traceId, correlationId, requestId, requestMethod, statusCode, searchText, cancellationToken)),
            _ => BadRequest(new { message = $"Query plan diagnostics currently supports SQLite and SqlServer. Active provider: {_storageOptions.Provider}" })
        };
    }

    private async Task<object> GetSqlitePlanAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int? level,
        string? serviceName,
        string? environment,
        string? traceId,
        string? correlationId,
        string? requestId,
        string? requestMethod,
        int? statusCode,
        string? searchText,
        CancellationToken cancellationToken)
    {
        string connectionString = ResolveSqliteConnectionString(_storageOptions);
        List<string> where = [];
        List<SqliteParameter> parameters = [];
        BuildFilters(where, parameters, fromUtc, toUtc, level, serviceName, environment, traceId, correlationId, requestId, requestMethod, statusCode, searchText);
        string whereClause = where.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", where)}";

        string sql = $"""
            EXPLAIN QUERY PLAN
            SELECT Id, TimestampUtc, Level, Message
            FROM Logs
            {whereClause}
            ORDER BY TimestampUtc DESC, Id DESC
            LIMIT $limit OFFSET $offset;
            """;

        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (SqliteParameter parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.ParameterName, parameter.Value);
        }

        command.Parameters.AddWithValue("$limit", 50);
        command.Parameters.AddWithValue("$offset", 0);

        List<object> planRows = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            planRows.Add(new
            {
                selectId = reader.GetInt32(0),
                order = reader.GetInt32(1),
                from = reader.GetInt32(2),
                detail = reader.GetString(3)
            });
        }

        return new
        {
            provider = "SQLite",
            sql,
            parameters = parameters.ToDictionary(p => p.ParameterName, p => p.Value),
            plan = planRows
        };
    }

    private async Task<object> GetSqlServerPlanAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int? level,
        string? serviceName,
        string? environment,
        string? traceId,
        string? correlationId,
        string? requestId,
        string? requestMethod,
        int? statusCode,
        string? searchText,
        CancellationToken cancellationToken)
    {
        string connectionString = ResolveSqlServerConnectionString(_storageOptions);
        List<string> where = [];
        List<SqlParameter> parameters = [];
        BuildFilters(where, parameters, fromUtc, toUtc, level, serviceName, environment, traceId, correlationId, requestId, requestMethod, statusCode, searchText);
        string whereClause = where.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", where)}";

        string selectSql = $"""
            SELECT Id, TimestampUtc, Level, Message
            FROM dbo.Logs
            {whereClause}
            ORDER BY TimestampUtc DESC, Id DESC
            OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;
            """;

        string showPlanSql = $"SET SHOWPLAN_TEXT ON; {selectSql} SET SHOWPLAN_TEXT OFF;";

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = showPlanSql;

        foreach (SqlParameter parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.SqlDbType) { Value = parameter.Value });
        }

        command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = 0 });
        command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = 50 });

        List<string> planLines = [];
        try
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    planLines.Add(reader.GetString(0));
                }
            }
        }
        catch (SqlException ex)
        {
            planLines.Add($"SHOWPLAN failed: {ex.Message}");
        }

        return new
        {
            provider = "SqlServer",
            sql = selectSql,
            parameters = parameters.ToDictionary(p => p.ParameterName, p => p.Value),
            plan = planLines
        };
    }

    private static void BuildFilters(List<string> where, List<SqliteParameter> parameters,
        DateTime? fromUtc, DateTime? toUtc, int? level, string? serviceName, string? environment, string? traceId,
        string? correlationId, string? requestId, string? requestMethod, int? statusCode, string? searchText)
    {
        if (fromUtc.HasValue)
        {
            where.Add("TimestampUtc >= $fromUtc");
            parameters.Add(new SqliteParameter("$fromUtc", fromUtc.Value.ToUniversalTime().ToString("O")));
        }

        if (toUtc.HasValue)
        {
            where.Add("TimestampUtc <= $toUtc");
            parameters.Add(new SqliteParameter("$toUtc", toUtc.Value.ToUniversalTime().ToString("O")));
        }

        AddEq(where, parameters, "Level", "$level", level);
        AddEq(where, parameters, "ServiceName", "$serviceName", serviceName);
        AddEq(where, parameters, "Environment", "$environment", environment);
        AddEq(where, parameters, "TraceId", "$traceId", traceId);
        AddEq(where, parameters, "CorrelationId", "$correlationId", correlationId);
        AddEq(where, parameters, "RequestId", "$requestId", requestId);
        AddEq(where, parameters, "RequestMethod", "$requestMethod", requestMethod);
        AddEq(where, parameters, "StatusCode", "$statusCode", statusCode);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            where.Add("(Message LIKE $search OR PropertiesJson LIKE $search OR Exception LIKE $search)");
            parameters.Add(new SqliteParameter("$search", $"%{searchText.Trim()}%"));
        }
    }

    private static void BuildFilters(List<string> where, List<SqlParameter> parameters,
        DateTime? fromUtc, DateTime? toUtc, int? level, string? serviceName, string? environment, string? traceId,
        string? correlationId, string? requestId, string? requestMethod, int? statusCode, string? searchText)
    {
        if (fromUtc.HasValue)
        {
            where.Add("TimestampUtc >= @fromUtc");
            parameters.Add(new SqlParameter("@fromUtc", SqlDbType.DateTime2) { Value = fromUtc.Value.ToUniversalTime() });
        }

        if (toUtc.HasValue)
        {
            where.Add("TimestampUtc <= @toUtc");
            parameters.Add(new SqlParameter("@toUtc", SqlDbType.DateTime2) { Value = toUtc.Value.ToUniversalTime() });
        }

        AddEq(where, parameters, "Level", "@level", SqlDbType.Int, level);
        AddEq(where, parameters, "ServiceName", "@serviceName", SqlDbType.NVarChar, serviceName);
        AddEq(where, parameters, "Environment", "@environment", SqlDbType.NVarChar, environment);
        AddEq(where, parameters, "TraceId", "@traceId", SqlDbType.NVarChar, traceId);
        AddEq(where, parameters, "CorrelationId", "@correlationId", SqlDbType.NVarChar, correlationId);
        AddEq(where, parameters, "RequestId", "@requestId", SqlDbType.NVarChar, requestId);
        AddEq(where, parameters, "RequestMethod", "@requestMethod", SqlDbType.NVarChar, requestMethod);
        AddEq(where, parameters, "StatusCode", "@statusCode", SqlDbType.Int, statusCode);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            where.Add("(Message LIKE @search OR PropertiesJson LIKE @search OR Exception LIKE @search)");
            parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar) { Value = $"%{searchText.Trim()}%" });
        }
    }

    private static void AddEq(List<string> where, List<SqliteParameter> parameters, string column, string name, object? value)
    {
        if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            return;
        }

        where.Add($"{column} = {name}");
        parameters.Add(new SqliteParameter(name, value));
    }

    private static void AddEq(List<string> where, List<SqlParameter> parameters, string column, string name, SqlDbType dbType, object? value)
    {
        if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            return;
        }

        where.Add($"{column} = {name}");
        parameters.Add(new SqlParameter(name, dbType) { Value = value });
    }

    private static string ResolveSqliteConnectionString(StorageOptions options)
    {
        string? configured = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : options.Connections.SQLite;

        if (string.IsNullOrWhiteSpace(configured))
        {
            return "Data Source=logs/ninjalogs.db";
        }

        string connectionString = configured.Trim();
        const string prefix = "Data Source=";
        if (connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            string rawPath = connectionString[prefix.Length..].Trim();
            if (!Path.IsPathRooted(rawPath))
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

    private static string ResolveSqlServerConnectionString(StorageOptions options)
    {
        string? configured = !string.IsNullOrWhiteSpace(options.ConnectionString)
            ? options.ConnectionString
            : options.Connections.SqlServer;

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Storage SQL Server connection string is not configured.");
        }

        return configured.Trim();
    }
}
