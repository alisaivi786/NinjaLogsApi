# Performance Smoke

Run with k6:

```bash
k6 run tests/perf/ingestion-smoke.js -e BASE_URL=http://localhost:8082 -e API_KEY=dev-ingestion-key
```

Use provider-specific base URL as needed.

Phase 4 baseline example (larger payload, stronger pressure):

```bash
k6 run tests/perf/ingestion-smoke.js \
  -e BASE_URL=http://localhost:8085 \
  -e API_KEY=dev-ingestion-key \
  -e VUS=80 \
  -e DURATION=120s \
  -e PAYLOAD_PADDING_BYTES=300
```

## Provider Benchmark + JSON Report

Run storage-provider benchmarks and generate a JSON report:

```bash
tests/perf/run-provider-benchmarks.sh
```

Optional env vars:

```bash
export PERF_EVENTS=20000
export PERF_BATCH_SIZE=200
export PERF_REPORT_PATH=tests/perf/reports/provider-performance.json
export NINJALOGS_POSTGRES_CS='Host=localhost;Port=5432;Database=ninjalogs;Username=postgres;Password=postgres'
export NINJALOGS_SQLSERVER_CS='Server=localhost,1433;User ID=sa;Password=Your_password123;Initial Catalog=NinjaLogs;TrustServerCertificate=True;Encrypt=False;'
tests/perf/run-provider-benchmarks.sh
```

Report includes per-provider:
- status (`ok`, `skipped`, `error`)
- single insert performance
- bulk insert performance (PostgreSQL when configured)
- query timing

## End-to-End API Benchmark + JSON Report

This benchmark hits `POST /api/v1.0/logs` directly and samples diagnostics for queue depth/runtime metrics.

Run:

```bash
tests/perf/run-e2e-benchmark.sh
```

Recommended env vars for Phase 4:

```bash
export PERF_BASE_URL=http://localhost:8085
export PERF_API_KEY=dev-ingestion-key
export PERF_CONCURRENCY=80
export PERF_DURATION_SECONDS=120
export PERF_PAYLOAD_PADDING_BYTES=300
export PERF_DIAGNOSTICS_INTERVAL_MS=1000
export PERF_REPORT_PATH=tests/perf/reports/e2e-performance.json
tests/perf/run-e2e-benchmark.sh
```

E2E report fields:
- request status mix (`202`, `429`, `413`, etc.)
- accepted logs/sec
- request latency p50/p95/p99
- queue depth stats and timeline samples
- end-of-run runtime metrics (`writtenPerSecond`, `p95IngestionLatencyMs`, `p95DbInsertDurationMs`)

## Combined Phase 4 Run (Provider + E2E + Merged JSON)

Run all benchmarks and generate one combined JSON:

```bash
tests/perf/run-cloudha-throughput-benchmarks.sh
```

Real Phase 4 local run (CloudHa + PostgreSQL):

```bash
export NINJALOGS_POSTGRES_CS='Host=localhost;Port=5432;Database=ninjalogs;Username=postgres;Password=postgres'
export PERF_BASE_URL=http://localhost:8085
export PERF_API_KEY=dev-ingestion-key
export PERF_CONCURRENCY=80
export PERF_DURATION_SECONDS=120
tests/perf/run-cloudha-throughput-benchmarks.sh
```

Outputs:
- `provider-performance-*.json`
- `e2e-performance-*.json`
- `cloudha-throughput-combined-*.json` (includes summary checks for 2k logs/sec and p95 latency target)

Optional output path env vars:
- `PERF_REPORT_DIR`
- `PERF_PROVIDER_REPORT_PATH`
- `PERF_E2E_REPORT_PATH`
- `PERF_COMBINED_REPORT_PATH`

## Git Hygiene

- Generated benchmark reports in `tests/perf/reports/*.json` are ignored by git.
- Runtime API data/log artifacts are ignored (`src/NinjaLogs.Api/logs`, `src/NinjaLogs.Api/data`).
- Tracked examples live in `tests/perf/report-examples/`:
  - `provider-performance.sample.json`
  - `e2e-performance.sample.json`
  - `cloudha-throughput-combined.sample.json`
