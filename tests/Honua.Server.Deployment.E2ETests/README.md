# Honua Server Deployment E2E Tests

## Overview

This test project contains comprehensive end-to-end (E2E) tests for the Honua server deployment workflow, covering the complete lifecycle from metadata ingestion through API availability and authentication.

## Purpose

These tests validate:
- **Metadata ingestion** (JSON/YAML parsing and loading)
- **Database migrations** (PostgreSQL schema setup)
- **Application startup** (service initialization)
- **Health check endpoints** (/healthz/startup, /healthz/live, /healthz/ready)
- **OGC API Features** endpoint availability
- **STAC catalog** query functionality
- **Authentication flows** (QuickStart and JWT token issuance)
- **Error handling** (invalid metadata, missing dependencies, failed migrations)

## Test Categories

### 1. Deployment Workflow Tests (`DeploymentWorkflowTests.cs`)
- **8 test scenarios** covering complete deployment workflows
- Tests both QuickStart and Local authentication modes
- Validates metadata ingestion, database initialization, and health checks
- Tests:
  - Complete deployment in QuickStart mode
  - Complete deployment with Local authentication
  - Metadata JSON parsing and loading
  - Application startup with all components
  - Database migrations for PostgreSQL
  - Health check endpoint availability
  - Redis cache integration

### 2. OGC API Features Tests (`OgcApiFeaturesTests.cs`)
- **7 test scenarios** for OGC API Features compliance
- Tests:
  - Landing page metadata
  - Conformance declaration
  - Collections listing
  - Collection items retrieval
  - Content negotiation (JSON/HTML)
  - Pagination support
  - CRS (Coordinate Reference System) support

### 3. STAC Catalog Tests (`StacCatalogTests.cs`)
- **9 test scenarios** for STAC catalog functionality
- Tests:
  - STAC root endpoint
  - Collections listing
  - Collection details retrieval
  - Collection items query
  - STAC search with filters
  - Pagination in search results
  - Error handling for non-existent collections
  - STAC version conformance

### 4. Authentication Flow Tests (`AuthenticationFlowTests.cs`)
- **9 test scenarios** covering authentication modes
- Tests:
  - QuickStart mode (unauthenticated access)
  - Local auth mode (JWT token issuance)
  - Login endpoint functionality
  - Token-based access to protected resources
  - Invalid credential rejection
  - Invalid token rejection
  - Token expiration handling
  - Health check accessibility
  - Admin user bootstrapping

### 5. Negative Scenario Tests (`NegativeScenarioTests.cs`)
- **15 test scenarios** for error handling and edge cases
- Tests:
  - Invalid/malformed JSON metadata
  - Missing metadata file
  - Invalid database connection strings
  - Missing data source references
  - Non-existent endpoints (404 errors)
  - Non-existent services and collections
  - Invalid pagination parameters
  - Malformed query parameters
  - Redis unavailability graceful degradation
  - Concurrent request handling
  - Large payload handling

## Total Test Count

**48 comprehensive E2E test scenarios** covering:
- Positive workflows
- Authentication modes
- API endpoint availability
- Error handling
- Edge cases

## Infrastructure

### Testcontainers
The tests use **Testcontainers** to provide isolated, reproducible test environments:

- **PostgreSQL with PostGIS** (`postgis/postgis:17-3.5`)
  - Provides full database functionality
  - Includes PostGIS extension for spatial data
  - Isolated per test run

- **Redis** (`redis:7-alpine`)
  - Tests caching functionality
  - Validates Redis health checks
  - Tests graceful degradation

### WebApplicationFactory
Uses ASP.NET Core's `WebApplicationFactory<T>` for integration testing:
- In-memory test server
- Real application pipeline
- Configurable authentication modes
- Test-specific configuration injection

## Test Data

### Sample Metadata Files
- `TestData/valid-metadata.json` - Valid metadata configuration
- `TestData/invalid-metadata.json` - Malformed JSON for negative testing

### Metadata Builder
`TestMetadataBuilder` class provides fluent API for creating test metadata:
```csharp
var metadata = new TestMetadataBuilder()
    .WithCatalog("test-catalog", "Test Catalog", "Description")
    .AddPostgresDataSource("db", connectionString)
    .AddFeatureService("service", "Service Name", "db")
    .Build();
```

## Running the Tests

### Prerequisites
1. **Docker** must be running (for Testcontainers)
2. **.NET 9.0 SDK** installed

### Run All Tests
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests
```

### Run Specific Test Class
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests --filter "FullyQualifiedName~DeploymentWorkflowTests"
```

### Run with Detailed Output
```bash
dotnet test tests/Honua.Server.Deployment.E2ETests --logger "console;verbosity=detailed"
```

## Expected Test Execution Time

| Test Suite | Estimated Time | Container Startup |
|------------|----------------|-------------------|
| DeploymentWorkflowTests | 30-45 seconds | PostgreSQL + Redis |
| OgcApiFeaturesTests | 25-40 seconds | PostgreSQL + Redis |
| StacCatalogTests | 30-50 seconds | PostgreSQL + Redis |
| AuthenticationFlowTests | 20-30 seconds | PostgreSQL + Redis |
| NegativeScenarioTests | 15-25 seconds | PostgreSQL + Redis |
| **Total** | **~2-3 minutes** | |

*Note: First run will be slower due to Docker image downloads*

## Test Organization

### Class Fixtures
Each test class uses `IClassFixture<DeploymentTestFactory>` to:
- Share container initialization across tests in the class
- Reduce container startup overhead
- Ensure test isolation through fresh metadata files

### Async Lifetime
`DeploymentTestFactory` implements `IAsyncLifetime` to:
- Start containers before any tests run
- Clean up containers after all tests complete
- Provide deterministic resource management

## Configuration Options

### QuickStart Mode
```csharp
_factory.UseQuickStartAuth();
```
- No authentication required
- Useful for development testing
- Tests unauthenticated API access

### Local Auth Mode
```csharp
_factory.UseLocalAuth();
```
- Requires JWT authentication
- Tests authentication workflow
- Validates token issuance and validation

## Troubleshooting

### Docker Not Running
**Error**: "Docker is either not running or misconfigured"

**Solution**: Start Docker daemon before running tests

### Container Port Conflicts
**Error**: Port already in use

**Solution**: Testcontainers automatically assigns random ports. Ensure no manual port assignments conflict.

### Database Connection Failures
**Error**: Connection timeout to PostgreSQL

**Solution**:
- Check Docker has sufficient resources
- Verify PostgreSQL container started successfully
- Check container logs for initialization errors

### Test Timeouts
**Error**: Test exceeded timeout

**Solution**:
- Increase test timeout in xUnit
- Check for resource constraints (CPU/memory)
- Verify containers are healthy before tests run

## CI/CD Integration

### GitHub Actions
```yaml
- name: Run E2E Tests
  run: |
    docker info
    dotnet test tests/Honua.Server.Deployment.E2E Tests --logger "trx;LogFileName=e2e-results.trx"
```

### Required Permissions
- Docker socket access
- Sufficient memory (recommend 4GB minimum)
- Network access for container communication

## Test Coverage

The E2E tests provide coverage for:

| Component | Coverage |
|-----------|----------|
| Metadata Loading | ✓ Valid, Invalid, Missing |
| Database Migrations | ✓ PostgreSQL schema creation |
| Health Checks | ✓ Startup, Live, Ready |
| OGC API Features | ✓ Landing, Collections, Items |
| STAC Catalog | ✓ Root, Collections, Search |
| Authentication | ✓ QuickStart, Local/JWT |
| Error Handling | ✓ 15 negative scenarios |
| Performance | ✓ Concurrent requests |

## Future Enhancements

Potential additions to the E2E test suite:
- [ ] Multi-region deployment tests
- [ ] Load testing scenarios
- [ ] Blue/green deployment validation
- [ ] Database backup/restore workflows
- [ ] Metrics and observability validation
- [ ] SSL/TLS certificate tests
- [ ] Rate limiting tests
- [ ] Custom CRS projection tests
- [ ] Large dataset ingestion tests
- [ ] Raster tile serving tests

## Contributing

When adding new E2E tests:
1. Use the existing test patterns (class fixtures, metadata builder)
2. Include both positive and negative scenarios
3. Add appropriate assertions with FluentAssertions
4. Document any new test data requirements
5. Ensure tests clean up resources properly
6. Keep test execution time reasonable (<10s per test)

## Support

For issues with E2E tests:
- Check Docker daemon status
- Verify container health
- Review test output logs
- Check container logs: `docker ps -a` and `docker logs <container-id>`
