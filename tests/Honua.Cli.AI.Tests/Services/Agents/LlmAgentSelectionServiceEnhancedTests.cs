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

/// <summary>
/// Enhanced tests for LlmAgentSelectionService covering confidence filtering,
/// improved caching, and edge cases identified in deep dive review.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmAgentSelectionServiceEnhancedTests
{
    private readonly Mock<ILlmProviderFactory> _mockLlmProviderFactory;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<LlmAgentSelectionService>> _mockLogger;
    private readonly AgentSelectionOptions _options;
    private readonly LlmAgentSelectionService _service;

    public LlmAgentSelectionServiceEnhancedTests()
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
            MinimumRelevanceScore = 0.5,  // 50% minimum confidence
            SelectionTemperature = 0.1,
            SelectionMaxTokens = 1000
        };

        var optionsWrapper = Options.Create(_options);

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
    public async Task SelectRelevantAgentsAsync_WithConfidenceScores_FiltersLowConfidenceAgents()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy my application to AWS";

        // LLM returns selections with varying confidence
        var llmResponse = new LlmResponse
        {
            Content = @"{
                ""selections"": [
                    {""index"": 0, ""confidence"": 0.95, ""reason"": ""Primary deployment agent""},
                    {""index"": 3, ""confidence"": 0.80, ""reason"": ""Security review""},
                    {""index"": 5, ""confidence"": 0.65, ""reason"": ""Cost optimization""},
                    {""index"": 7, ""confidence"": 0.30, ""reason"": ""Marginal relevance""},
                    {""index"": 9, ""confidence"": 0.10, ""reason"": ""Low relevance""}
                ]
            }",
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
        result.Should().HaveCount(3, "Only agents with confidence >= 0.5 should be selected");
        result[0].Name.Should().Be("Agent0", "Highest confidence agent should be first");
        result[1].Name.Should().Be("Agent3");
        result[2].Name.Should().Be("Agent5");

        // Low confidence agents (0.30, 0.10) should be filtered out
        result.Should().NotContain(a => a.Name == "Agent7");
        result.Should().NotContain(a => a.Name == "Agent9");
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithDuplicateRequests_UsesCachedResults()
    {
        // Arrange
        var agents = CreateTestAgents(5);
        var userRequest = "Deploy my application";

        var llmResponse = new LlmResponse
        {
            Content = @"{
                ""selections"": [
                    {""index"": 0, ""confidence"": 0.95, ""reason"": ""Deployment agent""}
                ]
            }",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act - First request (should call LLM)
        var result1 = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Act - Second identical request (should use cache)
        var result2 = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert
        result1.Should().BeEquivalentTo(result2);

        // LLM should only be called once (second call uses cache)
        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithVariedWording_CreatesDifferentCacheKeys()
    {
        // Arrange
        var agents = CreateTestAgents(5);
        var request1 = "Deploy my application to AWS";
        var request2 = "Deploy my app to Amazon Web Services";

        var llmResponse = new LlmResponse
        {
            Content = @"{
                ""selections"": [
                    {""index"": 0, ""confidence"": 0.95, ""reason"": ""Test""}
                ]
            }",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        await _service.SelectRelevantAgentsAsync(request1, agents, 5);
        await _service.SelectRelevantAgentsAsync(request2, agents, 5);

        // Assert
        // Different wording should result in different cache keys
        // LLM should be called twice
        _mockLlmProvider.Verify(
            p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithAllLowConfidence_FallsBackToDefaultAgents()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Obscure request with no clear agent match";

        // All agents have low confidence (below 0.5 threshold)
        var llmResponse = new LlmResponse
        {
            Content = @"{
                ""selections"": [
                    {""index"": 0, ""confidence"": 0.20, ""reason"": ""Weak match""},
                    {""index"": 1, ""confidence"": 0.15, ""reason"": ""Very weak match""},
                    {""index"": 2, ""confidence"": 0.10, ""reason"": ""Minimal relevance""}
                ]
            }",
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
        result.Should().HaveCount(5, "Should fallback to first N agents when all confidence scores are low");
        result[0].Name.Should().Be("Agent0");
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithLegacyFormat_StillWorks()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy application";

        // LLM returns old format (just indices array)
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
        result.Should().HaveCount(3, "Legacy format should still be parsed");
        result[0].Name.Should().Be("Agent0");
        result[1].Name.Should().Be("Agent3");
        result[2].Name.Should().Be("Agent5");
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithMalformedJson_FallsBackGracefully()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy application";

        // LLM returns malformed JSON
        var llmResponse = new LlmResponse
        {
            Content = "{ invalid json here",
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
        result.Should().HaveCount(5, "Should fallback to first N agents on parse failure");
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithLlmFailure_FallsBackToAllAgents()
    {
        // Arrange
        var agents = CreateTestAgents(10);
        var userRequest = "Deploy application";

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = false,
                ErrorMessage = "LLM service unavailable",
                Content = string.Empty,
                Model = "test-model"
            });

        // Act
        var result = await _service.SelectRelevantAgentsAsync(userRequest, agents, 5);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(agents.Count, "Should return all agents when LLM fails");
    }

    [Fact]
    public async Task SelectRelevantAgentsAsync_WithMarkdownWrappedJson_ParsesCorrectly()
    {
        // Arrange
        var agents = CreateTestAgents(5);
        var userRequest = "Deploy application";

        // LLM sometimes wraps JSON in markdown code blocks
        var llmResponse = new LlmResponse
        {
            Content = @"Here's the selection:

```json
{
    ""selections"": [
        {""index"": 0, ""confidence"": 0.95, ""reason"": ""Best match""}
    ]
}
```",
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
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Agent0");
    }

    [Fact]
    public async Task ExplainSelectionAsync_WithSelectedAgents_ReturnsExplanation()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var selectedAgents = agents.Take(2).ToList();
        var userRequest = "Deploy my application";

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse
            {
                Success = true,
                Content = "These agents were selected because they handle deployment and security.",
                Model = "test-model"
            });

        // Act
        var result = await _service.ExplainSelectionAsync(
            userRequest,
            selectedAgents.AsReadOnly(),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Reasoning.Should().Contain("deployment");
        result.AgentRelevanceScores.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0.3, 5)]  // Low threshold, should get 5 agents
    [InlineData(0.7, 2)]  // High threshold, should get 2 agents
    [InlineData(0.9, 1)]  // Very high threshold, should get 1 agent
    public async Task SelectRelevantAgentsAsync_WithDifferentThresholds_FiltersCorrectly(
        double threshold,
        int expectedCount)
    {
        // Arrange
        var options = new AgentSelectionOptions
        {
            MaxAgentsPerRequest = 10,
            EnableIntelligentSelection = true,
            MinimumRelevanceScore = threshold
        };

        var service = new LlmAgentSelectionService(
            _mockLlmProviderFactory.Object,
            _cache,
            _mockLogger.Object,
            Options.Create(options));

        var agents = CreateTestAgents(10);

        var llmResponse = new LlmResponse
        {
            Content = @"{
                ""selections"": [
                    {""index"": 0, ""confidence"": 0.95, ""reason"": ""Primary""},
                    {""index"": 1, ""confidence"": 0.85, ""reason"": ""Secondary""},
                    {""index"": 2, ""confidence"": 0.75, ""reason"": ""Tertiary""},
                    {""index"": 3, ""confidence"": 0.65, ""reason"": ""Quaternary""},
                    {""index"": 4, ""confidence"": 0.55, ""reason"": ""Quinary""}
                ]
            }",
            Model = "gpt-4",
            Success = true
        };

        _mockLlmProvider
            .Setup(p => p.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await service.SelectRelevantAgentsAsync("Deploy app", agents, 10);

        // Assert
        result.Should().HaveCount(expectedCount);
    }

    private List<Agent> CreateTestAgents(int count)
    {
        var agents = new List<Agent>();
        var kernel = new Kernel();

        for (int i = 0; i < count; i++)
        {
            var agent = new ChatCompletionAgent
            {
                Name = $"Agent{i}",
                Description = $"Test agent {i} description",
                Kernel = kernel,
                Instructions = $"You are agent {i}"
            };
            agents.Add(agent);
        }

        return agents;
    }
}
