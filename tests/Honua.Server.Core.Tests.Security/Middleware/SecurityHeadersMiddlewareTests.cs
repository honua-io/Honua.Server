// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
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

namespace Honua.Server.Core.Tests.Security.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> _mockLogger;
    private readonly DefaultHttpContext _httpContext;

    public SecurityHeadersMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
        _httpContext = new DefaultHttpContext();

        // Default to Production environment
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
    }

    [Fact]
    public void Constructor_WithNullNext_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions());

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SecurityHeadersMiddleware(null!, _mockEnvironment.Object, _mockLogger.Object, options));

        exception.ParamName.Should().Be("next");
    }

    [Fact]
    public void Constructor_WithNullEnvironment_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions());

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SecurityHeadersMiddleware(_mockNext.Object, null!, _mockLogger.Object, options));

        exception.ParamName.Should().Be("environment");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions());

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, null!, options));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, null!));

        exception.ParamName.Should().Be("options");
    }

    [Fact]
    public async Task InvokeAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions());
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_DoesNotAddHeadersAndCallsNext()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = false });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().BeEmpty();
        _mockNext.Verify(n => n(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_InProduction_AddsAllSecurityHeaders()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        _httpContext.Response.Headers.Should().ContainKey("X-Content-Type-Options");
        _httpContext.Response.Headers.Should().ContainKey("X-Frame-Options");
        _httpContext.Response.Headers.Should().ContainKey("Referrer-Policy");
        _httpContext.Response.Headers.Should().ContainKey("Content-Security-Policy");
        _httpContext.Response.Headers.Should().ContainKey("Permissions-Policy");
        _httpContext.Response.Headers.Should().ContainKey("X-Permitted-Cross-Domain-Policies");

        _mockNext.Verify(n => n(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_InProduction_AddsHstsWithPreload()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true, EnableHsts = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains; preload");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_AddsHstsWithoutPreload()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true, EnableHsts = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=86400; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_WhenHstsDisabled_DoesNotAddHstsHeader()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true, EnableHsts = false });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_OnHttpRequest_DoesNotAddHstsHeader()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true, EnableHsts = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "http";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_HstsProductionOnlyInDevelopment_DoesNotAddHsts()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            EnableHsts = true,
            HstsProductionOnly = true
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_HstsProductionOnlyInProduction_AddsHsts()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            EnableHsts = true,
            HstsProductionOnly = true
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().ContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_CustomStrictTransportSecurity_UsesCustomValue()
    {
        // Arrange
        var customHsts = "max-age=63072000; includeSubDomains; preload";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            EnableHsts = true,
            StrictTransportSecurity = customHsts
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        _httpContext.Request.Scheme = "https";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Strict-Transport-Security"].ToString().Should().Be(customHsts);
    }

    [Fact]
    public async Task InvokeAsync_AddsXContentTypeOptions()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_CustomXContentTypeOptions_UsesCustomValue()
    {
        // Arrange
        var customValue = "custom-value";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            XContentTypeOptions = customValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Content-Type-Options"].ToString().Should().Be(customValue);
    }

    [Fact]
    public async Task InvokeAsync_AddsXFrameOptions()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_CustomXFrameOptions_UsesCustomValue()
    {
        // Arrange
        var customValue = "SAMEORIGIN";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            XFrameOptions = customValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Frame-Options"].ToString().Should().Be(customValue);
    }

    [Fact]
    public async Task InvokeAsync_AddsReferrerPolicy()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_CustomReferrerPolicy_UsesCustomValue()
    {
        // Arrange
        var customValue = "no-referrer";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            ReferrerPolicy = customValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Referrer-Policy"].ToString().Should().Be(customValue);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCspNonceAndStoresInHttpContext()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Items.Should().ContainKey(SecurityHeadersMiddleware.CspNonceKey);
        var nonce = _httpContext.Items[SecurityHeadersMiddleware.CspNonceKey] as string;
        nonce.Should().NotBeNullOrWhiteSpace();
        nonce.Should().HaveLength(24); // Base64 encoding of 16 bytes = 24 characters
    }

    [Fact]
    public async Task InvokeAsync_InProduction_AddsCspWithNonceAndStrictDynamic()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("script-src");
        csp.Should().Contain("'nonce-");
        csp.Should().Contain("'self'");
        csp.Should().Contain("'strict-dynamic'");
        // Extract script-src directive to verify it doesn't have unsafe-inline
        var scriptSrcStart = csp.IndexOf("script-src");
        var scriptSrcEnd = csp.IndexOf(';', scriptSrcStart);
        var scriptSrcDirective = csp.Substring(scriptSrcStart, scriptSrcEnd - scriptSrcStart);
        scriptSrcDirective.Should().NotContain("'unsafe-inline'");
        scriptSrcDirective.Should().NotContain("'unsafe-eval'");
        csp.Should().Contain("upgrade-insecure-requests");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_AddsCspWithUnsafeInline()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            AllowUnsafeInlineInDevelopment = true,
            AllowUnsafeEvalInDevelopment = true
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("'unsafe-inline'");
        csp.Should().Contain("'unsafe-eval'");
        csp.Should().NotContain("'strict-dynamic'");
        csp.Should().NotContain("upgrade-insecure-requests");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopmentWithoutUnsafeInline_DoesNotAddUnsafeInline()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            AllowUnsafeInlineInDevelopment = false,
            AllowUnsafeEvalInDevelopment = false
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        // Extract script-src directive to verify it doesn't have unsafe-inline or unsafe-eval
        var scriptSrcStart = csp.IndexOf("script-src");
        var scriptSrcEnd = csp.IndexOf(';', scriptSrcStart);
        var scriptSrcDirective = csp.Substring(scriptSrcStart, scriptSrcEnd - scriptSrcStart);
        scriptSrcDirective.Should().NotContain("'unsafe-inline'");
        scriptSrcDirective.Should().NotContain("'unsafe-eval'");
    }

    [Fact]
    public async Task InvokeAsync_CustomCspWithNoncePlaceholder_ReplacesNonce()
    {
        // Arrange
        var customCsp = "default-src 'self'; script-src 'nonce-{nonce}'; style-src 'self'";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            ContentSecurityPolicy = customCsp
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().NotContain("{nonce}");
        csp.Should().Contain("'nonce-");
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("style-src 'self'");
    }

    [Fact]
    public async Task InvokeAsync_AddsPermissionsPolicy()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var permissionsPolicy = _httpContext.Response.Headers["Permissions-Policy"].ToString();
        permissionsPolicy.Should().Contain("camera=()");
        permissionsPolicy.Should().Contain("microphone=()");
        permissionsPolicy.Should().Contain("geolocation=()");
        permissionsPolicy.Should().Contain("payment=()");
    }

    [Fact]
    public async Task InvokeAsync_CustomPermissionsPolicy_UsesCustomValue()
    {
        // Arrange
        var customValue = "camera=(self), microphone=(self)";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            PermissionsPolicy = customValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Permissions-Policy"].ToString().Should().Be(customValue);
    }

    [Fact]
    public async Task InvokeAsync_AddsXPermittedCrossDomainPolicies()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be("none");
    }

    [Fact]
    public async Task InvokeAsync_CustomXPermittedCrossDomainPolicies_UsesCustomValue()
    {
        // Arrange
        var customValue = "master-only";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            XPermittedCrossDomainPolicies = customValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be(customValue);
    }

    [Fact]
    public async Task InvokeAsync_WithCrossOriginEmbedderPolicy_AddsHeader()
    {
        // Arrange
        var coepValue = "require-corp";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            CrossOriginEmbedderPolicy = coepValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Cross-Origin-Embedder-Policy"].ToString().Should().Be(coepValue);
    }

    [Fact]
    public async Task InvokeAsync_WithCrossOriginOpenerPolicy_AddsHeader()
    {
        // Arrange
        var coopValue = "same-origin";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            CrossOriginOpenerPolicy = coopValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Cross-Origin-Opener-Policy"].ToString().Should().Be(coopValue);
    }

    [Fact]
    public async Task InvokeAsync_WithCrossOriginResourcePolicy_AddsHeader()
    {
        // Arrange
        var corpValue = "same-origin";
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            CrossOriginResourcePolicy = corpValue
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Cross-Origin-Resource-Policy"].ToString().Should().Be(corpValue);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyCrossOriginPolicies_DoesNotAddHeaders()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            CrossOriginEmbedderPolicy = "",
            CrossOriginOpenerPolicy = "",
            CrossOriginResourcePolicy = ""
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Cross-Origin-Embedder-Policy");
        _httpContext.Response.Headers.Should().NotContainKey("Cross-Origin-Opener-Policy");
        _httpContext.Response.Headers.Should().NotContainKey("Cross-Origin-Resource-Policy");
    }

    [Fact]
    public async Task InvokeAsync_RemoveServerHeadersEnabled_RemovesServerHeaders()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            RemoveServerHeaders = true
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Add server headers before middleware runs
        _httpContext.Response.Headers["Server"] = "Kestrel";
        _httpContext.Response.Headers["X-Powered-By"] = "ASP.NET";
        _httpContext.Response.Headers["X-AspNet-Version"] = "5.0";
        _httpContext.Response.Headers["X-AspNetMvc-Version"] = "6.0";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("Server");
        _httpContext.Response.Headers.Should().NotContainKey("X-Powered-By");
        _httpContext.Response.Headers.Should().NotContainKey("X-AspNet-Version");
        _httpContext.Response.Headers.Should().NotContainKey("X-AspNetMvc-Version");
    }

    [Fact]
    public async Task InvokeAsync_RemoveServerHeadersDisabled_DoesNotRemoveServerHeaders()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            RemoveServerHeaders = false
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Add server headers before middleware runs
        _httpContext.Response.Headers["Server"] = "Kestrel";
        _httpContext.Response.Headers["X-Powered-By"] = "ASP.NET";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().ContainKey("Server");
        _httpContext.Response.Headers.Should().ContainKey("X-Powered-By");
    }

    [Fact]
    public async Task InvokeAsync_ExistingHeadersNotOverwritten()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        var existingCsp = "default-src 'self'; script-src 'self'";
        _httpContext.Response.Headers["Content-Security-Policy"] = existingCsp;
        _httpContext.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers["Content-Security-Policy"].ToString().Should().Be(existingCsp);
        _httpContext.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionInNext_LogsErrorAndRethrows()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        var expectedException = new InvalidOperationException("Test exception");
        _mockNext.Setup(n => n(It.IsAny<HttpContext>())).ThrowsAsync(expectedException);

        _httpContext.Request.Path = "/test/path";
        _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(_httpContext));

        actualException.Should().BeSameAs(expectedException);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in SecurityHeadersMiddleware")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_EmptyCustomHeaderValues_DoesNotAddHeaders()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions
        {
            Enabled = true,
            XContentTypeOptions = "",
            XFrameOptions = "",
            ReferrerPolicy = "",
            XPermittedCrossDomainPolicies = ""
        });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.Headers.Should().NotContainKey("X-Content-Type-Options");
        _httpContext.Response.Headers.Should().NotContainKey("X-Frame-Options");
        _httpContext.Response.Headers.Should().NotContainKey("Referrer-Policy");
        _httpContext.Response.Headers.Should().NotContainKey("X-Permitted-Cross-Domain-Policies");
    }

    [Fact]
    public async Task InvokeAsync_NoncesAreDifferentAcrossRequests()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        var httpContext1 = new DefaultHttpContext();
        var httpContext2 = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext1);
        await middleware.InvokeAsync(httpContext2);

        // Assert
        var nonce1 = httpContext1.Items[SecurityHeadersMiddleware.CspNonceKey] as string;
        var nonce2 = httpContext2.Items[SecurityHeadersMiddleware.CspNonceKey] as string;

        nonce1.Should().NotBeNullOrWhiteSpace();
        nonce2.Should().NotBeNullOrWhiteSpace();
        nonce1.Should().NotBe(nonce2);
    }

    [Fact]
    public async Task InvokeAsync_InProduction_CspContainsFrameAncestorsNone()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_CspContainsObjectSrcNone()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("object-src 'none'");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_CspContainsBaseUriSelf()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("base-uri 'self'");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_CspContainsFormActionSelf()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("form-action 'self'");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_FontSrcIncludesData()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("font-src 'self' data:");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_FontSrcDoesNotIncludeData()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("font-src 'self'");
        csp.Should().NotContain("font-src 'self' data:");
    }

    [Fact]
    public async Task InvokeAsync_CspAllowsUnsafeInlineForStyles()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        var csp = _httpContext.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("style-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        var options = Options.Create(new SecurityHeadersOptions { Enabled = true });
        var middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockEnvironment.Object, _mockLogger.Object, options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
    }
}
