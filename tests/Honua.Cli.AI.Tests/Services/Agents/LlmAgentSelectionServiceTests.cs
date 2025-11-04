using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Agents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Agents;

[Trait("Category", "Unit")]
public class LlmAgentSelectionServiceTests
{
    private readonly Mock<ILlmProviderFactory> _mockLlmProviderFactory;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<LlmAgentSelectionService>> _mockLogger;
    private readonly AgentSelectionOptions _options;
    private readonly LlmAgentSelectionService _service;

    public LlmAgentSelectionServiceTests()
    {
        _mockLlmProviderFactory = new Mock<ILlmProviderFactory>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<LlmAgentSelectionService>>();

        _options = new AgentSelectionOptions
        {
            MaxAgentsPerRequest = 5,
            EnableIntelligentSelection = true,
            EnableSelectionCaching = true,
            CacheDuration = TimeSpan.FromMinutes(15),
            MinimumRelevanceScore = 0.3,
            SelectionTemperature = 0.1,
            SelectionMaxTokens = 1000
        };

        var optionsWrapper = Options.Create(_options);

        _mockLlmProviderFactory
            .Setup(f => f.GetProvider(It.IsAny<string>()))
            .Returns(_mockLlmProvider.Object);

        _mockLlmProviderFactory
            .Setup(f => f.CreatePrimary())
            .Returns(_mockLlmProvider.Object);

        _service = new LlmAgentSelectionService(
            _mockLlmProviderFactory.Object,
            _cache,
            _mockLogger.Object,
            optionsWrapper);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithIntelligentSelectionEnabled_SelectsAgentsUsingLlm()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application to AWS";

        // LLM returns indices [0, 3, 5]
        var llmResponse = new LlmResponse
        {
            Content = "[0, 3, 5]",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Agent0");
        result[1].Name.Should().Be("Agent3");
        result[2].Name.Should().Be("Agent5");

        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithIntelligentSelectionDisabled_ReturnsAllAgents()
    {
        // Arrange
        var options = new AgentSelectionOptions
        {
            EnableIntelligentSelection = false
        };
        var service = new LlmAgentSelectionService(
            _mockLlmProviderFactory.Object,
            _cache,
            _mockLogger.Object,
            Options.Create(options));

        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        // Act
        var result = await service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert
        result.Should().HaveCount(10);
        result.Should().BeEquivalentTo(agents);

        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithCachingEnabled_UsesCachedResultsOnSecondCall()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application to AWS";

        var llmResponse = new LlmResponse
        {
            Content = "[0, 3, 5]",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act - First call
        var result1 = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Act - Second call (should use cache)
        var result2 = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert
        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
        result2.Should().BeEquivalentTo(result1);

        // LLM should only be called once
        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WhenLlmFails_FallsBackToAllAgents()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        var llmResponse = new LlmResponse
        {
            Content = "Error occurred",
            Model = "gpt-4",
            Success = false,
            ErrorMessage = "API rate limit exceeded"
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert - Should fallback to all agents
        result.Should().HaveCount(10);
        result.Should().BeEquivalentTo(agents);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WhenLlmThrowsException_FallsBackToAllAgents()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert - Should fallback to all agents
        result.Should().HaveCount(10);
        result.Should().BeEquivalentTo(agents);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithInvalidIndices_FiltersInvalidIndices()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        // LLM returns some invalid indices (negative, out of bounds)
        var llmResponse = new LlmResponse
        {
            Content = "[-1, 0, 3, 100, 5]",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert - Should only include valid indices (0, 3, 5)
        result.Should().HaveCount(3);
        result.Select(a => a.Name).Should().BeEquivalentTo(new[] { "Agent0", "Agent3", "Agent5" });
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_RespectsMaxAgentsLimit()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        // LLM returns 8 indices
        var llmResponse = new LlmResponse
        {
            Content = "[0, 1, 2, 3, 4, 5, 6, 7]",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act - Request max 5 agents
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, maxAgents: 5);

        // Assert - Should only return 5 agents
        result.Should().HaveCount(5);
        result.Select(a => a.Name).Should().BeEquivalentTo(
            new[] { "Agent0", "Agent1", "Agent2", "Agent3", "Agent4" });
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithDuplicateIndices_RemovesDuplicates()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        // LLM returns duplicate indices
        var llmResponse = new LlmResponse
        {
            Content = "[0, 3, 0, 5, 3]",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert - Should only include unique agents
        result.Should().HaveCount(3);
        result.Select(a => a.Name).Should().BeEquivalentTo(new[] { "Agent0", "Agent3", "Agent5" });
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithEmptyLlmResponse_FallsBackToFirstNAgents()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application";

        var llmResponse = new LlmResponse
        {
            Content = "[]",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, maxAgents: 5);

        // Assert - Should fallback to first 5 agents
        result.Should().HaveCount(5);
        result.Select(a => a.Name).Should().BeEquivalentTo(
            new[] { "Agent0", "Agent1", "Agent2", "Agent3", "Agent4" });
    }

    [Fact]
    public async Task ExplainSelectionAsync_ReturnsExplanation()
    {
        // Arrange
        var agents = CreateTestAgents(3).ToList();
        var userRequest = "Deploy my application to AWS";

        var llmResponse = new LlmResponse
        {
            Content = "The selected agents are relevant because they handle deployment, security, and cost optimization for AWS deployments.",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.ExplainSelectionAsync(userRequest, agents);

        // Assert
        result.Should().NotBeNull();
        result.Reasoning.Should().Contain("deployment");
        result.AgentRelevanceScores.Should().HaveCount(3);
    }

    private IReadOnlyList<Agent> CreateTestAgents(int count)
    {
        var agents = new List<Agent>();

        for (int i = 0; i < count; i++)
        {
            var kernel = new Kernel();
            agents.Add(new ChatCompletionAgent
            {
                Name = $"Agent{i}",
                Description = $"Description for agent {i}",
                Instructions = $"Instructions for agent {i}",
                Kernel = kernel
            });
        }

        return agents;
    }
}
