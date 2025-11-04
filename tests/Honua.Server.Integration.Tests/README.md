# Honua.Server.Integration.Tests

Comprehensive integration tests for the Honua build system that test complete end-to-end workflows using real PostgreSQL database instances in Docker containers.

## Overview

These integration tests validate the complete Honua build system pipeline:

1. **End-to-End Workflows** - Full intake → build → delivery cycles
2. **Build Queue Processing** - Concurrent builds, priorities, retries, timeouts
3. **Registry Provisioning** - Multi-cloud registry setup and credential management
4. **License Management** - License lifecycle, upgrades, downgrades, and expiration

## Architecture

### Test Infrastructure

- **Docker Containers**: PostgreSQL 16 with PostGIS extension via Testcontainers
- **Database Cleanup**: Respawn for fast database resets between tests
- **Mocked External Services**: AWS, Azure, GCP, and GitHub APIs
- **Parallel Execution**: xUnit collection-based isolation

### Key Components

#### IntegrationTestFixture

Shared test fixture that:
- Starts PostgreSQL container on a random port
- Runs database migrations to set up schema
- Provides connection string and database utilities
- Cleans up containers after test run

```csharp
[Collection("Integration")]
public class MyIntegrationTest : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public MyIntegrationTest(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task MyTest()
    {
        // Test implementation
    }
}
```

#### TestDataBuilder

Fluent API for seeding test data:

```csharp
await _fixture.SeedTestDataAsync(builder =>
{
    builder
        .WithCustomerLicense("customer-001", "Professional", maxConcurrentBuilds: 2)
        .WithCacheEntry("hash123", "manifest-001", "target-001", "ghcr.io/image:v1")
        .WithBuildInQueue("customer-001", "manifest-001", "hash123", priority: 100)
        .WithRegistryCredentials("customer-001", "GitHubContainerRegistry", "namespace", "ghcr.io");
});
```

#### ManifestBuilder

Fluent builder for creating test build manifests:

```csharp
var manifest = ManifestBuilder.CreateDefault()
    .WithId("test-manifest")
    .WithName("Test GIS Server")
    .WithModule("WMS")
    .WithModule("WFS")
    .WithTarget("aws-graviton3", "aws", "graviton3", "linux-arm64")
    .WithTarget("azure-ampere", "azure", "ampere", "linux-arm64")
    .Build();
```

## Test Categories

### EndToEndWorkflowTests

Complete workflow tests that validate the entire build pipeline:

- **Test_CompleteWorkflow_NewCustomer_Success** - Full intake to delivery for new customer
- **Test_CompleteWorkflow_CacheHit_FastDelivery** - Cache optimization validation
- **Test_CompleteWorkflow_MultipleTargets_ParallelBuilds** - Parallel target processing
- **Test_CompleteWorkflow_WithBuildDelivery_Success** - Build delivery to customer registry
- **Test_CompleteWorkflow_LicenseExpired_AccessDenied** - Access control validation

### BuildQueueIntegrationTests

Queue processing and concurrency tests:

- **Test_BuildQueue_ProcessInPriorityOrder** - Priority-based queue ordering
- **Test_BuildQueue_ConcurrentBuilds_RespectLimit** - Concurrent build limits
- **Test_BuildQueue_BuildFailure_RetryLogic** - Automatic retry on failure
- **Test_BuildQueue_BuildTimeout_Cancellation** - Timeout handling
- **Test_BuildQueue_ParallelTargets_IndependentProcessing** - Parallel target builds
- **Test_BuildQueue_CancellationRequest_StopsProcessing** - Cancellation support
- **Test_BuildQueue_MetricsCollection_RecordsPerformance** - Performance metrics

### RegistryProvisioningIntegrationTests

Container registry provisioning tests:

- **Test_ProvisionAws_CreateResources_Success** - AWS ECR provisioning
- **Test_ProvisionGitHub_CreateNamespace_Success** - GitHub Container Registry
- **Test_ProvisionAzure_TokenScoped_Success** - Azure ACR provisioning
- **Test_ProvisionGcp_ServiceAccount_Success** - GCP Artifact Registry
- **Test_RevokeCredentials_DeleteResources_Success** - Credential revocation
- **Test_ProvisionMultipleRegistries_CustomerIsolation** - Multi-tenant isolation
- **Test_ProvisionExisting_UpdatesCredentials** - Credential refresh
- **Test_LicenseTier_RestrictsRegistryAccess** - License-based access control
- **Test_TokenRefresh_GeneratesNewCredentials** - Token refresh
- **Test_BulkProvisioning_MultipleRegistries** - Bulk provisioning

### LicenseManagementIntegrationTests

License lifecycle tests:

- **Test_GenerateLicense_ValidCustomer_Success** - New license creation
- **Test_ExpiredLicense_AutoRevoke_Success** - Automatic expiration
- **Test_UpgradeLicense_PreserveHistory_Success** - License upgrades
- **Test_SuspendLicense_RevokeAccess_Success** - License suspension
- **Test_LicenseTierFeatures_StandardVsEnterprise** - Feature comparison
- **Test_RenewLicense_ExtendExpiration** - License renewal
- **Test_DowngradeLicense_RemoveFeatures** - License downgrades
- **Test_BulkLicenseExpiration_BatchProcessing** - Batch operations
- **Test_LicenseMetadata_CustomFields** - Custom metadata
- **Test_TrialLicense_AutoConvertOrExpire** - Trial license handling

## Database Schema

The tests use the following tables:

### build_queue

Tracks build jobs in the processing queue.

```sql
CREATE TABLE build_queue (
    id UUID PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL,
    manifest_id VARCHAR(100) NOT NULL,
    manifest_hash VARCHAR(64) NOT NULL,
    status VARCHAR(50) NOT NULL,
    priority INTEGER NOT NULL DEFAULT 100,
    target_id VARCHAR(100),
    output_path TEXT,
    error_message TEXT,
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    timeout_at TIMESTAMPTZ
);
```

### build_cache_registry

Stores cached build artifacts to avoid rebuilding.

```sql
CREATE TABLE build_cache_registry (
    id UUID PRIMARY KEY,
    manifest_hash VARCHAR(64) NOT NULL UNIQUE,
    manifest_id VARCHAR(100) NOT NULL,
    target_id VARCHAR(100) NOT NULL,
    image_reference TEXT NOT NULL,
    digest VARCHAR(128),
    architecture VARCHAR(50),
    binary_size BIGINT,
    cache_hit_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### customer_licenses

Manages customer license tiers and access rights.

```sql
CREATE TABLE customer_licenses (
    id UUID PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL UNIQUE,
    license_tier VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'active',
    issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ,
    max_concurrent_builds INTEGER NOT NULL DEFAULT 1,
    allowed_registries TEXT[] NOT NULL,
    metadata JSONB
);
```

### registry_credentials

Stores provisioned registry credentials per customer.

```sql
CREATE TABLE registry_credentials (
    id UUID PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL,
    registry_type VARCHAR(50) NOT NULL,
    namespace VARCHAR(255) NOT NULL,
    registry_url VARCHAR(255) NOT NULL,
    username VARCHAR(255),
    password_encrypted TEXT,
    access_token_encrypted TEXT,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at TIMESTAMPTZ,
    metadata JSONB
);
```

### build_metrics

Performance metrics for completed builds.

```sql
CREATE TABLE build_metrics (
    id UUID PRIMARY KEY,
    build_id UUID NOT NULL REFERENCES build_queue(id) ON DELETE CASCADE,
    metric_name VARCHAR(100) NOT NULL,
    metric_value DECIMAL,
    unit VARCHAR(50),
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

## Running the Tests

### Prerequisites

1. **Docker** must be installed and running
2. **.NET 9.0 SDK** installed
3. **Sufficient disk space** for PostgreSQL images (~500MB)

### Run All Tests

```bash
cd tests/Honua.Server.Integration.Tests
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~EndToEndWorkflowTests"
```

### Run Single Test

```bash
dotnet test --filter "FullyQualifiedName~Test_CompleteWorkflow_NewCustomer_Success"
```

### Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Generate Coverage Report

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Performance Considerations

### Test Execution Time

- **Container startup**: ~5-10 seconds (one-time per test run)
- **Database migrations**: ~2-3 seconds
- **Individual tests**: 100ms - 5 seconds
- **Full suite**: ~30-60 seconds

### Optimization Tips

1. **Reuse fixture** - Tests in same collection share the database container
2. **Parallel execution disabled** - Due to shared database state
3. **Fast cleanup** - Respawn truncates tables instead of recreating schema
4. **Indexed queries** - Database tables have appropriate indexes

## Troubleshooting

### Docker Connection Issues

If tests fail to start PostgreSQL container:

```bash
# Check Docker is running
docker ps

# Check available disk space
df -h

# Clean up old containers
docker system prune
```

### Port Conflicts

Testcontainers uses random ports to avoid conflicts. If issues persist:

```bash
# Check what's using PostgreSQL default port
lsof -i :5432
```

### Slow Test Execution

If tests are running slowly:

1. Ensure Docker has sufficient resources (4GB+ RAM recommended)
2. Check disk I/O performance
3. Consider using in-memory Respawn checkpoints

### Database Connection Errors

If seeing connection timeouts:

```bash
# Check container logs
docker logs <container-id>

# Increase connection timeout in fixture if needed
```

## Mock Services

The tests use mock implementations of external services to avoid dependencies:

- **MockRegistryProvisioner** - Simulates AWS/Azure/GCP/GitHub API calls
- **MockRegistryAccessManager** - Validates licenses and generates tokens
- **MockBuildDeliveryService** - Simulates image delivery operations

These mocks:
- Return realistic response structures
- Validate input parameters
- Store state in the test database
- Don't make external network calls

## CI/CD Integration

### GitHub Actions

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  integration-tests:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Run Integration Tests
        run: |
          cd tests/Honua.Server.Integration.Tests
          dotnet test --logger "trx;LogFileName=test-results.trx"

      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: integration-test-results
          path: '**/*.trx'
```

### Azure DevOps

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Integration Tests'
  inputs:
    command: 'test'
    projects: '**/Honua.Server.Integration.Tests.csproj'
    arguments: '--configuration Release --logger trx'
```

## Best Practices

1. **Always clean up** - Implement IAsyncLifetime and reset database
2. **Use descriptive test names** - Follow pattern Test_Scenario_Condition_ExpectedResult
3. **Seed minimal data** - Only create data needed for the test
4. **Assert thoroughly** - Check database state, not just return values
5. **Handle async properly** - Use async/await, not .Result or .Wait()
6. **Isolate tests** - Each test should be independently runnable
7. **Use builders** - Leverage ManifestBuilder and TestDataBuilder for readability

## Contributing

When adding new tests:

1. Add to appropriate test class or create new one
2. Use the [Collection("Integration")] attribute
3. Implement IAsyncLifetime for cleanup
4. Seed data with TestDataBuilder
5. Assert both return values and database state
6. Update this README if adding new test categories

## License

Copyright (c) 2025 HonuaIO. All rights reserved.
