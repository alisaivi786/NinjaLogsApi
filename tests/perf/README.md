# Performance Smoke

Run with k6:

```bash
k6 run tests/perf/ingestion-smoke.js -e BASE_URL=http://localhost:8082 -e API_KEY=dev-ingestion-key
```

Use provider-specific base URL as needed.
