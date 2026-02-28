# NinjaLogs Storage Strategy

## Overview
NinjaLogs uses a pluggable storage architecture so storage providers can be swapped without changing application services or API layers.

Supported providers:
- File (MVP)
- SQLite
- SQL Server
- PostgreSQL

## Abstraction Contract
All implementations must satisfy a shared interface:

```csharp
public interface ILogStorage
{
    Task AppendAsync(LogEvent log, CancellationToken cancellationToken = default);
    Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);
}
```

`ILogStorage` is the only dependency consumed by application services/controllers for log persistence and querying.

## Providers
### 1. File-Based (Phase 1)
- `FileLogStorage`
- Append-only NDJSON
- One file per day
- Stream-based reads
- In-memory filtering
- No indexing

### 2. SQLite (Phase 2)
- `SqliteLogStorage`
- Embedded DB
- Indexed columns
- Single file deployment (`.db`)

### 3. SQL Server (Future)
- `SqlServerLogStorage`
- Enterprise concurrency and scale options

### 4. PostgreSQL (Future)
- `PostgresLogStorage`
- Strong JSON/query capabilities and advanced indexing

## Configuration Switching
Storage choice is configuration-driven:

```json
{
  "Storage": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=ninjalogs.db"
  }
}
```

Supported values:
- `File`
- `SQLite`
- `SqlServer`
- `PostgreSQL`

## Design Rules
- Keep `LogEvent` storage-agnostic.
- Do not leak DB-specific types outside storage implementations.
- Do not expose `IQueryable` outside storage layer.
- Store flexible properties as JSON payload where needed.
- Use UTC timestamps for persisted events.

## Migration Path
1. Phase 1: file storage + basic filtering
2. Phase 2: SQLite + indexes
3. Phase 3: SQL Server or PostgreSQL

Storage switching should require only configuration and connection-string changes.
