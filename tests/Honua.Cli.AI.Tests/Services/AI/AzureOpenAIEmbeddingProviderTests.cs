using Xunit;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;

namespace Honua.Cli.AI.Tests.Services.AI;

[Trait("Category", "Unit")]
public sealed class AzureOpenAIEmbeddingProviderTests
{
    [Fact]
    public void Constructor_WithMissingEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = null,
                ApiKey = "test-key",
                EmbeddingDeploymentName = "test-deployment"
            }
        };

        // Act
        var act = () => new AzureOpenAIEmbeddingProvider(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint*");
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = null,
                EmbeddingDeploymentName = "test-deployment"
            }
        };

        // Act
        var act = () => new AzureOpenAIEmbeddingProvider(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key*");
    }

    [Fact]
    public void Constructor_WithMissingEmbeddingDeploymentName_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = "test-key",
                EmbeddingDeploymentName = null
            }
        };

        // Act
        var act = () => new AzureOpenAIEmbeddingProvider(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*embedding deployment name*");
    }

    [Fact]
    public void ProviderName_ReturnsAzureOpenAI()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Assert
        provider.ProviderName.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void DefaultModel_ReturnsConfiguredModel()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.DefaultEmbeddingModel = "text-embedding-3-large";

        // Act
        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Assert
        provider.DefaultModel.Should().Be("text-embedding-3-large");
    }

    [Fact]
    public void Dimensions_Returns3072()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Assert
        provider.Dimensions.Should().Be(3072);
    }

    [Fact]
    public async Task IsAvailableAsync_WithInvalidEndpoint_ReturnsFalse()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.EndpointUrl = "https://invalid-endpoint-embeddings.openai.azure.com";

        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WithInvalidApiKey_ReturnsFalse()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.ApiKey = "invalid-embedding-key-12345";

        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetEmbeddingAsync_WithInvalidConfiguration_ReturnsFailure()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.EndpointUrl = "https://invalid-endpoint.openai.azure.com";

        var provider = new AzureOpenAIEmbeddingProvider(options);

        // Act
        var response = await provider.GetEmbeddingAsync("test text");

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    private static LlmProviderOptions CreateValidOptions()
    {
        return new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = "test-api-key-12345",
                EmbeddingDeploymentName = "text-embedding-3-large",
                DefaultEmbeddingModel = "text-embedding-3-large"
            }
        };
    }
}
