# OGC Conformance Testing with Testcontainers

This directory contains automated OGC conformance tests using Testcontainers to manage Docker-based TEAM Engine test suites.

## Overview

The `OgcConformanceTests` class provides automated testing for:

- **OGC API Features 1.0** - RESTful API for feature data
- **WFS 2.0** - Web Feature Service conformance
- **WMS 1.3** - Web Map Service conformance
- **KML 2.2** - Keyhole Markup Language export validation

## Advantages Over Shell Scripts

**Previous approach:** Bash/PowerShell scripts manually managing Docker containers

**New Testcontainers approach:**
- ✅ **Integrated with xUnit** - Run conformance tests as part of your test suite
- ✅ **Automatic lifecycle management** - Containers automatically start/stop
- ✅ **CI/CD friendly** - No manual Docker setup required
- ✅ **Better error handling** - Exceptions and assertions instead of exit codes
- ✅ **Parallel execution** - xUnit can run tests concurrently (when using different containers)
- ✅ **Test output integration** - Results appear in test explorer and logs
- ✅ **Reliable cleanup** - Tests automatically clean up all Docker containers after run, including orphaned containers
- ✅ **No manual cleanup needed** - Tests can be run repeatedly without port conflicts or leftover containers

## Prerequisites

1. **Docker** - Docker Desktop (Windows/Mac) or Docker Engine (Linux) must be running
2. **.NET 9.0** - Project targets .NET 9.0
3. **Honua running** - The server must be accessible for testing

## Running the Tests

### Option 1: Enable for all test runs

Set the environment variable to enable conformance tests:

```bash
# Linux/Mac
export HONUA_RUN_OGC_CONFORMANCE=true

# Windows PowerShell
$env:HONUA_RUN_OGC_CONFORMANCE="true"

# Windows CMD
set HONUA_RUN_OGC_CONFORMANCE=true
```

Then run tests normally:

```bash
dotnet test tests/Honua.Server.Core.Tests --filter "Category=ogc-conformance"
```

### Option 2: Enable for single test run

```bash
HONUA_RUN_OGC_CONFORMANCE=true dotnet test tests/Honua.Server.Core.Tests --filter "FullyQualifiedName~OgcConformanceTests"
```

### Option 3: Run specific conformance test

```bash
HONUA_RUN_OGC_CONFORMANCE=true dotnet test tests/Honua.Server.Core.Tests --filter "FullyQualifiedName~OgcApiFeatures_PassesConformance"
```

## Configuration

### Honua Base URL

By default, tests assume Honua is running at `http://localhost:5555`. Override this with:

```bash
export HONUA_TEST_BASE_URL="http://your-server:port"
```

For **WSL/Docker** environments, you may need to use the host IP instead of localhost:

```bash
# Get your WSL host IP
ip addr show eth0 | grep "inet\b" | awk '{print $2}' | cut -d/ -f1

# Set it as the base URL
export HONUA_TEST_BASE_URL="http://172.18.0.1:5555"
```

### Starting Honua for Testing

Ensure Honua is running in QuickStart mode (no authentication):

```bash
export HONUA__METADATA__PROVIDER=yaml
export HONUA__METADATA__PATH=samples/ogc/metadata.yaml
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false

dotnet run --project src/Honua.Server.Host --urls http://0.0.0.0:5555
```

## Test Output

Test results are saved to the `qa-report/` directory in the project root:

```
qa-report/
├── ogcfeatures-20251001-143022/
│   └── testng-results.xml
├── wfs-20251001-143115/
│   └── wfs-conformance-response.xml
├── wms-20251001-143208/
│   └── wms-conformance-response.xml
└── kml-20251001-143301/
    └── earl-results.rdf
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: OGC Conformance Tests

on: [push, pull_request]

jobs:
  conformance:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Start Honua
        run: |
          export HONUA__METADATA__PROVIDER=yaml
          export HONUA__METADATA__PATH=samples/ogc/metadata.yaml
          export HONUA__AUTHENTICATION__MODE=QuickStart
          export HONUA__AUTHENTICATION__ENFORCE=false
          dotnet run --project src/Honua.Server.Host --urls http://0.0.0.0:5555 &
          sleep 10

      - name: Run OGC Conformance Tests
        run: |
          export HONUA_RUN_OGC_CONFORMANCE=true
          export HONUA_TEST_BASE_URL="http://localhost:5555"
          dotnet test tests/Honua.Server.Core.Tests \
            --filter "FullyQualifiedName~OgcConformanceTests" \
            --logger "trx;LogFileName=conformance-results.trx"

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: ogc-conformance-reports
          path: qa-report/
```

## Troubleshooting

### Tests are skipped

**Cause:** Environment variable not set
**Solution:** Ensure `HONUA_RUN_OGC_CONFORMANCE=true` is set

### Docker connection errors

**Cause:** Docker daemon not running
**Solution:** Start Docker Desktop or Docker Engine

### Port conflicts

**Cause:** Ports 8080-8083 or random ports (20000-40000) in use
**Solution:** Tests use dynamic port allocation - usually resolves automatically

### Honua connection errors

**Cause:** Honua not reachable from Docker containers
**Solution:** Use host IP instead of `localhost`:

```bash
# Linux
export HONUA_TEST_BASE_URL="http://$(hostname -I | awk '{print $1}'):5555"

# WSL
export HONUA_TEST_BASE_URL="http://$(ip route | grep default | awk '{print $3}'):5555"

# Mac (Docker Desktop)
export HONUA_TEST_BASE_URL="http://host.docker.internal:5555"
```

### Image pull failures

**Cause:** Docker registry authentication or network issues
**Solution:** Pre-pull images manually:

```bash
docker pull ghcr.io/opengeospatial/ets-ogcapi-features10:1.0.0
docker pull ogccite/ets-wfs20:latest
docker pull ogccite/ets-wms13:latest
docker pull ogccite/ets-kml22:latest
```

## Comparison with Shell Scripts

| Feature | Shell Scripts | Testcontainers |
|---------|--------------|----------------|
| Execution | Manual `./scripts/run-*.sh` | `dotnet test` |
| Container lifecycle | Manual start/stop | Automatic |
| Error reporting | Exit codes | Exceptions + assertions |
| CI integration | Custom scripting | Native xUnit |
| Parallel execution | Manual orchestration | xUnit collection |
| Results | XML files only | Test results + XML |
| Cleanup | Script-based | Automatic |

**Recommendation:** Use Testcontainers for automated testing and CI/CD. Keep shell scripts for manual ad-hoc testing.

## Architecture

```
OgcConformanceTests (xUnit test class)
    ├── Uses OgcConformanceFixture (shared fixture)
    │   ├── Manages Docker containers via Testcontainers
    │   ├── Pre-pulls TEAM Engine images
    │   ├── Handles container lifecycle
    │   └── Provides test execution methods
    │
    ├── OgcApiFeatures_PassesConformance()
    │   └── Runs ETS against /ogc endpoint
    │
    ├── WfsService_PassesConformance()
    │   └── Runs WFS 2.0 tests via TEAM Engine REST API
    │
    ├── WmsService_PassesConformance()
    │   └── Runs WMS 1.3 tests via TEAM Engine REST API
    │
    └── KmlExport_PassesConformance()
        └── Exports KML and validates with KML 2.2 suite
```

## Future Enhancements

- [ ] Integration with WebApplicationFactory for in-process Honua testing
- [ ] Parallel test execution with container isolation
- [ ] Test result parsing and detailed failure analysis
- [ ] Performance benchmarking during conformance tests
- [ ] Docker Compose alternative for local development
- [ ] Support for OGC API Tiles conformance tests
- [ ] Support for OGC API Records conformance tests
