using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.AI.Tests.Fixtures;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.VectorSearch;

/// <summary>
/// Integration tests for vector knowledge store using real Qdrant container.
/// These tests replace the mocked Azure AI Search tests with actual vector database operations.
/// </summary>
[Collection("QdrantContainer")]
[Trait("Category", "Integration")]
public sealed class QdrantKnowledgeStoreIntegrationTests : IAsyncLifetime
{
    private readonly QdrantTestFixture _fixture;
    private HttpClient? _httpClient;
    private const string CollectionName = "deployment_patterns_test";
    private readonly Dictionary<string, string> _patternIdToGuidMap = new(); // Maps pattern.Id to GUID

    public QdrantKnowledgeStoreIntegrationTests(QdrantTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsDockerAvailable || !_fixture.QdrantAvailable)
        {
            return;
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_fixture.Endpoint!)
        };

        // Create test collection
        await CreateCollectionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_httpClient != null && _fixture.QdrantAvailable)
        {
            // Clean up test collection
            await DeleteCollectionAsync();
            _httpClient.Dispose();
        }
    }

    [Fact]
    public async Task IndexPattern_WithValidEmbedding_StoresVectorSuccessfully()
    {
        if (!_fixture.QdrantAvailable)
        {
            return; // Skip test if Qdrant not available
        }

        // Arrange
        var mockEmbedding = CreateMockEmbeddingProvider();
        var pattern = CreateTestPattern("aws-standard-1");

        // Act - Index the pattern
        await IndexPatternAsync(pattern, mockEmbedding);

        // Assert - Verify the pattern was stored
        var points = await GetPointsAsync();
        points.Should().HaveCountGreaterThan(0, "Pattern should be stored in Qdrant");

        var storedPoint = points.First();
        storedPoint.Should().ContainKey("id");
        storedPoint.Should().ContainKey("vector");
        storedPoint.Should().ContainKey("payload");
    }

    [Fact]
    public async Task SearchPatterns_WithSimilarRequirements_ReturnsRelevantResults()
    {
        if (!_fixture.QdrantAvailable)
        {
            return; // Skip test if Qdrant not available
        }

        // Arrange
        var mockEmbedding = CreateMockEmbeddingProvider();

        // Index multiple patterns with different characteristics
        var awsPattern = CreateTestPattern("aws-prod-1", "aws", 100, 500, 10, 100);
        var azurePattern = CreateTestPattern("azure-dev-1", "azure", 10, 50, 1, 10);
        var gcpPattern = CreateTestPattern("gcp-prod-1", "gcp", 200, 1000, 50, 200);

        await IndexPatternAsync(awsPattern, mockEmbedding);
        await IndexPatternAsync(azurePattern, mockEmbedding);
        await IndexPatternAsync(gcpPattern, mockEmbedding);

        // Act - Search for production-scale AWS deployment
        var searchVector = CreateEmbeddingVector(123); // Unique seed for search query
        var results = await SearchAsync(searchVector, limit: 3);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCountGreaterThan(0, "Search should return results");

        // Verify results contain our indexed patterns by checking payload.patternId
        var resultPatternIds = results
            .Select(r => r.ContainsKey("payload") && r["payload"] is JsonElement payload && payload.TryGetProperty("patternId", out var patternId)
                ? patternId.GetString()
                : null)
            .Where(id => id != null)
            .ToList();
        resultPatternIds.Should().Contain(new[] { "aws-prod-1", "azure-dev-1", "gcp-prod-1" });
    }

    [Fact]
    public async Task IndexPattern_WithMultiplePatterns_AllStoredSuccessfully()
    {
        if (!_fixture.QdrantAvailable)
        {
            return; // Skip test if Qdrant not available
        }

        // Arrange
        var mockEmbedding = CreateMockEmbeddingProvider();
        var patterns = new[]
        {
            CreateTestPattern("pattern-1", "aws"),
            CreateTestPattern("pattern-2", "azure"),
            CreateTestPattern("pattern-3", "gcp"),
            CreateTestPattern("pattern-4", "aws"),
            CreateTestPattern("pattern-5", "azure")
        };

        // Act - Index all patterns
        foreach (var pattern in patterns)
        {
            await IndexPatternAsync(pattern, mockEmbedding);
        }

        // Assert
        var points = await GetPointsAsync();
        points.Should().HaveCount(5, "All patterns should be stored");
    }

    [Fact]
    public async Task SearchPatterns_WithFilters_ReturnsFilteredResults()
    {
        if (!_fixture.QdrantAvailable)
        {
            return; // Skip test if Qdrant not available
        }

        // Arrange
        var mockEmbedding = CreateMockEmbeddingProvider();

        await IndexPatternAsync(CreateTestPattern("aws-1", "aws", 100, 500), mockEmbedding);
        await IndexPatternAsync(CreateTestPattern("aws-2", "aws", 600, 1000), mockEmbedding);
        await IndexPatternAsync(CreateTestPattern("azure-1", "azure", 100, 500), mockEmbedding);

        // Act - Search with cloud provider filter
        var searchVector = CreateEmbeddingVector(456);
        var filter = new
        {
            must = new[]
            {
                new
                {
                    key = "cloudProvider",
                    match = new { value = "aws" }
                }
            }
        };

        var results = await SearchAsync(searchVector, filter: filter, limit: 10);

        // Assert
        results.Should().NotBeNull();

        // All results should be AWS patterns
        foreach (var result in results)
        {
            var payload = result["payload"] as JsonElement?;
            if (payload.HasValue && payload.Value.TryGetProperty("cloudProvider", out var cloudProvider))
            {
                cloudProvider.GetString().Should().Be("aws");
            }
        }
    }

    [Fact]
    public async Task DeletePattern_RemovesFromIndex()
    {
        if (!_fixture.QdrantAvailable)
        {
            return; // Skip test if Qdrant not available
        }

        // Arrange
        var mockEmbedding = CreateMockEmbeddingProvider();
        var pattern = CreateTestPattern("to-delete-1");
        await IndexPatternAsync(pattern, mockEmbedding);

        var pointsBefore = await GetPointsAsync();
        var countBefore = pointsBefore.Count;

        // Act - Delete the pattern
        await DeletePointAsync("to-delete-1");

        // Assert
        var pointsAfter = await GetPointsAsync();
        pointsAfter.Should().HaveCount(countBefore - 1, "Pattern should be removed");

        var ids = pointsAfter.Select(p => p["id"]?.ToString()).ToList();
        ids.Should().NotContain("to-delete-1");
    }

    #region Helper Methods

    private IEmbeddingProvider CreateMockEmbeddingProvider()
    {
        var mock = new Mock<IEmbeddingProvider>();

        mock.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                // Generate deterministic embedding based on text hash
                var seed = text.GetHashCode();
                var embedding = CreateEmbeddingVector(seed);

                return new EmbeddingResponse
                {
                    Embedding = embedding,
                    Model = "text-embedding-3-small",
                    Success = true,
                    TotalTokens = 100
                };
            });

        return mock.Object;
    }

    private static float[] CreateEmbeddingVector(int seed)
    {
        // Create a 384-dimensional vector (common size for small embedding models)
        var random = new Random(seed);
        var vector = new float[384];

        for (int i = 0; i < 384; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
        }

        // Normalize the vector
        var magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }

        return vector;
    }

    private static DeploymentPattern CreateTestPattern(
        string id,
        string cloudProvider = "aws",
        int dataVolumeMin = 100,
        int dataVolumeMax = 500,
        int concurrentUsersMin = 10,
        int concurrentUsersMax = 100)
    {
        return new DeploymentPattern
        {
            Id = id,
            Name = $"Test Pattern {id}",
            CloudProvider = cloudProvider,
            DataVolumeMin = dataVolumeMin,
            DataVolumeMax = dataVolumeMax,
            ConcurrentUsersMin = concurrentUsersMin,
            ConcurrentUsersMax = concurrentUsersMax,
            SuccessRate = 0.95,
            DeploymentCount = 15,
            Configuration = new { tier = "standard", region = "us-west-2" },
            ApprovedBy = "TestUser",
            ApprovedDate = DateTime.UtcNow
        };
    }

    private async Task CreateCollectionAsync()
    {
        // Check if collection already exists
        var checkResponse = await _httpClient!.GetAsync($"/collections/{CollectionName}");
        if (checkResponse.IsSuccessStatusCode)
        {
            // Collection already exists, skip creation
            return;
        }

        var createRequest = new
        {
            vectors = new
            {
                size = 384,
                distance = "Cosine"
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient!.PutAsync($"/collections/{CollectionName}", content);

        // OK if already exists or created successfully
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create Qdrant collection: {response.StatusCode} - {errorContent}");
        }
    }

    private async Task DeleteCollectionAsync()
    {
        try
        {
            await _httpClient!.DeleteAsync($"/collections/{CollectionName}");
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    private async Task IndexPatternAsync(DeploymentPattern pattern, IEmbeddingProvider embeddingProvider)
    {
        // Generate embedding for the pattern
        var patternText = $"{pattern.CloudProvider} deployment with {pattern.DataVolumeMin}-{pattern.DataVolumeMax}GB data, " +
                         $"{pattern.ConcurrentUsersMin}-{pattern.ConcurrentUsersMax} concurrent users";

        var embeddingResponse = await embeddingProvider.GetEmbeddingAsync(patternText, CancellationToken.None);

        if (!embeddingResponse.Success)
        {
            throw new InvalidOperationException("Failed to generate embedding");
        }

        // Upsert point to Qdrant
        // Qdrant requires either numeric IDs or UUID format for string IDs
        // Convert string ID to Guid for compatibility
        var idGuid = Guid.TryParse(pattern.Id, out var guid) ? guid : Guid.NewGuid();
        _patternIdToGuidMap[pattern.Id] = idGuid.ToString(); // Track the mapping for deletion

        var upsertRequest = new
        {
            points = new[]
            {
                new
                {
                    id = idGuid.ToString(),
                    vector = embeddingResponse.Embedding,
                    payload = new
                    {
                        patternId = pattern.Id, // Store original ID in payload
                        name = pattern.Name,
                        cloudProvider = pattern.CloudProvider,
                        dataVolumeMin = pattern.DataVolumeMin,
                        dataVolumeMax = pattern.DataVolumeMax,
                        concurrentUsersMin = pattern.ConcurrentUsersMin,
                        concurrentUsersMax = pattern.ConcurrentUsersMax,
                        successRate = pattern.SuccessRate,
                        deploymentCount = pattern.DeploymentCount,
                        approvedBy = pattern.ApprovedBy,
                        approvedDate = pattern.ApprovedDate
                    }
                }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(upsertRequest),
            Encoding.UTF8,
            "application/json");

        // Qdrant API uses PUT for upsert operations
        var response = await _httpClient!.PutAsync($"/collections/{CollectionName}/points", content);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to upsert points to Qdrant. Status: {response.StatusCode}, Error: {errorContent}");
        }
    }

    private async Task<List<Dictionary<string, object>>> SearchAsync(
        float[] vector,
        object? filter = null,
        int limit = 10)
    {
        var searchRequest = new Dictionary<string, object>
        {
            ["vector"] = vector,
            ["limit"] = limit,
            ["with_payload"] = true
        };

        if (filter != null)
        {
            searchRequest["filter"] = filter;
        }

        var content = new StringContent(
            JsonSerializer.Serialize(searchRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient!.PostAsync($"/collections/{CollectionName}/points/search", content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseBody);

        var results = new List<Dictionary<string, object>>();
        if (jsonDoc.RootElement.TryGetProperty("result", out var resultArray))
        {
            foreach (var item in resultArray.EnumerateArray())
            {
                var result = new Dictionary<string, object>();

                if (item.TryGetProperty("id", out var id))
                {
                    result["id"] = id.GetString() ?? string.Empty;
                }

                if (item.TryGetProperty("score", out var score))
                {
                    result["score"] = score.GetDouble();
                }

                if (item.TryGetProperty("payload", out var payload))
                {
                    result["payload"] = payload;
                }

                results.Add(result);
            }
        }

        return results;
    }

    private async Task<List<Dictionary<string, object>>> GetPointsAsync()
    {
        var scrollRequest = new
        {
            limit = 100,
            with_payload = true,
            with_vector = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(scrollRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient!.PostAsync($"/collections/{CollectionName}/points/scroll", content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseBody);

        var points = new List<Dictionary<string, object>>();
        if (jsonDoc.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("points", out var pointsArray))
        {
            foreach (var item in pointsArray.EnumerateArray())
            {
                var point = new Dictionary<string, object>();

                if (item.TryGetProperty("id", out var id))
                {
                    point["id"] = id.GetString() ?? string.Empty;
                }

                if (item.TryGetProperty("vector", out var vector))
                {
                    point["vector"] = vector;
                }

                if (item.TryGetProperty("payload", out var payload))
                {
                    point["payload"] = payload;
                }

                points.Add(point);
            }
        }

        return points;
    }

    private async Task DeletePointAsync(string patternId)
    {
        // Look up the GUID that corresponds to this pattern ID
        if (!_patternIdToGuidMap.TryGetValue(patternId, out var guidId))
        {
            throw new InvalidOperationException($"Pattern ID '{patternId}' not found in mapping");
        }

        var deleteRequest = new
        {
            points = new[] { guidId }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(deleteRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient!.PostAsync($"/collections/{CollectionName}/points/delete", content);
        response.EnsureSuccessStatusCode();
    }

    #endregion
}
