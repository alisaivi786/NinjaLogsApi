# Phase 4: Throughput Focus

## Objective
Sustain **2,000 logs/sec per node** in `CloudHa` with PostgreSQL, with:
- average payload around **500 bytes**
- **P95 ingestion latency < 150 ms**
- stable queue depth (no unbounded growth)

## Scope
Only throughput-path work is in-scope:
1. PostgreSQL bulk insert path (batched values, one transaction per batch, reused connection)
2. Batch tuning (`BatchSize` baseline 200)
3. Writer concurrency tuning (`WriterWorkers` max 2)
4. Focused performance tests with representative payload
5. PostgreSQL write-path tuning (WAL + pool + checkpoints)

Out of scope for this phase:
- new storage providers
- auth platform changes (OIDC, etc.)
- query/filter feature expansion
- dashboard/alerting work

## Runtime Baseline (implemented)
- `IngestionPipeline:BatchSize = 200` in HA profiles
- `IngestionPipeline:WriterWorkers = 2` in HA profiles
- PostgreSQL batch-write support in storage layer
- Queue acknowledgment safety for parallel writers
- Focus metrics surfaced from diagnostics:
  - `writtenPerSecond`
  - `p95IngestionLatencyMs`
  - `p95DbInsertDurationMs`
  - queue depth

## Implementation Map (code-aligned)
- PostgreSQL bulk path:
  - `src/NinjaLogs.Modules.Logging/Application/Interfaces/IBulkLogStorage.cs`
  - `src/NinjaLogs.Modules.Logging/Infrastructure/Storage/PostgreSQL/PostgresLogStorage.cs`
  - `src/NinjaLogs.Modules.Logging/Infrastructure/Storage/PostgreSQL/Repositories/PostgresLogEventRepository.cs`
- Parallel writer + batch persistence:
  - `src/NinjaLogs.Api/Configuration/QueuedLogWriterBackgroundService.cs`
- Queue item/ack safety for multi-worker writer:
  - `src/NinjaLogs.Modules.Logging/Application/Interfaces/IngestionQueueItem.cs`
  - `src/NinjaLogs.Modules.Logging/Application/Interfaces/ILogIngestionQueue.cs`
  - `src/NinjaLogs.Api/Configuration/DurableSpoolIngestionQueue.cs`
- Runtime metrics:
  - `src/NinjaLogs.Api/Configuration/StorageRuntimeMetrics.cs`
  - `src/NinjaLogs.Api/Controllers/DiagnosticsController.cs`
- HA profile defaults:
  - `src/NinjaLogs.Api/appsettings.CloudHa.json`
  - `src/NinjaLogs.Api/appsettings.OnPremHa.json`

## Acceptance Criteria
Phase 4 is complete when all are true in sustained load:
1. `writtenPerSecond >= 2000` per node
2. `p95IngestionLatencyMs < 150`
3. queue depth remains bounded and recovers after spikes
4. no DLQ growth under normal operation
5. no sustained write-failure growth

## Measurement Endpoints
- `GET /api/v1.0/diagnostics/storage`
- `GET /api/v1.0/diagnostics/storage-health`

Track only:
1. logs/sec written
2. p95 ingestion latency
3. queue depth over time
4. p95 DB insert duration

## Benchmark Tooling (implemented)
- Provider benchmark runner:
  - `tests/NinjaLogs.PerformanceRunner/`
  - script: `tests/perf/run-provider-benchmarks.sh`
- End-to-end API benchmark runner:
  - `tests/NinjaLogs.E2EPerfRunner/`
  - script: `tests/perf/run-e2e-benchmark.sh`
- Combined merge runner:
  - `tests/NinjaLogs.PerfMergeRunner/`
  - script: `tests/perf/run-cloudha-throughput-benchmarks.sh`

## Real Local Run (CloudHa + PostgreSQL)
```bash
export NINJALOGS_POSTGRES_CS='Host=localhost;Port=5432;Database=ninjalogs;Username=postgres;Password=postgres'
export PERF_BASE_URL=http://localhost:8085
export PERF_API_KEY=dev-ingestion-key
export PERF_CONCURRENCY=80
export PERF_DURATION_SECONDS=120
tests/perf/run-cloudha-throughput-benchmarks.sh
```

## Report Outputs
Combined run writes:
- `tests/perf/reports/provider-performance-*.json`
- `tests/perf/reports/e2e-performance-*.json`
- `tests/perf/reports/cloudha-throughput-combined-*.json`

Console summary includes:
- achieved throughput/latency snapshot
- SQL provider-by-provider details
- top failure reasons (`401`, `429`, `413`, `other`, `exceptions`)
- target check pass/fail list

## CI Integration
Workflow:
- `.github/workflows/cloudha-throughput-benchmark-report.yml`

Behavior:
- auto-runs on push to `main` (merge path)
- starts PostgreSQL + API in CloudHa mode
- runs combined benchmark script
- uploads artifact `cloudha-throughput-benchmark-reports`
- renders readable markdown summary in GitHub Actions Job Summary

## Git Hygiene
- Generated reports are ignored:
  - `tests/perf/reports/*.json`
- Runtime artifacts are ignored:
  - `src/NinjaLogs.Api/logs/`
  - `src/NinjaLogs.Api/data/`
- Example tracked report schema samples:
  - `tests/perf/report-examples/provider-performance.sample.json`
  - `tests/perf/report-examples/e2e-performance.sample.json`
  - `tests/perf/report-examples/cloudha-throughput-combined.sample.json`
