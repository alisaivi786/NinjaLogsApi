# NinjaLogs

NinjaLogs is a modular, enterprise-ready centralized logging platform (inspired by Seq), built as a .NET 9 modular monolith.

## What It Does
- Ingest structured logs via REST endpoints
- Store/query logs using pluggable storage providers
- Enforce ingestion API key security
- Apply quota, rate-limit, retention, and diagnostics controls
- Support provider switching by configuration

## Current Architecture
- **Pattern:** Modular monolith
- **Runtime:** .NET 9
- **Primary modules:** Logging, Identity, Authorization, ApiKeys
- **Storage abstraction:** `ILogStorage`

### Supported Storage Providers
- File (NDJSON)
- SegmentedFile
- SQLite
- SQL Server
- PostgreSQL

## API (Current)
- `POST /api/v1.0/logs` (and `/api/logs`)
- `GET /api/v1.0/logs` (and `/api/logs`)
- `POST /api/events/raw` (CLEF/Seq-style)
- `GET /health`
- `GET /api/v1.0/diagnostics/storage`
- `GET /api/v1.0/diagnostics/storage-health`
- `GET /api/v1.0/diagnostics/query-plan`
- `GET /api/v1.0/diagnostics/dlq/files`
- `POST /api/v1.0/diagnostics/dlq/replay`

## Key Runtime Features Implemented
- Async ingestion queue + background writer
- Durable spool queue with checkpoint/compaction
- Retry + dead-letter handling
- Query normalization planner
- Ingestion payload limits + API-key rate limiting
- Startup deployment/profile validation
- Multi-node guard for File/Segmented (single-writer lease lock)
- Retention background worker

## Build & Package Governance
Centralized management is enabled:
- `Directory.Build.props`
- `Directory.Packages.props`
- `Directory.Build.targets`

Rules:
- Warnings are treated as errors
- `Newtonsoft.Json` package usage is blocked
- `System.Text.Json` is the enforced JSON direction

## Run Locally
### Prerequisites
- .NET SDK 9

### Run API
```bash
dotnet restore NinjaLogs.sln --configfile NuGet.Config
dotnet run --project src/NinjaLogs.Api/NinjaLogs.Api.csproj
```

### Run Tests
```bash
./run-tests.command
```

Or manually:
```bash
dotnet test tests/NinjaLogs.UnitTests/NinjaLogs.UnitTests.csproj
dotnet test tests/NinjaLogs.IntegrationTests/NinjaLogs.IntegrationTests.csproj
```

## Docker
```bash
docker compose down
docker compose up --build -d
```

HTTPS ports (Docker API services):
- File: `https://localhost:8441`
- SQLite: `https://localhost:8442`
- Segmented: `https://localhost:8443`
- SQL Server: `https://localhost:8444`
- PostgreSQL: `https://localhost:8445`

## Configuration Profiles
In `src/NinjaLogs.Api/`:
- `appsettings.SingleNode.json`
- `appsettings.OnPremHa.json`
- `appsettings.CloudHa.json`

## Documentation
- Phase 1: `PHASE-1.md`
- Phase 2 (Docker): `PHASE-2-DOCKER.md`
- Phase 3 (Hardening): `PHASE-3-PLAN.md`
- Phase 4 (Throughput Focus): `PHASE-4.md`

## Current Open Items
- Exactly-once queue semantics (currently at-least-once)
- Cross-instance atomic quota backend
- Deep health metrics (p95/p99, fragmentation/index bloat)
- Full CI parity matrix enforcement for all relational providers
