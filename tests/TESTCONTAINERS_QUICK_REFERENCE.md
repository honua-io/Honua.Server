# Testcontainers Quick Reference

## Quick Start

### Run All Tests
```bash
dotnet test
```
That's it! Testcontainers handles everything automatically.

## Common Commands

### Run Only Integration Tests
```bash
dotnet test --filter "Category=IntegrationTest"
```

### Run Specific Container Tests
```bash
# Storage tests (MinIO + Azurite)
dotnet test --filter "Collection=StorageContainers"

# Redis tests
dotnet test --filter "Collection=RedisContainer"

# Database provider tests
dotnet test --filter "FullyQualifiedName~MultiProviderTestFixture"
```

### Manual Testing Environment
```bash
# Start services
cd tests/Honua.Server.Core.Tests
docker-compose -f docker-compose.storage-emulators.yml up -d

# Run tests
dotnet test

# Stop services
docker-compose -f docker-compose.storage-emulators.yml down
```

## Container Details

### MinIO (S3-Compatible)
- **Console**: http://localhost:9001
- **User**: minioadmin
- **Password**: minioadmin
- **Used by**: S3RasterTileCacheProviderIntegrationTests

### Azurite (Azure Storage)
- **Blob**: http://localhost:10000
- **Connection String**: Well-known Azurite connection string
- **Used by**: AzureRasterTileCacheProviderIntegrationTests

### Redis
- **Port**: localhost:6379
- **Used by**: RedisWfsLockManagerIntegrationTests

## Troubleshooting

### Tests are skipped
**Problem**: "Docker is not available"

**Solution**:
```bash
# Check Docker is running
docker ps

# Start Docker Desktop (Windows/Mac)
# Or start Docker service (Linux)
sudo systemctl start docker
```

### Slow first run
**Problem**: Tests take long on first execution

**Solution**: Pre-pull images
```bash
docker pull minio/minio:latest
docker pull mcr.microsoft.com/azure-storage/azurite:latest
docker pull redis:7-alpine
```

### Port conflicts
**Problem**: "Port already in use"

**Solution**: Testcontainers auto-assigns ports. If using docker-compose:
```bash
# Stop conflicting services
docker-compose -f docker-compose.storage-emulators.yml down

# Or change ports in docker-compose.storage-emulators.yml
```

### Container cleanup
**Problem**: Old containers not removed

**Solution**:
```bash
# Remove all testcontainers
docker ps -a | grep testcontainers | awk '{print $1}' | xargs docker rm -f

# Clean up volumes
docker volume prune -f
```

## Writing Tests

### Using Shared Storage Fixture
```csharp
[Collection("StorageContainers")]
public class MyS3Tests : IAsyncLifetime
{
    private readonly StorageContainerFixture _fixture;

    public MyS3Tests(StorageContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.MinioAvailable)
        {
            throw new SkipException("MinIO not available");
        }

        var s3Client = _fixture.MinioClient;
        // Use s3Client...
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MyTest() { }
}
```

### Using Shared Redis Fixture
```csharp
[Collection("RedisContainer")]
public class MyRedisTests : IAsyncLifetime
{
    private readonly RedisContainerFixture _fixture;

    public MyRedisTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.RedisAvailable)
        {
            throw new SkipException("Redis not available");
        }

        var redis = _fixture.Redis;
        // Use redis...
    }
}
```

## Environment Check

### Verify Setup
```bash
./tests/verify-testcontainers.sh
```

### Expected Output
```
✓ Docker is available and ready
✓ Testcontainers packages configured
✓ Test infrastructure created
✓ Docker Compose configuration ready
```

## Performance Tips

1. **Use shared fixtures** - Already implemented
2. **Pre-pull images** - Saves time on first run
3. **Increase Docker resources** - 4 CPU, 8GB RAM recommended
4. **Keep images cached** - Don't run `docker system prune -a`

## Documentation

- **Full Guide**: `tests/TESTCONTAINERS_GUIDE.md`
- **Docker Compose**: `tests/Honua.Server.Core.Tests/DOCKER_COMPOSE_TESTING.md`
- **Implementation Summary**: `TESTCONTAINERS_IMPLEMENTATION_SUMMARY.md`

## Support

If tests are failing:
1. Check Docker is running: `docker ps`
2. Check container logs: `docker logs <container-id>`
3. Verify images: `docker images | grep -E "minio|azurite|redis"`
4. Review test output for specific errors
5. See full troubleshooting guide in `TESTCONTAINERS_GUIDE.md`

## CI/CD

Tests work automatically in:
- ✅ GitHub Actions
- ✅ GitLab CI (with docker:dind)
- ✅ Azure Pipelines
- ✅ Jenkins (with Docker plugin)

No special configuration needed - Testcontainers just works!
