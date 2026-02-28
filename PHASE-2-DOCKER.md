# NinjaLogs Phase 2 - Docker Deployment and Validation

## Purpose
Run NinjaLogs with all storage environments via Docker Compose and validate what is currently production-ready.

## Files Added
- `Dockerfile`
- `.dockerignore`
- `docker-compose.yml`

## Services and Ports
- `api-file` -> `http://localhost:8081`, `https://localhost:8441`
- `api-sqlite` -> `http://localhost:8082`, `https://localhost:8442`
- `api-segmented` -> `http://localhost:8083`, `https://localhost:8443`
- `api-sqlserver` -> `http://localhost:8084`, `https://localhost:8444`
- `api-postgresql` -> `http://localhost:8085`, `https://localhost:8445`
- `sqlserver` -> internal compose network `1433` (not host-published)
- `postgres` -> `localhost:5432`

## Data Bind Mounts (Host Inspectable)
- File logs -> `./docker-data/file/logs`
- SQLite DB -> `./docker-data/sqlite/ninjalogs.db`
- Segmented data -> `./docker-data/segmented/`

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

HTTPS checks (self-signed cert):
```bash
curl -k https://localhost:8441/health
curl -k https://localhost:8442/health
curl -k https://localhost:8443/health
curl -k https://localhost:8444/health
curl -k https://localhost:8445/health
```

## Provider Readiness Status
- File: implemented and usable
- SQLite: implemented and usable
- SegmentedFile: implemented and usable for baseline append/query
- SqlServer: implemented and usable
- PostgreSQL: implemented and usable

## Current Notes
- Docker uses HTTPS via mounted `./certs/ninjalogs-dev.pfx`.
- For local dev certificates, use `./setup-https-certs.command`.
- SQL Server and PostgreSQL passwords are compose env-driven (`MSSQL_SA_PASSWORD`, `POSTGRES_PASSWORD`).
