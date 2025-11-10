// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Middleware;

/// <summary>
/// Comprehensive tests for SecurityHeadersMiddleware.
/// Tests configuration, header injection, environment-specific behavior, and security controls.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class SecurityHeadersMiddlewareTests
{
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> _mockLogger;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;

    public SecurityHeadersMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
    }

    [Fact]
    public async Task InvokeAsync_DefaultConfiguration_AddsAllSecurityHeaders()
    {
        // Arrange
        var options = CreateDefaultOptions();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.True(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
        Assert.True(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.True(context.Response.Headers.ContainsKey("Referrer-Policy"));
        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.True(context.Response.Headers.ContainsKey("Permissions-Policy"));
        Assert.True(context.Response.Headers.ContainsKey("X-Permitted-Cross-Domain-Policies"));
    }

    [Fact]
    public async Task InvokeAsync_ProductionEnvironment_AddsHstsWithPreload()
    {
        // Arrange
        var options = CreateDefaultOptions();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        var hstsValue = context.Response.Headers["Strict-Transport-Security"].ToString();
        Assert.Contains("max-age=31536000", hstsValue);
        Assert.Contains("includeSubDomains", hstsValue);
        Assert.Contains("preload", hstsValue);
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentEnvironment_AddsHstsWithoutPreload()
    {
        // Arrange
        var options = CreateDefaultOptions();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        var hstsValue = context.Response.Headers["Strict-Transport-Security"].ToString();
        Assert.Contains("max-age=86400", hstsValue);
        Assert.Contains("includeSubDomains", hstsValue);
        Assert.DoesNotContain("preload", hstsValue);
    }

    [Fact]
    public async Task InvokeAsync_HttpRequest_DoesNotAddHsts()
    {
        // Arrange
        var options = CreateDefaultOptions();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext(); // HTTP, not HTTPS

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_HstsDisabled_DoesNotAddHsts()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.EnableHsts = false;
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_HstsProductionOnly_DevelopmentEnvironment_DoesNotAddHsts()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.HstsProductionOnly = true;
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_CustomHstsValue_UsesConfiguredValue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.StrictTransportSecurity = "max-age=63072000; includeSubDomains; preload";
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal("max-age=63072000; includeSubDomains; preload",
            context.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_DefaultConfiguration_AddsCorrectXFrameOptions()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_CustomXFrameOptions_UsesConfiguredValue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.XFrameOptions = "SAMEORIGIN";
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_DefaultConfiguration_AddsCorrectXContentTypeOptions()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_DefaultConfiguration_AddsCorrectReferrerPolicy()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin",
            context.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_CustomReferrerPolicy_UsesConfiguredValue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.ReferrerPolicy = "no-referrer";
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_DefaultConfiguration_AddsPermissionsPolicy()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Permissions-Policy"));
        var policy = context.Response.Headers["Permissions-Policy"].ToString();
        Assert.Contains("camera=()", policy);
        Assert.Contains("microphone=()", policy);
        Assert.Contains("geolocation=()", policy);
    }

    [Fact]
    public async Task InvokeAsync_CustomPermissionsPolicy_UsesConfiguredValue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.PermissionsPolicy = "geolocation=(self), microphone=()";
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("geolocation=(self), microphone=()",
            context.Response.Headers["Permissions-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_DefaultConfiguration_AddsXPermittedCrossDomainPolicies()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Permitted-Cross-Domain-Policies"));
        Assert.Equal("none",
            context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_ProductionEnvironment_AddsStrictCsp()
    {
        // Arrange
        var options = CreateDefaultOptions();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'nonce-", csp);
        Assert.Contains("'strict-dynamic'", csp);
        Assert.DoesNotContain("'unsafe-inline'", csp.Replace("style-src", "")); // unsafe-inline only in style-src
        Assert.DoesNotContain("'unsafe-eval'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("upgrade-insecure-requests", csp);
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentEnvironment_AddsRelaxedCsp()
    {
        // Arrange
        var options = CreateDefaultOptions();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains("'unsafe-inline'", csp);
        Assert.Contains("'unsafe-eval'", csp);
        Assert.DoesNotContain("upgrade-insecure-requests", csp);
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentWithDisabledUnsafeInline_DoesNotAddUnsafeInline()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.AllowUnsafeInlineInDevelopment = false;
        options.AllowUnsafeEvalInDevelopment = false;
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        // Remove style-src from CSP before checking (style-src legitimately uses unsafe-inline)
        var scriptPart = csp.Substring(0, csp.IndexOf("style-src"));
        Assert.DoesNotContain("'unsafe-inline'", scriptPart);
        Assert.DoesNotContain("'unsafe-eval'", csp);
    }

    [Fact]
    public async Task InvokeAsync_CustomCspWithNoncePlaceholder_InjectsNonce()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.ContentSecurityPolicy = "default-src 'self'; script-src 'nonce-{nonce}' 'self'";
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains("script-src 'nonce-", csp);
        Assert.DoesNotContain("{nonce}", csp);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesUniqueNoncePerRequest()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context1 = CreateHttpContext();
        var context2 = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        // Assert
        var nonce1 = context1.Items[SecurityHeadersMiddleware.CspNonceKey] as string;
        var nonce2 = context2.Items[SecurityHeadersMiddleware.CspNonceKey] as string;

        Assert.NotNull(nonce1);
        Assert.NotNull(nonce2);
        Assert.NotEqual(nonce1, nonce2);
    }

    [Fact]
    public async Task InvokeAsync_StoresNonceInHttpContextItems()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Items.ContainsKey(SecurityHeadersMiddleware.CspNonceKey));
        var nonce = context.Items[SecurityHeadersMiddleware.CspNonceKey] as string;
        Assert.NotNull(nonce);
        Assert.NotEmpty(nonce);

        // Verify nonce is in CSP header
        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains($"'nonce-{nonce}'", csp);
    }

    [Fact]
    public async Task InvokeAsync_RemoveServerHeadersEnabled_RemovesServerHeaders()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.RemoveServerHeaders = true;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Pre-populate headers that would normally be added by ASP.NET
        context.Response.Headers["Server"] = "Kestrel";
        context.Response.Headers["X-Powered-By"] = "ASP.NET Core";
        context.Response.Headers["X-AspNet-Version"] = "5.0";
        context.Response.Headers["X-AspNetMvc-Version"] = "5.0";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Server"));
        Assert.False(context.Response.Headers.ContainsKey("X-Powered-By"));
        Assert.False(context.Response.Headers.ContainsKey("X-AspNet-Version"));
        Assert.False(context.Response.Headers.ContainsKey("X-AspNetMvc-Version"));
    }

    [Fact]
    public async Task InvokeAsync_RemoveServerHeadersDisabled_KeepsServerHeaders()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.RemoveServerHeaders = false;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        context.Response.Headers["Server"] = "Kestrel";
        context.Response.Headers["X-Powered-By"] = "ASP.NET Core";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Server"));
        Assert.True(context.Response.Headers.ContainsKey("X-Powered-By"));
    }

    [Fact]
    public async Task InvokeAsync_ExistingHeaders_DoesNotOverwrite()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Pre-set a custom CSP
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        // Act
        await middleware.InvokeAsync(context);

        // Assert - existing headers should not be overwritten
        Assert.Equal("default-src 'none'",
            context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Equal("SAMEORIGIN",
            context.Response.Headers["X-Frame-Options"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_MiddlewareDisabled_SkipsAllHeaders()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.Enabled = false;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpsContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.False(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
        Assert.False(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.False(context.Response.Headers.ContainsKey("Referrer-Policy"));
        Assert.False(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        Assert.False(context.Response.Headers.ContainsKey("Permissions-Policy"));
    }

    [Fact]
    public async Task InvokeAsync_EmptyHeaderValue_SkipsHeader()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.XFrameOptions = "";
        options.ReferrerPolicy = null;
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.False(context.Response.Headers.ContainsKey("Referrer-Policy"));
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();
        var nextCalled = false;

        var next = new RequestDelegate(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var customMiddleware = new SecurityHeadersMiddleware(
            next,
            _mockEnvironment.Object,
            _mockLogger.Object,
            Options.Create(options));

        // Act
        await customMiddleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionInNext_PropagatesException()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var context = CreateHttpContext();
        var expectedException = new InvalidOperationException("Test exception");

        var next = new RequestDelegate(_ => throw expectedException);

        var middleware = new SecurityHeadersMiddleware(
            next,
            _mockEnvironment.Object,
            _mockLogger.Object,
            Options.Create(options));

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));
        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task InvokeAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var middleware = CreateMiddleware(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => middleware.InvokeAsync(null!));
    }

    private SecurityHeadersOptions CreateDefaultOptions()
    {
        return new SecurityHeadersOptions
        {
            Enabled = true,
            EnableHsts = true,
            HstsProductionOnly = false,
            RemoveServerHeaders = true,
            AllowUnsafeInlineInDevelopment = true,
            AllowUnsafeEvalInDevelopment = true,
            StrictTransportSecurity = null,
            ContentSecurityPolicy = null,
            ReferrerPolicy = "strict-origin-when-cross-origin",
            PermissionsPolicy = null,
            XFrameOptions = "DENY",
            XContentTypeOptions = "nosniff",
            XPermittedCrossDomainPolicies = "none"
        };
    }

    private SecurityHeadersMiddleware CreateMiddleware(SecurityHeadersOptions options)
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        return new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            _mockEnvironment.Object,
            _mockLogger.Object,
            Options.Create(options));
    }

    private HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.IsHttps = false;
        return context;
    }

    private HttpContext CreateHttpsContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.IsHttps = true;
        return context;
    }
}
