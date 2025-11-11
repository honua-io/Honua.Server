// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Honua.Server.Host.Authentication;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="UserContext"/>.
/// </summary>
public class UserContextTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;

    public UserContextTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
    }

    [Fact]
    public void UserId_WithAuthenticatedUser_ReturnsNameIdentifierClaim()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "John Doe")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var userId = userContext.UserId;

        // Assert
        userId.Should().Be("user123");
    }

    [Fact]
    public void UserId_WithUnauthenticatedUser_ReturnsSystem()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var userId = userContext.UserId;

        // Assert
        userId.Should().Be("system");
    }

    [Fact]
    public void UserName_WithAuthenticatedUser_ReturnsNameClaim()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "John Doe"),
            new Claim(ClaimTypes.Email, "john@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var userName = userContext.UserName;

        // Assert
        userName.Should().Be("John Doe");
    }

    [Fact]
    public void UserName_WithLowercaseNameClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim("name", "Jane Smith") // lowercase claim type
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var userName = userContext.UserName;

        // Assert
        userName.Should().Be("Jane Smith");
    }

    [Fact]
    public void UserName_WithEmailOnly_ReturnsEmail()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Email, "user@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var userName = userContext.UserName;

        // Assert
        userName.Should().Be("user@example.com");
    }

    [Fact]
    public void UserName_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var userName = userContext.UserName;

        // Assert
        userName.Should().BeNull();
    }

    [Fact]
    public void SessionId_WithHeaderPresent_ReturnsHeaderValue()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var httpContext = CreateHttpContext(principal: null);
        httpContext.Request.Headers["X-Session-Id"] = sessionId.ToString();

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.SessionId;

        // Assert
        result.Should().Be(sessionId);
    }

    [Fact]
    public void SessionId_WithCorrelationIdHeader_ReturnsHeaderValue()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var httpContext = CreateHttpContext(principal: null);
        httpContext.Request.Headers["X-Correlation-Id"] = correlationId.ToString();

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.SessionId;

        // Assert
        result.Should().Be(correlationId);
    }

    [Fact]
    public void SessionId_WithoutHeader_GeneratesNewGuid()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.SessionId;

        // Assert
        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void SessionId_MultipleAccesses_ReturnsSameValue()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var sessionId1 = userContext.SessionId;
        var sessionId2 = userContext.SessionId;

        // Assert
        sessionId1.Should().Be(sessionId2);
    }

    [Fact]
    public void TenantId_WithTenantClaim_ReturnsGuid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim("tenant_id", tenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.TenantId;

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WithTenantIdCamelCase_ReturnsGuid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim("tenantId", tenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.TenantId;

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WithAzureAdTid_ReturnsGuid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim("tid", tenantId.ToString()) // Azure AD tenant ID claim
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.TenantId;

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_WithoutClaim_ReturnsNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.TenantId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsAuthenticated_WithAuthenticatedUser_ReturnsTrue()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user123") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.IsAuthenticated;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_WithUnauthenticatedUser_ReturnsFalse()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.IsAuthenticated;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IpAddress_WithXForwardedFor_ReturnsFirstIp()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        httpContext.Request.Headers["X-Forwarded-For"] = "192.168.1.100, 10.0.0.1";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.IpAddress;

        // Assert
        result.Should().Be("192.168.1.100");
    }

    [Fact]
    public void IpAddress_WithXRealIp_ReturnsValue()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        httpContext.Request.Headers["X-Real-IP"] = "192.168.1.200";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.IpAddress;

        // Assert
        result.Should().Be("192.168.1.200");
    }

    [Fact]
    public void IpAddress_WithRemoteIpAddress_ReturnsValue()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.IpAddress;

        // Assert
        result.Should().Be("10.20.30.40");
    }

    [Fact]
    public void UserAgent_WithHeader_ReturnsValue()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.UserAgent;

        // Assert
        result.Should().Be("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    [Fact]
    public void UserAgent_WithoutHeader_ReturnsNull()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.UserAgent;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void AuthenticationMethod_WithAuthenticatedUser_ReturnsScheme()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user123") };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.AuthenticationMethod;

        // Assert
        result.Should().Be("Bearer");
    }

    [Fact]
    public void AuthenticationMethod_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        var httpContext = CreateHttpContext(principal: null);
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act
        var result = userContext.AuthenticationMethod;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpContextAccessor_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new UserContext(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpContextAccessor");
    }

    [Fact]
    public void Properties_WithNullHttpContext_HandleGracefully()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act & Assert - should not throw, should return default values
        userContext.UserId.Should().Be("system");
        userContext.UserName.Should().BeNull();
        userContext.SessionId.Should().NotBe(Guid.Empty);
        userContext.TenantId.Should().BeNull();
        userContext.IsAuthenticated.Should().BeFalse();
        userContext.IpAddress.Should().BeNull();
        userContext.UserAgent.Should().BeNull();
        userContext.AuthenticationMethod.Should().BeNull();
    }

    [Fact]
    public void CachedValues_AreConsistentAcrossMultipleAccesses()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "John Doe")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = CreateHttpContext(principal);
        httpContext.Request.Headers["User-Agent"] = "TestAgent/1.0";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        var userContext = new UserContext(_mockHttpContextAccessor.Object);

        // Act - Access properties multiple times
        var userId1 = userContext.UserId;
        var userId2 = userContext.UserId;
        var sessionId1 = userContext.SessionId;
        var sessionId2 = userContext.SessionId;
        var ipAddress1 = userContext.IpAddress;
        var ipAddress2 = userContext.IpAddress;

        // Assert - Values should be consistent
        userId1.Should().Be(userId2);
        sessionId1.Should().Be(sessionId2);
        ipAddress1.Should().Be(ipAddress2);
    }

    /// <summary>
    /// Helper method to create a DefaultHttpContext with optional ClaimsPrincipal.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext(ClaimsPrincipal? principal)
    {
        var context = new DefaultHttpContext();
        if (principal != null)
        {
            context.User = principal;
        }
        return context;
    }
}
