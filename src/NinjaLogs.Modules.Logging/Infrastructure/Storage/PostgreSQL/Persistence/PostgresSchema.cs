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
            properties_json JSONB NULL
        );
        """;
}
