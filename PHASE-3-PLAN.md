# Phase 3 Plan (Hardening Track)

## Scope
Phase 3 production-hardening foundation for ingestion reliability, query consistency, and operational safety.

## Completed
- [x] Async ingestion pipeline
  - API enqueue path (`ILogIngestionQueue`)
  - background writer service decoupled from request path
- [x] Durable spool-backed ingestion queue
  - accepted events are persisted before enqueue
  - restart replays queue from spool file (at-least-once delivery)
- [x] Durable queue checkpoint + compaction
  - processed events are acknowledged
  - checkpoint file maintained
  - spool compaction removes acked records
- [x] Configurable ingestion queue
  - queue capacity via `Storage:IngestionPipeline:QueueCapacity`
- [x] Background writer batching
  - batch writes with `BatchSize`
- [x] Retry + dead-letter support
  - retries with delay (`MaxWriteRetries`, `RetryDelayMs`)
  - failed events saved to `logs/deadletter/*.ndjson`
- [x] Query planner abstraction
  - `ILogQueryPlanner` + normalization logic used by query service
- [x] Index strategy contract
  - `ILogIndexStrategy` descriptors per provider for diagnostics
- [x] Storage runtime metrics
  - queued/written/failed/query counters
  - avg write/query latencies
- [x] Ingestion payload size limit
  - `MaxPayloadBytes` enforced, returns 413 when exceeded
- [x] Ingestion rate limiting per API key
  - fixed-window in-memory limit, returns 429 + `Retry-After`
- [x] Diagnostics redaction
  - masks sensitive connection-string values
  - hides path internals outside development
- [x] Multi-node safety policy (File/Segmented)
  - startup writer lease lock enforced
  - second writer instance fails fast with clear error
- [x] Atomic quota gate (single-node)
  - quota-check + enqueue are serialized with ingestion coordinator
- [x] Storage health endpoint
  - `GET /api/v1.0/diagnostics/storage-health`
  - provider-specific size/file-count details
- [x] Dead-letter replay tooling
  - `GET /api/v1.0/diagnostics/dlq/files`
  - `POST /api/v1.0/diagnostics/dlq/replay`
- [x] Retention execution worker
  - periodic cleanup for File/Segmented/SQLite/SQL Server/PostgreSQL
- [x] PostgreSQL provider implementation
  - insert/query implemented
  - schema + indexes bootstrap integrated
- [x] Provider parity contract tests
  - cross-provider semantics test for File/Segmented/SQLite
  - relational parity test path for SQLite/SQL Server/PostgreSQL when configured
- [x] Data redaction baseline
  - sensitive key masking for body/headers/properties
  - oversized bodies truncated
- [x] Schema versioning baseline
  - `SchemaVersions` table bootstrap on SQLite/SQL Server/PostgreSQL
- [x] Performance smoke assets
  - k6 ingestion smoke script added
- [x] Deployment profiles + startup safety validation
  - `appsettings.SingleNode.json`
  - `appsettings.OnPremHa.json`
  - `appsettings.CloudHa.json`
  - invalid profile/provider/node-mode combinations fail fast on startup
- [x] Centralized build/package management
  - `Directory.Build.props` for shared build settings
  - `Directory.Packages.props` for centralized NuGet versions
  - all project files cleaned from local TargetFramework/package versions
- [x] Architecture/build guardrails
  - `Directory.Build.targets` blocks `Newtonsoft.Json`
  - architecture test enforces centralized conventions
  - warnings treated as errors globally
- [x] CI hardening
  - warnings-as-errors enforced in CI build/test commands
  - optional manual perf-smoke (`k6`) workflow job
  - SQL Server + PostgreSQL test service support in workflow
- [x] Expanded test suite
  - unit tests for queue, sanitizer, validator, quota coordinator, DLQ replay, build conventions
  - integration tests for parity, retention, PostgreSQL storage, DLQ replay-like flow

## Config Added
`Storage:IngestionPipeline`
- `QueueCapacity`
- `BatchSize`
- `MaxWriteRetries`
- `RetryDelayMs`
- `DeadLetterDirectory`
- `MaxPayloadBytes`
- `MaxRequestsPerMinutePerApiKey`

`Storage:Deployment`
- `Profile` (`SingleNode`, `OnPremHa`, `CloudHa`)
- `NodeMode` (`SingleWriter`, `MultiWriter`)

## Open Gaps (Next)
1. Durable queue exactly-once semantics
   currently at-least-once; duplicate replay can still occur around crash boundaries.
2. Cross-instance atomic quota reservation
   currently atomic only inside one app instance.
3. Deep storage health analytics
   add queue age, fragmentation %, index bloat, and P95/P99 latency metrics.
4. CI provider parity matrix hard enforcement
   relational parity tests are present; add strict matrix gates for SQL Server + PostgreSQL in all CI branches.
5. External secret manager integration
   currently config-file/.env based by design.

## Out of Scope In Phase 3
- Dashboard auth/role policies (planned for next phase per roadmap).
