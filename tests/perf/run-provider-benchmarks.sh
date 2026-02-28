#!/usr/bin/env bash
set -euo pipefail

REPORT_PATH="${PERF_REPORT_PATH:-tests/perf/reports/provider-performance-$(date -u +%Y%m%d-%H%M%S).json}"

DOTNET_CMD=(dotnet run --project tests/NinjaLogs.PerformanceRunner/NinjaLogs.PerformanceRunner.csproj -- "$REPORT_PATH")

echo "Running provider benchmarks..."
"${DOTNET_CMD[@]}"

echo "Report: $REPORT_PATH"
