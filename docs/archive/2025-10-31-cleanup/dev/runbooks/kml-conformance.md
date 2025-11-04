# KML/KMZ Conformance Runbook

## Purpose
Validate Honua-generated KML/KMZ files against the official OGC KML ETS using the helper scripts under `scripts/`.

## Prerequisites
- Docker installed and running.
- Honua instance capable of exporting KML/KMZ (Phase 0 scope).
- Optional: run `scripts/fetch-ogc-kml-data.sh` (Bash) or `scripts/fetch-ogc-kml-data.ps1` (PowerShell) to download a canonical sample KML for practice.

## Exporting KML/KMZ from Honua
Use the OGC API Features endpoints with the `f=kml` format selector to generate a standards-compliant payload:
```bash
# Export every feature in the collection as KML
curl -o temp/roads.kml "https://localhost:5001/ogc/collections/roads::roads-primary/items?f=kml"

# Export a single feature by identifier
curl -o temp/roads-1.kml "https://localhost:5001/ogc/collections/roads::roads-primary/items/1?f=kml"
```
Ensure the file path passed to the conformance scripts matches the output location.

## Running Conformance Tests (Bash)
```bash
./scripts/run-kml-conformance.sh temp/example.kml
```
Optional overrides:
```bash
ETS_VERSION=1.0.1 REPORT_ROOT=qa-report ./scripts/run-kml-conformance.sh temp/example.kml
```

## Running Conformance Tests (PowerShell)
```powershell
./scripts/run-kml-conformance.ps1 -InputPath temp/example.kml
```
Optional parameters:
```powershell
./scripts/run-kml-conformance.ps1 -InputPath temp/example.kml -EtsVersion 1.0.1 -ReportRoot qa-report
```

## Outputs
- Results stored under `qa-report/kml-<timestamp>/`.
- `testng-results.xml` contains detailed pass/fail info.

## Troubleshooting
- **Docker not found**: Install Docker Desktop or ensure the daemon is running.
- **Invalid input**: Verify the KML path is correct and readable.
- **Failed checks**: Inspect `testng-results.xml` to identify validation errors and adjust export logic accordingly.



