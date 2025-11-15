// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Logging;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Integration.Tests.Middleware;

public class CsrfValidationMiddlewareTests
{
    private readonly Mock<RequestDelegate> nextMock;
    private readonly Mock<IAntiforgery> antiforgeryMock;
    private readonly Mock<ILogger<CsrfValidationMiddleware>> loggerMock;
    private readonly Mock<ISecurityAuditLogger> auditLoggerMock;
    private readonly CsrfProtectionOptions options;
    private readonly CsrfValidationMiddleware middleware;

    public CsrfValidationMiddlewareTests()
    {
        this.nextMock = new Mock<RequestDelegate>();
        this.antiforgeryMock = new Mock<IAntiforgery>();
        this.loggerMock = new Mock<ILogger<CsrfValidationMiddleware>>();
        this.auditLoggerMock = new Mock<ISecurityAuditLogger>();
        this.options = new CsrfProtectionOptions { Enabled = true };
        this.middleware = new CsrfValidationMiddleware(
            this.nextMock.Object,
            this.antiforgeryMock.Object,
            this.loggerMock.Object,
            this.auditLoggerMock.Object,
            this.options);
    }

    [Fact]
    public async Task InvokeAsync_WhenCsrfProtectionDisabled_SkipsValidation()
    {
        // Arrange
        this.options.Enabled = false;
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public async Task InvokeAsync_WithSafeMethods_SkipsValidation(string method)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/data";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/livez")]
    [InlineData("/readyz")]
    [InlineData("/metrics")]
    [InlineData("/swagger")]
    [InlineData("/api-docs")]
    [InlineData("/stac/collections")]
    [InlineData("/v1/stac/search")]
    [InlineData("/ogc/processes")]
    [InlineData("/records/items")]
    public async Task InvokeAsync_WithExcludedPaths_SkipsValidation(string path)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithApiKeyHeader_SkipsValidation()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";
        context.Request.Headers["X-API-Key"] = "test-api-key";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_WithStateChangingMethods_ValidatesCsrfToken(string method)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/data";

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .Returns(Task.CompletedTask);

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(context), Times.Once);
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenCsrfValidationSucceeds_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .Returns(Task.CompletedTask);

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenCsrfValidationFails_Returns403AndLogsSuspiciousActivity()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";
        context.Response.Body = new MemoryStream();

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .ThrowsAsync(new AntiforgeryValidationException("Invalid token"));

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().Be("application/problem+json");

        this.auditLoggerMock.Verify(
            a => a.LogSuspiciousActivity(
                "csrf_validation_failure",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);

        this.nextMock.Verify(next => next(context), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenCsrfValidationFails_ReturnsProblemDetailsJson()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace-id";

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .ThrowsAsync(new AntiforgeryValidationException("Invalid token"));

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(403);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        responseBody.Should().Contain("CSRF Token Validation Failed");
        responseBody.Should().Contain("/api/test");
        responseBody.Should().Contain("test-trace-id");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedErrorOccurs_ThrowsException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";

        var expectedException = new InvalidOperationException("Unexpected error");
        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.middleware.InvokeAsync(context));

        exception.Should().Be(expectedException);
        this.nextMock.Verify(next => next(context), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithoutApiKey_ValidatesToken()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .Returns(Task.CompletedTask);

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithCustomExcludedPath_SkipsValidation()
    {
        // Arrange
        var customOptions = new CsrfProtectionOptions
        {
            Enabled = true,
            ExcludedPaths = new[] { "/custom/path" }
        };

        var customMiddleware = new CsrfValidationMiddleware(
            this.nextMock.Object,
            this.antiforgeryMock.Object,
            this.loggerMock.Object,
            this.auditLoggerMock.Object,
            customOptions);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/custom/path/action";

        // Act
        await customMiddleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithAnonymousUser_StillValidatesToken()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .Returns(Task.CompletedTask);

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CaseInsensitiveMethods_AreHandledCorrectly()
    {
        // Arrange - Test lowercase method
        var context = new DefaultHttpContext();
        context.Request.Method = "get";
        context.Request.Path = "/api/data";

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert - Should skip validation for safe methods regardless of case
        this.antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WithRemoteIpAddress_LogsIpInAudit()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        context.Response.Body = new MemoryStream();

        this.antiforgeryMock.Setup(a => a.ValidateRequestAsync(context))
            .ThrowsAsync(new AntiforgeryValidationException("Invalid token"));

        // Act
        await this.middleware.InvokeAsync(context);

        // Assert
        this.auditLoggerMock.Verify(
            a => a.LogSuspiciousActivity(
                "csrf_validation_failure",
                It.IsAny<string>(),
                "192.168.1.100",
                It.IsAny<string>()),
            Times.Once);
    }
}
