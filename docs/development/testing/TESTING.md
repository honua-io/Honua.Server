# Honua Testing Guide

This document describes the testing strategy and how to run tests for Honua.

## Testing Philosophy

Honua follows **Test-Driven Development (TDD)** principles:

1. ✅ **Tests are mandatory** - All new features must have tests
2. ✅ **Integration tests over mocks** - Use real emulators for cloud services
3. ✅ **Fast feedback** - Unit tests run in milliseconds, integration tests in seconds
4. ✅ **CI/CD ready** - Tests skip gracefully when dependencies unavailable
5. ✅ **Coverage enforcement** - Code coverage thresholds enforced in CI

## Code Coverage Requirements

Honua maintains strict code coverage thresholds to ensure quality:

| Project | Minimum Coverage |
|---------|-----------------|
| Honua.Server.Core | 65% |
| Honua.Server.Host | 60% |
| Honua.Cli.AI | 55% |
| Honua.Cli | 50% |
| Overall | 60% |

**See [CODE_COVERAGE.md](CODE_COVERAGE.md) for detailed coverage documentation.**

### Quick Coverage Check

```bash
# Run tests and check coverage
./scripts/check-coverage.sh

# View HTML report
open ./CoverageReport/index.html
```

## Test Structure

```
tests/
├── Honua.Server.Core.Tests/          # Core library tests
│   ├── Data/                          # Data access tests
│   ├── Raster/                        # Raster processing tests
│   │   └── Caching/                   # Raster cache provider tests
│   ├── VectorTiles/                   # Vector tile tests
│   │   └── Caching/                   # Vector cache provider tests
│   ├── docker-compose.storage-emulators.yml
│   └── STORAGE_INTEGRATION_TESTS.md
├── Honua.Server.Host.Tests/          # API endpoint tests
├── Honua.Cli.Tests/                  # CLI command tests
└── Honua.Cli.AI.Tests/               # AI consultant tests
```

## Running Tests

### Quick Start - Unit Tests Only

```bash
# Run all tests (skips integration tests if emulators not running)
dotnet test

# Run specific test project
dotnet test tests/Honua.Server.Core.Tests

# Run specific test class
dotnet test --filter "FullyQualifiedName~FeatureRepositoryTests"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Integration Tests with Emulators

Honua supports two approaches for running integration tests:

#### Option 1: Dev Container (Recommended)

**Prerequisites:**
- Docker
- VS Code with Dev Containers extension OR GitHub Codespaces

**Steps:**
1. Open the project in VS Code
2. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac) and select "Dev Containers: Reopen in Container"
3. Wait for the container to build and start (includes all emulators automatically)
4. Run tests inside the container:

```bash
# All tests (unit + integration)
dotnet test

# Only integration tests
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Specific integration test suite
dotnet test --filter "FullyQualifiedName~GcsRasterTileCacheProviderIntegrationTests"
```

**Benefits:**
- ✅ Emulators start automatically (LocalStack, Azurite, GCS)
- ✅ Consistent environment across all developers
- ✅ No manual emulator management
- ✅ Pre-configured environment variables
- ✅ Works identically on any OS (Windows, Mac, Linux)

#### Option 2: Local with Docker Compose

**Prerequisites:**
- Docker and Docker Compose
- .NET 9.0 SDK

**1. Start Cloud Storage Emulators:**

```bash
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml up -d
```

This starts:
- **LocalStack** (AWS S3) on `localhost:4566`
- **Azurite** (Azure Blob) on `localhost:10000`
- **fake-gcs-server** (GCS) on `localhost:4443`

**2. Verify Emulators:**

```bash
# Check all containers are running
docker-compose -f docker-compose.storage-emulators.yml ps

# Health checks
curl http://localhost:4566/_localstack/health  # S3
curl http://localhost:10000/                    # Azure
curl http://localhost:4443/storage/v1/b        # GCS
```

**3. Run Integration Tests:**

```bash
# Run all integration tests
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Run only GCS integration tests
dotnet test --filter "FullyQualifiedName~GcsRasterTileCacheProviderIntegrationTests"
dotnet test --filter "FullyQualifiedName~GcsVectorTileCacheProviderIntegrationTests"

# Run only S3 integration tests (when implemented)
dotnet test --filter "FullyQualifiedName~S3.*IntegrationTests"

# Run only Azure integration tests (when implemented)
dotnet test --filter "FullyQualifiedName~Azure.*IntegrationTests"
```

**4. Stop Emulators:**

```bash
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml down

# Clean up volumes (optional)
docker-compose -f docker-compose.storage-emulators.yml down -v
```

## Test Categories

### Unit Tests

**Fast, isolated tests with no external dependencies.**

```csharp
[Fact]
public void PathHelper_ShouldSanitizeSpecialCharacters()
{
    var sanitized = RasterTileCachePathHelper.Sanitize("my/path\\with:bad*chars");
    sanitized.Should().Be("my_path_with_bad_chars");
}
```

**Characteristics:**
- No I/O, no network, no database
- Run in < 100ms
- Can use mocks/stubs for dependencies
- Always run in CI/CD

### Integration Tests

**Tests against real cloud emulators.**

```csharp
[Collection("StorageEmulators")]
public class GcsRasterTileCacheProviderIntegrationTests : IAsyncLifetime
{
    private StorageClient? _storageClient;

    public async Task InitializeAsync()
    {
        // Check if emulator is running
        if (!await IsEmulatorRunningAsync())
        {
            throw new SkipException("GCS emulator not running");
        }

        // Set up real GCS client pointing to emulator
        _storageClient = new StorageClientBuilder
        {
            BaseUri = "http://localhost:4443",
            UnauthenticatedAccess = true
        }.Build();
    }

    [Fact]
    public async Task StoreAsync_ThenTryGetAsync_ShouldReturnStoredTile()
    {
        // Test against real emulator
        var key = new RasterTileCacheKey(...);
        await _provider.StoreAsync(key, entry);

        var result = await _provider.TryGetAsync(key);

        result.Should().NotBeNull();
    }
}
```

**Characteristics:**
- Test real SDKs (AWS SDK, Azure SDK, Google Cloud SDK)
- Require Docker emulators
- Run in seconds
- Skip gracefully if emulators unavailable
- Marked with `[Collection("StorageEmulators")]`

### E2E Tests

**End-to-end tests of full system.**

Located in `tests/Honua.Cli.Tests/E2E/` and `tests/e2e-assistant/`.

```bash
# Run E2E tests (requires full stack)
cd tests/Honua.Cli.Tests/E2E
docker-compose up -d
dotnet test --filter "Category=E2E"
docker-compose down
```

## Writing Tests

### TDD Workflow

1. **Write failing test first**
   ```csharp
   [Fact]
   public async Task NewFeature_ShouldWork()
   {
       var result = await _service.NewFeature();
       result.Should().Be(expected);
   }
   ```

2. **Run test - it should fail**
   ```bash
   dotnet test --filter "NewFeature_ShouldWork"
   # Expected: FAIL (feature doesn't exist yet)
   ```

3. **Implement feature**
   ```csharp
   public async Task<int> NewFeature()
   {
       // Implementation
   }
   ```

4. **Run test - it should pass**
   ```bash
   dotnet test --filter "NewFeature_ShouldWork"
   # Expected: PASS
   ```

### Integration Test Template

```csharp
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.MyFeature;

[Collection("StorageEmulators")]  // For tests using emulators
public class MyProviderIntegrationTests : IAsyncLifetime
{
    private MyProvider? _provider;

    public async Task InitializeAsync()
    {
        // Check if emulator is running
        if (!await IsEmulatorAvailableAsync())
        {
            throw new SkipException("Emulator not running. " +
                "Run: docker-compose -f docker-compose.storage-emulators.yml up -d");
        }

        // Set up provider with emulator endpoint
        _provider = new MyProvider(
            endpoint: "http://localhost:4566",  // Emulator URL
            credentials: "test");

        // Create test resources
        await _provider.CreateBucketAsync("test-bucket");
    }

    public async Task DisposeAsync()
    {
        // Clean up test resources
        if (_provider != null && await IsEmulatorAvailableAsync())
        {
            await _provider.DeleteBucketAsync("test-bucket");
        }
    }

    private static async Task<bool> IsEmulatorAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync("http://localhost:4566/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task MyFeature_ShouldWork()
    {
        // Arrange
        var input = "test-data";

        // Act
        var result = await _provider!.ProcessAsync(input);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("expected");
    }
}
```

## Continuous Integration

Honua uses GitHub Actions for automated testing in CI/CD pipelines. The integration tests run automatically with cloud storage emulators.

### CI/CD Workflows

The project has a dedicated workflow for integration tests with cloud storage emulators:

- **Integration Tests Workflow**: `.github/workflows/integration-tests.yml`
  - Runs on push to `master`, `main`, `dev` branches
  - Runs on pull requests to `master`, `main`, `dev` branches
  - Can be triggered manually via workflow dispatch
  - Separate jobs for unit tests and integration tests
  - Automatic emulator health checks with 120s timeout
  - Test result reporting and code coverage

### Helper Script

Use the provided health check script to ensure emulators are ready:

```bash
# Wait for all emulators to be healthy (default 120s timeout)
./scripts/wait-for-emulators.sh

# Custom timeout
./scripts/wait-for-emulators.sh 60

# With custom URLs
LOCALSTACK_URL=http://localhost:4566 \
AZURITE_URL=http://localhost:10000 \
GCS_URL=http://localhost:4443 \
./scripts/wait-for-emulators.sh
```

### Manual Workflow Dispatch

You can manually trigger the integration tests workflow from the GitHub Actions UI:

1. Go to **Actions** tab in GitHub
2. Select **Integration Tests with Cloud Storage Emulators**
3. Click **Run workflow**
4. Optional: Check "Run only integration tests" to skip unit tests

For complete CI/CD documentation, including workflow architecture, troubleshooting, and maintainer guide, see **[CI/CD Documentation](./CI_CD.md)**.

## Test Coverage

### Generating Coverage Reports

```bash
# Install coverage tools
dotnet tool install --global dotnet-coverage
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

# Open report
open coverage-report/index.html  # macOS
xdg-open coverage-report/index.html  # Linux
```

### Coverage Goals

- **Core Libraries**: ≥ 80% line coverage
- **API Endpoints**: ≥ 70% line coverage
- **Critical Paths**: 100% coverage (authentication, data access, cache operations)

## Troubleshooting

### Emulator Won't Start

```bash
# Check Docker is running
docker ps

# Check port conflicts
lsof -i :4566  # LocalStack
lsof -i :10000 # Azurite
lsof -i :4443  # GCS

# View logs
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml logs
```

### Tests Are Skipped

```bash
# Verify emulators are healthy
curl http://localhost:4566/_localstack/health
curl http://localhost:10000/
curl http://localhost:4443/storage/v1/b

# If unhealthy, restart
docker-compose -f docker-compose.storage-emulators.yml restart
```

### Tests Fail with Connection Errors

```bash
# Clean restart
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml down -v
docker-compose -f docker-compose.storage-emulators.yml up -d

# Wait for health checks
sleep 10
curl http://localhost:4566/_localstack/health
```

## Best Practices

### ✅ DO

- Write tests before implementation (TDD)
- Use FluentAssertions for readable assertions
- Test edge cases and error conditions
- Clean up resources in `DisposeAsync()`
- Use descriptive test names: `Method_Scenario_ExpectedResult`
- Use integration tests for cloud storage providers
- Make tests deterministic (no random data, no timing dependencies)

### ❌ DON'T

- Skip writing tests
- Use mocks for cloud storage (use emulators instead)
- Leave test resources (buckets, containers) behind
- Use `Thread.Sleep()` for timing (use proper async/await)
- Test implementation details (test behavior, not internals)
- Copy-paste test code (use helper methods)

## References

- **[Storage Integration Tests](../tests/Honua.Server.Core.Tests/STORAGE_INTEGRATION_TESTS.md)** - Detailed guide for cloud storage tests
- **[FluentAssertions Documentation](https://fluentassertions.com/)** - Assertion library
- **[xUnit Documentation](https://xunit.net/)** - Test framework
- **[LocalStack Documentation](https://docs.localstack.cloud/)** - AWS emulator
- **[Azurite Documentation](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)** - Azure Storage emulator
- **[fake-gcs-server](https://github.com/fsouza/fake-gcs-server)** - GCS emulator
