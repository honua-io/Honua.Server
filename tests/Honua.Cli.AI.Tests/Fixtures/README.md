# Test Fixtures for Honua.Cli.AI.Tests

This directory contains shared test fixtures for integration testing with cloud emulators.

## Available Fixtures

### QdrantTestFixture

**Purpose:** Provides a real Qdrant vector database instance for testing semantic search and embeddings.

**Replaces:** Azure AI Search mocks in `AzureAISearchKnowledgeStoreTests.cs`

**Usage:**
```csharp
[Collection("QdrantContainer")]
public class MyVectorSearchTests
{
    private readonly QdrantTestFixture _fixture;

    public MyVectorSearchTests(QdrantTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestVectorSearch()
    {
        if (!_fixture.QdrantAvailable)
        {
            return; // Skip if Qdrant not available
        }

        // Use _fixture.Endpoint for HTTP API
        // Use _fixture.GrpcPort for gRPC API
    }
}
```

**Benefits:**
- Tests actual vector similarity calculations
- Real search ranking and filtering
- No Azure AI Search costs (~$0.10/hour)
- Works offline

**Requirements:**
- Docker running
- ~200MB RAM for container
- Startup time: ~2-3 seconds

## Integration Tests Using Fixtures

### Vector Search Tests

**File:** `Services/VectorSearch/QdrantKnowledgeStoreIntegrationTests.cs`

**What it tests:**
- Vector indexing and storage
- Semantic search with cosine similarity
- Filtering by metadata (cloud provider, data volume, etc.)
- Point deletion and updates
- Collection management

**Example test:**
```csharp
[Fact]
public async Task SearchPatterns_WithSimilarRequirements_ReturnsRelevantResults()
{
    // Index multiple deployment patterns
    await IndexPatternAsync(awsPattern, mockEmbedding);
    await IndexPatternAsync(azurePattern, mockEmbedding);
    await IndexPatternAsync(gcpPattern, mockEmbedding);

    // Search for similar patterns
    var searchVector = CreateEmbeddingVector(123);
    var results = await SearchAsync(searchVector, limit: 3);

    // Verify results contain our patterns
    results.Should().HaveCountGreaterThan(0);
}
```

## Running Tests

### Run all vector search tests:
```bash
dotnet test --filter "Collection=QdrantContainer"
```

### Run specific test:
```bash
dotnet test --filter "FullyQualifiedName~QdrantKnowledgeStoreIntegrationTests"
```

### Skip if Docker unavailable:
Tests automatically skip if Docker is not running. No manual configuration needed.

## Troubleshooting

### Qdrant container fails to start
1. Ensure Docker is running: `docker ps`
2. Check port availability: `lsof -i:6333`
3. Verify image: `docker pull qdrant/qdrant:v1.7.4`

### Collection already exists error
The fixture automatically cleans up collections in `DisposeAsync()`. If tests fail:
```bash
curl -X DELETE http://localhost:6333/collections/deployment_patterns_test
```

### Search returns no results
- Ensure vectors are normalized (unit length)
- Verify embedding dimensions match (384 for text-embedding-3-small)
- Wait for indexing: `await Task.Delay(100);` after upsert

## Performance

**First run (image pull):**
- Image download: ~6 seconds
- Container start: ~2 seconds
- Total: ~8 seconds

**Subsequent runs (cached):**
- Container start: ~2 seconds

**Memory usage:** ~100-200MB per container

**Best for:** Small to medium test datasets (< 100K vectors)

## See Also

- [TESTCONTAINERS_GUIDE.md](../../TESTCONTAINERS_GUIDE.md) - Complete guide to all emulators
- [Qdrant Documentation](https://qdrant.tech/documentation/)
- [Testcontainers .NET](https://dotnet.testcontainers.org/)
