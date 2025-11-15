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

public class CollectionAuthorizationHandlerTests
{
    private readonly Mock<IResourceAuthorizationCache> _mockCache;
    private readonly Mock<ILogger<CollectionAuthorizationHandler>> _mockLogger;
    private readonly Mock<IOptionsMonitor<ResourceAuthorizationOptions>> _mockOptions;
    private readonly ResourceAuthorizationMetrics _metrics;
    private readonly CollectionAuthorizationHandler _handler;

    public CollectionAuthorizationHandlerTests()
    {
        _mockCache = new Mock<IResourceAuthorizationCache>();
        _mockLogger = new Mock<ILogger<CollectionAuthorizationHandler>>();
        _mockOptions = new Mock<IOptionsMonitor<ResourceAuthorizationOptions>>();

        // Create a real metrics instance with a mock meter factory
        var mockMeterFactory = new Mock<IMeterFactory>();
        mockMeterFactory.Setup(x => x.Create(It.IsAny<MeterOptions>())).Returns(new Meter("TestMeter"));
        _metrics = new ResourceAuthorizationMetrics(mockMeterFactory.Object);

        // Setup default options
        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = true,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        _handler = new CollectionAuthorizationHandler(
            _mockCache.Object,
            _metrics,
            _mockLogger.Object,
            _mockOptions.Object);
    }

    [Fact]
    public async Task AuthorizeAsync_WithReadAccess_Succeeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, "Reader")
        }, "TestAuth"));

        var policy = new ResourcePolicy
        {
            Id = "read-policy",
            ResourceType = "collection",
            ResourcePattern = "collection-1",
            AllowedOperations = new List<string> { "Read" },
            Roles = new List<string> { "Reader" },
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
        var result = await _handler.AuthorizeAsync(user, "collection", "collection-1", "Read");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithWriteAccessDenied_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456"),
            new Claim(ClaimTypes.Role, "Reader") // Reader role, not allowed to write
        }, "TestAuth"));

        // No write policy for Reader role
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
        var result = await _handler.AuthorizeAsync(user, "collection", "collection-2", "Write");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthorizeAsync_WithPublicCollection_AllowsAccess()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-789")
        }, "TestAuth"));

        var policy = new ResourcePolicy
        {
            Id = "public-policy",
            ResourceType = "collection",
            ResourcePattern = "public-collection",
            AllowedOperations = new List<string> { "Read" },
            Roles = new List<string>(), // No specific roles, applies to everyone
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
        var result = await _handler.AuthorizeAsync(user, "collection", "public-collection", "Read");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Editor", true)]
    [InlineData("Viewer", false)]
    public async Task AuthorizeAsync_WithDifferentRoles_ChecksPermissions(string role, bool shouldSucceed)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-role-test"),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth"));

        var policy = new ResourcePolicy
        {
            Id = "write-policy",
            ResourceType = "collection",
            ResourcePattern = "collection-3",
            AllowedOperations = new List<string> { "Write" },
            Roles = new List<string> { "Admin", "Editor" }, // Only Admin and Editor can write
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
        var result = await _handler.AuthorizeAsync(user, "collection", "collection-3", "Write");

        // Assert
        result.Succeeded.Should().Be(shouldSucceed);
    }

    [Fact]
    public async Task AuthorizeAsync_WithCachedResult_ReturnsCachedValue()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-cache"),
            new Claim(ClaimTypes.Role, "Reader")
        }, "TestAuth"));

        var cachedResult = ResourceAuthorizationResult.Success();
        _mockCache.Setup(x => x.TryGet(It.IsAny<string>(), out cachedResult)).Returns(true);

        // Act
        var result = await _handler.AuthorizeAsync(user, "collection", "collection-cached", "Read");

        // Assert
        result.Succeeded.Should().BeTrue();
        _mockCache.Verify(x => x.TryGet(It.IsAny<string>(), out cachedResult), Times.Once);
    }

    [Fact]
    public async Task AuthorizeAsync_WithWrongResourceType_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123")
        }, "TestAuth"));

        // Act
        var result = await _handler.AuthorizeAsync(user, "layer", "layer-1", "Read");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("not supported");
    }

    [Fact]
    public async Task AuthorizeAsync_WithDisabledAuthorization_AllowsAccess()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456")
        }, "TestAuth"));

        _mockOptions.Setup(x => x.CurrentValue).Returns(new ResourceAuthorizationOptions
        {
            Enabled = false,
            DefaultAction = DefaultAction.Deny,
            Policies = new List<ResourcePolicy>()
        });

        // Act
        var result = await _handler.AuthorizeAsync(user, "collection", "collection-any", "Write");

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ResourceType_ReturnsCollection()
    {
        // Assert
        _handler.ResourceType.Should().Be("collection");
    }
}
