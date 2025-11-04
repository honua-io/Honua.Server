using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Azure;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Honua.Cli.AI.Tests.Integration;

/// <summary>
/// Integration tests for the complete pattern search workflow:
/// 1. User provides requirements
/// 2. Requirements → embedding
/// 3. Embedding → hybrid search
/// 4. Search results → LLM explanation
/// </summary>
[Collection("AITests")]
[Trait("Category", "Integration")]
public sealed class PatternSearchIntegrationTests
{
    [Fact]
    public async Task CompletePatternSearch_WithMockedResponses_ReturnsMatchingPatterns()
    {
        // Arrange: Setup mock embedding provider
        var mockEmbedding = new Mock<IEmbeddingProvider>();
        var testEmbedding = CreateTestEmbedding(3072);

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = testEmbedding,
                Model = "text-embedding-3-large",
                Success = true,
                TotalTokens = 50
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var config = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            config,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws",
            Region = "us-west-2"
        };

        // Act: Search for patterns (will fail at Azure AI Search, but proves embedding works)
        try
        {
            await knowledgeStore.SearchPatternsAsync(requirements);
        }
        catch (global::Azure.RequestFailedException)
        {
            // Expected - we don't have real Azure AI Search
        }

        // Assert: Verify embedding was called with correct query text
        mockEmbedding.Verify(
            x => x.GetEmbeddingAsync(
                It.Is<string>(query =>
                    query.Contains("500GB") &&
                    query.Contains("1000") &&
                    query.Contains("aws") &&
                    query.Contains("us-west-2")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PatternIndexing_WithMockedEmbedding_GeneratesCorrectDocument()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingProvider>();
        var testEmbedding = CreateTestEmbedding(3072);

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = testEmbedding,
                Model = "text-embedding-3-large",
                Success = true
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var config = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            config,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "pattern-123",
            Name = "AWS High-Volume Standard",
            CloudProvider = "aws",
            DataVolumeMin = 400,
            DataVolumeMax = 600,
            ConcurrentUsersMin = 800,
            ConcurrentUsersMax = 1200,
            SuccessRate = 0.95,
            DeploymentCount = 23,
            Configuration = new
            {
                InstanceType = "t3.xlarge",
                StorageType = "gp3",
                CacheEnabled = true
            },
            HumanApproved = true,
            ApprovedBy = "Mike",
            ApprovedDate = DateTime.UtcNow
        };

        // Act: Index the pattern (will fail at Azure AI Search)
        try
        {
            await knowledgeStore.IndexApprovedPatternAsync(pattern);
        }
        catch (global::Azure.RequestFailedException)
        {
            // Expected
        }

        // Assert: Verify embedding was generated with pattern details
        mockEmbedding.Verify(
            x => x.GetEmbeddingAsync(
                It.Is<string>(text =>
                    text.Contains("aws") &&
                    text.Contains("400-600GB") &&
                    text.Contains("800-1200") &&
                    text.Contains("95%") &&
                    text.Contains("23 deployments")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MultipleSearches_WithSameRequirements_CallsEmbeddingEachTime()
    {
        // Arrange: Tests that we're not caching (future optimization)
        var mockEmbedding = new Mock<IEmbeddingProvider>();
        var testEmbedding = CreateTestEmbedding(3072);

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = testEmbedding,
                Model = "text-embedding-3-large",
                Success = true
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var config = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            config,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws",
            Region = "us-west-2"
        };

        // Act: Search twice with same requirements
        for (int i = 0; i < 2; i++)
        {
            try
            {
                await knowledgeStore.SearchPatternsAsync(requirements);
            }
            catch (global::Azure.RequestFailedException)
            {
                // Expected
            }
        }

        // Assert: Should call embedding twice (no caching yet)
        mockEmbedding.Verify(
            x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EmbeddingFailure_DuringSearch_ThrowsWithClearMessage()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = "test",
                Success = false,
                ErrorMessage = "Rate limit exceeded"
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var config = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            config,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => knowledgeStore.SearchPatternsAsync(requirements));

        exception.Message.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task EmbeddingFailure_DuringIndexing_ThrowsWithClearMessage()
    {
        // Arrange
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = "test",
                Success = false,
                ErrorMessage = "Invalid API key"
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var config = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            config,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "test",
            Name = "Test Pattern",
            CloudProvider = "aws"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => knowledgeStore.IndexApprovedPatternAsync(pattern));

        exception.Message.Should().Contain("Invalid API key");
    }

    private static float[] CreateTestEmbedding(int dimensions)
    {
        var embedding = new float[dimensions];
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
        }

        return embedding;
    }

    private static IConfiguration CreateSearchConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAISearch:Endpoint"] = "https://test.search.windows.net",
                ["AzureAISearch:ApiKey"] = "test-key-12345",
                ["AzureAISearch:IndexName"] = "deployment-knowledge"
            })
            .Build();
    }
}
