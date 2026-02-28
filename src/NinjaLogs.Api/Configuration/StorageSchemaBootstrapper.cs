using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Persistence;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Persistence;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Persistence;

namespace NinjaLogs.Api.Configuration;

public sealed class StorageSchemaBootstrapper(StorageOptions storageOptions)
{
    private readonly StorageOptions _storageOptions = storageOptions;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        string provider = _storageOptions.GetNormalizedProvider();
        switch (provider)
        {
            case "sqlite":
                await EnsureSqliteAsync(cancellationToken);
                break;
            case "sqlserver":
                await EnsureSqlServerAsync(cancellationToken);
                break;
            case "postgresql":
                await EnsurePostgresAsync(cancellationToken);
                break;
        }
    }

    private async Task EnsureSqliteAsync(CancellationToken cancellationToken)
    {
        string connectionString = ResolveSqliteConnectionString();
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand createTable = connection.CreateCommand();
        createTable.CommandText = SqliteSchema.CreateLogsTableSql;
        await createTable.ExecuteNonQueryAsync(cancellationToken);

        await using SqliteCommand createIndexes = connection.CreateCommand();
        createIndexes.CommandText = SqliteSchema.CreateIndexesSql;
        await createIndexes.ExecuteNonQueryAsync(cancellationToken);
        await EnsureSchemaVersionSqliteAsync(connection, cancellationToken);
    }

    private async Task EnsureSqlServerAsync(CancellationToken cancellationToken)
    {
        string? connectionString = !string.IsNullOrWhiteSpace(_storageOptions.ConnectionString)
            ? _storageOptions.ConnectionString
            : _storageOptions.Connections.SqlServer;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Storage SQL Server connection string is not configured.");
        }

        string normalized = connectionString.Trim();
        await EnsureSqlServerDatabaseExistsAsync(normalized, cancellationToken);

        await using SqlConnection connection = new(normalized);
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand createTable = connection.CreateCommand();
        createTable.CommandText = SqlServerSchema.CreateLogsTableSql;
        await createTable.ExecuteNonQueryAsync(cancellationToken);

        await using SqlCommand createIndexes = connection.CreateCommand();
        createIndexes.CommandText = SqlServerSchema.CreateIndexesSql;
        await createIndexes.ExecuteNonQueryAsync(cancellationToken);
        await EnsureSchemaVersionSqlServerAsync(connection, cancellationToken);
    }

    private static async Task EnsureSqlServerDatabaseExistsAsync(string connectionString, CancellationToken cancellationToken)
    {
        SqlConnectionStringBuilder target = new(connectionString);
        string databaseName = string.IsNullOrWhiteSpace(target.InitialCatalog) ? "master" : target.InitialCatalog;
        if (string.Equals(databaseName, "master", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SqlConnectionStringBuilder master = new(connectionString)
        {
            InitialCatalog = "master"
        };

        await using SqlConnection connection = new(master.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(@dbName) IS NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE [' + REPLACE(@dbName, ']', ']]') + N']';
                EXEC (@sql);
            END
            """;
        command.Parameters.AddWithValue("@dbName", databaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string ResolveSqliteConnectionString()
    {
        string? configured = !string.IsNullOrWhiteSpace(_storageOptions.ConnectionString)
            ? _storageOptions.ConnectionString
            : _storageOptions.Connections.SQLite;

        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "Data Source=logs/ninjalogs.db";
        }

        string connectionString = configured.Trim();
        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        string rawPath = connectionString[prefix.Length..].Trim();
        string fullPath = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rawPath));

        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return $"{prefix}{fullPath}";
    }

    private async Task EnsurePostgresAsync(CancellationToken cancellationToken)
    {
        string? connectionString = !string.IsNullOrWhiteSpace(_storageOptions.ConnectionString)
            ? _storageOptions.ConnectionString
            : _storageOptions.Connections.PostgreSQL;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Storage PostgreSQL connection string is not configured.");
        }

        await using NpgsqlConnection connection = new(connectionString.Trim());
        await connection.OpenAsync(cancellationToken);

        await using NpgsqlCommand createTable = connection.CreateCommand();
        createTable.CommandText = PostgresSchema.CreateLogsTableSql;
        await createTable.ExecuteNonQueryAsync(cancellationToken);

        await using NpgsqlCommand createIndexes = connection.CreateCommand();
        createIndexes.CommandText = PostgresSchema.CreateIndexesSql;
        await createIndexes.ExecuteNonQueryAsync(cancellationToken);
        await EnsureSchemaVersionPostgresAsync(connection, cancellationToken);
    }

    private static async Task EnsureSchemaVersionSqliteAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersions(Version TEXT PRIMARY KEY, AppliedUtc TEXT NOT NULL);
            INSERT OR IGNORE INTO SchemaVersions(Version, AppliedUtc) VALUES ('v1', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaVersionSqlServerAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using SqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF OBJECT_ID('dbo.SchemaVersions', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchemaVersions(Version NVARCHAR(50) PRIMARY KEY, AppliedUtc DATETIME2 NOT NULL);
            END
            IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE Version = 'v1')
            BEGIN
                INSERT INTO dbo.SchemaVersions(Version, AppliedUtc) VALUES ('v1', SYSUTCDATETIME());
            END
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaVersionPostgresAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schemaversions(version TEXT PRIMARY KEY, applied_utc TIMESTAMPTZ NOT NULL);
            INSERT INTO schemaversions(version, applied_utc)
            VALUES ('v1', NOW())
            ON CONFLICT (version) DO NOTHING;
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
