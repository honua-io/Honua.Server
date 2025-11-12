// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Authorization;

public class CollectionAuthorizationHandlerTests
{
    private readonly Mock<IResourceAuthorizationService> _mockAuthService;
    private readonly CollectionAuthorizationHandler _handler;

    public CollectionAuthorizationHandlerTests()
    {
        _mockAuthService = new Mock<IResourceAuthorizationService>();
        _handler = new CollectionAuthorizationHandler(_mockAuthService.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithReadAccess_Succeeds()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Read");
        var resource = new CollectionResource { CollectionId = "collection-1" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessCollectionAsync("user-123", "collection-1", "Read"))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithWriteAccessDenied_Fails()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-456")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Write");
        var resource = new CollectionResource { CollectionId = "collection-2" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessCollectionAsync("user-456", "collection-2", "Write"))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithPublicCollection_AllowsAccess()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-789")
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Read");
        var resource = new CollectionResource { CollectionId = "public-collection", IsPublic = true };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessCollectionAsync("user-789", "public-collection", "Read"))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Editor", true)]
    [InlineData("Viewer", false)]
    public async Task HandleRequirementAsync_WithDifferentRoles_ChecksPermissions(string role, bool shouldSucceed)
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-role-test"),
            new Claim(ClaimTypes.Role, role)
        }, "TestAuth"));

        var requirement = new ResourceAccessRequirement("Write");
        var resource = new CollectionResource { CollectionId = "collection-3" };
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);

        _mockAuthService
            .Setup(x => x.CanAccessCollectionAsync("user-role-test", "collection-3", "Write"))
            .ReturnsAsync(shouldSucceed);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().Be(shouldSucceed);
    }
}

public class CollectionResource
{
    public string CollectionId { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
}
