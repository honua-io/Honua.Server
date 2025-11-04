# OGC API Features Runbook

## Purpose
Provide a repeatable workflow for preparing the Honua host with the canonical OGC API Features sample dataset and executing the official ETS conformance suite. Use this runbook during verification and before releases that touch protocol or data-access layers.

## Prerequisites
- Honua server running locally (dotnet run --project src/Honua.Server.Host) or equivalent container deployment
- Metadata admin API enabled (defaults from Honua.Server.Host)
- Toolchain installed:
  - ogr2ogr (GDAL 3.8 or newer)
  - sqlite3
  - curl / Invoke-WebRequest
  - Java 17+ runtime (used by the ETS all-in-one jar)
- Network access to download ETS artifacts from Maven Central (first run only)

## Seed the Sample Dataset
1. From the repository root run the platform-specific helper:
   - macOS/Linux: ./scripts/fetch-ogc-sample-data.sh
   - Windows PowerShell: ./scripts/fetch-ogc-sample-data.ps1
2. The script copies the bundled SQLite file from samples/ogc/ogc-sample.db (or downloads a replacement when --SourceUrl is provided), rewrites connection strings, and applies metadata via POST /admin/metadata/apply.
3. Verify the host responds:
   `ash
   curl http://localhost:5000/ogc/collections | jq '.collections[0].id'
   `

## Format Negotiation Smoke Test
- Run these checks before the ETS suite:
  `ash
  curl "http://localhost:5000/ogc/collections/roads/items?limit=5" | jq '.type'            # default GeoJSON
  curl -H "Accept: application/vnd.google-earth.kml+xml" "http://localhost:5000/ogc/collections/roads/items?f=kml&limit=1" | xmllint --xpath '/kml:kml' -
  curl "http://localhost:5000/ogc/collections/roads/items?f=geojson&limit=1" | jq '.features[0].geometry.type'
  curl "http://localhost:5000/ogc/collections/roads/items?f=mvt&tileMatrix=0&tileRow=0&tileCol=0" --output tile.mvt
  `
- For Esri REST parity, call .../FeatureServer/0/query?f=kml and ?f=geojson and confirm responses.
- Record results in the QA report before proceeding.

## Run the ETS Conformance Suite
1. Ensure Honua is running at the URL under test (default http://localhost:5000/ogc).
2. Execute the helper:
   - macOS/Linux: ./scripts/run-ogc-conformance.sh --server-url http://localhost:5000/ogc
   - Windows PowerShell: ./scripts/run-ogc-conformance.ps1 -ServerUrl http://localhost:5000/ogc
3. The script downloads ets-ogcapi-features10-<version>-aio.jar on first use, writes results under qa-report/, and invokes the ETS runner (Docker by default; use --runner jar for a local JVM).
4. Review outputs in qa-report/ogcfeatures-sqlite-<timestamp>/testng-results.xml and attach the folder or archive to the QA report.

## Interpreting Results
- 	estng-results.xml lists pass/fail counts; any <failed> entries must be resolved.
- The optional HTML report (when generated) lives under qa-report/.../html/.
- un-metadata.json records suite version, target URL, and artifact provenance.

## Troubleshooting
- **Jar download failures**: ensure outbound HTTPS access; rerun with --suite-version pinned (baseline 1.9).
- **Metadata apply errors**: inspect logs/apply-metadata.log and validate against schemas/metadata-schema.json.
- **ETS returns exit code 1**: check qa-report/.../ logs—common causes include host connectivity issues or missing dataset seeding.
- **TEAM Engine authentication**: not required with the jar runner; Docker UI credentials ogctest/ogctest.

## Next Steps
- Automate the run inside CI (spin up host container, execute helper, archive qa-report/).
- Keep ETS suite versions aligned with design/ogcfeatures-sqlite.md when upstream releases change.
