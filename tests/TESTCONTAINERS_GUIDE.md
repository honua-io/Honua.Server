# Testcontainers Integration Guide

This guide explains how the Honua project uses Testcontainers for integration testing with all dependencies.

## Overview

Testcontainers provides lightweight, throwaway instances of common databases, message brokers, and cloud services that can run in Docker containers. This ensures consistent test environments and eliminates the need for complex local setup.

## Supported Dependencies

The following dependencies are managed via Testcontainers:

1. **PostgreSQL** - Primary relational database with PostGIS extension
2. **MySQL** - Alternative relational database
3. **Microsoft SQL Server** - Enterprise database support
4. **Redis** - Caching and distributed locking
5. **MinIO** - S3-compatible object storage
6. **Azurite** - Azure Blob Storage emulator
7. **Qdrant** - Vector database for semantic search and embeddings
8. **Ollama** - Local LLM for AI/ML testing without API costs
9. **WireMock** - HTTP mock server for OIDC and external API testing

## Prerequisites

### Required

- Docker Desktop or Docker Engine installed and running
- .NET 9.0 SDK
- Sufficient Docker resources (minimum 4GB RAM recommended)

### Optional

- Docker Compose (for manual testing environments)

## Architecture

### Shared Fixtures

Testcontainers uses shared fixtures to reuse container instances across multiple test classes, improving performance:

```
StorageContainerFixture
├── MinIO Container (S3-compatible storage)
└── Azurite Container (Azure Blob Storage)

RedisContainerFixture
└── Redis Container (Caching/Locking)

MultiProviderTestFixture
├── PostgreSQL Container (with PostGIS)
├── MySQL Container
└── SQLite (in-memory, no container)

QdrantTestFixture
└── Qdrant Container (Vector database)

OllamaTestFixture
└── Ollama Container (Local LLM with phi3:mini model)
```

### Collection-Based Sharing

Test collections share fixture instances:

```csharp
[Collection("StorageContainers")]
public class MyStorageTests : IAsyncLifetime
{
    private readonly StorageContainerFixture _fixture;

    public MyStorageTests(StorageContainerFixture fixture)
    {
        _fixture = fixture;
    }
}
```

## Running Tests

### 1. With Docker Running (Recommended)

All integration tests will automatically start required containers:

```bash
# Run all tests (unit + integration)
dotnet test

# Run only integration tests
dotnet test --filter "Category=IntegrationTest|FullyQualifiedName~IntegrationTests"

# Run specific container tests
dotnet test --filter "Collection=StorageContainers"
dotnet test --filter "Collection=RedisContainer"
```

### 2. Without Docker (Unit Tests Only)

If Docker is not available, integration tests are automatically skipped:

```bash
dotnet test
# Output: Integration tests skipped (Docker not available)
```

### 3. Manual Container Management (Advanced)

For debugging or manual testing, you can use Docker Compose:

```bash
cd tests/Honua.Server.Core.Tests

# Start all emulators
docker-compose -f docker-compose.storage-emulators.yml up -d

# Run tests against running containers
dotnet test

# Stop emulators
docker-compose -f docker-compose.storage-emulators.yml down
```

## Container Lifecycle

### Startup Sequence

1. **Docker Availability Check**: Tests verify Docker is running
2. **Container Start**: Testcontainers pulls images and starts containers
3. **Health Checks**: Wait for services to be ready (port availability, health endpoints)
4. **Client Creation**: Initialize service clients (S3, Redis, etc.)
5. **Test Execution**: Run actual tests

### Shutdown Sequence

1. **Cleanup**: Remove test data (buckets, keys, tables)
2. **Disposal**: Close client connections
3. **Container Stop**: Testcontainers stops and removes containers
4. **Cleanup**: Remove volumes and networks

### Performance Optimization

- **Shared Fixtures**: Containers are reused across test classes in the same collection
- **Parallel Execution**: Different collections can run in parallel
- **Health Checks**: Fast startup with proper wait strategies
- **Resource Limits**: Containers use minimal resources

## Graceful Failure Handling

Tests fail gracefully when dependencies are unavailable:

### Docker Not Running

```csharp
public async Task InitializeAsync()
{
    if (!_fixture.IsDockerAvailable)
    {
        throw new SkipException("Docker is not available. Install Docker Desktop and ensure it's running.");
    }
}
```

**Output**: Test is skipped with helpful message, not failed.

### Container Startup Failure

```csharp
try
{
    await InitializeMinioAsync();
    MinioAvailable = true;
}
catch (Exception ex)
{
    Console.WriteLine($"MinIO container initialization failed: {ex.Message}");
    MinioAvailable = false;
}
```

**Output**: Specific container tests are skipped; other tests continue.

### Network Issues

Testcontainers automatically handles:
- Port conflicts (finds available ports)
- Network creation/cleanup
- Container name conflicts

## Writing New Integration Tests

### Pattern 1: Using Shared Fixture

```csharp
[Collection("StorageContainers")]
public class MyS3IntegrationTests : IAsyncLifetime
{
    private readonly StorageContainerFixture _fixture;
    private IAmazonS3? _s3Client;

    public MyS3IntegrationTests(StorageContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Check availability
        if (!_fixture.IsDockerAvailable || !_fixture.MinioAvailable)
        {
            throw new SkipException("Docker or MinIO not available");
        }

        // Use shared client
        _s3Client = _fixture.MinioClient;

        // Create test resources
        await _s3Client!.PutBucketAsync("my-test-bucket");
    }

    public async Task DisposeAsync()
    {
        // Clean up test resources
        if (_s3Client != null && _fixture.MinioAvailable)
        {
            await _s3Client.DeleteBucketAsync("my-test-bucket");
        }
    }

    [Fact]
    public async Task MyTest()
    {
        // Test implementation
    }
}
```

### Pattern 2: Dedicated Container

```csharp
public class MyDedicatedTests : IAsyncLifetime
{
    private MinioContainer? _minioContainer;
    private IAmazonS3? _s3Client;

    public async Task InitializeAsync()
    {
        // Start dedicated container
        _minioContainer = new MinioBuilder()
            .WithImage("minio/minio:latest")
            .Build();

        await _minioContainer.StartAsync();

        // Create client
        var config = new AmazonS3Config
        {
            ServiceURL = _minioContainer.GetConnectionString(),
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client("minioadmin", "minioadmin", config);
    }

    public async Task DisposeAsync()
    {
        _s3Client?.Dispose();
        if (_minioContainer != null)
        {
            await _minioContainer.DisposeAsync();
        }
    }
}
```

## Container Configuration

### MinIO (S3-Compatible)

```csharp
var container = new MinioBuilder()
    .WithImage("minio/minio:latest")
    .Build();

await container.StartAsync();

// Connection details
var endpoint = container.GetConnectionString();
var accessKey = "minioadmin";
var secretKey = "minioadmin";
```

### Azurite (Azure Storage)

```csharp
var container = new AzuriteBuilder()
    .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
    .Build();

await container.StartAsync();

// Connection string
var connectionString = container.GetConnectionString();
var client = new BlobServiceClient(connectionString);
```

### Redis

```csharp
var container = new RedisBuilder()
    .WithImage("redis:7-alpine")
    .Build();

await container.StartAsync();

// Connection
var connectionString = container.GetConnectionString();
var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
```

### PostgreSQL with PostGIS

```csharp
var container = new ContainerBuilder()
    .WithImage("postgis/postgis:16-3.4")
    .WithPortBinding(5432, true)
    .WithEnvironment("POSTGRES_USER", "test_user")
    .WithEnvironment("POSTGRES_PASSWORD", "test_password")
    .WithEnvironment("POSTGRES_DB", "test_db")
    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
    .Build();

await container.StartAsync();
```

### Qdrant (Vector Database)

```csharp
var container = new QdrantBuilder()
    .WithImage("qdrant/qdrant:v1.7.4")
    .Build();

await container.StartAsync();

// Connection details
var httpPort = container.GetMappedPublicPort(6333);
var grpcPort = container.GetMappedPublicPort(6334);
var endpoint = $"http://localhost:{httpPort}";

// Create collection
using var httpClient = new HttpClient();
var createRequest = new
{
    vectors = new
    {
        size = 384,  // Embedding dimensions
        distance = "Cosine"
    }
};

await httpClient.PutAsync($"{endpoint}/collections/my_collection",
    new StringContent(JsonSerializer.Serialize(createRequest)));
```

**Use Cases:**
- Testing semantic search without Azure AI Search costs
- Embedding and vector similarity testing
- Knowledge base and RAG (Retrieval-Augmented Generation) testing
- Replacing mocked vector database calls

**Performance Notes:**
- Startup: ~2-3 seconds
- Memory: ~100-200MB
- Suitable for small-to-medium datasets (< 100K vectors) in tests

### Ollama (Local LLM)

```csharp
var container = new ContainerBuilder()
    .WithImage("ollama/ollama:latest")
    .WithPortBinding(11434, true)
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilPortIsAvailable(11434))
    .Build();

await container.StartAsync();

var port = container.GetMappedPublicPort(11434);
var endpoint = $"http://localhost:{port}";

// Pull a small model (phi3:mini is ~2.3GB)
using var httpClient = new HttpClient();
var pullRequest = new
{
    name = "phi3:mini",
    stream = false
};

await httpClient.PostAsync($"{endpoint}/api/pull",
    new StringContent(JsonSerializer.Serialize(pullRequest)));

// Generate completion
var generateRequest = new
{
    model = "phi3:mini",
    prompt = "What is the capital of France?",
    stream = false
};

var response = await httpClient.PostAsync($"{endpoint}/api/generate",
    new StringContent(JsonSerializer.Serialize(generateRequest)));
```

**Use Cases:**
- LLM integration testing without API costs
- Replacing conditional `USE_REAL_LLM=true` tests
- Testing AI/ML workflows offline
- Consultant and planning system validation

**WARNING:**
- First run downloads model (~2.3GB for phi3:mini)
- Requires 4GB+ RAM allocated to Docker
- Model pull can take 5-10 minutes on first run
- Subsequent runs use cached model (instant startup)

**Recommended Models:**
- `phi3:mini` (2.3GB) - Best balance of size/quality for tests
- `tinyllama` (637MB) - Smallest, fastest, lower quality
- `llama3.2:1b` (1.3GB) - Good quality, reasonable size

### WireMock (HTTP Mock Server)

WireMock is used for mocking external APIs, particularly OIDC providers.

```csharp
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

// Start WireMock server
var server = WireMockServer.Start();

// Mock OIDC discovery document
server
    .Given(Request.Create()
        .WithPath("/.well-known/openid-configuration")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithHeader("Content-Type", "application/json")
        .WithBody(JsonSerializer.Serialize(new
        {
            issuer = server.Url,
            authorization_endpoint = $"{server.Url}/oauth2/authorize",
            token_endpoint = $"{server.Url}/oauth2/token",
            jwks_uri = $"{server.Url}/.well-known/jwks.json"
        })));

// Mock JWKS endpoint
server
    .Given(Request.Create()
        .WithPath("/.well-known/jwks.json")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBody(jwksJson));

// Cleanup
server.Stop();
server.Dispose();
```

**Use Cases:**
- OIDC provider testing without real identity servers
- JWT token validation testing
- External API integration testing
- Simulating API failures and edge cases

**Performance Notes:**
- In-process, no container needed
- Instant startup
- Zero network latency

## Troubleshooting

### Tests Are Skipped

**Problem**: All integration tests are skipped

**Solution**:
```bash
# Check Docker is running
docker ps

# On Linux, ensure current user is in docker group
sudo usermod -aG docker $USER
newgrp docker

# On Windows/Mac, restart Docker Desktop
```

### Container Startup Timeout

**Problem**: Tests hang during container startup

**Solution**:
```bash
# Increase Docker resources (RAM/CPU)
# Docker Desktop > Settings > Resources

# Pull images manually
docker pull minio/minio:latest
docker pull mcr.microsoft.com/azure-storage/azurite:latest
docker pull redis:7-alpine
docker pull postgis/postgis:16-3.4
```

### Port Conflicts

**Problem**: Container fails to start (port in use)

**Solution**: Testcontainers automatically finds available ports. If issues persist:

```bash
# Kill conflicting processes
lsof -ti:4566 | xargs kill -9  # MinIO
lsof -ti:10000 | xargs kill -9 # Azurite
lsof -ti:6379 | xargs kill -9  # Redis

# Or let Testcontainers use random ports (default behavior)
```

### Slow Test Execution

**Problem**: Integration tests are slow

**Optimization**:
- Use shared fixtures (already implemented)
- Run tests in parallel: `dotnet test --parallel`
- Keep Docker images cached locally
- Increase Docker resources

### Container Cleanup Issues

**Problem**: Containers not removed after tests

**Solution**:
```bash
# Manual cleanup
docker ps -a | grep testcontainers | awk '{print $1}' | xargs docker rm -f

# Clean up volumes
docker volume prune -f

# Clean up networks
docker network prune -f
```

### Ollama Model Pull Timeout

**Problem**: Ollama model pull times out or takes too long

**Solution**:
```bash
# Pre-pull the model manually
docker run -v ollama:/root/.ollama -p 11434:11434 ollama/ollama
# In another terminal:
docker exec -it <container_id> ollama pull phi3:mini

# Or increase Docker resources (Settings > Resources)
# - RAM: 4GB minimum, 8GB recommended
# - CPU: 2+ cores recommended

# Alternative: Use smaller model
# In OllamaTestFixture.cs, change ModelName to "tinyllama"
```

### Qdrant Collection Already Exists

**Problem**: Test fails because collection already exists from previous run

**Solution**:
```bash
# Delete the collection manually
curl -X DELETE http://localhost:6333/collections/deployment_patterns_test

# Or ensure DisposeAsync is called in test cleanup
# The fixture should handle this automatically
```

### WireMock Port Conflicts

**Problem**: WireMock fails to start due to port conflict

**Solution**:
```csharp
// WireMock automatically finds available port
var server = WireMockServer.Start(); // Uses random available port

// Or specify port range
var server = WireMockServer.Start(new WireMockServerSettings
{
    Port = 5000,
    UseSSL = false
});
```

### Vector Search Returns No Results

**Problem**: Qdrant search returns empty results

**Solution**:
- Ensure vectors are normalized (unit length)
- Verify embedding dimensions match collection config (e.g., 384)
- Check distance metric (Cosine, Euclidean, Dot)
- Wait for indexing to complete after upsert

```csharp
// Wait for point to be indexed
await Task.Delay(100); // Small delay after upsert
```

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

      - name: Run integration tests
        run: dotnet test --filter "Category=IntegrationTest"
        # Testcontainers automatically uses Docker in GitHub Actions

      - name: Publish test results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Integration Test Results
          path: '**/TestResults/*.trx'
          reporter: dotnet-trx
```

### GitLab CI

```yaml
integration-tests:
  image: mcr.microsoft.com/dotnet/sdk:9.0
  services:
    - docker:dind
  variables:
    DOCKER_HOST: tcp://docker:2375
    DOCKER_TLS_CERTDIR: ""
  script:
    - dotnet test --filter "Category=IntegrationTest"
```

### Azure Pipelines

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Integration Tests'
  inputs:
    command: 'test'
    arguments: '--filter "Category=IntegrationTest"'
  # Testcontainers works with Azure Pipelines Docker support
```

## Performance Benchmarks

Typical startup times (first run with image pull):

| Container | Pull Time | Start Time | Model Pull | Total     |
|-----------|-----------|------------|------------|-----------|
| MinIO     | ~5s       | ~2s        | N/A        | ~7s       |
| Azurite   | ~3s       | ~1s        | N/A        | ~4s       |
| Redis     | ~2s       | ~1s        | N/A        | ~3s       |
| PostgreSQL| ~8s       | ~3s        | N/A        | ~11s      |
| MySQL     | ~7s       | ~4s        | N/A        | ~11s      |
| Qdrant    | ~6s       | ~2s        | N/A        | ~8s       |
| Ollama    | ~10s      | ~3s        | 5-10 min   | 5-10 min* |
| WireMock  | N/A       | <1s        | N/A        | <1s       |

*First run only - model is cached after initial pull

Subsequent runs (images and models cached):

| Container  | Start Time |
|------------|------------|
| MinIO      | ~2s        |
| Azurite    | ~1s        |
| Redis      | ~1s        |
| PostgreSQL | ~3s        |
| MySQL      | ~4s        |
| Qdrant     | ~2s        |
| Ollama     | ~3s        |
| WireMock   | <1s        |

## Best Practices

### Do

✅ Use shared fixtures for common dependencies
✅ Implement graceful failure when Docker unavailable
✅ Clean up test data in DisposeAsync
✅ Use specific container versions (not 'latest' in production CI)
✅ Implement health checks and wait strategies
✅ Skip tests gracefully, don't fail

### Don't

❌ Don't start containers in test constructors
❌ Don't use hardcoded ports (let Testcontainers assign)
❌ Don't leave test data behind
❌ Don't run integration tests in parallel within same collection
❌ Don't assume Docker is always available

## Migration from Manual Emulators

If you previously used docker-compose emulators:

**Before** (Manual):
```bash
docker-compose up -d
dotnet test
docker-compose down
```

**After** (Testcontainers):
```bash
dotnet test  # Everything automatic!
```

Benefits:
- No manual setup required
- Consistent versions across team
- Automatic cleanup
- Parallel test execution
- CI/CD ready out of the box

## Migration from Mocks and Real APIs

### Tests Converted to Emulators

The following tests were converted from mocks or conditional real API calls to use emulators:

#### 1. Vector Search (Qdrant)

**Before:**
- `AzureAISearchKnowledgeStoreTests.cs` - Used mocks, couldn't test actual vector search
- Required Azure AI Search credentials for integration tests
- No actual vector similarity testing

**After:**
- `QdrantKnowledgeStoreIntegrationTests.cs` - Uses real Qdrant container
- Tests actual vector indexing, search, and filtering
- No credentials or costs required
- Full vector database operations tested

**Benefits:**
- Real vector similarity calculations
- Actual search ranking and filtering
- No Azure costs (~$0.10/hour for Azure AI Search)
- Tests work offline

#### 2. LLM Testing (Ollama)

**Before:**
- `RealLlmConsultantIntegrationTests.cs` - Required `USE_REAL_LLM=true` flag
- Used real OpenAI/Anthropic APIs with API keys
- Tests incurred costs ($0.002-0.02 per test)
- Could only run with valid API keys

**After:**
- `OllamaConsultantIntegrationTests.cs` - Uses local Ollama container
- `RealLlmConsultantIntegrationTests.cs` - Now has API cost warnings
- No API keys required
- Zero cost per test
- Tests work offline

**Benefits:**
- No API costs (saves ~$0.50-2.00 per full test run)
- Faster iteration (no network latency)
- Deterministic results (same model version)
- Works in air-gapped environments

#### 3. OIDC Authentication (WireMock)

**Before:**
- `OidcDiscoveryHealthCheckIntegrationTests.cs` - Only tested health check
- No full token validation flow testing
- Relied on external OIDC providers or manual setup

**After:**
- `OidcIntegrationTests.cs` - Full OIDC flow with WireMock
- Tests token validation, claim mapping, role assignment
- Simulates various token scenarios (expired, invalid signature, etc.)
- No external dependencies

**Benefits:**
- Complete OIDC flow testing
- Edge case simulation (expired tokens, invalid signatures)
- No dependency on external identity providers
- Instant test execution

### API Cost Savings

Estimated cost savings per test run with emulators:

| Service          | Before (Real APIs) | After (Emulators) | Savings per Run |
|------------------|-------------------|-------------------|-----------------|
| Azure AI Search  | $0.001-0.005      | $0                | ~$0.003         |
| OpenAI API       | $0.20-2.00        | $0                | ~$1.00          |
| Anthropic API    | $0.10-1.00        | $0                | ~$0.50          |
| OIDC Provider    | Free-$0.01        | $0                | ~$0.005         |
| **Total**        | **$0.31-3.02**    | **$0**            | **~$1.50**      |

For a typical development cycle with 50 test runs per day:
- **Daily savings:** ~$75
- **Monthly savings:** ~$1,500-2,250
- **Annual savings:** ~$18,000-27,000

### Quality Improvements

Beyond cost savings, emulators provide:

1. **Better Test Coverage:**
   - Real vector search operations (not mocked)
   - Actual LLM responses (not hardcoded)
   - Full OIDC token validation flow

2. **Faster Feedback:**
   - Ollama: ~5-10s per test vs 30-60s with real APIs
   - Qdrant: Local, no network latency
   - WireMock: In-process, instant responses

3. **Reliability:**
   - No API rate limits
   - No network failures
   - Consistent behavior across environments

4. **Security:**
   - No API keys in CI/CD
   - No PII sent to external services
   - Tests work in air-gapped environments

## Additional Resources

- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [Testcontainers GitHub](https://github.com/testcontainers/testcontainers-dotnet)
- [Docker Documentation](https://docs.docker.com/)
- [Xunit Collection Fixtures](https://xunit.net/docs/shared-context)
- [Qdrant Documentation](https://qdrant.tech/documentation/)
- [Ollama Documentation](https://github.com/ollama/ollama)
- [WireMock.Net Documentation](https://github.com/WireMock-Net/WireMock.Net)

## Support

For issues:
1. Check Docker is running: `docker ps`
2. Check container logs: `docker logs <container_id>`
3. Verify image availability: `docker pull <image_name>`
4. Review test output for specific errors
5. Check GitHub Issues for known problems
6. For Ollama: Verify sufficient Docker resources (4GB+ RAM)
