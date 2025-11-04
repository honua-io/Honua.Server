using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.AI;

[Trait("Category", "Unit")]
public sealed class MockLlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithDefaultSetup_ReturnsDefaultResponse()
    {
        // Arrange
        var provider = new MockLlmProvider();
        var request = new LlmRequest
        {
            UserPrompt = "Hello, how are you?"
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Be("This is a mock response from the test LLM provider.");
        response.Model.Should().Be("mock-model-v1");
    }

    [Fact]
    public async Task CompleteAsync_WithPatternMatch_ReturnsConfiguredResponse()
    {
        // Arrange
        var provider = new MockLlmProvider();
        provider.SetupResponse("postgis", "Use PostGIS for spatial data storage with excellent performance.");

        var request = new LlmRequest
        {
            UserPrompt = "How should I configure PostGIS?"
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("PostGIS");
    }

    [Fact]
    public async Task CompleteAsync_WithResponseGenerator_CallsGeneratorFunction()
    {
        // Arrange
        var provider = new MockLlmProvider();
        provider.SetupResponseGenerator("configuration", prompt =>
        {
            return $"Generated response for: {prompt}";
        });

        var request = new LlmRequest
        {
            UserPrompt = "What configuration do I need?"
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("Generated response for:");
        response.Content.Should().Contain("configuration");
    }

    [Fact]
    public async Task CompleteAsync_WhenUnavailable_ReturnsFailure()
    {
        // Arrange
        var provider = new MockLlmProvider();
        provider.SetAvailability(false);

        var request = new LlmRequest
        {
            UserPrompt = "Test"
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("unavailable");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenAvailable_ReturnsTrue()
    {
        // Arrange
        var provider = new MockLlmProvider();

        // Act
        var available = await provider.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenUnavailable_ReturnsFalse()
    {
        // Arrange
        var provider = new MockLlmProvider();
        provider.SetAvailability(false);

        // Act
        var available = await provider.IsAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsAvailableModels()
    {
        // Arrange
        var provider = new MockLlmProvider();

        // Act
        var models = await provider.ListModelsAsync();

        // Assert
        models.Should().NotBeEmpty();
        models.Should().Contain("mock-model-v1");
    }

    [Fact]
    public async Task Reset_ClearsAllConfiguredResponses()
    {
        // Arrange
        var provider = new MockLlmProvider();
        provider.SetupResponse("test", "Custom response");
        provider.SetDefaultResponse("Custom default");
        provider.SetAvailability(false);

        // Act
        provider.Reset();
        var available = await provider.IsAvailableAsync();
        var response = await provider.CompleteAsync(new LlmRequest { UserPrompt = "test" });

        // Assert
        available.Should().BeTrue();
        response.Content.Should().Be("This is a mock response from the test LLM provider.");
    }
}
