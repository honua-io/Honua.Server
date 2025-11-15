// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Integration.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    private readonly Mock<RequestDelegate> nextMock;
    private readonly Mock<IWebHostEnvironment> environmentMock;
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> loggerMock;
    private readonly SecurityHeadersOptions options;
    private readonly Mock<IOptions<SecurityHeadersOptions>> optionsMock;

    public SecurityHeadersMiddlewareTests()
    {
        this.nextMock = new Mock<RequestDelegate>();
        this.environmentMock = new Mock<IWebHostEnvironment>();
        this.loggerMock = new Mock<ILogger<SecurityHeadersMiddleware>>();
        this.options = new SecurityHeadersOptions { Enabled = true };
        this.optionsMock = new Mock<IOptions<SecurityHeadersOptions>>();
        this.optionsMock.Setup(o => o.Value).Returns(this.options);

        // Default to Production environment
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_SkipsHeaderGeneration()
    {
        // Arrange
        this.options.Enabled = false;
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        this.nextMock.Verify(next => next(context), Times.Once);
        context.Response.Headers.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCspNonceAndStoresInContext()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items.Should().ContainKey(SecurityHeadersMiddleware.CspNonceKey);
        var nonce = context.Items[SecurityHeadersMiddleware.CspNonceKey] as string;
        nonce.Should().NotBeNullOrEmpty();
        nonce!.Length.Should().BeGreaterThan(10); // Base64 encoded nonce should be reasonably long
    }

    [Fact]
    public async Task InvokeAsync_InProduction_SetsHstsWithPreload()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var middleware = CreateMiddleware();
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        var hsts = context.Response.Headers["Strict-Transport-Security"].ToString();
        hsts.Should().Contain("max-age=31536000");
        hsts.Should().Contain("includeSubDomains");
        hsts.Should().Contain("preload");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_SetsHstsWithoutPreload()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var middleware = CreateMiddleware();
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        var hsts = context.Response.Headers["Strict-Transport-Security"].ToString();
        hsts.Should().Contain("max-age=86400");
        hsts.Should().Contain("includeSubDomains");
        hsts.Should().NotContain("preload");
    }

    [Fact]
    public async Task InvokeAsync_WithHttpRequest_DoesNotSetHsts()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext(); // HTTP, not HTTPS
        context.Request.Scheme = "http";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_WithHstsDisabled_DoesNotSetHsts()
    {
        // Arrange
        this.options.EnableHsts = false;
        var middleware = CreateMiddleware();
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_WithHstsProductionOnly_OnlyInProduction()
    {
        // Arrange
        this.options.HstsProductionOnly = true;
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var middleware = CreateMiddleware();
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_SetsXContentTypeOptions()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_SetsXFrameOptions()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_SetsReferrerPolicy()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_SetsStrictCspWithNonce()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Content-Security-Policy");
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();

        csp.Should().Contain("script-src 'nonce-");
        csp.Should().Contain("'self'");
        csp.Should().Contain("'strict-dynamic'");
        csp.Should().NotContain("'unsafe-inline'");
        csp.Should().NotContain("'unsafe-eval'");
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("object-src 'none'");
        csp.Should().Contain("base-uri 'self'");
        csp.Should().Contain("form-action 'self'");
        csp.Should().Contain("frame-ancestors 'none'");
        csp.Should().Contain("upgrade-insecure-requests");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_AllowsUnsafeInlineAndEval()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        this.options.AllowUnsafeInlineInDevelopment = true;
        this.options.AllowUnsafeEvalInDevelopment = true;
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("'unsafe-inline'");
        csp.Should().Contain("'unsafe-eval'");
        csp.Should().NotContain("upgrade-insecure-requests");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomCsp_UsesCustomValue()
    {
        // Arrange
        this.options.ContentSecurityPolicy = "default-src 'self'; script-src 'nonce-{nonce}'";
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("script-src 'nonce-");
        csp.Should().NotContain("{nonce}"); // Placeholder should be replaced
    }

    [Fact]
    public async Task InvokeAsync_SetsPermissionsPolicy()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("Permissions-Policy");
        var permissionsPolicy = context.Response.Headers["Permissions-Policy"].ToString();

        permissionsPolicy.Should().Contain("accelerometer=()");
        permissionsPolicy.Should().Contain("camera=()");
        permissionsPolicy.Should().Contain("geolocation=()");
        permissionsPolicy.Should().Contain("microphone=()");
        permissionsPolicy.Should().Contain("payment=()");
    }

    [Fact]
    public async Task InvokeAsync_SetsXPermittedCrossDomainPolicies()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be("none");
    }

    [Fact]
    public async Task InvokeAsync_WithCrossOriginPolicies_SetsHeaders()
    {
        // Arrange
        this.options.CrossOriginEmbedderPolicy = "require-corp";
        this.options.CrossOriginOpenerPolicy = "same-origin";
        this.options.CrossOriginResourcePolicy = "same-origin";
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Cross-Origin-Embedder-Policy"].ToString().Should().Be("require-corp");
        context.Response.Headers["Cross-Origin-Opener-Policy"].ToString().Should().Be("same-origin");
        context.Response.Headers["Cross-Origin-Resource-Policy"].ToString().Should().Be("same-origin");
    }

    [Fact]
    public async Task InvokeAsync_WithRemoveServerHeaders_RemovesServerIdentification()
    {
        // Arrange
        this.options.RemoveServerHeaders = true;
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Pre-populate headers that should be removed
        context.Response.Headers["Server"] = "TestServer";
        context.Response.Headers["X-Powered-By"] = "ASP.NET";
        context.Response.Headers["X-AspNet-Version"] = "5.0";
        context.Response.Headers["X-AspNetMvc-Version"] = "5.0";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().NotContainKey("Server");
        context.Response.Headers.Should().NotContainKey("X-Powered-By");
        context.Response.Headers.Should().NotContainKey("X-AspNet-Version");
        context.Response.Headers.Should().NotContainKey("X-AspNetMvc-Version");
    }

    [Fact]
    public async Task InvokeAsync_DoesNotOverwriteExistingHeaders()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomHeaderValues_UsesCustomValues()
    {
        // Arrange
        this.options.XFrameOptions = "SAMEORIGIN";
        this.options.XContentTypeOptions = "custom-value";
        this.options.ReferrerPolicy = "no-referrer";
        this.options.XPermittedCrossDomainPolicies = "master-only";
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("custom-value");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be("master-only");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomStrictTransportSecurity_UsesCustomValue()
    {
        // Arrange
        this.options.StrictTransportSecurity = "max-age=3600";
        var middleware = CreateMiddleware();
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Be("max-age=3600");
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccurs_RethrowsException()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        var expectedException = new InvalidOperationException("Test exception");

        this.nextMock.Setup(n => n(It.IsAny<HttpContext>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesUniqueCspNoncePerRequest()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context1 = new DefaultHttpContext();
        var context2 = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        // Assert
        var nonce1 = context1.Items[SecurityHeadersMiddleware.CspNonceKey] as string;
        var nonce2 = context2.Items[SecurityHeadersMiddleware.CspNonceKey] as string;

        nonce1.Should().NotBe(nonce2);
    }

    [Fact]
    public async Task InvokeAsync_InProduction_IncludesStrictDynamicInCsp()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("'strict-dynamic'");
    }

    [Fact]
    public async Task InvokeAsync_AllowsStyleUnsafeInline()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("style-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public async Task InvokeAsync_SetsImageSourcePolicy()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("img-src 'self' data: https:");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_SetsRestrictiveFontSource()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("font-src 'self'");
        csp.Should().NotContain("font-src 'self' data:");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_AllowsDataFontSource()
    {
        // Arrange
        this.environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("font-src 'self' data:");
    }

    [Fact]
    public async Task InvokeAsync_SetsConnectSrcToSelf()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("connect-src 'self'");
    }

    private SecurityHeadersMiddleware CreateMiddleware()
    {
        return new SecurityHeadersMiddleware(
            this.nextMock.Object,
            this.environmentMock.Object,
            this.loggerMock.Object,
            this.optionsMock.Object);
    }

    private static DefaultHttpContext CreateHttpsContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        return context;
    }
}
