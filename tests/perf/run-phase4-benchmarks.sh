#!/usr/bin/env bash
set -euo pipefail

echo "[info] Script renamed to tests/perf/run-cloudha-throughput-benchmarks.sh"
exec tests/perf/run-cloudha-throughput-benchmarks.sh "$@"
