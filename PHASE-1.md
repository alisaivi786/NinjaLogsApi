# NinjaLogs Phase 1 (Implementation Status)

## Goal
Deliver a clean, modular monolith MVP for centralized structured logging, with pluggable storage and Seq-inspired ingestion/query patterns.

## Current Framework/Architecture
- Runtime: `.NET 9`
- Architecture: modular monolith (`src/` + `tests/`)
- Module boundaries enforced through project references
- Logging module split into:
  - `Domain`
  - `Application`
  - `Infrastructure`

## Implemented Modules and Foundations
- Solution and project structure completed
- Shared architecture docs:
  - `docs/architecture/storage-strategy.md`
  - `docs/architecture/persistence-implementation-context.md`
- Logging domain implemented:
  - `LogEvent` (extended structured fields)
  - `LogLevel`
  - `LogQuery`
  - `PagedResult<T>`
- Application services implemented:
  - `ILogIngestionService` + `LogIngestionService`
  - `ILogQueryService` + `LogQueryService`
- Storage abstraction implemented:
  - `ILogStorage`

## API Implemented (Working)
- `GET /health`
- `POST /api/v1.0/logs` (JSON ingestion)
- `POST /api/logs` (alias)
- `GET /api/v1.0/logs` (query with filters + paging)
- `GET /api/logs` (alias)
- `POST /api/events/raw` (Seq/CLEF-style ingestion)
- `GET /api/v1.0/diagnostics/storage` (active provider/path diagnostics)

## Authentication Implemented for Ingestion
- API key validation on ingestion endpoints
- Header: `X-Api-Key`
- Config section: `ApiKeys:IngestionKeys`
- Returns `401` for missing/invalid key

## Storage Providers Status
1. `File` provider: implemented and working
- NDJSON append-only
- one file per day
- filtering + paging in query

2. `SQLite` provider: implemented and working
- auto schema creation
- indexes for common filters
- insert + query + paging

3. `SqlServer` provider: implemented and wired
- auto schema creation
- indexes for common filters
- insert + query + paging

4. `PostgreSQL` provider: scaffold only (not implemented yet)

## Structured Logging Fields Supported
- Core: timestamp, level, message, service, environment, exception
- Metadata: eventId, sourceContext, requestId, correlationId, traceId, spanId
- User/client: userId, userName, clientIp, userAgent
- App context: machineName, application, version
- HTTP context: requestPath, requestMethod, statusCode, durationMs
- Payloads: requestHeadersJson, responseHeadersJson, requestBody, responseBody
- Custom data: propertiesJson

## Query Filters Implemented
- `fromUtc`, `toUtc`
- `level`
- `serviceName`, `environment`
- `traceId`, `correlationId`, `requestId`
- `requestMethod`, `statusCode`
- `searchText`
- `page`, `pageSize`

## Provider Switching (No Deleting Config Needed)
- Keep all provider connection strings in config
- Switch only:
  - `Storage:Provider = File | SQLite | SqlServer | PostgreSQL`
- Launch profiles available:
  - `https-file`
  - `https-sqlite`
  - `https-sqlserver`

## Postman Assets
- `docs/postman/NinjaLogs-Phase1.postman_collection.json`
- `docs/postman/NinjaLogs-Local.postman_environment.json`

## Directory Structure
```text
NinjaLogs-BE/
├── docs/
│   ├── architecture/
│   │   ├── persistence-implementation-context.md
│   │   └── storage-strategy.md
│   └── postman/
│       ├── NinjaLogs-Local.postman_environment.json
│       └── NinjaLogs-Phase1.postman_collection.json
├── src/
│   ├── NinjaLogs.Api/
│   │   ├── Configuration/
│   │   ├── Controllers/
│   │   ├── Properties/
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── NinjaLogs.Infrastructure/
│   ├── NinjaLogs.Modules.ApiKeys/
│   ├── NinjaLogs.Modules.Authorization/
│   ├── NinjaLogs.Modules.Identity/
│   ├── NinjaLogs.Modules.Logging/
│   │   ├── Application/
│   │   │   ├── Interfaces/
│   │   │   └── Services/
│   │   ├── Domain/
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   └── Models/
│   │   └── Infrastructure/
│   │       ├── Options/
│   │       └── Storage/
│   │           ├── File/
│   │           ├── PostgreSQL/
│   │           ├── Relational/
│   │           ├── SQLite/
│   │           └── SqlServer/
│   └── NinjaLogs.Shared/
├── tests/
│   ├── NinjaLogs.IntegrationTests/
│   └── NinjaLogs.UnitTests/
├── NinjaLogs.sln
└── PHASE-1.md
```

## Known Caveats
- `/api/events/raw` expects CLEF-compatible input; supports NDJSON and single JSON body
- PostgreSQL provider remains a scaffold
- OIDC wiring is not implemented in Phase 1 yet

## Phase 1 Outcome
Phase 1 foundation is functionally delivered for ingest/query with pluggable storage and working File/SQLite/SqlServer paths, while preserving modular architecture for future expansion.
