# Data Ingestion Guide

Honua’s control plane lets you upload vector datasets (GeoPackage, GeoJSON, zipped Shapefiles) and load them into existing layers without manual database work. This guide explains prerequisites, CLI workflows, and raw API calls.

## Prerequisites

- **User role**: your account must carry the `datapublisher` role (or `administrator`).
- **Target metadata**: the service/layer you ingest into must already exist in metadata with storage configuration (table, geometry column, SRID, etc.).
- **Server URL**: base address for the Honua host (e.g., `https://honua.example.com`).
- **Authentication token**: for secured environments, obtain a JWT via `/api/auth/local/login` or your configured IdP.

## CLI Workflow (`honua data *`)

Before ingesting, capture your defaults and verify connectivity:

```bash
honua config init --host https://honua.example.com
honua status --token "<JWT>"
```

The configuration step stores values under the CLI config root so subsequent commands can omit `--host`/`--token` unless you need overrides.

```bash
# Upload a GeoPackage into service "transport", layer "roads"
honua data ingest ./roads.gpkg \
  --service-id transport \
  --layer-id roads
```

Flags:
- `--host` defaults to `http://localhost:5000` for local sandboxes.
- `--token` is optional in QuickStart mode; required when auth is enabled.
- `--overwrite` is currently rejected; clear the destination manually before ingesting again.
- `--poll-interval` controls status polling frequency (seconds).

During upload the CLI prints progress snapshots (Queued → Validating → Importing → Completed/Failed). Press `Ctrl+C` to cancel; the CLI will issue a cancellation request to the server.

Use the companion commands to manage jobs without crafting HTTP requests:

```bash
honua data jobs              # list recent jobs
honua data status <job-id>   # inspect a single job
honua data cancel <job-id>   # request cancellation
```

## REST API (Multipart Upload)

```bash
curl -X POST https://honua.example.com/admin/ingestion/jobs \
  -H "Authorization: Bearer <JWT>" \
  -F serviceId=transport \
  -F layerId=roads \
  -F overwrite=false \
  -F file=@roads.geojson
```

Response (`202 Accepted`):
```json
{
  "job": {
    "jobId": "08f4...",
    "serviceId": "transport",
    "layerId": "roads",
    "status": "Queued",
    "stage": "Validating source dataset",
    "progress": 0.0,
    "createdAtUtc": "2025-09-18T10:30:41Z"
  }
}
```

### Poll Job Status

```bash
curl https://honua.example.com/admin/ingestion/jobs/08f4... \
  -H "Authorization: Bearer <JWT>"
```

The payload updates `status`, `stage`, `progress`, and `message`. Terminal states: `Completed`, `Failed`, `Cancelled`.

### Cancel a Job

```bash
curl -X DELETE https://honua.example.com/admin/ingestion/jobs/08f4... \
  -H "Authorization: Bearer <JWT>"
```

## Supported Formats & Limits

- GeoPackage (`.gpkg`), GeoJSON (`.geojson` / `.json`), zipped Shapefile archives (`.zip`).
- The control plane writes uploads to `/tmp/honua-ingest/<job-id>` and deletes the directory when a job finishes.
- Large datasets stream through GDAL/OGR; monitor storage/temp space and database performance during big imports.
- Overwrite/merge semantics are roadmap items—API requests supplying `overwrite=true` receive `400 Bad Request`. Clear the destination table manually if needed.

## Troubleshooting

| Issue | Suggested Action |
|-------|------------------|
| Job fails with “dataset could not be opened” | Verify file format, compression (single layer per zip), and permissions. |
| Job stuck in “Validating” | Check host logs for GDAL errors; ensure required drivers are available. |
| HTTP 403 / 401 responses | Confirm your token/roles and that the host is not in QuickStart mode. |
| Data ingested but geometry misaligned | Ensure metadata SRID matches the dataset; verify projection before ingest. |

## Related References

- Host design snapshot: `docs/phase1-host-design.md`
- Developer architecture guide: `docs/dev/developer-architecture-guide.md`
- Metrics/observability runbooks: `docs/dev/runbooks/`
