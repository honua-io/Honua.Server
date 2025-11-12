// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Authorization;

public class LayerAuthorizationHandlerTests
{
    private readonly Mock<IResourceAuthorizationCache> _mockCache;
    private readonly Mock<ILogger<LayerAuthorizationHandler>> _mockLogger;
    private readonly Mock<IOptionsMonitor<ResourceAuthorizationOptions>> _mockOptions;
    private readonly ResourceAuthorizationMetrics _metrics;
    private readonly LayerAuthorizationHandler _handler;

    public LayerAuthorizationHandlerTests()
    {
        _mockCache = new Mock<IResourceAuthorizationCache>();
        _mockLogger = new Mock<ILogger<LayerAuthorizationHandler>>();
        _mockOptions = new Mock<IOptionsMonitor<ResourceAuthorizationOptions>>();

        // Create a real metrics instance with a mock meter factory
        var mockMeterFactory = new Mock<IMeterFactory>();
        var mockMeter = new Mock<Meter>("TestMeter");
        mockMeterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));
        _metrics = new ResourceAuthorizationMetrics(mockMeterFactory.Object);

        // Setup default options
        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        _handler = new LayerAuthorizationHandler(
            _mockCache.Object,
            _metrics,
            _mockLogger.Object,
            _mockOptions.Object);
    }

    [Fact]
    public async Task AuthorizeAsync_WithAuthorizedUser_Succeeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, "User")
        }, "TestAuth"));

        var policy = new ResourcePolicy
        {
            Id = "test-policy",
            ResourceType = "layer",
            ResourcePattern = "layer-1",
            AllowedOperations = new List<string> { "Read" },
            Roles = new List<string> { "User" },
            Enabled = true,
            Priority = 100
        };

        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy> { policy }
        });

        // Setup cache to return miss
        ResourceAuthorizationResult? cachedResult = null;
        _mockCache.Setup(x => x.TryGet(It.IsAny<string>(), out cachedResult)).Returns(false);

        // Act
        var result = await _handler.AuthorizeAsync(user, "layer", "layer-1", "Read");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithUnauthorizedUser_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456")
        }, "TestAuth"));

        // No matching policy for this user
        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        // Setup cache to return miss
        ResourceAuthorizationResult? cachedResult = null;
        _mockCache.Setup(x => x.TryGet(It.IsAny<string>(), out cachedResult)).Returns(false);

        // Act
        var result = await _handler.AuthorizeAsync(user, "layer", "layer-2", "Write");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthorizeAsync_WithAnonymousUser_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        // Setup cache to return miss
        ResourceAuthorizationResult? cachedResult = null;
        _mockCache.Setup(x => x.TryGet(It.IsAny<string>(), out cachedResult)).Returns(false);

        // Act
        var result = await _handler.AuthorizeAsync(user, "layer", "layer-3", "Read");

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WithAdminRole_AlwaysSucceeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-123"),
            new Claim(ClaimTypes.Role, "Administrator")
        }, "TestAuth"));

        // No policies, but admin role should grant access
        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        // Setup cache to return miss
        ResourceAuthorizationResult? cachedResult = null;
        _mockCache.Setup(x => x.TryGet(It.IsAny<string>(), out cachedResult)).Returns(false);

        // Act
        var result = await _handler.AuthorizeAsync(user, "layer", "layer-4", "Delete");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("Delete")]
    [InlineData("Admin")]
    public async Task AuthorizeAsync_WithDifferentOperations_ChecksCorrectPermission(string operation)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-789"),
            new Claim(ClaimTypes.Role, "TestRole")
        }, "TestAuth"));

        var policy = new ResourcePolicy
        {
            Id = "wildcard-policy",
            ResourceType = "layer",
            ResourcePattern = "layer-5",
            AllowedOperations = new List<string> { "*" }, // Allow all operations
            Roles = new List<string> { "TestRole" },
            Enabled = true,
            Priority = 100
        };

        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy> { policy }
        });

        // Setup cache to return miss
        ResourceAuthorizationResult? cachedResult = null;
        _mockCache.Setup(x => x.TryGet(It.IsAny<string>(), out cachedResult)).Returns(false);

        // Act
        var result = await _handler.AuthorizeAsync(user, "layer", "layer-5", operation);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
