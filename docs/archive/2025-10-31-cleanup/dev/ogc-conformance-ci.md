# OGC Conformance Testing in CI/CD

This document describes the automated OGC conformance testing infrastructure using GitHub Actions and Testcontainers.

## Overview

Honua includes comprehensive OGC standards conformance testing for:

- **OGC API Features 1.0** - Modern RESTful API for feature data
- **WFS 2.0** - Web Feature Service with transactional support
- **WMS 1.3** - Web Map Service for rendered map images
- **KML 2.2** - Keyhole Markup Language export validation

## CI Workflows

### 1. Nightly Conformance Tests

**File:** `.github/workflows/ogc-conformance-nightly.yml`

**Schedule:** Runs every night at 2 AM UTC

**Purpose:** Continuous validation of OGC standards compliance

**Features:**
- ✅ Runs all enabled conformance test suites
- ✅ Matrix execution for parallel testing
- ✅ Automatic issue creation on failure
- ✅ 30-day retention of test results
- ✅ Conformance badge generation

**Triggering:**
```bash
# Runs automatically via cron schedule
# Can also be triggered manually from GitHub Actions UI
```

### 2. Manual Conformance Tests

**File:** `.github/workflows/ogc-conformance-manual.yml`

**Purpose:** On-demand conformance testing with custom configuration

**Features:**
- ✅ Choose specific test suite or run all
- ✅ Specify custom metadata file
- ✅ Configure server port
- ✅ Detailed test summaries

**Triggering:**
1. Go to GitHub Actions tab
2. Select "OGC Conformance Tests (Manual)"
3. Click "Run workflow"
4. Select options:
   - Test suite: `all`, `ogc-features`, `wfs`, `wms`, or `kml`
   - Metadata file: Path to JSON metadata (default: `samples/ogc/metadata.json`)
   - Port: Server port (default: `5555`)

## Test Infrastructure

### Architecture

```
GitHub Actions Workflow
├── Start Honua Server (QuickStart mode)
│   └── Configured with test metadata
├── Wait for Server Readiness
│   └── Health check polling (30 retries × 2s)
├── Run Testcontainers-based Tests
│   ├── Pull TEAM Engine Docker images
│   ├── Start test containers
│   ├── Execute conformance suites
│   └── Parse results
├── Generate Reports
│   ├── TestNG XML results
│   ├── TRX results for CI
│   └── HTML summaries
└── Upload Artifacts
    ├── Test results (30 days)
    └── Server logs (7 days)
```

### Docker Images Used

| Suite | Image | Size | Source |
|-------|-------|------|--------|
| OGC Features | `ghcr.io/opengeospatial/ets-ogcapi-features10:1.0.0` | ~500MB | OGC GitHub |
| WFS 2.0 | `ogccite/ets-wfs20:latest` | ~800MB | OGC CITE |
| WMS 1.3 | `ogccite/ets-wms13:latest` | ~800MB | OGC CITE |
| KML 2.2 | `ogccite/ets-kml22:latest` | ~700MB | OGC CITE |

### Test Results

Results are available in two locations:

1. **GitHub Actions Artifacts**
   - Test results: `./test-results/` (TRX, HTML)
   - OGC reports: `./qa-report/` (XML, RDF)
   - Retention: 30 days

2. **Local Execution**
   - Results written to `qa-report/` in project root
   - Timestamped directories per test run

## Local Development

### Running Conformance Tests Locally

```bash
# 1. Start Honua
export HONUA__METADATA__PROVIDER=json
export HONUA__METADATA__PATH=samples/ogc/metadata.json
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false
dotnet run --project src/Honua.Server.Host --urls http://0.0.0.0:5555

# 2. In another terminal, run tests
export HONUA_RUN_OGC_CONFORMANCE=true
export HONUA_TEST_BASE_URL=http://localhost:5555

# Run all tests
dotnet test tests/Honua.Server.Core.Tests --filter "FullyQualifiedName~OgcConformanceTests"

# Run specific suite
dotnet test --filter "FullyQualifiedName~OgcApiFeatures_PassesConformance"
dotnet test --filter "FullyQualifiedName~WfsService_PassesConformance"
dotnet test --filter "FullyQualifiedName~WmsService_PassesConformance"
dotnet test --filter "FullyQualifiedName~KmlExport_PassesConformance"
```

### Prerequisites for Local Testing

- ✅ Docker Desktop or Docker Engine running
- ✅ .NET 9.0 SDK
- ✅ At least 4GB free RAM (for Docker containers)
- ✅ At least 5GB free disk (for Docker images)

## Test Execution Times

| Suite | Typical Duration | Notes |
|-------|------------------|-------|
| OGC API Features | 5-10 minutes | Depends on feature count |
| WFS 2.0 | 10-15 minutes | Includes transactional tests |
| WMS 1.3 | 8-12 minutes | Includes rendering validation |
| KML 2.2 | 3-5 minutes | File-based validation |
| **Total (all)** | **25-40 minutes** | Parallel execution reduces time |

## Troubleshooting

### Tests Skip Automatically

**Symptom:** All conformance tests are skipped

**Cause:** Environment variable not set

**Solution:**
```bash
export HONUA_RUN_OGC_CONFORMANCE=true
```

### Server Startup Fails in CI

**Symptom:** Health check timeout in GitHub Actions

**Common Causes:**
1. Missing or invalid metadata file
2. Port already in use
3. Configuration error

**Solution:**
- Check `samples/ogc/metadata.json` exists and is valid JSON
- Review server logs in uploaded artifacts
- Verify environment variables are set correctly

### Docker Pull Failures

**Symptom:** Image pull errors in test execution

**Cause:** Docker registry issues or authentication

**Solution:** Pre-pull images in workflow:
```yaml
- name: Pre-pull Docker images
  run: |
    docker pull ghcr.io/opengeospatial/ets-ogcapi-features10:1.0.0
    docker pull ogccite/ets-wfs20:latest
    docker pull ogccite/ets-wms13:latest
    docker pull ogccite/ets-kml22:latest
```

### Test Failures

**Symptom:** Conformance tests fail

**Diagnosis:**
1. Download test results artifacts from GitHub Actions
2. Review `qa-report/` directory for detailed failure information
3. Check `testng-results.xml` or `*-conformance-response.xml`
4. Review server logs for errors

**Common Issues:**
- Missing required OGC endpoints
- Incorrect conformance class declarations
- Invalid GeoJSON/XML output format
- CRS handling errors
- Missing mandatory parameters

## Monitoring and Alerts

### Automatic Issue Creation

When nightly tests fail, the workflow automatically creates a GitHub issue with:
- Link to failed workflow run
- Commit SHA that failed
- Labels: `conformance`, `automated-test`, `bug`

### Conformance Badge

The workflow generates a badge showing current conformance status:

```markdown
![OGC Conformance](.github/badges/ogc-conformance.md)
```

Status indicators:
- 🟢 **Passing** - All conformance tests pass
- 🔴 **Failing** - One or more tests fail

## Future Enhancements

- [ ] Integration with WebApplicationFactory for in-process testing
- [ ] Parallel test execution with isolated containers
- [ ] Performance benchmarks during conformance runs
- [ ] OGC API Tiles conformance tests
- [ ] OGC API Records conformance tests
- [ ] Trend analysis and historical tracking
- [ ] Slack/Teams notifications on failure
- [ ] Coverage reports for conformance classes

## References

- [OGC CITE Test Suites](https://github.com/opengeospatial)
- [OGC API Features Spec](https://docs.ogc.org/is/17-069r4/17-069r4.html)
- [WFS 2.0 Spec](https://docs.ogc.org/is/09-025r2/09-025r2.html)
- [WMS 1.3 Spec](https://docs.ogc.org/is/06-042/06-042.html)
- [KML 2.2 Spec](https://docs.ogc.org/is/07-147r2/07-147r2.html)
- [Testcontainers .NET](https://dotnet.testcontainers.org/)

## Support

For issues with:
- **Test infrastructure**: Open GitHub issue with label `conformance`
- **Test failures**: Review conformance reports, then open issue with label `ogc-compliance`
- **CI/CD workflows**: Open GitHub issue with label `ci-cd`
