namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Persistence;

public static class SqliteSchema
{
    public const string CreateLogsTableSql = """
        CREATE TABLE IF NOT EXISTS Logs (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TimestampUtc TEXT NOT NULL,
            Level INTEGER NOT NULL,
            ServiceName TEXT NULL,
            Environment TEXT NULL,
            Message TEXT NOT NULL,
            Exception TEXT NULL,
            PropertiesJson TEXT NULL,
            EventId TEXT NULL,
            SourceContext TEXT NULL,
            RequestId TEXT NULL,
            CorrelationId TEXT NULL,
            TraceId TEXT NULL,
            SpanId TEXT NULL,
            UserId TEXT NULL,
            UserName TEXT NULL,
            ClientIp TEXT NULL,
            UserAgent TEXT NULL,
            MachineName TEXT NULL,
            Application TEXT NULL,
            Version TEXT NULL,
            RequestPath TEXT NULL,
            RequestMethod TEXT NULL,
            StatusCode INTEGER NULL,
            DurationMs REAL NULL,
            RequestHeadersJson TEXT NULL,
            ResponseHeadersJson TEXT NULL,
            RequestBody TEXT NULL,
            ResponseBody TEXT NULL
        );
        """;

    public const string CreateIndexesSql = """
        CREATE INDEX IF NOT EXISTS idx_logs_timestamp_id ON Logs(TimestampUtc DESC, Id DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_level ON Logs(Level);
        CREATE INDEX IF NOT EXISTS idx_logs_service ON Logs(ServiceName);
        CREATE INDEX IF NOT EXISTS idx_logs_trace ON Logs(TraceId);
        CREATE INDEX IF NOT EXISTS idx_logs_correlation ON Logs(CorrelationId);
        CREATE INDEX IF NOT EXISTS idx_logs_request_id ON Logs(RequestId);
        CREATE INDEX IF NOT EXISTS idx_logs_status ON Logs(StatusCode);
        CREATE INDEX IF NOT EXISTS idx_logs_level_timestamp ON Logs(Level, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_service_timestamp ON Logs(ServiceName, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_environment_timestamp ON Logs(Environment, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_trace_timestamp ON Logs(TraceId, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_correlation_timestamp ON Logs(CorrelationId, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_request_id_timestamp ON Logs(RequestId, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_request_method_timestamp ON Logs(RequestMethod, TimestampUtc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_status_timestamp ON Logs(StatusCode, TimestampUtc DESC);
        """;
}
