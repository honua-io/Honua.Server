# WMS Conformance Runbook

## Purpose
Execute the official OGC WMS 1.3 executable test suite (ETS) against a Honua
instance exposing `/wms`. The helper scripts in `scripts/` launch the ETS Docker
image, invoke the suite, and archive the output for later review.

## Prerequisites
- Docker installed locally.
- Honua running with WMS enabled and reachable from the Docker host.
- The WMS GetCapabilities endpoint accessible, e.g.
  `https://localhost:5001/wms?service=WMS&request=GetCapabilities&version=1.3.0`.
- Optional: set `ETS_VERSION`/`-EtsVersion` to pin a specific ETS tag
  (defaults to `latest`).

## Running from Bash
```bash
export HONUA_BASE_URL="https://localhost:5001"
./scripts/run-wms-conformance.sh
```

Supply an explicit capabilities URL:
```bash
./scripts/run-wms-conformance.sh \
  "https://staging.honua.local/wms?service=WMS&request=GetCapabilities&version=1.3.0"
```

Override defaults:
```bash
ETS_VERSION="1.30" \
REPORT_ROOT="qa-report" \
./scripts/run-wms-conformance.sh
```

## Running from PowerShell
```powershell
./scripts/run-wms-conformance.ps1 -HonuaBaseUrl "https://localhost:5001"
```

Or pass a fully-qualified GetCapabilities URL:
```powershell
./scripts/run-wms-conformance.ps1 `
  -CapabilitiesUrl "https://staging.honua.local/wms?service=WMS&request=GetCapabilities&version=1.3.0" `
  -EtsVersion "1.30" `
  -ReportRoot "qa-report"
```

## Outputs
- Results are written to `qa-report/wms-<timestamp>/` by default.
- The key artifact is `wms-conformance-response.xml`. The scripts exit with a
  non-zero status if the ETS returns HTTP >= 400 or the payload contains
  "FAIL" markers.

## CI Integration
- Configure `HONUA_BASE_URL` (or pass `-CapabilitiesUrl`) to the environment
  under test.
- Archive the `qa-report/wms-*` folder as a build artifact.
- Treat a non-zero exit code as a failed conformance gate.

## Troubleshooting
- **HTTP errors**: Ensure the capabilities URL is reachable from inside the
  Docker container (avoid `localhost`; use an IP or routable hostname).
- **Docker authentication prompts**: Run `docker login` before executing the
  script if your registry requires credentials.
- **Suite failures**: Inspect `wms-conformance-response.xml` for detailed
  failure notes returned by the ETS harness.

## References
- Docker image: `ogccite/ets-wms13`
- Test suite documentation: https://github.com/opengeospatial/ets-wms13
