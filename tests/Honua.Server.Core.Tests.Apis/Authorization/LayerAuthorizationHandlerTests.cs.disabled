// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Authorization;

public class LayerAuthorizationHandlerTests
{
    private readonly Mock<IResourceAuthorizationService> _mockAuthService;
    private readonly LayerAuthorizationHandler _handler;

    public LayerAuthorizationHandlerTests()
    {
        _mockAuthService = new Mock<IResourceAuthorizationService>();
        _handler = new LayerAuthorizationHandler(_mockAuthService.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithAuthorizedUser_Succeeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, "User")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Read");
        var resource = new LayerResource { LayerId = "layer-1", CollectionId = "collection-1" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessLayerAsync("user-123", "layer-1", "Read"))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithUnauthorizedUser_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Write");
        var resource = new LayerResource { LayerId = "layer-2", CollectionId = "collection-1" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessLayerAsync("user-456", "layer-2", "Write"))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithAnonymousUser_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
        var requirement = new ResourceAccessRequirement("Read");
        var resource = new LayerResource { LayerId = "layer-3", CollectionId = "collection-1" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithAdminRole_AlwaysSucceeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-123"),
            new Claim(ClaimTypes.Role, "Administrator")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Delete");
        var resource = new LayerResource { LayerId = "layer-4", CollectionId = "collection-1" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessLayerAsync("admin-123", "layer-4", "Delete"))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("Delete")]
    [InlineData("Admin")]
    public async Task HandleRequirementAsync_WithDifferentOperations_ChecksCorrectPermission(string operation)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-789")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement(operation);
        var resource = new LayerResource { LayerId = "layer-5", CollectionId = "collection-1" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessLayerAsync("user-789", "layer-5", operation))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        _mockAuthService.Verify(
            x => x.CanAccessLayerAsync("user-789", "layer-5", operation),
            Times.Once);
    }
}

// Mock classes for testing
public class ResourceAccessRequirement : IAuthorizationRequirement
{
    public string Operation { get; }
    public ResourceAccessRequirement(string operation) => Operation = operation;
}

public class LayerResource
{
    public string LayerId { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
}
