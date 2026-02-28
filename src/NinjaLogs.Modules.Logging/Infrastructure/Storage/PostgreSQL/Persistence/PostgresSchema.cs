namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Persistence;

public static class PostgresSchema
{
    public const string CreateLogsTableSql = """
        CREATE TABLE IF NOT EXISTS logs (
            id BIGSERIAL PRIMARY KEY,
            timestamp_utc TIMESTAMPTZ NOT NULL,
            level INT NOT NULL,
            service_name TEXT NULL,
            environment TEXT NULL,
            message TEXT NOT NULL,
            exception TEXT NULL,
            properties_json TEXT NULL,
            event_id TEXT NULL,
            source_context TEXT NULL,
            request_id TEXT NULL,
            correlation_id TEXT NULL,
            trace_id TEXT NULL,
            span_id TEXT NULL,
            user_id TEXT NULL,
            user_name TEXT NULL,
            client_ip TEXT NULL,
            user_agent TEXT NULL,
            machine_name TEXT NULL,
            application TEXT NULL,
            version TEXT NULL,
            request_path TEXT NULL,
            request_method TEXT NULL,
            status_code INT NULL,
            duration_ms DOUBLE PRECISION NULL,
            request_headers_json TEXT NULL,
            response_headers_json TEXT NULL,
            request_body TEXT NULL,
            response_body TEXT NULL
        );
        """;

    public const string CreateIndexesSql = """
        CREATE INDEX IF NOT EXISTS idx_logs_timestamp_id ON logs(timestamp_utc DESC, id DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_level ON logs(level);
        CREATE INDEX IF NOT EXISTS idx_logs_service ON logs(service_name);
        CREATE INDEX IF NOT EXISTS idx_logs_trace ON logs(trace_id);
        CREATE INDEX IF NOT EXISTS idx_logs_correlation ON logs(correlation_id);
        CREATE INDEX IF NOT EXISTS idx_logs_request_id ON logs(request_id);
        CREATE INDEX IF NOT EXISTS idx_logs_status ON logs(status_code);
        CREATE INDEX IF NOT EXISTS idx_logs_level_timestamp ON logs(level, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_service_timestamp ON logs(service_name, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_environment_timestamp ON logs(environment, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_trace_timestamp ON logs(trace_id, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_correlation_timestamp ON logs(correlation_id, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_request_id_timestamp ON logs(request_id, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_request_method_timestamp ON logs(request_method, timestamp_utc DESC);
        CREATE INDEX IF NOT EXISTS idx_logs_status_timestamp ON logs(status_code, timestamp_utc DESC);
        """;
}
