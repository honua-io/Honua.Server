// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Tests for IntakeAgent - AI-powered conversation system for gathering build requirements.
/// </summary>
[Trait("Category", "Unit")]
public class IntakeAgentTests
{
    private readonly Mock<IConversationStore> _mockConversationStore;
    private readonly Mock<ILogger<IntakeAgent>> _mockLogger;
    private readonly MockHttpMessageHandler _mockHttpHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IntakeAgentOptions _options;

    public IntakeAgentTests()
    {
        _mockConversationStore = new Mock<IConversationStore>();
        _mockLogger = new Mock<ILogger<IntakeAgent>>();
        _mockHttpHandler = new MockHttpMessageHandler();

        // Setup HttpClientFactory
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(_mockHttpHandler.ToHttpClient());
        _httpClientFactory = mockFactory.Object;

        _options = new IntakeAgentOptions
        {
            Provider = "openai",
            OpenAIApiKey = "test-api-key",
            OpenAIModel = "gpt-4-turbo-preview",
            MaxTokens = 2000,
            Temperature = 0.7
        };
    }

    [Fact]
    public async Task StartConversationAsync_CreatesNewConversation()
    {
        // Arrange
        var customerId = "customer-123";
        ConversationRecord? capturedRecord = null;

        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationRecord, CancellationToken>((record, ct) => capturedRecord = record)
            .Returns(Task.CompletedTask);

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var response = await agent.StartConversationAsync(customerId);

        // Assert
        response.Should().NotBeNull();
        response.ConversationId.Should().NotBeNullOrEmpty();
        response.CustomerId.Should().Be(customerId);
        response.InitialMessage.Should().NotBeNullOrEmpty();
        response.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        capturedRecord.Should().NotBeNull();
        capturedRecord!.ConversationId.Should().Be(response.ConversationId);
        capturedRecord.CustomerId.Should().Be(customerId);
        capturedRecord.Status.Should().Be("active");

        _mockConversationStore.Verify(
            x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartConversationAsync_WithoutCustomerId_CreatesAnonymousConversation()
    {
        // Arrange
        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var response = await agent.StartConversationAsync();

        // Assert
        response.Should().NotBeNull();
        response.ConversationId.Should().NotBeNullOrEmpty();
        response.CustomerId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessMessageAsync_ValidMessage_ReturnsAIResponse()
    {
        // Arrange
        var conversationId = "conv-123";
        var userMessage = "I need WFS and PostGIS support";

        var existingConversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-123",
            MessagesJson = JsonSerializer.Serialize(new List<ConversationMessage>
            {
                new() { Role = "system", Content = "System prompt" }
            }),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock OpenAI response
        var openAIResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Great! WFS and PostGIS are excellent choices. What cloud provider would you like to use?"
                    }
                }
            }
        };

        _mockHttpHandler
            .When("https://api.openai.com/v1/chat/completions")
            .Respond("application/json", JsonSerializer.Serialize(openAIResponse));

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var response = await agent.ProcessMessageAsync(conversationId, userMessage);

        // Assert
        response.Should().NotBeNull();
        response.ConversationId.Should().Be(conversationId);
        response.Message.Should().Contain("cloud provider");
        response.IntakeComplete.Should().BeFalse();
        response.Requirements.Should().BeNull();

        _mockConversationStore.Verify(
            x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ConversationNotFound_ThrowsException()
    {
        // Arrange
        var conversationId = "nonexistent-conv";
        var userMessage = "test message";

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationRecord?)null);

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var act = async () => await agent.ProcessMessageAsync(conversationId, userMessage);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ProcessMessageAsync_IntakeComplete_ExtractsRequirements()
    {
        // Arrange
        var conversationId = "conv-complete";
        var userMessage = "Yes, that's everything I need";

        var existingConversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-123",
            MessagesJson = JsonSerializer.Serialize(new List<ConversationMessage>
            {
                new() { Role = "system", Content = "System prompt" },
                new() { Role = "user", Content = "I need WFS and PostGIS" },
                new() { Role = "assistant", Content = "What cloud provider?" },
                new() { Role = "user", Content = "AWS" }
            }),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock OpenAI response with function call
        var functionArgs = new
        {
            protocols = new[] { "WFS", "WMS" },
            databases = new[] { "PostGIS" },
            cloudProvider = "aws",
            architecture = "linux-x64",
            tier = "pro",
            expectedLoad = new
            {
                concurrentUsers = 100,
                requestsPerSecond = 50.0,
                classification = "moderate"
            }
        };

        var openAIResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Perfect! I have all the information I need.",
                        function_call = new
                        {
                            name = "complete_intake",
                            arguments = JsonSerializer.Serialize(functionArgs)
                        }
                    }
                }
            }
        };

        _mockHttpHandler
            .When("https://api.openai.com/v1/chat/completions")
            .Respond("application/json", JsonSerializer.Serialize(openAIResponse));

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var response = await agent.ProcessMessageAsync(conversationId, userMessage);

        // Assert
        response.Should().NotBeNull();
        response.IntakeComplete.Should().BeTrue();
        response.Requirements.Should().NotBeNull();
        response.Requirements!.Protocols.Should().Contain("WFS");
        response.Requirements.Databases.Should().Contain("PostGIS");
        response.Requirements.CloudProvider.Should().Be("aws");
        response.Requirements.Tier.Should().Be("pro");
        response.EstimatedMonthlyCost.Should().BeGreaterThan(0);
        response.CostBreakdown.Should().NotBeNull();
        response.CostBreakdown.Should().ContainKeys("license", "infrastructure", "storage");
    }

    [Fact]
    public async Task ProcessMessageAsync_GeneratesCostEstimate_ForProTier()
    {
        // Arrange
        var conversationId = "conv-cost";
        var userMessage = "I'm ready to proceed";

        var existingConversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-123",
            MessagesJson = JsonSerializer.Serialize(new List<ConversationMessage>
            {
                new() { Role = "system", Content = "System prompt" }
            }),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var functionArgs = new
        {
            protocols = new[] { "WFS" },
            databases = new[] { "PostGIS" },
            cloudProvider = "aws",
            architecture = "linux-arm64",
            tier = "pro",
            expectedLoad = new
            {
                concurrentUsers = 50,
                requestsPerSecond = 25.0,
                dataVolumeGb = 100.0,
                classification = "light"
            }
        };

        var openAIResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Configuration complete!",
                        function_call = new
                        {
                            name = "complete_intake",
                            arguments = JsonSerializer.Serialize(functionArgs)
                        }
                    }
                }
            }
        };

        _mockHttpHandler
            .When("https://api.openai.com/v1/chat/completions")
            .Respond("application/json", JsonSerializer.Serialize(openAIResponse));

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var response = await agent.ProcessMessageAsync(conversationId, userMessage);

        // Assert
        response.Should().NotBeNull();
        response.EstimatedMonthlyCost.Should().BeGreaterThan(0);
        response.CostBreakdown.Should().NotBeNull();

        // Pro tier license is $499/month
        response.CostBreakdown!["license"].Should().Be(499m);

        // AWS ARM64 light load should be around $24/month
        response.CostBreakdown["infrastructure"].Should().BeGreaterThan(0);

        // Storage cost should be calculated
        response.CostBreakdown["storage"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcessMessageAsync_Anthropic_ProcessesSuccessfully()
    {
        // Arrange
        var conversationId = "conv-anthropic";
        var userMessage = "I need WMS support";

        var existingConversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-123",
            MessagesJson = JsonSerializer.Serialize(new List<ConversationMessage>
            {
                new() { Role = "system", Content = "System prompt" }
            }),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock Anthropic response
        var anthropicResponse = new
        {
            content = new[]
            {
                new
                {
                    text = "Excellent choice! WMS is widely used. What cloud provider would you prefer?"
                }
            }
        };

        _mockHttpHandler
            .When("https://api.anthropic.com/v1/messages")
            .Respond("application/json", JsonSerializer.Serialize(anthropicResponse));

        var anthropicOptions = new IntakeAgentOptions
        {
            Provider = "anthropic",
            AnthropicApiKey = "test-anthropic-key",
            AnthropicModel = "claude-3-opus-20240229"
        };

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(anthropicOptions),
            _mockLogger.Object);

        // Act
        var response = await agent.ProcessMessageAsync(conversationId, userMessage);

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().Contain("cloud provider");
    }

    [Fact]
    public async Task GetConversationAsync_ExistingConversation_ReturnsRecord()
    {
        // Arrange
        var conversationId = "conv-456";
        var expectedRecord = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-456",
            MessagesJson = "[]",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRecord);

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var result = await agent.GetConversationAsync(conversationId);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedRecord);

        _mockConversationStore.Verify(
            x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetConversationAsync_NonExistentConversation_ReturnsNull()
    {
        // Arrange
        var conversationId = "nonexistent";

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationRecord?)null);

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var result = await agent.GetConversationAsync(conversationId);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("core", 0)]
    [InlineData("pro", 499)]
    [InlineData("enterprise", 2500)]
    [InlineData("enterprise-asp", 5000)]
    public async Task ProcessMessageAsync_CalculatesCorrectLicenseCost(string tier, decimal expectedLicenseCost)
    {
        // Arrange
        var conversationId = $"conv-{tier}";
        var userMessage = "Ready to proceed";

        var existingConversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-123",
            MessagesJson = JsonSerializer.Serialize(new List<ConversationMessage>
            {
                new() { Role = "system", Content = "System prompt" }
            }),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockConversationStore
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        _mockConversationStore
            .Setup(x => x.SaveConversationAsync(It.IsAny<ConversationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var functionArgs = new
        {
            protocols = new[] { "WFS" },
            databases = new[] { "PostGIS" },
            cloudProvider = "aws",
            architecture = "linux-x64",
            tier = tier
        };

        var openAIResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Configuration complete!",
                        function_call = new
                        {
                            name = "complete_intake",
                            arguments = JsonSerializer.Serialize(functionArgs)
                        }
                    }
                }
            }
        };

        _mockHttpHandler
            .When("https://api.openai.com/v1/chat/completions")
            .Respond("application/json", JsonSerializer.Serialize(openAIResponse));

        var agent = new IntakeAgent(
            _httpClientFactory,
            _mockConversationStore.Object,
            Options.Create(_options),
            _mockLogger.Object);

        // Act
        var response = await agent.ProcessMessageAsync(conversationId, userMessage);

        // Assert
        response.Should().NotBeNull();
        response.CostBreakdown.Should().NotBeNull();
        response.CostBreakdown!["license"].Should().Be(expectedLicenseCost);
    }
}
