// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Integration.Tests.Middleware;

public class SecurityPolicyMiddlewareTests
{
    private readonly Mock<RequestDelegate> nextMock;
    private readonly Mock<ILogger<SecurityPolicyMiddleware>> loggerMock;
    private readonly SecurityPolicyMiddleware middleware;

    public SecurityPolicyMiddlewareTests()
    {
        this.nextMock = new Mock<RequestDelegate>();
        this.loggerMock = new Mock<ILogger<SecurityPolicyMiddleware>>();
        this.middleware = new SecurityPolicyMiddleware(this.nextMock.Object, this.loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_WithNoEndpoint_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithAuthorizeAttribute_CallsNext()
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: true);

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithAllowAnonymousAttribute_CallsNext()
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAllowAnonymous: true);

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Theory]
    [InlineData("/admin/users")]
    [InlineData("/admin/settings")]
    [InlineData("/Admin/Dashboard")]
    public async Task InvokeAsync_WithAdminRouteWithoutAuthorization_Returns403(string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        this.nextMock.Verify(next => next(context), Times.Never);
    }

    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/config")]
    [InlineData("/API/ADMIN/settings")]
    public async Task InvokeAsync_WithApiAdminRouteWithoutAuthorization_Returns403(string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        this.nextMock.Verify(next => next(context), Times.Never);
    }

    [Theory]
    [InlineData("/control-plane/status")]
    [InlineData("/control-plane/health")]
    [InlineData("/Control-Plane/metrics")]
    public async Task InvokeAsync_WithControlPlaneRouteWithoutAuthorization_Returns403(string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        this.nextMock.Verify(next => next(context), Times.Never);
    }

    [Theory]
    [InlineData("POST", "/api/data")]
    [InlineData("PUT", "/api/users/1")]
    [InlineData("DELETE", "/api/items/5")]
    [InlineData("PATCH", "/api/settings")]
    public async Task InvokeAsync_WithMutationOperationWithoutAuthorization_Returns403(string method, string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        this.nextMock.Verify(next => next(context), Times.Never);
    }

    [Theory]
    [InlineData("POST", "/stac/collections")]
    [InlineData("PUT", "/records/items/123")]
    [InlineData("DELETE", "/ogc/processes/abc")]
    [InlineData("POST", "/v1/stac/search")]
    [InlineData("PATCH", "/v2/records/item")]
    public async Task InvokeAsync_WithAllowListedMutationRoutes_CallsNext(string method, string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = method;
        context.Request.Path = path;

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Theory]
    [InlineData("GET", "/api/data")]
    [InlineData("HEAD", "/api/users")]
    [InlineData("OPTIONS", "/api/items")]
    public async Task InvokeAsync_WithSafeMethodsWithoutAuthorization_CallsNext(string method, string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = method;
        context.Request.Path = path;

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithGetOnApiRouteWithoutAuthorization_LogsWarning()
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "GET";
        context.Request.Path = "/api/data";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Security policy warning")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithProtectedRouteWithoutAuthorization_LogsWarningWithDetails()
    {
        // Arrange
        var endpoint = new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(),
            displayName: "TestEndpoint");

        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Security Policy Violation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithForbiddenRoute_ReturnsProblemDetailsJson()
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace-id";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(403);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        responseBody.Should().Contain("Access Denied");
        responseBody.Should().Contain("/api/data");
        responseBody.Should().Contain("test-trace-id");
    }

    [Theory]
    [InlineData("/admin/test")]
    [InlineData("/api/admin/test")]
    [InlineData("/control-plane/test")]
    public async Task InvokeAsync_WithAuthorizationMetadata_AllowsProtectedRoutes(string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: true, hasAllowAnonymous: false);
        context.Request.Method = "POST";
        context.Request.Path = path;

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithNullEndpointDisplayName_HandlesGracefully()
    {
        // Arrange
        var endpoint = new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(),
            displayName: null);

        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Request.Method = "POST";
        context.Request.Path = "/admin/test";
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(403);
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("(unknown)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/home")]
    [InlineData("/about")]
    public async Task InvokeAsync_WithNonApiNonAdminRoutes_CallsNext(string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "GET";
        context.Request.Path = path;

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Theory]
    [InlineData("/v1/ogc/processes")]
    [InlineData("/V2/STAC/collections")]
    [InlineData("/v10/records/items")]
    public async Task InvokeAsync_WithVersionedAllowListedRoutes_CallsNext(string path)
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "POST";
        context.Request.Path = path;

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CaseInsensitivePathMatching_WorksCorrectly()
    {
        // Arrange
        var context = CreateContextWithEndpoint(hasAuthorize: false, hasAllowAnonymous: false);
        context.Request.Method = "GET";
        context.Request.Path = "/ADMIN/test";
        context.Response.Body = new MemoryStream();

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(403);
    }

    private static DefaultHttpContext CreateContextWithEndpoint(bool hasAuthorize = false, bool hasAllowAnonymous = false)
    {
        var metadata = new List<object>();

        if (hasAuthorize)
        {
            metadata.Add(new AuthorizeAttribute());
        }

        if (hasAllowAnonymous)
        {
            metadata.Add(new AllowAnonymousAttribute());
        }

        var endpoint = new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(metadata),
            displayName: "TestEndpoint");

        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);

        return context;
    }

    private class AllowAnonymousAttribute : Attribute, IAllowAnonymous
    {
    }

    private class AuthorizeAttribute : Attribute, IAuthorizeData
    {
        public string? Policy { get; set; }
        public string? Roles { get; set; }
        public string? AuthenticationSchemes { get; set; }
    }
}
