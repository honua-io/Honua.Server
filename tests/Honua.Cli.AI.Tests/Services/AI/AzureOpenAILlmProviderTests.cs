using Xunit;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Microsoft.Extensions.Options;

namespace Honua.Cli.AI.Tests.Services.AI;

[Trait("Category", "Unit")]
public sealed class AzureOpenAILlmProviderTests
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
                DeploymentName = "test-deployment"
            }
        };

        // Act
        var act = () => new AzureOpenAILlmProvider(options);

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
                DeploymentName = "test-deployment"
            }
        };

        // Act
        var act = () => new AzureOpenAILlmProvider(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key*");
    }

    [Fact]
    public void Constructor_WithMissingDeploymentName_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = "test-key",
                DeploymentName = null
            }
        };

        // Act
        var act = () => new AzureOpenAILlmProvider(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deployment name*");
    }

    [Fact]
    public void ProviderName_ReturnsAzureOpenAI()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var provider = new AzureOpenAILlmProvider(options);

        // Assert
        provider.ProviderName.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void DefaultModel_ReturnsConfiguredModel()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.DefaultModel = "gpt-4o";

        // Act
        var provider = new AzureOpenAILlmProvider(options);

        // Assert
        provider.DefaultModel.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task IsAvailableAsync_WithInvalidEndpoint_ReturnsFalse()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.EndpointUrl = "https://invalid-endpoint-that-does-not-exist.openai.azure.com";

        var provider = new AzureOpenAILlmProvider(options);

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
        options.Azure.ApiKey = "invalid-key-12345";

        var provider = new AzureOpenAILlmProvider(options);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsConfiguredModel()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Azure.DefaultModel = "gpt-4-turbo";

        var provider = new AzureOpenAILlmProvider(options);

        // Act
        var models = await provider.ListModelsAsync();

        // Assert
        models.Should().Contain("gpt-4-turbo");
    }

    private static LlmProviderOptions CreateValidOptions()
    {
        return new LlmProviderOptions
        {
            Azure = new AzureOpenAIOptions
            {
                EndpointUrl = "https://test.openai.azure.com",
                ApiKey = "test-api-key-12345",
                DeploymentName = "gpt-4-turbo",
                DefaultModel = "gpt-4o"
            }
        };
    }
}
