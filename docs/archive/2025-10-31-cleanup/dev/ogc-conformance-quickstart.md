# OGC Conformance Testing - Quick Start Guide

Get up and running with OGC conformance testing in 5 minutes.

## Prerequisites

- Docker running
- .NET 9.0 SDK
- Clone of Honua repository

## Option 1: Run via GitHub Actions (Recommended)

**Easiest way to run conformance tests:**

1. Go to your repository on GitHub
2. Click **Actions** tab
3. Select **"OGC Conformance Tests (Manual)"**
4. Click **"Run workflow"**
5. Choose test suite and click **"Run workflow"** button
6. Wait 10-30 minutes for results
7. Download artifacts for detailed reports

**Benefits:**
- ✅ Clean CI environment
- ✅ No local setup needed
- ✅ Automatic artifact archival
- ✅ Can share results easily

## Option 2: Run Locally

### Step 1: Start Honua Server

```bash
cd /path/to/HonuaIO

# Set environment variables
export HONUA__METADATA__PROVIDER=json
export HONUA__METADATA__PATH=samples/ogc/metadata.json
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false

# Start server
dotnet run --project src/Honua.Server.Host --urls http://0.0.0.0:5555
```

**Keep this terminal open.** Server must stay running.

### Step 2: Run Tests (New Terminal)

```bash
cd /path/to/HonuaIO

# Enable conformance tests
export HONUA_RUN_OGC_CONFORMANCE=true
export HONUA_TEST_BASE_URL=http://localhost:5555

# Run all tests (25-40 minutes)
dotnet test tests/Honua.Server.Core.Tests \
  --filter "FullyQualifiedName~OgcConformanceTests" \
  --logger "console;verbosity=detailed"
```

**Or run individual suites:**

```bash
# OGC API Features (5-10 min)
dotnet test --filter "FullyQualifiedName~OgcApiFeatures_PassesConformance"

# WFS 2.0 (10-15 min)
dotnet test --filter "FullyQualifiedName~WfsService_PassesConformance"

# WMS 1.3 (8-12 min)
dotnet test --filter "FullyQualifiedName~WmsService_PassesConformance"

# KML 2.2 (3-5 min) - Requires implementation
dotnet test --filter "FullyQualifiedName~KmlExport_PassesConformance"
```

### Step 3: View Results

Results are in `qa-report/` directory:

```bash
ls -R qa-report/
```

Example output:
```
qa-report/
├── ogcfeatures-20251001-140532/
│   └── testng-results.xml
├── wfs-20251001-141203/
│   └── wfs-conformance-response.xml
└── wms-20251001-142015/
    └── wms-conformance-response.xml
```

## Understanding Results

### Success ✅

```bash
Test Run Successful.
Total tests: 1
     Passed: 1
```

All conformance classes passed!

### Failure ❌

```bash
Test Run Failed.
Total tests: 1
     Passed: 0
     Failed: 1
```

Check the XML reports in `qa-report/` for details on which tests failed.

## Quick Troubleshooting

### "Tests are skipped"

**Fix:** Set environment variable:
```bash
export HONUA_RUN_OGC_CONFORMANCE=true
```

### "Connection refused"

**Fix:** Ensure Honua is running on the correct port:
```bash
curl http://localhost:5555/ogc
# Should return JSON response
```

### "Docker not found"

**Fix:** Start Docker:
```bash
# Check if Docker is running
docker ps

# If not, start Docker Desktop or Docker Engine
```

### "Tests take too long"

Docker images are large (~3GB total). First run downloads images:
- Subsequent runs are much faster (containers reuse cached images)
- Consider running in CI where images are cached

## Next Steps

- **Investigate failures:** Review XML reports in `qa-report/`
- **Fix conformance issues:** Update OGC endpoint implementations
- **Re-run tests:** Iterate until all tests pass
- **Enable nightly CI:** Let automated tests catch regressions
- **Add to PR checks:** Gate merges on conformance

## Advanced: Using Shell Scripts (Legacy)

The old shell script approach still works:

```bash
export HONUA_BASE_URL="http://localhost:5555"

# OGC API Features
./scripts/run-ogc-conformance.sh

# WFS 2.0
./scripts/run-wfs-conformance.sh

# WMS 1.3
./scripts/run-wms-conformance.sh

# KML 2.2
./scripts/run-kml-conformance.sh /path/to/exported.kml
```

**However, Testcontainers approach is recommended** for better integration with test framework.

## Getting Help

- **Test infrastructure issues:** [Open GitHub issue](../../issues/new?labels=conformance)
- **Conformance failures:** Review [OGC Conformance CI Guide](./ogc-conformance-ci.md)
- **Questions:** Check [README](../Ogc/README.md) in test project
