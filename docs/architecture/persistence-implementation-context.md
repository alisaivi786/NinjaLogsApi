# NinjaLogs Persistence Implementation Context

## Purpose
This document captures the intended implementation style for storage providers so we keep a consistent architecture while moving from MVP to enterprise providers.

## Pattern Borrowed from Enterprise Codebases
- Keep configuration in typed options classes.
- Keep provider selection in DI/bootstrap code.
- Keep provider internals behind interfaces consumed by services/controllers.
- Separate storage classes from provider-specific persistence/repository layers.

## Current Scaffold
- `ILogStorage` is the application-facing contract.
- `FileLogStorage` is the Phase 1 provider.
- Relational providers are split into:
  - `.../Persistence/*Schema.cs` for schema SQL constants
  - `.../Repositories/*LogEventRepository.cs` for query/insert logic
  - `.../*LogStorage.cs` adapter implementing `ILogStorage`

## Files To Extend Next
- `SQLite/Repositories/SqliteLogEventRepository.cs`
- `SqlServer/Repositories/SqlServerLogEventRepository.cs`
- `PostgreSQL/Repositories/PostgresLogEventRepository.cs`

## Rules
- Do not expose provider-specific types outside provider folders.
- Keep timestamps in UTC.
- Keep `LogEvent` storage-agnostic.
- Keep API/controller layer unaware of provider choice.
