# Honua CI/CD Documentation

This document describes the continuous integration and continuous deployment (CI/CD) workflows for Honua.

## Table of Contents

- [Overview](#overview)
- [Workflows](#workflows)
- [Integration Tests with Cloud Storage Emulators](#integration-tests-with-cloud-storage-emulators)
- [Test Execution](#test-execution)
- [Troubleshooting](#troubleshooting)
- [Maintainer Guide](#maintainer-guide)

## Overview

Honua uses GitHub Actions for CI/CD automation. The workflows are designed to:

1. **Fast Feedback** - Unit tests run on every push and PR
2. **Comprehensive Testing** - Integration tests with real cloud emulators
3. **Quality Gates** - Code coverage, security scanning, conformance testing
4. **Multi-Environment** - Test against LocalStack (S3), Azurite (Azure), and GCS emulators

## Workflows

### Main Workflows

| Workflow | File | Purpose | Triggers |
|----------|------|---------|----------|
| **Continuous Integration** | `ci.yml` | Full CI pipeline with unit tests, security scanning, and conformance tests | Push to `master`/`main`/`develop`, PRs |
| **Integration Tests** | `integration-tests.yml` | Cloud storage integration tests with emulators | Push to `master`/`main`/`dev`, PRs, Manual dispatch |
| **Nightly Tests** | `nightly-tests.yml` | Extended test suite including long-running tests | Scheduled (nightly) |
| **Docker Tests** | `docker-tests.yml` | Container image build and smoke tests | Push, PRs |
| **OGC Conformance** | `ogc-conformance-*.yml` | WFS/OGC API compliance validation | Nightly, Manual |
| **Performance Monitoring** | `performance-monitoring.yml` | Performance regression detection | Push to main branches |
| **Security Scanning** | `secrets-scanning.yml`, `codeql.yml` | Security vulnerability detection | Push, PRs, Scheduled |

### Test Categories

```
┌─────────────────────────────────────────────────────────────────┐
│                         Test Pyramid                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  E2E Tests (Nightly)                                            │
│  ├─ Full system tests                                           │
│  └─ QGIS smoke tests                                            │
│                                                                  │
│  Integration Tests (integration-tests.yml)                      │
│  ├─ Cloud storage emulators (S3, Azure, GCS)                   │
│  ├─ Real SDK testing                                            │
│  └─ Multi-service orchestration                                 │
│                                                                  │
│  Unit Tests (ci.yml)                                            │
│  ├─ Fast, isolated tests                                        │
│  ├─ No external dependencies                                    │
│  └─ High coverage requirements                                  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Integration Tests with Cloud Storage Emulators

The `integration-tests.yml` workflow runs comprehensive integration tests against real cloud storage emulators.

### Architecture

```yaml
┌─────────────────────────────────────────────────────────────────┐
│                    GitHub Actions Runner                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐        ┌─────────────────────────────┐  │
│  │                  │        │   Docker Containers          │  │
│  │  .NET Test       │───────▶│                              │  │
│  │  Runner          │        │  ┌────────────────────────┐ │  │
│  │                  │        │  │ LocalStack (S3)        │ │  │
│  │  - Unit Tests    │        │  │ Port: 4566             │ │  │
│  │  - Integration   │        │  └────────────────────────┘ │  │
│  │    Tests with    │        │                              │  │
│  │    Real SDKs     │        │  ┌────────────────────────┐ │  │
│  │                  │        │  │ Azurite (Azure Blob)   │ │  │
│  │                  │        │  │ Port: 10000            │ │  │
│  │                  │        │  └────────────────────────┘ │  │
│  │                  │        │                              │  │
│  │                  │        │  ┌────────────────────────┐ │  │
│  │                  │        │  │ GCS Emulator           │ │  │
│  │                  │        │  │ Port: 4443             │ │  │
│  │                  │        │  └────────────────────────┘ │  │
│  └──────────────────┘        └─────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Workflow Jobs

#### 1. Unit Tests

**Purpose:** Run fast, isolated unit tests without external dependencies.

**Steps:**
1. Setup .NET 9.0 SDK
2. Install GDAL dependencies
3. Restore and build solution
4. Run tests excluding integration tests: `--filter "FullyQualifiedName!~IntegrationTests"`
5. Upload test results and coverage

**Duration:** ~5-10 minutes

**Coverage:** Unit tests only (no cloud storage dependencies)

#### 2. Integration Tests

**Purpose:** Test cloud storage providers with real SDKs against emulators.

**Steps:**

1. **Setup Environment**
   ```bash
   - Setup .NET 9.0 SDK
   - Install GDAL dependencies
   - Cache NuGet packages
   ```

2. **Start Emulators**
   ```bash
   cd tests/Honua.Server.Core.Tests
   docker-compose -f docker-compose.storage-emulators.yml up -d
   ```

3. **Health Checks (with 120s timeout)**
   - **LocalStack (S3):** `http://localhost:4566/_localstack/health`
   - **Azurite (Azure):** `http://localhost:10000/devstoreaccount1?comp=list`
   - **GCS Emulator:** `http://localhost:4443/storage/v1/b`

4. **Run Integration Tests**
   ```bash
   dotnet test --filter "FullyQualifiedName~IntegrationTests"
   ```

5. **Upload Results**
   - Test results (.trx files)
   - Code coverage reports
   - Publish to test reporter

6. **Cleanup (Always Runs)**
   ```bash
   docker-compose down -v
   ```

**Duration:** ~10-20 minutes

**Coverage:** Integration tests with real cloud storage SDKs

#### 3. Test Summary

**Purpose:** Aggregate and report combined test results.

**Steps:**
1. Download all test artifacts
2. Generate combined test report
3. Calculate code coverage summary
4. Post coverage report to PR (if applicable)

### Emulator Configuration

The workflow uses three cloud storage emulators defined in `docker-compose.storage-emulators.yml`:

| Emulator | Service | Port | Health Check Endpoint |
|----------|---------|------|----------------------|
| **LocalStack** | AWS S3 | 4566 | `/_localstack/health` |
| **Azurite** | Azure Blob | 10000 | `/devstoreaccount1?comp=list` |
| **fake-gcs-server** | Google Cloud Storage | 4443 | `/storage/v1/b` |

### Triggers

The workflow runs on:

1. **Push Events**
   - Branches: `master`, `main`, `dev`
   - Automatically runs on every push

2. **Pull Request Events**
   - Target branches: `master`, `main`, `dev`
   - Runs on PR creation and updates

3. **Manual Dispatch**
   - Can be triggered manually from GitHub Actions UI
   - Option to skip unit tests and run integration tests only

### Environment Variables

```yaml
DOTNET_VERSION: '9.0.x'
SOLUTION_FILE: 'Honua.sln'
DOCKER_COMPOSE_FILE: 'tests/Honua.Server.Core.Tests/docker-compose.storage-emulators.yml'
```

## Test Execution

### Running Tests Locally

#### Prerequisites

```bash
# Install dependencies
- Docker and Docker Compose
- .NET 9.0 SDK
- GDAL libraries (for raster processing)
```

#### Quick Start

```bash
# 1. Start emulators
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml up -d

# 2. Wait for emulators to be healthy
../../scripts/wait-for-emulators.sh

# 3. Run unit tests only
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# 4. Run integration tests only
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# 5. Run all tests
dotnet test

# 6. Stop emulators
docker-compose -f docker-compose.storage-emulators.yml down -v
```

#### Helper Script

Use the provided health check script:

```bash
# Default timeout (120s)
./scripts/wait-for-emulators.sh

# Custom timeout
./scripts/wait-for-emulators.sh 60

# With custom endpoints
LOCALSTACK_URL=http://localhost:4566 \
AZURITE_URL=http://localhost:10000 \
GCS_URL=http://localhost:4443 \
./scripts/wait-for-emulators.sh
```

### Running Specific Test Suites

```bash
# GCS integration tests only
dotnet test --filter "FullyQualifiedName~GcsRasterTileCacheProviderIntegrationTests"

# S3 integration tests only (when implemented)
dotnet test --filter "FullyQualifiedName~S3.*IntegrationTests"

# Azure integration tests only (when implemented)
dotnet test --filter "FullyQualifiedName~Azure.*IntegrationTests"

# All raster cache integration tests
dotnet test --filter "FullyQualifiedName~RasterTileCacheProvider&FullyQualifiedName~IntegrationTests"

# All vector tile cache integration tests
dotnet test --filter "FullyQualifiedName~VectorTileCacheProvider&FullyQualifiedName~IntegrationTests"
```

### Test Results and Artifacts

Test results are uploaded as artifacts and accessible in the GitHub Actions UI:

- **unit-test-results**: Unit test TRX files and coverage reports
- **integration-test-results**: Integration test TRX files and coverage reports

Artifacts are retained for 7 days.

## Troubleshooting

### Emulator Health Check Failures

**Symptom:** Health checks timeout or fail

**Solutions:**

1. **Check Docker daemon**
   ```bash
   docker ps
   ```

2. **View emulator logs**
   ```bash
   cd tests/Honua.Server.Core.Tests
   docker-compose -f docker-compose.storage-emulators.yml logs
   ```

3. **Check port conflicts**
   ```bash
   lsof -i :4566   # LocalStack
   lsof -i :10000  # Azurite
   lsof -i :4443   # GCS emulator
   ```

4. **Restart emulators**
   ```bash
   cd tests/Honua.Server.Core.Tests
   docker-compose -f docker-compose.storage-emulators.yml restart
   ```

5. **Clean restart**
   ```bash
   cd tests/Honua.Server.Core.Tests
   docker-compose -f docker-compose.storage-emulators.yml down -v
   docker-compose -f docker-compose.storage-emulators.yml up -d
   ```

### Integration Tests Skipped

**Symptom:** Integration tests are marked as "skipped" in test results

**Cause:** Emulators are not running or not healthy

**Solution:**

1. Verify emulators are running and healthy:
   ```bash
   ./scripts/wait-for-emulators.sh
   ```

2. Check individual emulator health:
   ```bash
   curl http://localhost:4566/_localstack/health
   curl http://localhost:10000/devstoreaccount1?comp=list
   curl http://localhost:4443/storage/v1/b
   ```

3. Review test logs for skip reasons

### Test Failures

**Symptom:** Tests fail with connection errors or timeouts

**Solutions:**

1. **Check emulator health**
   ```bash
   ./scripts/wait-for-emulators.sh
   ```

2. **Verify network connectivity**
   ```bash
   curl -v http://localhost:4566/_localstack/health
   curl -v http://localhost:10000/devstoreaccount1?comp=list
   curl -v http://localhost:4443/storage/v1/b
   ```

3. **Check Docker network**
   ```bash
   docker network ls
   docker network inspect honua-storage-test-network
   ```

4. **Review application logs**
   - Test output in GitHub Actions logs
   - Emulator container logs

### Coverage Report Issues

**Symptom:** Code coverage reports are missing or incomplete

**Solutions:**

1. **Verify coverage collection**
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```

2. **Check coverage files**
   ```bash
   find . -name "coverage.opencover.xml"
   ```

3. **Regenerate coverage report**
   ```bash
   dotnet tool install --global dotnet-reportgenerator-globaltool
   reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"coverage-report"
   ```

## Maintainer Guide

### Adding New Integration Tests

1. **Create test class with `[Collection("StorageEmulators")]` attribute:**
   ```csharp
   [Collection("StorageEmulators")]
   public class MyNewIntegrationTests : IAsyncLifetime
   {
       // Test implementation
   }
   ```

2. **Implement health check in `InitializeAsync()`:**
   ```csharp
   public async Task InitializeAsync()
   {
       if (!await IsEmulatorRunningAsync())
       {
           throw new SkipException("Emulator not running");
       }
       // Setup test resources
   }
   ```

3. **Add cleanup in `DisposeAsync()`:**
   ```csharp
   public async Task DisposeAsync()
   {
       // Clean up test resources
   }
   ```

4. **Tests will automatically run in CI** when:
   - Named with `IntegrationTests` suffix
   - Emulators are healthy
   - Marked with `[Collection("StorageEmulators")]`

### Modifying Workflow

#### To skip unit tests:

```yaml
# Use workflow_dispatch with run_integration_only: true
if: github.event.inputs.run_integration_only != 'true'
```

#### To add new emulator:

1. **Update `docker-compose.storage-emulators.yml`:**
   ```yaml
   my-emulator:
     image: my-emulator:latest
     ports:
       - "1234:1234"
     healthcheck:
       test: ["CMD", "curl", "-f", "http://localhost:1234/health"]
   ```

2. **Add health check to workflow:**
   ```yaml
   - name: Wait for My Emulator to be healthy
     run: |
       timeout 120 bash -c '
         until curl -sf http://localhost:1234/health; do
           sleep 3
         done
       '
   ```

3. **Update health check script:**
   ```bash
   # Add new check function
   check_my_emulator() {
       curl -sf http://localhost:1234/health
   }

   # Add to main()
   wait_for_service "My Emulator" check_my_emulator
   ```

#### To adjust timeouts:

```yaml
# Job timeout
timeout-minutes: 30

# Health check timeout
timeout 120 bash -c '...'  # 120 seconds
```

### Best Practices

1. **Always run unit tests before integration tests**
   - Fail fast if basic functionality is broken
   - Unit tests are faster and cheaper

2. **Use health checks with reasonable timeouts**
   - Default: 120 seconds
   - Add retries with exponential backoff

3. **Clean up resources in `if: always()` blocks**
   - Ensure emulators are stopped even on failure
   - Prevent resource leaks in CI environment

4. **Upload logs on failure**
   - Helps debugging CI-specific issues
   - Include emulator logs in artifacts

5. **Cache dependencies**
   - NuGet packages
   - Docker images (when appropriate)
   - Speeds up workflow execution

6. **Set appropriate retention periods**
   - Test results: 7 days
   - Build artifacts: 1 day
   - Coverage reports: 7 days

### Workflow Maintenance Checklist

- [ ] Update .NET version when upgrading
- [ ] Update emulator images to stable versions
- [ ] Review timeout settings periodically
- [ ] Monitor workflow execution times
- [ ] Check artifact storage usage
- [ ] Update dependencies in health check script
- [ ] Review and optimize caching strategy
- [ ] Validate test filters are correct
- [ ] Ensure cleanup steps always run
- [ ] Update documentation when making changes

## Related Documentation

- **[Testing Guide](./TESTING.md)** - How to write and run tests
- **[Storage Integration Tests](../tests/Honua.Server.Core.Tests/STORAGE_INTEGRATION_TESTS.md)** - Cloud storage testing details
- **[GitHub Actions Workflows](../.github/workflows/)** - Workflow YAML files

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [LocalStack Documentation](https://docs.localstack.cloud/)
- [Azurite Documentation](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
- [fake-gcs-server Documentation](https://github.com/fsouza/fake-gcs-server)
- [.NET Testing Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
