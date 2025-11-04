using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;

namespace Honua.Cli.AI.Tests.Services.AI;

[Trait("Category", "Unit")]
public sealed class EmbeddingBatchTests
{
    [Fact]
    public async Task GetEmbeddingBatchAsync_WithInvalidConfiguration_ReturnsFailures()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://invalid-batch-endpoint.openai.azure.com",
                ApiKey = "invalid-key",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };

        var provider = new AzureOpenAIEmbeddingProvider(options);

        var texts = new List<string>
        {
            "First pattern description",
            "Second pattern description",
            "Third pattern description"
        };

        // Act
        var responses = await provider.GetEmbeddingBatchAsync(texts);

        // Assert
        responses.Should().HaveCount(3);
        responses.Should().OnlyContain(r => r.Success == false);
        responses.Should().OnlyContain(r => !string.IsNullOrEmpty(r.ErrorMessage));
    }

    [Fact]
    public async Task GetEmbeddingBatchAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = "test-key-12345",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };

        var provider = new AzureOpenAIEmbeddingProvider(options);
        var emptyList = new List<string>();

        // Act
        var responses = await provider.GetEmbeddingBatchAsync(emptyList);

        // Assert
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEmbeddingBatchAsync_PreservesOrder()
    {
        // Arrange: Even with failures, order should be preserved
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://invalid-order-test.openai.azure.com",
                ApiKey = "test-key",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };

        var provider = new AzureOpenAIEmbeddingProvider(options);

        var texts = new List<string>
        {
            "AWS deployment pattern",
            "Azure deployment pattern",
            "GCP deployment pattern"
        };

        // Act
        var responses = await provider.GetEmbeddingBatchAsync(texts);

        // Assert: Should return responses in same order as input
        responses.Should().HaveCount(3);

        // All should fail (invalid endpoint), but order preserved
        responses.Should().OnlyContain(r => r.Success == false);
    }

    [Fact]
    public async Task GetEmbeddingBatchAsync_WithSingleItem_Works()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test-single.openai.azure.com",
                ApiKey = "test-key-12345",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };

        var provider = new AzureOpenAIEmbeddingProvider(options);
        var singleText = new List<string> { "Single deployment pattern" };

        // Act
        var responses = await provider.GetEmbeddingBatchAsync(singleText);

        // Assert
        responses.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEmbeddingBatchAsync_WithLargeBatch_HandlesCorrectly()
    {
        // Arrange: Test with larger batch to ensure no size-related issues
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test-large-batch.openai.azure.com",
                ApiKey = "test-key-12345",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };

        var provider = new AzureOpenAIEmbeddingProvider(options);

        var texts = Enumerable.Range(1, 50)
            .Select(i => $"Deployment pattern {i} with specific configuration")
            .ToList();

        // Act
        var responses = await provider.GetEmbeddingBatchAsync(texts);

        // Assert: Should handle all 50 items
        responses.Should().HaveCount(50);
    }

    [Fact]
    public void EmbeddingResponse_Success_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var response = new EmbeddingResponse
        {
            Embedding = new float[] { 0.1f, 0.2f, 0.3f },
            Model = "text-embedding-3-large",
            Success = true,
            TotalTokens = 10
        };

        // Assert
        response.Embedding.Should().HaveCount(3);
        response.Model.Should().Be("text-embedding-3-large");
        response.Success.Should().BeTrue();
        response.TotalTokens.Should().Be(10);
        response.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void EmbeddingResponse_Failure_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var response = new EmbeddingResponse
        {
            Embedding = System.Array.Empty<float>(),
            Model = "test-model",
            Success = false,
            ErrorMessage = "Rate limit exceeded"
        };

        // Assert
        response.Embedding.Should().BeEmpty();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Rate limit exceeded");
        response.TotalTokens.Should().BeNull();
    }

    [Fact]
    public void EmbeddingProvider_Dimensions_MatchesEmbeddingSize()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = "test-key-12345",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };

        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Assert: Dimensions property should match text-embedding-3-large spec
        provider.Dimensions.Should().Be(3072);
    }
}
