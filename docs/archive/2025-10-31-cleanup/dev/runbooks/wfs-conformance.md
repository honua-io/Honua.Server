# WFS Conformance Runbook

## Purpose
Execute the official OGC WFS 2.0 executable test suite (ETS) against a Honua
deployment that exposes the `/wfs` endpoint. The helper scripts in `scripts/`
spin up the ETS Docker image, trigger the suite, and archive the response for
later review.

## Prerequisites
- Docker installed locally.
- Honua running with WFS enabled and reachable from the Docker host.
- The WFS GetCapabilities endpoint must be publicly accessible (for example,
  `https://localhost:5001/wfs?service=WFS&request=GetCapabilities&version=2.0.0`).
- Optional: set `ETS_VERSION`/`EtsVersion` to pin a specific ETS release
  (defaults to `latest`).

## Running from Bash
```bash
export HONUA_BASE_URL="https://localhost:5001"
./scripts/run-wfs-conformance.sh
```

Override the capabilities URL explicitly:
```bash
./scripts/run-wfs-conformance.sh \
  "https://staging.honua.local/wfs?service=WFS&request=GetCapabilities&version=2.0.0"
```

Optional environment overrides:
```bash
ETS_VERSION="1.30" \
REPORT_ROOT="qa-report" \
./scripts/run-wfs-conformance.sh
```

## Running from PowerShell
```powershell
./scripts/run-wfs-conformance.ps1 -HonuaBaseUrl "https://localhost:5001"
```

Or pass a fully-qualified GetCapabilities URL:
```powershell
./scripts/run-wfs-conformance.ps1 `
  -CapabilitiesUrl "https://staging.honua.local/wfs?service=WFS&request=GetCapabilities&version=2.0.0" `
  -EtsVersion "1.30" `
  -ReportRoot "qa-report"
```

## Outputs
- Results are written to `qa-report/wfs-<timestamp>/` by default.
- The primary artifact is `wfs-conformance-response.xml`, which contains the
  ETS response for the executed run. If the ETS reports an error or failure,
  the script exits with a non-zero status.

## CI Integration
- Configure `HONUA_BASE_URL` (or pass `-CapabilitiesUrl`) to point at the
  environment under test.
- Archive the `qa-report/wfs-...` folder as a build artifact.
- The script exits with code `1` if the ETS returns an HTTP error (>= 400) or
  if the response payload contains a failure marker.

## Troubleshooting
- **HTTP 500 from ETS**: Typically indicates the capabilities document could
  not be fetched or parsed. Verify the URL and ensure it is reachable from the
  Docker container (avoid `localhost`; use an IP or routable hostname).
- **Docker authentication prompts**: If the registry requires authentication,
  run `docker login` before executing the script.
- **False positives**: The response XML is stored even on failure. Inspect the
  contents for additional diagnostic information furnished by the ETS.

## References
- Docker image: `ogccite/ets-wfs20`
- Test suite documentation: https://github.com/opengeospatial/ets-wfs20
