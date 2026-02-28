#!/usr/bin/env bash
set -euo pipefail

STAMP="$(date -u +%Y%m%d-%H%M%S)"
REPORT_DIR="${PERF_REPORT_DIR:-tests/perf/reports}"
PROVIDER_REPORT="${PERF_PROVIDER_REPORT_PATH:-$REPORT_DIR/provider-performance-$STAMP.json}"
E2E_REPORT="${PERF_E2E_REPORT_PATH:-$REPORT_DIR/e2e-performance-$STAMP.json}"
COMBINED_REPORT="${PERF_COMBINED_REPORT_PATH:-$REPORT_DIR/cloudha-throughput-combined-$STAMP.json}"

mkdir -p "$REPORT_DIR"

echo "[1/3] Running provider benchmark..."
PERF_REPORT_PATH="$PROVIDER_REPORT" tests/perf/run-provider-benchmarks.sh

echo "[2/3] Running end-to-end benchmark..."
PERF_REPORT_PATH="$E2E_REPORT" tests/perf/run-e2e-benchmark.sh

echo "[3/3] Merging reports..."
dotnet run --project tests/NinjaLogs.PerfMergeRunner/NinjaLogs.PerfMergeRunner.csproj -- "$PROVIDER_REPORT" "$E2E_REPORT" "$COMBINED_REPORT"

echo "Provider report: $PROVIDER_REPORT"
echo "E2E report: $E2E_REPORT"
echo "Combined report: $COMBINED_REPORT"

python3 - "$PROVIDER_REPORT" "$E2E_REPORT" "$COMBINED_REPORT" <<'PY'
import json, sys

provider_path, e2e_path, combined_path = sys.argv[1], sys.argv[2], sys.argv[3]
with open(provider_path, "r", encoding="utf-8") as f:
    provider = json.load(f)
with open(e2e_path, "r", encoding="utf-8") as f:
    e2e = json.load(f)
with open(combined_path, "r", encoding="utf-8") as f:
    combined = json.load(f)

GREEN = "\033[92m"
YELLOW = "\033[93m"
RED = "\033[91m"
BOLD = "\033[1m"
RESET = "\033[0m"

def f2(v):
    try:
        return f"{float(v):.2f}"
    except Exception:
        return "0.00"

def badge(status):
    s = str(status).lower()
    if s == "ok":
        return f"{GREEN}OK{RESET}"
    if s == "skipped":
        return f"{YELLOW}SKIPPED{RESET}"
    return f"{RED}{status}{RESET}"

summary = combined.get("Summary", {})
quick = summary.get("QuickView", {})
checks = summary.get("Checks", [])

print("")
print(f"{GREEN}{BOLD}================================{RESET}")
print(f"{GREEN}{BOLD}CloudHA Throughput Benchmark{RESET}")
print(f"{GREEN}{BOLD}================================{RESET}")
print("")
print(f"{BOLD}What We Achieved In This Run{RESET}")
print(f"- Accepted logs/sec: {f2(quick.get('AcceptedLogsPerSec'))}")
print(f"- Request p95 latency (ms): {f2(quick.get('RequestP95LatencyMs'))}")
print(f"- Runtime written/sec: {f2(quick.get('RuntimeWrittenPerSecond'))}")
print(f"- Runtime p95 ingestion latency (ms): {f2(quick.get('RuntimeP95IngestionLatencyMs'))}")
print(f"- Queue depth avg/max: {f2(quick.get('QueueDepthAverage'))}/{f2(quick.get('QueueDepthMax'))}")
print("")

print(f"{GREEN}================================{RESET}")
print(f"{GREEN}{BOLD}SQL Performance: details{RESET}")
print(f"{GREEN}================================{RESET}")
for r in provider.get("Results", []):
    single = r.get("SingleInsert") or {}
    bulk = r.get("BulkInsert") or {}
    query = r.get("Query") or {}
    print(f"- {r.get('Provider','-')}: {badge(r.get('Status','-'))}")
    print(f"  single logs/sec: {f2(single.get('ThroughputLogsPerSec'))}, p95(ms): {f2(single.get('P95LatencyMs'))}")
    print(f"  bulk   logs/sec: {f2(bulk.get('ThroughputLogsPerSec'))}, p95(ms): {f2(bulk.get('P95LatencyMs'))}")
    print(f"  query latency(ms): {f2(query.get('ElapsedMs'))}")
print("")

print(f"{GREEN}================================{RESET}")
print(f"{GREEN}{BOLD}SQLite Performance{RESET}")
print(f"{GREEN}================================{RESET}")
sqlite = next((x for x in provider.get("Results", []) if str(x.get("Provider","")).lower() == "sqlite"), None)
if sqlite:
    s = sqlite.get("SingleInsert") or {}
    q = sqlite.get("Query") or {}
    print(f"- Status: {badge(sqlite.get('Status','-'))}")
    print(f"- Throughput logs/sec: {f2(s.get('ThroughputLogsPerSec'))}")
    print(f"- p95 insert latency(ms): {f2(s.get('P95LatencyMs'))}")
    print(f"- Query latency(ms): {f2(q.get('ElapsedMs'))}")
else:
    print("- SQLite result not found.")
print("")

req = e2e.get("Requests", {})
print(f"{BOLD}End-to-End API Snapshot{RESET}")
print(f"- Accepted 202: {req.get('Accepted202',0)}")
print(f"- Throttled 429: {req.get('Throttled429',0)}")
print(f"- Total requests: {req.get('TotalRequests',0)}")
print(f"- p50/p95/p99 (ms): {f2(req.get('P50LatencyMs'))}/{f2(req.get('P95LatencyMs'))}/{f2(req.get('P99LatencyMs'))}")
print("")

failure_counts = {
    "401 Unauthorized": req.get("Unauthorized401", 0),
    "429 Throttled": req.get("Throttled429", 0),
    "413 Payload Too Large": req.get("PayloadTooLarge413", 0),
    "Other HTTP Errors": req.get("OtherErrors", 0),
    "Request Exceptions": req.get("RequestExceptions", 0),
}

print(f"{BOLD}Top failure reasons{RESET}")
ordered = sorted(failure_counts.items(), key=lambda x: x[1], reverse=True)
for name, count in ordered:
    color = RED if count > 0 else GREEN
    print(f"- {name}: {color}{count}{RESET}")
print("")

print(f"{BOLD}Target Checks{RESET}")
for c in checks:
    status = f"{GREEN}PASS{RESET}" if c.get("Pass") else f"{RED}FAIL{RESET}"
    print(f"- {c.get('Name','check')}: {status} ({c.get('Detail','')})")
print("")
PY
