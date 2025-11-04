# Quick Start Guide - Deployment E2E Tests

## Running Tests Locally

### 1. Start Docker
```bash
# Verify Docker is running
docker info

# If not running, start Docker Desktop or daemon
```

### 2. Run All Tests
```bash
cd /home/mike/projects/HonuaIO

# Run all deployment E2E tests
dotnet test tests/Honua.Server.Deployment.E2ETests --configuration Release
```

### 3. View Results
Tests will output results to console showing:
- Total tests: 48
- Passed/Failed/Skipped count
- Execution time
- Any failures with stack traces

## Running Specific Test Categories

### Deployment Workflow Tests Only
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests \
  --filter "FullyQualifiedName~DeploymentWorkflowTests"
```

### OGC API Tests Only
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests \
  --filter "FullyQualifiedName~OgcApiFeaturesTests"
```

### STAC Tests Only
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests \
  --filter "FullyQualifiedName~StacCatalogTests"
```

### Authentication Tests Only
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests \
  --filter "FullyQualifiedName~AuthenticationFlowTests"
```

### Negative Scenario Tests Only
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests \
  --filter "FullyQualifiedName~NegativeScenarioTests"
```

## Expected Output

```
Test run for /home/mike/projects/HonuaIO/tests/Honua.Server.Deployment.E2ETests/bin/Release/net9.0/Honua.Server.Deployment.E2ETests.dll (.NETCoreApp,Version=v9.0)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 2 m 30 s
```

## Test Execution Time

| Phase | Duration |
|-------|----------|
| Container Startup | 15-30s |
| Test Execution | 2-3 min |
| **Total** | **~3 min** |

## What Gets Tested

### ✅ Metadata Loading
- Valid JSON parsing
- Invalid JSON handling
- Missing file detection

### ✅ Database Setup
- PostgreSQL container startup
- Schema migrations
- PostGIS extension installation
- STAC table creation

### ✅ Application Startup
- Service initialization
- Health check registration
- Metadata loading
- Data source connection

### ✅ Health Endpoints
- `/healthz/startup` - Startup probe
- `/healthz/live` - Liveness probe
- `/healthz/ready` - Readiness probe

### ✅ OGC API Features
- Landing page
- Conformance
- Collections
- Items
- Pagination
- CRS support

### ✅ STAC Catalog
- Root catalog
- Collections
- Items
- Search
- Pagination

### ✅ Authentication
- QuickStart mode (no auth)
- Local mode (JWT tokens)
- Login endpoint
- Token validation
- Admin bootstrapping

### ✅ Error Handling
- 404 Not Found
- 401 Unauthorized
- 400 Bad Request
- Graceful degradation

## Troubleshooting

### Issue: Docker not running
**Error**: `Docker is either not running or misconfigured`

**Fix**: Start Docker
```bash
# Linux
sudo systemctl start docker

# macOS/Windows
# Start Docker Desktop application
```

### Issue: Tests timeout
**Error**: Test exceeded timeout

**Fix**: Increase test timeout or check resources
```bash
# Check Docker resources
docker stats

# Increase Docker memory if needed (Docker Desktop Settings)
```

### Issue: Port conflicts
**Error**: Port already in use

**Fix**: Testcontainers uses random ports, so this should be rare. If it occurs:
```bash
# Find and stop conflicting containers
docker ps
docker stop <container-id>
```

### Issue: Container startup failure
**Error**: Container failed to start

**Fix**: Check Docker logs
```bash
# List all containers (including stopped)
docker ps -a

# Check logs
docker logs <container-id>
```

## Viewing Container Logs

While tests are running, you can view container logs:

```bash
# List running containers
docker ps

# View PostgreSQL logs
docker logs <postgres-container-id>

# View Redis logs
docker logs <redis-container-id>

# Follow logs in real-time
docker logs -f <container-id>
```

## Clean Up

Tests automatically clean up containers after execution. If containers persist:

```bash
# Remove all stopped containers
docker container prune -f

# Remove all unused volumes
docker volume prune -f
```

## Integration with CI/CD

### GitHub Actions Example
```yaml
name: E2E Deployment Tests

on: [push, pull_request]

jobs:
  e2e-tests:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Run E2E Tests
        run: |
          dotnet test tests/Honua.Server.Deployment.E2ETests \
            --configuration Release \
            --logger "trx;LogFileName=e2e-results.trx" \
            --collect:"XPlat Code Coverage"

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: '**/e2e-results.trx'
```

## Next Steps

After running tests locally:
1. Review test output for any failures
2. Check container logs if issues occur
3. Examine failing test details
4. Fix any identified issues
5. Re-run tests to verify fixes

For more detailed information, see [README.md](README.md)
