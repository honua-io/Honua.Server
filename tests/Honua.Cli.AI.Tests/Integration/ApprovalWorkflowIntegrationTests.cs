using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Azure;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Honua.Cli.AI.Tests.Integration;

/// <summary>
/// Integration tests for the complete approval workflow:
/// 1. Pattern recommendation created (SQL analysis)
/// 2. Human reviews and approves
/// 3. Pattern indexed in Azure AI Search
/// 4. Available for future searches
/// </summary>
[Collection("AITests")]
[Trait("Category", "Integration")]
public sealed class ApprovalWorkflowIntegrationTests
{
    [Fact]
    public void ApprovalWorkflow_WithAllComponents_InitializesCorrectly()
    {
        // Arrange: Create all components needed for approval workflow
        var mockEmbedding = CreateMockEmbeddingProvider();
        var searchConfig = CreateSearchConfiguration();
        var approvalConfig = CreateApprovalConfiguration();

        // Create knowledge store
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            searchConfig,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        // Create approval service
        var approvalService = new PatternApprovalService(
            approvalConfig,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Assert: All components initialized successfully
        knowledgeStore.Should().NotBeNull();
        approvalService.Should().NotBeNull();
    }

    [Fact]
    public async Task ApprovalService_IndexingFailure_DoesNotCallDatabase()
    {
        // Arrange: Mock embedding provider that fails
        var mockEmbedding = new Mock<IEmbeddingProvider>();

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = "test",
                Success = false,
                ErrorMessage = "Embedding service unavailable"
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var searchConfig = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            searchConfig,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "test-pattern",
            Name = "Test Pattern",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 500,
            ConcurrentUsersMin = 50,
            ConcurrentUsersMax = 200,
            SuccessRate = 0.9,
            DeploymentCount = 10
        };

        // Act & Assert: Indexing should fail early with embedding error
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => knowledgeStore.IndexApprovedPatternAsync(pattern));

        exception.Message.Should().Contain("Embedding service unavailable");

        // Verify embedding was attempted but failed before reaching Azure AI Search
        mockEmbedding.Verify(
            x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApprovalService_MissingConnectionString_FailsFast()
    {
        // Arrange: Configuration without database connection string
        var mockEmbedding = CreateMockEmbeddingProvider();
        var searchConfig = CreateSearchConfiguration();
        var invalidConfig = new ConfigurationBuilder().Build(); // No connection string

        var knowledgeStore = new AzureAISearchKnowledgeStore(
            searchConfig,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var approvalService = new PatternApprovalService(
            invalidConfig,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Act & Assert: Should fail immediately when trying to access database
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.GetPendingRecommendationsAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.ApprovePatternAsync(Guid.NewGuid(), "TestUser", "Test"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.RejectPatternAsync(Guid.NewGuid(), "TestUser", "Test"));
    }

    [Fact]
    public async Task PatternIndexing_SuccessfulEmbedding_GeneratesExpectedText()
    {
        // Arrange: Track what text is sent for embedding
        var mockEmbedding = new Mock<IEmbeddingProvider>();
        string? capturedText = null;
        var testEmbedding = CreateTestEmbedding(3072);

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                capturedText = text;
                return new EmbeddingResponse
                {
                    Embedding = testEmbedding,
                    Model = "text-embedding-3-large",
                    Success = true,
                    TotalTokens = 100
                };
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var searchConfig = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            searchConfig,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "aws-standard-v1",
            Name = "AWS Standard Deployment",
            CloudProvider = "aws",
            DataVolumeMin = 100,
            DataVolumeMax = 500,
            ConcurrentUsersMin = 50,
            ConcurrentUsersMax = 200,
            SuccessRate = 0.92,
            DeploymentCount = 15,
            Configuration = new { tier = "standard", region = "us-west-2" }
        };

        // Act: Index the pattern
        try
        {
            await knowledgeStore.IndexApprovedPatternAsync(pattern);
        }
        catch (global::Azure.RequestFailedException)
        {
            // Expected - no real Azure AI Search
        }

        // Assert: Verify the embedding text contains all key details
        capturedText.Should().NotBeNull();
        capturedText.Should().Contain("aws");
        capturedText.Should().Contain("100-500GB");
        capturedText.Should().Contain("50-200");
        capturedText.Should().Contain("92%");
        capturedText.Should().Contain("15 deployments");
        capturedText.Should().Contain("tier");
        capturedText.Should().Contain("standard");
    }

    [Fact]
    public async Task SearchQuery_GeneratesRichSemanticText()
    {
        // Arrange: Capture query text sent for embedding
        var mockEmbedding = new Mock<IEmbeddingProvider>();
        string? capturedQuery = null;
        var testEmbedding = CreateTestEmbedding(3072);

        mockEmbedding
            .Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                capturedQuery = text;
                return new EmbeddingResponse
                {
                    Embedding = testEmbedding,
                    Model = "text-embedding-3-large",
                    Success = true
                };
            });

        mockEmbedding.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mockEmbedding.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mockEmbedding.Setup(x => x.Dimensions).Returns(3072);

        var searchConfig = CreateSearchConfiguration();
        var knowledgeStore = new AzureAISearchKnowledgeStore(
            searchConfig,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 750,
            ConcurrentUsers = 2500,
            CloudProvider = "azure",
            Region = "eastus"
        };

        // Act: Search for patterns
        try
        {
            await knowledgeStore.SearchPatternsAsync(requirements);
        }
        catch (global::Azure.RequestFailedException)
        {
            // Expected
        }

        // Assert: Query text should be semantic and complete
        capturedQuery.Should().NotBeNull();
        capturedQuery.Should().Contain("750GB");
        capturedQuery.Should().Contain("2500");
        capturedQuery.Should().Contain("azure");
        capturedQuery.Should().Contain("eastus");
    }

    [Fact]
    public void MultipleApprovalServices_CanShareKnowledgeStore()
    {
        // Arrange: Test that knowledge store can be shared (dependency injection scenario)
        var mockEmbedding = CreateMockEmbeddingProvider();
        var searchConfig = CreateSearchConfiguration();
        var approvalConfig = CreateApprovalConfiguration();

        var sharedKnowledgeStore = new AzureAISearchKnowledgeStore(
            searchConfig,
            mockEmbedding.Object,
            NullLogger<AzureAISearchKnowledgeStore>.Instance);

        // Act: Create multiple approval services using same knowledge store
        var approvalService1 = new PatternApprovalService(
            approvalConfig,
            sharedKnowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        var approvalService2 = new PatternApprovalService(
            approvalConfig,
            sharedKnowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Assert: Both services initialized successfully
        approvalService1.Should().NotBeNull();
        approvalService2.Should().NotBeNull();
    }

    private static Mock<IEmbeddingProvider> CreateMockEmbeddingProvider()
    {
        var mock = new Mock<IEmbeddingProvider>();
        var testEmbedding = CreateTestEmbedding(3072);

        mock.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse
            {
                Embedding = testEmbedding,
                Model = "text-embedding-3-large",
                Success = true
            });

        mock.Setup(x => x.ProviderName).Returns("AzureOpenAI");
        mock.Setup(x => x.DefaultModel).Returns("text-embedding-3-large");
        mock.Setup(x => x.Dimensions).Returns(3072);

        return mock;
    }

    private static float[] CreateTestEmbedding(int dimensions)
    {
        var embedding = new float[dimensions];
        var random = new Random(42);

        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
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

    private static IConfiguration CreateApprovalConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=honua;Username=test;Password=test"
            })
            .Build();
    }
}
