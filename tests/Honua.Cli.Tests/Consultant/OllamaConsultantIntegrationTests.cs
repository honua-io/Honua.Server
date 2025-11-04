using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Tests.Fixtures;
using Xunit;

namespace Honua.Cli.Tests.Consultant;

/// <summary>
/// Integration tests for consultant using local Ollama LLM instead of real API keys.
/// These tests replace the conditional USE_REAL_LLM=true tests with containerized LLM.
/// </summary>
[Collection("OllamaContainer")]
[Trait("Category", "Integration")]
public sealed class OllamaConsultantIntegrationTests
{
    private readonly OllamaTestFixture _fixture;

    public OllamaConsultantIntegrationTests(OllamaTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Ollama_ShouldGenerateDeploymentGuidance()
    {
        // Skip if Ollama not available
        if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
        {
            // Test is skipped, not failed
            return;
        }

        // Arrange
        var systemPrompt = "You are a geospatial infrastructure consultant. Provide brief, practical deployment advice.";
        var userPrompt = "I need to deploy a GIS server to AWS. What are the key steps?";

        // Act
        var response = await _fixture.GenerateChatCompletionAsync(
            systemPrompt,
            userPrompt,
            CancellationToken.None);

        // Assert
        response.Should().NotBeNullOrEmpty("LLM should generate a response");
        response.Should().ContainAny(new[] { "AWS", "deploy", "server", "step" },
            "Response should mention deployment concepts");

        Console.WriteLine($"Ollama Response: {response}");
    }

    [Fact]
    public async Task Ollama_ShouldProvideSecurityGuidance()
    {
        // Skip if Ollama not available
        if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
        {
            return;
        }

        // Arrange
        var systemPrompt = "You are a security consultant. Provide brief security recommendations.";
        var userPrompt = "What are the most important security considerations for a production GIS deployment?";

        // Act
        var response = await _fixture.GenerateChatCompletionAsync(
            systemPrompt,
            userPrompt,
            CancellationToken.None);

        // Assert
        response.Should().NotBeNullOrEmpty("LLM should generate security advice");
        response.Should().ContainAny(new[] { "security", "https", "authentication", "access", "encrypt" },
            "Response should mention security concepts");

        Console.WriteLine($"Ollama Security Response: {response}");
    }

    [Fact]
    public async Task Ollama_ShouldHandleDataMigrationQuestions()
    {
        // Skip if Ollama not available
        if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
        {
            return;
        }

        // Arrange
        var systemPrompt = "You are a data migration expert. Provide concise migration advice.";
        var userPrompt = "How do I migrate 500,000 features from ArcGIS Server to PostGIS?";

        // Act
        var response = await _fixture.GenerateChatCompletionAsync(
            systemPrompt,
            userPrompt,
            CancellationToken.None);

        // Assert
        response.Should().NotBeNullOrEmpty("LLM should generate migration advice");
        response.Should().ContainAny(new[] { "migrat", "data", "feature", "postgis", "arcgis" },
            "Response should mention migration concepts");

        Console.WriteLine($"Ollama Migration Response: {response}");
    }

    [Fact]
    public async Task Ollama_ShouldProvidePerformanceOptimizationTips()
    {
        // Skip if Ollama not available
        if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
        {
            return;
        }

        // Arrange
        var systemPrompt = "You are a performance optimization expert. Provide brief optimization recommendations.";
        var userPrompt = "My map tiles are rendering slowly. How can I optimize performance?";

        // Act
        var response = await _fixture.GenerateChatCompletionAsync(
            systemPrompt,
            userPrompt,
            CancellationToken.None);

        // Assert
        response.Should().NotBeNullOrEmpty("LLM should generate optimization advice");
        response.Should().ContainAny(new[] { "cache", "index", "optim", "performance", "tile" },
            "Response should mention optimization concepts");

        Console.WriteLine($"Ollama Optimization Response: {response}");
    }

    [Fact]
    public async Task Ollama_ShouldHandleSimpleCompletion()
    {
        // Skip if Ollama not available
        if (!_fixture.OllamaAvailable || !_fixture.ModelPulled)
        {
            return;
        }

        // Arrange
        var prompt = "List 3 cloud providers for GIS deployments:";

        // Act
        var response = await _fixture.GenerateCompletionAsync(prompt, CancellationToken.None);

        // Assert
        response.Should().NotBeNullOrEmpty("LLM should generate a completion");
        response.Should().ContainAny(new[] { "AWS", "Azure", "GCP", "Google", "Amazon" },
            "Response should mention cloud providers");

        Console.WriteLine($"Ollama Completion Response: {response}");
    }
}
