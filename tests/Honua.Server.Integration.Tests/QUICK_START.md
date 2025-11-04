# Quick Start Guide - Honua Integration Tests

## 5-Minute Setup

### 1. Prerequisites Check

```bash
# Check Docker is running
docker ps

# Check .NET version
dotnet --version  # Should be 9.0.x
```

### 2. Run Tests

```bash
cd tests/Honua.Server.Integration.Tests
dotnet test
```

That's it! The tests will:
1. Pull PostgreSQL image if needed (~500MB, one-time)
2. Start container on random port
3. Run migrations
4. Execute 34 tests
5. Clean up automatically

## Common Commands

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "EndToEndWorkflowTests"

# Run single test
dotnet test --filter "Test_CompleteWorkflow_NewCustomer_Success"

# Verbose output
dotnet test -v detailed

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Watch mode (rerun on changes)
dotnet watch test
```

## Test Structure

```
EndToEndWorkflowTests          - 6 tests  - Complete build workflows
BuildQueueIntegrationTests     - 8 tests  - Queue processing & concurrency
RegistryProvisioningTests      - 10 tests - Multi-cloud registry setup
LicenseManagementTests         - 10 tests - License lifecycle
                                --------
                                34 tests total
```

## Example Test

```csharp
[Fact]
public async Task Test_CompleteWorkflow_NewCustomer_Success()
{
    // Arrange - Create customer with license
    await _fixture.SeedTestDataAsync(builder =>
    {
        builder.WithCustomerLicense("customer-001", "Professional");
    });

    var manifest = ManifestBuilder.CreateDefault()
        .WithTarget("aws-graviton3", "aws", "graviton3", "linux-arm64")
        .Build();

    // Act - Process build
    var buildId = await QueueBuildAsync("customer-001", manifest.Id, hash, "aws-graviton3");
    await SimulateBuildProcessingAsync(buildId, hash, "aws-graviton3");

    // Assert - Check database state
    var build = await GetBuildRecordAsync(buildId);
    build.Status.Should().Be("success");
}
```

## Writing Your First Test

### Step 1: Create test class

```csharp
[Collection("Integration")]
public class MyNewTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public MyNewTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }
}
```

### Step 2: Seed test data

```csharp
[Fact]
public async Task Test_MyScenario()
{
    // Seed data
    await _fixture.SeedTestDataAsync(builder =>
    {
        builder.WithCustomerLicense("test-customer", "Professional");
    });

    // Get connection
    using var connection = _fixture.CreateConnection();
    await connection.OpenAsync();

    // Query and assert
    var license = await connection.QuerySingleAsync<dynamic>(@"
        SELECT * FROM customer_licenses WHERE customer_id = @Id
    ", new { Id = "test-customer" });

    license.license_tier.Should().Be("Professional");
}
```

## Debugging Tips

### See what's happening

```csharp
// Enable verbose logging in test
_fixture.LoggerFactory.CreateLogger<MyTest>()
    .LogInformation("Processing build {BuildId}", buildId);
```

### Inspect database during test

```csharp
// Add breakpoint, then in debug console:
var data = await connection.QueryAsync("SELECT * FROM build_queue");
```

### Check Docker container

```bash
# While test is paused
docker ps  # Find container ID
docker exec -it <container-id> psql -U postgres -d honua_test

# Run queries
SELECT * FROM customer_licenses;
```

## Performance Tips

### Fast cleanup with Respawn
- Fixture automatically truncates tables between tests
- ~100-200ms vs ~2-3s for recreating schema

### Reuse fixture
- All tests in `[Collection("Integration")]` share same container
- Container starts once per test run

### Seed minimal data
```csharp
// ✅ Good - only what you need
builder.WithCustomerLicense("customer-001");

// ❌ Bad - unnecessary data
builder.WithCustomerLicense("customer-001");
builder.WithCustomerLicense("customer-002");
builder.WithCustomerLicense("customer-003");
```

## Common Patterns

### Test build workflow
```csharp
var buildId = await QueueBuildAsync(customerId, manifestId, hash, targetId);
await SimulateBuildProcessingAsync(buildId, hash, targetId);
var build = await GetBuildRecordAsync(buildId);
build.Status.Should().Be("success");
```

### Test cache hit
```csharp
await _fixture.SeedTestDataAsync(builder =>
{
    builder.WithCacheEntry(hash, manifestId, targetId, imageRef);
});
var stopwatch = Stopwatch.StartNew();
await ProcessBuildAsync(buildId);
stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
```

### Test license validation
```csharp
var accessManager = _services.GetRequiredService<IRegistryAccessManager>();
var result = await accessManager.ValidateAccessAsync(customerId, RegistryType.AwsEcr);
result.AccessGranted.Should().BeTrue();
```

## Troubleshooting

### Tests hang
```bash
# Check Docker
docker ps
docker stats

# Ensure enough resources (4GB+ RAM recommended)
```

### Connection errors
```bash
# Check PostgreSQL logs
docker logs <container-id>

# Verify port not in use
lsof -i :5432
```

### Slow tests
```bash
# Check disk I/O
iostat -x 1

# Clean Docker
docker system prune
```

### Port conflicts
- Testcontainers uses random ports automatically
- No configuration needed

## Next Steps

1. ✅ Read [README.md](README.md) for full documentation
2. ✅ Review [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) for architecture
3. ✅ Explore existing tests for patterns
4. ✅ Add your own tests!

## Test Data Builders

### ManifestBuilder
```csharp
var manifest = ManifestBuilder.CreateDefault()
    .WithId("my-manifest")
    .WithModule("WMS")
    .WithTarget("aws-graviton3", "aws", "graviton3", "linux-arm64")
    .Build();
```

### TestDataBuilder
```csharp
await _fixture.SeedTestDataAsync(builder =>
{
    builder
        .WithCustomerLicense("customer", "Professional", maxConcurrentBuilds: 2)
        .WithCacheEntry(hash, manifestId, targetId, imageRef)
        .WithBuildInQueue(customerId, manifestId, hash, priority: 100)
        .WithRegistryCredentials(customerId, "GitHubContainerRegistry", ns, url);
});
```

## License Tiers

| Tier | Max Builds | Registries |
|------|-----------|------------|
| Standard | 1 | GHCR |
| Professional | 2-3 | GHCR, ECR, ACR |
| Enterprise | 10+ | All (GHCR, ECR, ACR, GCP) |

## Registry Types

- **GitHubContainerRegistry** - ghcr.io (1hr tokens)
- **AwsEcr** - AWS Elastic Container Registry (12hr tokens)
- **AzureAcr** - Azure Container Registry (1hr tokens)
- **GcpArtifactRegistry** - GCP Artifact Registry (service accounts)

## Build Statuses

- **queued** - Waiting to process
- **running** - Currently building
- **success** - Build completed
- **failed** - Build failed (retryable)
- **timeout** - Exceeded time limit
- **cancelled** - User cancelled

## Quick Reference

```csharp
// Get connection
var conn = _fixture.CreateConnection();

// Seed data
await _fixture.SeedTestDataAsync(builder => {});

// Reset database
await _fixture.ResetDatabaseAsync();

// Create manifest
var manifest = ManifestBuilder.CreateDefault().Build();

// Query database
var data = await conn.QueryAsync<T>("SELECT ...");

// Assert
data.Should().HaveCount(5);
data.Should().OnlyContain(x => x.Status == "success");
```

## Help & Support

- 📖 Full docs: [README.md](README.md)
- 🏗️ Architecture: [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)
- 🐛 Issues: Check Docker logs and test output
- 💡 Examples: See existing test classes

Happy testing! 🚀
