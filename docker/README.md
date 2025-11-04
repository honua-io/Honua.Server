# Docker Quick Start

This folder contains a single `docker-compose.yml` that brings up Honua Web and a PostgreSQL database for local evaluation.

## Prerequisites

- Docker Desktop (or Docker Engine with Compose v2)
- The repository checked out locally (the compose file builds from the project root)

## Usage

1. Copy `.env.example` to `.env` and tweak values if required.
2. From this directory run:
 ```bash
  docker compose up --build
  ```
 3. Browse to `http://localhost:8080` (or the port you configured in `.env`).

   Need an administrator account? Run `./scripts/honua.sh auth bootstrap --mode Local` (or the PowerShell equivalent) before you start experimenting so the SQLite store is initialised with credentials on first launch.

### Adding Prometheus

To launch Prometheus alongside Honua and scrape the `/metrics` endpoint:

```bash
docker compose \
  -f docker-compose.yml \
  -f docker-compose.prometheus.yml \
  up --build
```

The overlay enables the Honua metrics exporter and adds the Prometheus service defined in `prometheus/prometheus.yml`. Access the Prometheus UI at `http://localhost:9090` (override with `PROMETHEUS_PORT` in `.env`).

### Stopping and cleaning up

```bash
docker compose down        # stop containers
docker compose down -v     # stop and drop the PostgreSQL volume
```

## Customisation

- Override any value by editing `.env` before invoking Compose.
- To experiment with a different database engine you will need to adjust the `db` service and the `ConnectionStrings__DefaultConnection` value in `docker-compose.yml`.
