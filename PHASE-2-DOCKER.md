# NinjaLogs Phase 2 - Docker Deployment and Validation

## Purpose
Run NinjaLogs with all storage environments via Docker Compose and validate what is currently production-ready.

## Files Added
- `Dockerfile`
- `.dockerignore`
- `docker-compose.yml`

## Services and Ports
- `api-file` -> `http://localhost:8081`
- `api-sqlite` -> `http://localhost:8082`
- `api-segmented` -> `http://localhost:8083`
- `api-sqlserver` -> `http://localhost:8084`
- `api-postgresql` -> `http://localhost:8085`
- `sqlserver` -> `localhost:1433`
- `postgres` -> `localhost:5432`

## Start All
```bash
docker compose up --build -d
```

## Stop All
```bash
docker compose down
```

## Quick Health Checks
```bash
curl http://localhost:8081/health
curl http://localhost:8082/health
curl http://localhost:8083/health
curl http://localhost:8084/health
curl http://localhost:8085/health
```

## Provider Readiness Status
- File: implemented and usable
- SQLite: implemented and usable
- SegmentedFile (Phase A): implemented skeleton, usable for append/query baseline
- SqlServer: implemented and usable
- PostgreSQL: scaffold only (not production-ready yet)

## Important Note
`api-postgresql` starts, but ingestion/query will throw `NotImplementedException` for provider internals until PostgreSQL repository implementation is completed.
