#!/usr/bin/env bash
set -euo pipefail

REPORT_PATH="${PERF_REPORT_PATH:-tests/perf/reports/e2e-performance-$(date -u +%Y%m%d-%H%M%S).json}"

DOTNET_CMD=(dotnet run --project tests/NinjaLogs.E2EPerfRunner/NinjaLogs.E2EPerfRunner.csproj -- "$REPORT_PATH")

echo "Running end-to-end ingestion benchmark..."
"${DOTNET_CMD[@]}"

echo "Report: $REPORT_PATH"
