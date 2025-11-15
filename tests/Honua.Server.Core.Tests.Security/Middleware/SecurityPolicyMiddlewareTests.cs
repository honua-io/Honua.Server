// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Middleware;

public sealed class SecurityPolicyMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<SecurityPolicyMiddleware>> _mockLogger;
    private readonly DefaultHttpContext _httpContext;
    private readonly SecurityPolicyMiddleware _middleware;

    public SecurityPolicyMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<SecurityPolicyMiddleware>>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
        _middleware = new SecurityPolicyMiddleware(_mockNext.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullNext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SecurityPolicyMiddleware(null!, _mockLogger.Object));

        exception.ParamName.Should().Be("next");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SecurityPolicyMiddleware(_mockNext.Object, null!));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointIsNull_CallsNext()
    {
        // Arrange
        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/data";
        _httpContext.SetEndpoint(null);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointHasAuthorizeAttribute_CallsNext()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "TestEndpoint");

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/admin/users";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointHasAllowAnonymousAttribute_CallsNext()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "TestEndpoint");

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/admin/dashboard";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_AdminRouteWithoutAuthorizationMetadata_Returns403()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "AdminEndpoint");

        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = "/admin/dashboard";
        _httpContext.TraceIdentifier = "test-trace-123";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _httpContext.Response.ContentType.Should().StartWith("application/json");
        _mockNext.Verify(n => n(_httpContext), Times.Never);

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Security Policy Violation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseBody);

        problemDetails.GetProperty("status").GetInt32().Should().Be(403);
        problemDetails.GetProperty("title").GetString().Should().Be("Access Denied");
        problemDetails.GetProperty("traceId").GetString().Should().Be("test-trace-123");
        problemDetails.GetProperty("instance").GetString().Should().Be("/admin/dashboard");
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/users")]
    [InlineData("/admin/settings/security")]
    [InlineData("/Admin")]
    [InlineData("/ADMIN/users")]
    public async Task InvokeAsync_AdminRoutes_RequireAuthorization(string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Theory]
    [InlineData("/api/admin")]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/config")]
    [InlineData("/API/admin/settings")]
    [InlineData("/api/ADMIN/users")]
    public async Task InvokeAsync_ApiAdminRoutes_RequireAuthorization(string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Theory]
    [InlineData("/control-plane")]
    [InlineData("/control-plane/metrics")]
    [InlineData("/control-plane/health")]
    [InlineData("/Control-Plane/status")]
    [InlineData("/CONTROL-PLANE/info")]
    public async Task InvokeAsync_ControlPlaneRoutes_RequireAuthorization(string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_MutationMethodsWithoutAuthorization_Returns403(string method)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = "/api/data";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task InvokeAsync_SafeMethodsOnNonAdminRoutes_CallsNext(string method)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = "/api/data";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("POST", "/stac/search")]
    [InlineData("POST", "/stac/collections/test/items")]
    [InlineData("PUT", "/ogc/records/123")]
    [InlineData("DELETE", "/records/456")]
    [InlineData("PATCH", "/stac/items/789")]
    [InlineData("POST", "/STAC/search")]
    [InlineData("PUT", "/OGC/records/123")]
    public async Task InvokeAsync_MutationAllowListedRoutes_CallsNext(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("POST", "/v1/stac/search")]
    [InlineData("PUT", "/v2/ogc/records/123")]
    [InlineData("DELETE", "/v1/records/456")]
    [InlineData("PATCH", "/v3/stac/items/789")]
    [InlineData("POST", "/V1/stac/search")]
    [InlineData("PUT", "/v1/OGC/records/123")]
    public async Task InvokeAsync_VersionPrefixedMutationAllowListedRoutes_CallsNext(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/data/report")]
    [InlineData("GET", "/api/v1/items")]
    public async Task InvokeAsync_GetRequestsOnApiRoutesWithoutAuth_LogsWarning(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Security policy warning")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/status")]
    [InlineData("GET", "/public/data")]
    public async Task InvokeAsync_GetRequestsOnNonApiRoutes_DoesNotLogWarning(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);

        // Verify no warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_EmptyPath_CallsNext()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = "/";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData("POST", "/api/data")]
    [InlineData("PUT", "/api/users/123")]
    [InlineData("DELETE", "/api/items/456")]
    [InlineData("PATCH", "/api/config")]
    public async Task InvokeAsync_NonAllowListedMutations_Returns403(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_AdminRouteWithAuthorizeMixedCase_CallsNext()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(new AuthorizeAttribute { Roles = "Admin" }),
            "TestEndpoint");

        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = "/AdMiN/users";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithEndpointDisplayName_IncludesInLogMessage()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "MyCustomEndpoint");

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/admin/test";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MyCustomEndpoint")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("POST", "/v123/data")]
    [InlineData("PUT", "/version/data")]
    [InlineData("DELETE", "/v/data")]
    public async Task InvokeAsync_InvalidVersionPrefixes_RequireAuthorization(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Theory]
    [InlineData("POST", "/stac")]
    [InlineData("PUT", "/ogc")]
    [InlineData("DELETE", "/records")]
    public async Task InvokeAsync_AllowListedRootPaths_CallsNext(string method, string path)
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(),
            "TestEndpoint");

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_MultipleAuthorizationAttributes_CallsNext()
    {
        // Arrange
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(
                new AuthorizeAttribute(),
                new AuthorizeAttribute { Roles = "Admin" }),
            "TestEndpoint");

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/admin/secure";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_BothAuthorizeAndAllowAnonymous_CallsNext()
    {
        // Arrange - AllowAnonymous should take precedence
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(
                new AuthorizeAttribute(),
                new AllowAnonymousAttribute()),
            "TestEndpoint");

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/admin/public";
        _httpContext.SetEndpoint(endpoint);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _httpContext.Response.StatusCode.Should().Be(200);
    }
}
