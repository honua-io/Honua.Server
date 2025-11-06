// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.Controllers;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Tests for IntakeController - REST API endpoints for the intake system.
/// </summary>
[Trait("Category", "Unit")]
public class IntakeControllerTests
{
    private readonly Mock<IIntakeAgent> _mockIntakeAgent;
    private readonly Mock<IManifestGenerator> _mockManifestGenerator;
    private readonly Mock<IRegistryProvisioner> _mockRegistryProvisioner;
    private readonly Mock<IBuildDeliveryService> _mockBuildDeliveryService;
    private readonly Mock<ILogger<IntakeController>> _mockLogger;
    private readonly IntakeController _controller;

    public IntakeControllerTests()
    {
        _mockIntakeAgent = new Mock<IIntakeAgent>();
        _mockManifestGenerator = new Mock<IManifestGenerator>();
        _mockRegistryProvisioner = new Mock<IRegistryProvisioner>();
        _mockBuildDeliveryService = new Mock<IBuildDeliveryService>();
        _mockLogger = new Mock<ILogger<IntakeController>>();

        _controller = new IntakeController(
            _mockIntakeAgent.Object,
            _mockManifestGenerator.Object,
            _mockRegistryProvisioner.Object,
            _mockBuildDeliveryService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task StartConversation_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new StartConversationRequest { CustomerId = "customer-123" };
        var expectedResponse = new ConversationResponse
        {
            ConversationId = "conv-123",
            InitialMessage = "Hello! I'm here to help configure your Honua server.",
            StartedAt = DateTimeOffset.UtcNow,
            CustomerId = "customer-123"
        };

        _mockIntakeAgent
            .Setup(x => x.StartConversationAsync("customer-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.StartConversation(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ConversationResponse>().Subject;
        response.ConversationId.Should().Be("conv-123");
        response.CustomerId.Should().Be("customer-123");

        _mockIntakeAgent.Verify(
            x => x.StartConversationAsync("customer-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartConversation_WithoutCustomerId_ReturnsOk()
    {
        // Arrange
        var expectedResponse = new ConversationResponse
        {
            ConversationId = "conv-456",
            InitialMessage = "Hello!",
            StartedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.StartConversationAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.StartConversation(null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ConversationResponse>().Subject;
        response.ConversationId.Should().Be("conv-456");
    }

    [Fact]
    public async Task StartConversation_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockIntakeAgent
            .Setup(x => x.StartConversationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.StartConversation(null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task SendMessage_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = "conv-123",
            Message = "I need WFS support"
        };

        var expectedResponse = new IntakeResponse
        {
            ConversationId = "conv-123",
            Message = "Great choice! What cloud provider would you like to use?",
            IntakeComplete = false,
            Timestamp = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.ProcessMessageAsync("conv-123", "I need WFS support", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<IntakeResponse>().Subject;
        response.ConversationId.Should().Be("conv-123");
        response.IntakeComplete.Should().BeFalse();

        _mockIntakeAgent.Verify(
            x => x.ProcessMessageAsync("conv-123", "I need WFS support", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_EmptyConversationId_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = "",
            Message = "Test message"
        };

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _mockIntakeAgent.Verify(
            x => x.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendMessage_EmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = "conv-123",
            Message = ""
        };

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SendMessage_ConversationNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = "nonexistent",
            Message = "Test message"
        };

        _mockIntakeAgent
            .Setup(x => x.ProcessMessageAsync("nonexistent", "Test message", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Conversation nonexistent not found"));

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SendMessage_IntakeComplete_ReturnsRequirements()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = "conv-123",
            Message = "Yes, that's everything"
        };

        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS", "WMS" },
            Databases = new List<string> { "PostGIS" },
            CloudProvider = "aws",
            Architecture = "linux-x64",
            Tier = "pro"
        };

        var expectedResponse = new IntakeResponse
        {
            ConversationId = "conv-123",
            Message = "Perfect! I have all the information I need.",
            IntakeComplete = true,
            Requirements = requirements,
            EstimatedMonthlyCost = 549m,
            CostBreakdown = new Dictionary<string, decimal>
            {
                ["license"] = 499m,
                ["infrastructure"] = 40m,
                ["storage"] = 10m
            },
            Timestamp = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.ProcessMessageAsync("conv-123", "Yes, that's everything", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<IntakeResponse>().Subject;
        response.IntakeComplete.Should().BeTrue();
        response.Requirements.Should().NotBeNull();
        response.Requirements!.Tier.Should().Be("pro");
        response.EstimatedMonthlyCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetConversation_ExistingConversation_ReturnsOk()
    {
        // Arrange
        var conversationId = "conv-123";
        var expectedRecord = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = "customer-123",
            MessagesJson = "[]",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRecord);

        // Act
        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ConversationRecord>().Subject;
        response.ConversationId.Should().Be(conversationId);
    }

    [Fact]
    public async Task GetConversation_NonExistentConversation_ReturnsNotFound()
    {
        // Arrange
        var conversationId = "nonexistent";

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationRecord?)null);

        // Act
        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TriggerBuild_ValidRequest_ReturnsOk()
    {
        // Arrange
        var conversationId = "conv-123";
        var customerId = "customer-123";

        var conversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            RequirementsJson = JsonSerializer.Serialize(new BuildRequirements
            {
                Protocols = new List<string> { "WFS" },
                Databases = new List<string> { "PostGIS" },
                CloudProvider = "aws",
                Architecture = "linux-x64",
                Tier = "pro"
            }),
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var manifest = new BuildManifest
        {
            Version = "1.0",
            Name = "honua-server-pro",
            Architecture = "linux-x64",
            Modules = new List<string> { "WFS", "PostGIS" },
            Tier = "pro",
            GeneratedAt = DateTimeOffset.UtcNow
        };

        var registryResult = new RegistryProvisioningResult
        {
            Success = true,
            RegistryType = RegistryType.AwsEcr,
            CustomerId = customerId,
            Namespace = $"honua/{customerId}",
            ProvisionedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockManifestGenerator
            .Setup(x => x.GenerateAsync(It.IsAny<BuildRequirements>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);

        _mockRegistryProvisioner
            .Setup(x => x.ProvisionAsync(customerId, RegistryType.AwsEcr, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registryResult);

        var request = new TriggerBuildRequest
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            BuildName = "my-honua-server"
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TriggerBuildResponse>().Subject;
        response.Success.Should().BeTrue();
        response.BuildId.Should().NotBeNullOrEmpty();
        response.Manifest.Should().NotBeNull();
        response.RegistryResult.Should().NotBeNull();

        _mockManifestGenerator.Verify(
            x => x.GenerateAsync(It.IsAny<BuildRequirements>(), "my-honua-server", It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRegistryProvisioner.Verify(
            x => x.ProvisionAsync(customerId, RegistryType.AwsEcr, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerBuild_EmptyConversationId_ReturnsBadRequest()
    {
        // Arrange
        var request = new TriggerBuildRequest
        {
            ConversationId = "",
            CustomerId = "customer-123"
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TriggerBuild_EmptyCustomerId_ReturnsBadRequest()
    {
        // Arrange
        var request = new TriggerBuildRequest
        {
            ConversationId = "conv-123",
            CustomerId = ""
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TriggerBuild_ConversationNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new TriggerBuildRequest
        {
            ConversationId = "nonexistent",
            CustomerId = "customer-123"
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationRecord?)null);

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TriggerBuild_NoRequirementsAvailable_ReturnsBadRequest()
    {
        // Arrange
        var conversationId = "conv-123";
        var customerId = "customer-123";

        var conversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            RequirementsJson = null,
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var request = new TriggerBuildRequest
        {
            ConversationId = conversationId,
            CustomerId = customerId
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TriggerBuild_RegistryProvisioningFails_ReturnsInternalServerError()
    {
        // Arrange
        var conversationId = "conv-123";
        var customerId = "customer-123";

        var conversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            RequirementsJson = JsonSerializer.Serialize(new BuildRequirements
            {
                CloudProvider = "aws",
                Tier = "pro"
            }),
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var manifest = new BuildManifest
        {
            Name = "test-build",
            Architecture = "linux-x64",
            GeneratedAt = DateTimeOffset.UtcNow
        };

        var failedRegistryResult = new RegistryProvisioningResult
        {
            Success = false,
            ErrorMessage = "Failed to create ECR repository",
            ProvisionedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockManifestGenerator
            .Setup(x => x.GenerateAsync(It.IsAny<BuildRequirements>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);

        _mockRegistryProvisioner
            .Setup(x => x.ProvisionAsync(customerId, It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedRegistryResult);

        var request = new TriggerBuildRequest
        {
            ConversationId = conversationId,
            CustomerId = customerId
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TriggerBuild_WithRequirementsOverride_UsesOverride()
    {
        // Arrange
        var conversationId = "conv-123";
        var customerId = "customer-123";

        var conversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            RequirementsJson = JsonSerializer.Serialize(new BuildRequirements
            {
                CloudProvider = "aws",
                Tier = "core"
            }),
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var overrideRequirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS", "WMS", "WMTS" },
            CloudProvider = "azure",
            Tier = "enterprise"
        };

        var manifest = new BuildManifest
        {
            Name = "test",
            Architecture = "linux-x64",
            Tier = "enterprise",
            GeneratedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockManifestGenerator
            .Setup(x => x.GenerateAsync(It.Is<BuildRequirements>(r => r.Tier == "enterprise"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);

        _mockRegistryProvisioner
            .Setup(x => x.ProvisionAsync(It.IsAny<string>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryProvisioningResult { Success = true, ProvisionedAt = DateTimeOffset.UtcNow });

        var request = new TriggerBuildRequest
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            RequirementsOverride = overrideRequirements
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TriggerBuildResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Manifest!.Tier.Should().Be("enterprise");

        _mockManifestGenerator.Verify(
            x => x.GenerateAsync(It.Is<BuildRequirements>(r => r.Tier == "enterprise"), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetBuildStatus_ValidBuildId_ReturnsOk()
    {
        // Arrange
        var buildId = "build-123";

        // Act
        var result = await _controller.GetBuildStatus(buildId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BuildStatusResponse>().Subject;
        response.BuildId.Should().Be(buildId);
        response.Status.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("aws", RegistryType.AwsEcr)]
    [InlineData("azure", RegistryType.AzureAcr)]
    [InlineData("gcp", RegistryType.GcpArtifactRegistry)]
    [InlineData("unknown", RegistryType.GitHubContainerRegistry)]
    public async Task TriggerBuild_DeterminesCorrectRegistryType(string cloudProvider, RegistryType expectedRegistryType)
    {
        // Arrange
        var conversationId = "conv-123";
        var customerId = "customer-123";

        var conversation = new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            RequirementsJson = JsonSerializer.Serialize(new BuildRequirements
            {
                CloudProvider = cloudProvider,
                Tier = "core"
            }),
            Status = "completed",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _mockIntakeAgent
            .Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockManifestGenerator
            .Setup(x => x.GenerateAsync(It.IsAny<BuildRequirements>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildManifest { Name = "test", Architecture = "linux-x64", GeneratedAt = DateTimeOffset.UtcNow });

        _mockRegistryProvisioner
            .Setup(x => x.ProvisionAsync(customerId, expectedRegistryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryProvisioningResult { Success = true, ProvisionedAt = DateTimeOffset.UtcNow });

        var request = new TriggerBuildRequest
        {
            ConversationId = conversationId,
            CustomerId = customerId
        };

        // Act
        var result = await _controller.TriggerBuild(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockRegistryProvisioner.Verify(
            x => x.ProvisionAsync(customerId, expectedRegistryType, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
