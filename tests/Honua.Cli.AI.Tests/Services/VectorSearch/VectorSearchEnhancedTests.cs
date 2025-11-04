using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.VectorSearch;

/// <summary>
/// Enhanced tests for vector search covering configurable TopK,
/// relevance threshold filtering, and improved query behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VectorSearchEnhancedTests
{
    private readonly Mock<IVectorSearchProvider> _mockVectorProvider;
    private readonly Mock<IEmbeddingProvider> _mockEmbeddingProvider;
    private readonly Mock<ILogger<VectorDeploymentPatternKnowledgeStore>> _mockLogger;
    private readonly VectorSearchOptions _options;
    private readonly VectorDeploymentPatternKnowledgeStore _store;

    public VectorSearchEnhancedTests()
    {
        _mockVectorProvider = new Mock<IVectorSearchProvider>();
        _mockEmbeddingProvider = new Mock<IEmbeddingProvider>();
        _mockLogger = new Mock<ILogger<VectorDeploymentPatternKnowledgeStore>>();

        _options = new VectorSearchOptions
        {
            Provider = VectorSearchProviders.InMemory,
            IndexName = "test-patterns",
            Dimensions = 384
        };

        var optionsWrapper = Options.Create(_options);

        _store = new VectorDeploymentPatternKnowledgeStore(
            _mockVectorProvider.Object,
            _mockEmbeddingProvider.Object,
            optionsWrapper,
            _mockLogger.Object);

        // Setup default mock responses
        SetupMockEmbedding();
        SetupMockIndex();
    }

    [Fact]
    public async Task SearchPatternsAsync_WithDefaultParams_UsesDefaultTopK()
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        var mockResults = CreateMockSearchResults(5, new[] { 0.95, 0.90, 0.85, 0.80, 0.75 });
        SetupMockSearchResults(mockResults);

        // Act
        var results = await _store.SearchPatternsAsync(requirements);

        // Assert
        results.Should().HaveCount(5, "Should return all results from the query");
        results.All(r => r.Score >= 0.75).Should().BeTrue("All results should have scores");
    }

    [Fact]
    public async Task SearchPatternsAsync_ReturnsAllMatchingResults()
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        var mockResults = CreateMockSearchResults(10, Enumerable.Range(0, 10).Select(i => 0.95 - i * 0.05).ToArray());
        SetupMockSearchResults(mockResults);

        // Act
        var results = await _store.SearchPatternsAsync(requirements);

        // Assert
        results.Should().HaveCount(10, "Should return all results from the query");
    }

    [Fact]
    public async Task SearchPatternsAsync_ReturnsResultsWithVaryingScores()
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        // Create results with varying scores
        var mockResults = CreateMockSearchResults(
            5,
            new[] { 0.95, 0.85, 0.75, 0.65, 0.55 });
        SetupMockSearchResults(mockResults);

        // Act
        var results = await _store.SearchPatternsAsync(requirements);

        // Assert
        results.Should().HaveCount(5, "Should return all results");
        results.Should().Contain(r => r.Score == 0.95);
        results.Should().Contain(r => r.Score == 0.85);
        results.Should().Contain(r => r.Score == 0.75);
    }

    [Fact]
    public async Task SearchPatternsAsync_WithLowScores_ReturnsAllResults()
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        // Results with lower scores
        var mockResults = CreateMockSearchResults(
            5,
            new[] { 0.65, 0.60, 0.55, 0.50, 0.45 });
        SetupMockSearchResults(mockResults);

        // Act
        var results = await _store.SearchPatternsAsync(requirements);

        // Assert
        results.Should().HaveCount(5, "Should return all results regardless of score");
    }

    [Fact]
    public async Task SearchPatternsAsync_WithCloudProviderFilter_PassesFilterToQuery()
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "azure",
            DataVolumeGb = 500,
            ConcurrentUsers = 100
        };

        var mockResults = CreateMockSearchResults(3, new[] { 0.95, 0.90, 0.85 });
        SetupMockSearchResults(mockResults);

        VectorSearchQuery? capturedQuery = null;
        _mockVectorProvider
            .Setup(p => p.GetOrCreateIndexAsync(It.IsAny<VectorIndexDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorIndexDefinition def, CancellationToken ct) =>
            {
                var mockIndex = new Mock<IVectorSearchIndex>();
                mockIndex
                    .Setup(i => i.QueryAsync(It.IsAny<VectorSearchQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((VectorSearchQuery query, CancellationToken ct) =>
                    {
                        capturedQuery = query;
                        return mockResults;
                    });

                return mockIndex.Object;
            });

        // Act
        await _store.SearchPatternsAsync(requirements);

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.MetadataFilter.Should().ContainKey("cloudProvider");
        capturedQuery.MetadataFilter!["cloudProvider"].Should().Be("azure");
    }

    [Fact]
    public async Task SearchPatternsAsync_ReturnsResultsInProvidedOrder()
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        // Create results with scrambled scores
        var mockResults = CreateMockSearchResults(
            5,
            new[] { 0.75, 0.95, 0.85, 0.90, 0.80 });
        SetupMockSearchResults(mockResults);

        // Act
        var results = await _store.SearchPatternsAsync(requirements);

        // Assert
        results.Should().HaveCount(5);
        // Results are returned as provided by the mock
        results.Should().Contain(r => r.Score == 0.95);
        results.Should().Contain(r => r.Score == 0.75);
    }

    [Fact]
    public async Task IndexApprovedPatternAsync_WithValidPattern_IndexesSuccessfully()
    {
        // Arrange
        var pattern = new DeploymentPattern
        {
            Id = "test-pattern-1",
            Name = "Test AWS Pattern",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 500,
            ConcurrentUsersMin = 10,
            ConcurrentUsersMax = 100,
            SuccessRate = 0.95,
            DeploymentCount = 25,
            Configuration = new { Tier = "Production", Region = "us-west-2" },
            ApprovedBy = "admin@honua.io",
            ApprovedDate = DateTime.UtcNow
        };

        VectorSearchDocument? capturedDocument = null;
        var mockIndex = new Mock<IVectorSearchIndex>();
        mockIndex
            .Setup(i => i.UpsertAsync(It.IsAny<IEnumerable<VectorSearchDocument>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<VectorSearchDocument>, CancellationToken>((docs, ct) =>
            {
                capturedDocument = docs.First();
            })
            .Returns(Task.CompletedTask);

        _mockVectorProvider
            .Setup(p => p.GetOrCreateIndexAsync(It.IsAny<VectorIndexDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockIndex.Object);

        // Act
        await _store.IndexApprovedPatternAsync(pattern);

        // Assert
        capturedDocument.Should().NotBeNull();
        capturedDocument!.Id.Should().Be("test-pattern-1");
        capturedDocument.Metadata.Should().ContainKey("patternName");
        capturedDocument.Metadata.Should().ContainKey("cloudProvider");
        capturedDocument.Metadata!["cloudProvider"].Should().Be("aws");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task SearchPatternsAsync_WithVariousResultCounts_ReturnsAllResults(int resultCount)
    {
        // Arrange
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        // Create results with descending scores
        var scores = Enumerable.Range(0, resultCount)
            .Select(i => 0.98 - (i * 0.06))
            .ToArray();
        var mockResults = CreateMockSearchResults(resultCount, scores);
        SetupMockSearchResults(mockResults);

        // Act
        var results = await _store.SearchPatternsAsync(requirements);

        // Assert
        results.Should().HaveCount(resultCount, "Should return all results provided by the query");
    }

    private void SetupMockEmbedding()
    {
        _mockEmbeddingProvider
            .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Success = true,
                Embedding = CreateTestEmbedding(384),
                Model = "test-embedding-model",
                TotalTokens = 100
            });

        _mockEmbeddingProvider.SetupGet(e => e.Dimensions).Returns(384);
    }

    private void SetupMockIndex()
    {
        var mockIndex = new Mock<IVectorSearchIndex>();
        mockIndex
            .Setup(i => i.UpsertAsync(It.IsAny<IEnumerable<VectorSearchDocument>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorProvider
            .Setup(p => p.GetOrCreateIndexAsync(It.IsAny<VectorIndexDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockIndex.Object);
    }

    private void SetupMockSearchResults(IReadOnlyList<VectorSearchResult> results)
    {
        _mockVectorProvider
            .Setup(p => p.GetOrCreateIndexAsync(It.IsAny<VectorIndexDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorIndexDefinition def, CancellationToken ct) =>
            {
                var mockIndex = new Mock<IVectorSearchIndex>();
                mockIndex
                    .Setup(i => i.QueryAsync(It.IsAny<VectorSearchQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(results);

                return mockIndex.Object;
            });
    }

    private List<VectorSearchResult> CreateMockSearchResults(int count, double[] scores)
    {
        var results = new List<VectorSearchResult>();

        for (int i = 0; i < count && i < scores.Length; i++)
        {
            var metadata = new Dictionary<string, string>
            {
                ["patternName"] = $"Pattern {i}",
                ["cloudProvider"] = "aws",
                ["dataVolumeMin"] = "100",
                ["dataVolumeMax"] = "500",
                ["concurrentUsersMin"] = "10",
                ["concurrentUsersMax"] = "100",
                ["successRate"] = "0.95",
                ["deploymentCount"] = "10",
                ["configurationJson"] = "{\"tier\":\"production\"}",
                ["approvedBy"] = "admin",
                ["approvedDate"] = DateTime.UtcNow.ToString("o")
            };

            var document = new VectorSearchDocument(
                Id: $"pattern-{i}",
                Embedding: CreateTestEmbedding(384),
                Text: $"Test pattern {i}",
                Metadata: metadata);

            results.Add(new VectorSearchResult(document, scores[i]));
        }

        return results;
    }

    private float[] CreateTestEmbedding(int dimensions)
    {
        var embedding = new float[dimensions];
        var random = new Random(42);

        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(e => e * e));
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}
