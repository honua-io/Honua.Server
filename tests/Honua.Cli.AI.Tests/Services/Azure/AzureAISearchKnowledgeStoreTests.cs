using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Azure;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Honua.Cli.AI.Tests.Services.Azure;

[Trait("Category", "Unit")]
public sealed class AzureAISearchKnowledgeStoreTests
{
    [Fact]
    public void Constructor_WithMissingEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAISearch:ApiKey"] = "test-key"
            })
            .Build();

        var mockEmbedding = new Mock<IEmbeddingProvider>();

        // Act
        var act = () => new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Endpoint*");
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAISearch:Endpoint"] = "https://test.search.windows.net"
            })
            .Build();

        var mockEmbedding = new Mock<IEmbeddingProvider>();

        // Act
        var act = () => new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ApiKey*");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_Succeeds()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        // Act
        var store = new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task IndexApprovedPatternAsync_WithEmbeddingFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = "test",
                Success = false,
                ErrorMessage = "Embedding failed"
            });

        var store = new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "test-123",
            Name = "Test Pattern",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 500,
            ConcurrentUsersMin = 10,
            ConcurrentUsersMax = 100,
            SuccessRate = 0.95,
            DeploymentCount = 15
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.IndexApprovedPatternAsync(pattern));
    }

    [Fact]
    public async Task SearchPatternsAsync_WithEmbeddingFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = "test",
                Success = false,
                ErrorMessage = "Query embedding failed"
            });

        var store = new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws",
            Region = "us-west-2"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SearchPatternsAsync(requirements));
    }

    [Fact]
    public async Task IndexApprovedPatternAsync_CallsEmbeddingProvider()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        var testEmbedding = new float[3072];
        for (int i = 0; i < 3072; i++)
        {
            testEmbedding[i] = i * 0.001f;
        }

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = testEmbedding,
                Model = "text-embedding-3-large",
                Success = true,
                TotalTokens = 100
            });

        var store = new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "test-123",
            Name = "AWS Standard Pattern",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 500,
            ConcurrentUsersMin = 10,
            ConcurrentUsersMax = 100,
            SuccessRate = 0.95,
            DeploymentCount = 15,
            Configuration = new { tier = "standard", region = "us-west-2" },
            ApprovedBy = "Mike",
            ApprovedDate = DateTime.UtcNow
        };

        // Act
        try
        {
            await store.IndexApprovedPatternAsync(pattern);
        }
        catch (global::Azure.RequestFailedException)
        {
            // Expected - we're mocking, not connecting to real Azure AI Search
            // The important part is that GetEmbeddingAsync was called
        }

        // Assert
        mockEmbedding.Verify(
            x => x.GetEmbeddingAsync(
                It.Is<string>(s => s.Contains("aws") && s.Contains("100-500GB")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchPatternsAsync_GeneratesCorrectQueryText()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        var testEmbedding = new float[3072];
        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = testEmbedding,
                Model = "text-embedding-3-large",
                Success = true
            });

        var store = new AzureAISearchKnowledgeStore(config, mockEmbedding.Object, NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws",
            Region = "us-west-2"
        };

        // Act
        try
        {
            await store.SearchPatternsAsync(requirements);
        }
        catch (global::Azure.RequestFailedException)
        {
            // Expected - we're mocking
        }

        // Assert
        mockEmbedding.Verify(
            x => x.GetEmbeddingAsync(
                It.Is<string>(s =>
                    s.Contains("500GB") &&
                    s.Contains("1000") &&
                    s.Contains("aws") &&
                    s.Contains("us-west-2")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IConfiguration CreateValidConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAISearch:Endpoint"] = "https://test.search.windows.net",
                ["AzureAISearch:ApiKey"] = "test-api-key-12345",
                ["AzureAISearch:IndexName"] = "deployment-knowledge"
            })
            .Build();
    }
}
