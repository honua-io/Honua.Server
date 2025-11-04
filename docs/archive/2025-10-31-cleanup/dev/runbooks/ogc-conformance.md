# OGC API Features Conformance Runbook

## Purpose
Run the official OGC API Features ETS (Executable Test Suite) against a Honua.Next instance backed by the SQLite provider. Use the scripts in `scripts/` to execute the tests and archive reports.

## Prerequisites
- Docker installed locally (required to run the ETS container).
- Honua.Next running and accessible (e.g., `https://localhost:5001`). Ensure the OGC API Features endpoints are enabled and seeded with the canonical sample data.
- Optional: Set `ETS_VERSION` / `EtsVersion` if you need a specific ETS release (defaults to `1.0.0`).

## Running from Bash
```bash
export HONUA_BASE_URL="https://localhost:5001"
./scripts/run-ogc-conformance.sh
```
Optional overrides:
```bash
HONUA_BASE_URL="https://staging.honua.local" \
ETS_VERSION="1.0.1" \
REPORT_ROOT="qa-report" \
./scripts/run-ogc-conformance.sh
```

## Running from PowerShell
```powershell
$HonuaBaseUrl = "https://localhost:5001"
./scripts/run-ogc-conformance.ps1 -HonuaBaseUrl $HonuaBaseUrl
```
Optional parameters:
```powershell
./scripts/run-ogc-conformance.ps1 `
  -HonuaBaseUrl "https://staging.honua.local" `
  -EtsVersion "1.0.1" `
  -ReportRoot "qa-report"
```

## Outputs
- Reports and logs are stored under `qa-report/ogcfeatures-<timestamp>/`.
- The primary result file is `testng-results.xml`. Additional ETS output may appear in the same directory.

## CI Integration Notes
- In CI pipelines, set `HONUA_BASE_URL` (or pass `-HonuaBaseUrl`) to the deployed test environment.
- Archive the `qa-report/...` directory as a build artifact for review.

## Troubleshooting
- **Docker not found**: Install Docker or ensure the Docker daemon is running.
- **Connection errors**: Verify Honua is reachable at the specified base URL and TLS certificates are trusted.
- **Test failures**: Inspect `testng-results.xml` for details, address failures, and rerun the suite.
