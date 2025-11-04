# Docker Testing Guide

This guide explains how to run Docker integration tests for Honua Server.

## Prerequisites

- Docker installed and running
- .NET 9.0 SDK
- Docker image built: `honua-server:test`

## Building the Docker Image

Before running Docker tests, build the test image:

```bash
# From repository root
docker build -t honua-server:test .
```

## Running Docker Tests

### Run All Docker Tests

```bash
dotnet test --filter "Category=Docker"
```

### Run Specific Docker Test

```bash
dotnet test --filter "FullyQualifiedName~HonuaContainer_StartsSuccessfully"
```

## Docker Test Coverage

The Docker integration tests verify:

1. **Container Lifecycle**
   - Container starts successfully
   - Container responds to HTTP requests
   - Container shutdown is clean

2. **Database Connectivity**
   - PostgreSQL container networking
   - Connection string configuration
   - PostGIS extension availability

3. **Environment Configuration**
   - Environment variables are respected
   - QuickStart mode works in containers
   - ASPNETCORE_ENVIRONMENT is honored

4. **Volume Mounts**
   - Metadata files load from volumes
   - Configuration overrides work
   - File permissions are correct

5. **Resource Limits**
   - CPU limits don't prevent startup
   - Memory limits are respected
   - Container runs with constraints

## Test Configuration

### Skip Reasons

Docker tests are skipped by default with this message:

```
Docker image 'honua-server:test' must be built first.
Run 'docker build -t honua-server:test .' to enable this test.
```

To enable tests, build the image (see above).

### Test Data

Tests use:
- `samples/ogc/metadata.json` for OGC API configuration
- Temporary metadata files for custom scenarios
- In-memory SQLite databases where applicable

## Common Issues

### Issue: Tests Fail with "Image Not Found"

**Solution**: Build the Docker image first

```bash
docker build -t honua-server:test .
```

### Issue: Port Already in Use

**Cause**: Another container or process is using the mapped port

**Solution**: Kill existing containers

```bash
docker ps -a | grep honua-server | awk '{print $1}' | xargs docker rm -f
```

### Issue: Network Connectivity Fails

**Cause**: Docker networking may need `host.docker.internal`

**Solution**: Tests use `host.docker.internal` for host network access. Ensure Docker Desktop is configured to allow this.

### Issue: Tests Time Out

**Cause**: Container startup takes longer than expected

**Solution**: Increase wait strategy timeout in test code

## CI/CD Integration

### GitHub Actions

Docker tests run in the `docker-tests.yml` workflow:

```yaml
- name: Build Docker image
  run: docker build -t honua-server:test .

- name: Run Docker tests
  run: dotnet test --filter "Category=Docker"
```

### Local CI Simulation

To simulate CI environment:

```bash
# Clean everything
docker rm -f $(docker ps -aq)
docker system prune -af

# Build and test
docker build -t honua-server:test .
dotnet test --filter "Category=Docker"
```

## Performance Benchmarks

Expected test execution times:

| Test | Duration |
|------|----------|
| Container startup | ~10s |
| HTTP response | ~1s |
| Database connectivity | ~15s |
| Environment config | ~8s |
| Volume mounts | ~10s |
| Resource limits | ~12s |

## Debugging Docker Tests

### View Container Logs

```bash
# Get container ID from test output
docker logs <container-id>
```

### Interactive Shell

```bash
docker run -it --entrypoint /bin/bash honua-server:test
```

### Inspect Test Containers

```bash
# List containers from tests
docker ps -a | grep honua

# Inspect specific container
docker inspect <container-id>
```

## Advanced Testing

### Multi-Container Scenarios

Tests use Testcontainers library for orchestration:

```csharp
_postgresContainer = new ContainerBuilder()
    .WithImage("postgis/postgis:16-3.4")
    .WithPortBinding(5432, true)
    .Build();

await _postgresContainer.StartAsync();
```

### Network Testing

Tests verify container-to-container networking:

```csharp
var connectionString = $"Host=host.docker.internal;Port={port};...";
```

### Resource Constraints

Tests apply CPU and memory limits:

```csharp
.WithResourceMapping(new ContainerResourceMapping
{
    CpuCount = 1,
    Memory = 512 * 1024 * 1024 // 512MB
})
```

## Best Practices

1. **Always clean up containers** after tests
2. **Use unique tags** for test images to avoid conflicts
3. **Set explicit timeouts** to prevent hanging tests
4. **Log container output** for debugging failures
5. **Test both success and failure scenarios**

## Related Documentation

- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [Docker Networking](https://docs.docker.com/network/)
- [OGC Conformance Testing](./ogc-conformance-quickstart.md)
