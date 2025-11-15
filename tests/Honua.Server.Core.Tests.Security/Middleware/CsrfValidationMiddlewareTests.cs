// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Logging;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Middleware;

public sealed class CsrfValidationMiddlewareTests
{
    private readonly Mock<IAntiforgery> _mockAntiforgery;
    private readonly Mock<ILogger<CsrfValidationMiddleware>> _mockLogger;
    private readonly Mock<ISecurityAuditLogger> _mockAuditLogger;
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly DefaultHttpContext _httpContext;

    public CsrfValidationMiddlewareTests()
    {
        _mockAntiforgery = new Mock<IAntiforgery>();
        _mockLogger = new Mock<ILogger<CsrfValidationMiddleware>>();
        _mockAuditLogger = new Mock<ISecurityAuditLogger>();
        _mockNext = new Mock<RequestDelegate>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
    }

    [Fact]
    public async Task InvokeAsync_WhenProtectionDisabled_CallsNext()
    {
        // Arrange
        var options = new CsrfProtectionOptions { Enabled = false };
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("get")] // Test case insensitivity
    public async Task InvokeAsync_SafeMethod_SkipsValidation(string method)
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = method;

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/livez")]
    [InlineData("/readyz")]
    [InlineData("/metrics")]
    [InlineData("/swagger")]
    [InlineData("/stac/collections")]
    [InlineData("/ogc/records")]
    public async Task InvokeAsync_ExcludedPath_SkipsValidation(string path)
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = path;

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ApiKeyAuthenticated_SkipsValidation()
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Headers["X-API-Key"] = "test-api-key";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ValidToken_CallsNext()
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/data";

        _mockAntiforgery
            .Setup(a => a.ValidateRequestAsync(_httpContext))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_InvalidToken_Returns403()
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/data";
        _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "testuser") }));
        _httpContext.TraceIdentifier = "test-trace-id";

        _mockAntiforgery
            .Setup(a => a.ValidateRequestAsync(_httpContext))
            .ThrowsAsync(new AntiforgeryValidationException("Invalid token"));

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.Should().Be(403);
        _httpContext.Response.ContentType.Should().StartWith("application/json");

        _mockNext.Verify(n => n(_httpContext), Times.Never);
        _mockAuditLogger.Verify(
            a => a.LogSuspiciousActivity(
                "csrf_validation_failure",
                "testuser",
                "192.168.1.1",
                It.IsAny<string>()),
            Times.Once);

        // Read and verify response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<JsonElement>(responseBody);

        problemDetails.GetProperty("status").GetInt32().Should().Be(403);
        problemDetails.GetProperty("title").GetString().Should().Be("CSRF Token Validation Failed");
        problemDetails.GetProperty("traceId").GetString().Should().Be("test-trace-id");
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_Rethrows()
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/data";

        var expectedException = new InvalidOperationException("Unexpected error");
        _mockAntiforgery
            .Setup(a => a.ValidateRequestAsync(_httpContext))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(_httpContext));

        actualException.Should().BeSameAs(expectedException);
        _mockNext.Verify(n => n(_httpContext), Times.Never);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_StateChangingMethod_RequiresValidation(string method)
    {
        // Arrange
        var options = new CsrfProtectionOptions();
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = method;
        _httpContext.Request.Path = "/api/data";

        _mockAntiforgery
            .Setup(a => a.ValidateRequestAsync(_httpContext))
            .Returns(Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CustomExcludedPaths_Respected()
    {
        // Arrange
        var options = new CsrfProtectionOptions
        {
            ExcludedPaths = new[] { "/custom/path", "/another/path" }
        };
        var middleware = new CsrfValidationMiddleware(
            _mockNext.Object,
            _mockAntiforgery.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object,
            options);

        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/custom/path/subpath";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockAntiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }
}
