using System.Text;
using FluentAssertions;
using Honua.Server.AlertReceiver.Configuration;
using Honua.Server.AlertReceiver.Middleware;
using Honua.Server.AlertReceiver.Security;
using Honua.Server.AlertReceiver.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Honua.Server.AlertReceiver.Tests.Middleware;

/// <summary>
/// Comprehensive tests for WebhookSignatureMiddleware.
/// Tests HTTP method tampering bypass prevention, signature validation, and security features.
/// </summary>
[Trait("Category", "Unit")]
public class WebhookSignatureMiddlewareTests
{
    private readonly Mock<ILogger<WebhookSignatureMiddleware>> _mockLogger;
    private readonly Mock<IWebhookSignatureValidator> _mockValidator;
    private readonly Mock<IWebhookSecurityMetrics> _mockMetrics;
    private readonly WebhookSecurityOptions _options;
    private readonly IOptions<WebhookSecurityOptions> _optionsWrapper;
    private const string TestSecret = "test-secret-key-for-webhook-validation-minimum-64-chars-required";

    public WebhookSignatureMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<WebhookSignatureMiddleware>>();
        _mockValidator = new Mock<IWebhookSignatureValidator>();
        _mockMetrics = new Mock<IWebhookSecurityMetrics>();

        _options = new WebhookSecurityOptions
        {
            RequireSignature = true,
            SignatureHeaderName = "X-Hub-Signature-256",
            SharedSecret = TestSecret,
            MaxPayloadSize = 1_048_576,
            AllowInsecureHttp = false,
            AllowedHttpMethods = new List<string> { "POST" },
            RejectUnknownMethods = true,
            MaxWebhookAge = 0 // Disable timestamp validation for most tests
        };

        _optionsWrapper = Options.Create(_options);
    }

    #region HTTP Method Tampering Prevention Tests

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("TRACE")]
    public async Task InvokeAsync_AllHttpMethods_RejectedWhenNotInAllowlist(string httpMethod)
    {
        // Arrange
        var context = CreateHttpContext(httpMethod, validSignature: true);
        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status405MethodNotAllowed);
        _mockMetrics.Verify(m => m.RecordMethodRejection(httpMethod, "not_in_allowlist"), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_PostMethod_AllowedWhenInAllowlist()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: true);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _mockMetrics.Verify(m => m.RecordValidationAttempt("POST", true), Times.Once);
    }

    [Theory]
    [InlineData("POST", "PUT")]
    [InlineData("POST", "PUT", "PATCH")]
    [InlineData("POST", "PUT", "PATCH", "DELETE")]
    public async Task InvokeAsync_MultipleAllowedMethods_AcceptsConfiguredMethods(params string[] allowedMethods)
    {
        // Arrange
        _options.AllowedHttpMethods = allowedMethods.ToList();
        var allPassed = true;

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        foreach (var method in allowedMethods)
        {
            var context = CreateHttpContext(method, validSignature: true);
            var nextInvoked = false;
            var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

            await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

            if (!nextInvoked || context.Response.StatusCode != StatusCodes.Status200OK)
            {
                allPassed = false;
                break;
            }
        }

        allPassed.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_GetMethodWithValidSignature_StillRejectedWhenNotInAllowlist()
    {
        // Arrange - Even with valid signature, GET should be rejected if not in allowlist
        var context = CreateHttpContext("GET", validSignature: true);
        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status405MethodNotAllowed);
        // Signature validation should NOT even be attempted for rejected methods
        _mockValidator.Verify(
            v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RejectUnknownMethodsFalse_AllowsAnyMethodWithValidSignature()
    {
        // Arrange
        _options.RejectUnknownMethods = false;
        _options.AllowedHttpMethods = new List<string> { "POST" };

        var context = CreateHttpContext("GET", validSignature: true);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    #endregion

    #region Signature Validation Tests

    [Fact]
    public async Task InvokeAsync_ValidSignature_ProceedsToNextMiddleware()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: true);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        _mockMetrics.Verify(m => m.RecordValidationAttempt("POST", true), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_InvalidSignature_Returns401()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: false);
        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockMetrics.Verify(m => m.RecordValidationAttempt("POST", false), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_MultipleSecrets_TriesAllSecrets()
    {
        // Arrange
        _options.AdditionalSecrets = new List<string>
        {
            "secret2-additional-key-for-webhook-validation-minimum-64-chars-ok",
            "secret3-additional-key-for-webhook-validation-minimum-64-chars-ok"
        };
        var context = CreateHttpContext("POST", validSignature: true);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        var callCount = 0;
        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 2; // Second secret succeeds
            });

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        _mockValidator.Verify(
            v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _mockMetrics.Verify(m => m.RecordSecretRotation(3), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_NoSecretsConfigured_Returns500()
    {
        // Arrange
        _options.SharedSecret = null;
        _options.AdditionalSecrets = new List<string>();
        var context = CreateHttpContext("POST", validSignature: false);
        var middleware = CreateMiddleware(invokeNext: false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region HTTPS Enforcement Tests

    [Fact]
    public async Task InvokeAsync_HttpRequest_Returns403WhenHttpsRequired()
    {
        // Arrange
        _options.AllowInsecureHttp = false;
        var context = CreateHttpContext("POST", validSignature: true, isHttps: false);
        var middleware = CreateMiddleware(invokeNext: false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _mockMetrics.Verify(m => m.RecordHttpsViolation(), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_HttpRequest_AllowedWhenInsecureHttpEnabled()
    {
        // Arrange
        _options.AllowInsecureHttp = true;
        var context = CreateHttpContext("POST", validSignature: true, isHttps: false);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        _mockMetrics.Verify(m => m.RecordHttpsViolation(), Times.Never);
    }

    #endregion

    #region Timestamp Validation Tests

    [Fact]
    public async Task InvokeAsync_ExpiredTimestamp_Returns401()
    {
        // Arrange
        _options.MaxWebhookAge = 300; // 5 minutes
        var context = CreateHttpContext("POST", validSignature: true);

        // Add expired timestamp (10 minutes ago)
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        context.Request.Headers[_options.TimestampHeaderName] = expiredTimestamp.ToString();

        var middleware = CreateMiddleware(invokeNext: false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockMetrics.Verify(
            m => m.RecordTimestampValidationFailure(It.Is<string>(s => s.Contains("too old"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ValidTimestamp_ProceedsToValidation()
    {
        // Arrange
        _options.MaxWebhookAge = 300; // 5 minutes
        var context = CreateHttpContext("POST", validSignature: true);

        // Add valid timestamp (1 minute ago)
        var validTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        context.Request.Headers[_options.TimestampHeaderName] = validTimestamp.ToString();

        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        _mockMetrics.Verify(m => m.RecordTimestampValidationFailure(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_FutureTimestamp_Returns401()
    {
        // Arrange
        _options.MaxWebhookAge = 300;
        var context = CreateHttpContext("POST", validSignature: true);

        // Add future timestamp (2 minutes in future, beyond allowed skew)
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds();
        context.Request.Headers[_options.TimestampHeaderName] = futureTimestamp.ToString();

        var middleware = CreateMiddleware(invokeNext: false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockMetrics.Verify(
            m => m.RecordTimestampValidationFailure(It.Is<string>(s => s.Contains("future"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_DisabledTimestampValidation_SkipsTimestampCheck()
    {
        // Arrange
        _options.MaxWebhookAge = 0; // Disabled
        var context = CreateHttpContext("POST", validSignature: true);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        _mockMetrics.Verify(m => m.RecordTimestampValidationFailure(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Signature Disabled Tests

    [Fact]
    public async Task InvokeAsync_SignatureDisabled_SkipsAllValidation()
    {
        // Arrange
        _options.RequireSignature = false;
        var context = CreateHttpContext("GET", validSignature: false); // GET with no signature
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        nextInvoked.Should().BeTrue();
        _mockValidator.Verify(
            v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockMetrics.Verify(m => m.RecordValidationAttempt(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public async Task InvokeAsync_NullMetrics_DoesNotThrow()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: true);
        var nextInvoked = false;
        var middleware = CreateMiddleware(() => { nextInvoked = true; return Task.CompletedTask; });

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, metrics: null);

        // Assert
        nextInvoked.Should().BeTrue();
    }

    #endregion

    #region Header Redaction Tests

    [Fact]
    public async Task RecordFailedValidation_RedactsSensitiveHeaders_OnlyLogsAllowedHeaders()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers["Authorization"] = "Bearer secret-token";
        context.Request.Headers["X-Api-Key"] = "super-secret-api-key";
        context.Request.Headers["X-Hub-Signature-256"] = "sha256=signature";
        context.Request.Headers["Cookie"] = "session=abc123";
        context.Request.Headers["X-Auth-Token"] = "auth-token-value";
        context.Request.Headers["User-Agent"] = "TestClient/1.0";
        context.Request.Headers["Content-Type"] = "application/json";
        context.Request.Headers["Content-Length"] = "100";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("User-Agent") &&
                    v.ToString()!.Contains("Content-Type") &&
                    v.ToString()!.Contains("Content-Length") &&
                    !v.ToString()!.Contains("Bearer") &&
                    !v.ToString()!.Contains("secret-token") &&
                    !v.ToString()!.Contains("super-secret-api-key") &&
                    !v.ToString()!.Contains("sha256=signature") &&
                    !v.ToString()!.Contains("session=abc123") &&
                    !v.ToString()!.Contains("auth-token-value")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("X-Api-Key")]
    [InlineData("X-Auth-Key")]
    [InlineData("Custom-Key")]
    [InlineData("X-Auth-Token")]
    [InlineData("Bearer-Token")]
    [InlineData("X-Webhook-Secret")]
    [InlineData("X-Hub-Signature-256")]
    [InlineData("X-Webhook-Signature")]
    [InlineData("Custom-Signature")]
    [InlineData("Cookie")]
    [InlineData("Session")]
    [InlineData("X-Session")]
    [InlineData("Session-Id")]
    [InlineData("Proxy-Authorization")]
    [InlineData("X-Password")]
    [InlineData("WWW-Authenticate")]
    public async Task RecordFailedValidation_NeverLogsSensitiveHeader(string sensitiveHeaderName)
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers[sensitiveHeaderName] = "sensitive-value-that-should-not-be-logged";
        context.Request.Headers["User-Agent"] = "TestClient/1.0";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    !v.ToString()!.Contains("sensitive-value-that-should-not-be-logged")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordFailedValidation_CustomAllowedHeaders_LogsOnlyConfiguredHeaders()
    {
        // Arrange
        _options.AllowedLogHeaders = new List<string> { "User-Agent", "Accept" };

        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers["User-Agent"] = "TestClient/1.0";
        context.Request.Headers["Accept"] = "application/json";
        context.Request.Headers["Content-Type"] = "application/json"; // Not in allowlist
        context.Request.Headers["X-Api-Key"] = "secret-key";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("User-Agent") &&
                    v.ToString()!.Contains("Accept") &&
                    // Content-Type should NOT be logged since it's not in custom allowlist
                    !v.ToString()!.Contains("Content-Type") &&
                    !v.ToString()!.Contains("secret-key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordFailedValidation_EmptyAllowlist_UsesDefaultSafeHeaders()
    {
        // Arrange
        _options.AllowedLogHeaders = new List<string>(); // Empty allowlist

        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers["User-Agent"] = "TestClient/1.0";
        context.Request.Headers["Content-Type"] = "application/json";
        context.Request.Headers["Content-Length"] = "100";
        context.Request.Headers["X-Api-Key"] = "secret-key";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert - Should use default safe headers
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("User-Agent") &&
                    v.ToString()!.Contains("Content-Type") &&
                    v.ToString()!.Contains("Content-Length") &&
                    !v.ToString()!.Contains("secret-key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordFailedValidation_DefenseInDepth_ExcludesSensitiveEvenIfInAllowlist()
    {
        // Arrange - Try to sneak a sensitive header into the allowlist
        _options.AllowedLogHeaders = new List<string>
        {
            "User-Agent",
            "X-Api-Key", // Sensitive header in allowlist (should still be blocked)
            "Content-Type"
        };

        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers["User-Agent"] = "TestClient/1.0";
        context.Request.Headers["X-Api-Key"] = "secret-key-that-should-never-be-logged";
        context.Request.Headers["Content-Type"] = "application/json";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert - Defense in depth should block X-Api-Key even though it's in allowlist
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("User-Agent") &&
                    v.ToString()!.Contains("Content-Type") &&
                    !v.ToString()!.Contains("X-Api-Key") &&
                    !v.ToString()!.Contains("secret-key-that-should-never-be-logged")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordFailedValidation_UsesStructuredLogging_NotJsonSerialization()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers["User-Agent"] = "TestClient/1.0";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert - Verify structured logging with individual fields
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("EventType:") &&
                    v.ToString()!.Contains("Timestamp:") &&
                    v.ToString()!.Contains("RemoteIp:") &&
                    v.ToString()!.Contains("Path:") &&
                    v.ToString()!.Contains("Method:") &&
                    v.ToString()!.Contains("UserAgent:") &&
                    v.ToString()!.Contains("ContentType:") &&
                    v.ToString()!.Contains("ContentLength:") &&
                    v.ToString()!.Contains("SafeHeaders:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordRejectedMethod_UsesStructuredLogging_NotJsonSerialization()
    {
        // Arrange
        var context = CreateHttpContext("GET", validSignature: false);
        var middleware = CreateMiddleware(invokeNext: false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert - Verify structured logging with individual fields (no JSON serialization)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("EventType:") &&
                    v.ToString()!.Contains("Timestamp:") &&
                    v.ToString()!.Contains("RemoteIp:") &&
                    v.ToString()!.Contains("Path:") &&
                    v.ToString()!.Contains("Method:") &&
                    v.ToString()!.Contains("AllowedMethods:") &&
                    v.ToString()!.Contains("UserAgent:") &&
                    v.ToString()!.Contains("Referer:") &&
                    v.ToString()!.Contains("IsPotentialBrowserRequest:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordFailedValidation_CaseInsensitiveHeaderMatching()
    {
        // Arrange
        var context = CreateHttpContext("POST", validSignature: false);
        context.Request.Headers["x-api-key"] = "secret-key"; // lowercase
        context.Request.Headers["AUTHORIZATION"] = "Bearer token"; // uppercase
        context.Request.Headers["CoOkIe"] = "session=abc"; // mixed case
        context.Request.Headers["User-Agent"] = "TestClient/1.0";

        // Add service provider for options
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<WebhookSecurityOptions>)))
            .Returns(_optionsWrapper);
        context.RequestServices = serviceProvider.Object;

        var middleware = CreateMiddleware(invokeNext: false);

        _mockValidator
            .Setup(v => v.ValidateSignatureAsync(It.IsAny<HttpRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await middleware.InvokeAsync(context, _mockValidator.Object, _optionsWrapper, _mockMetrics.Object);

        // Assert - All sensitive headers should be blocked regardless of case
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    !v.ToString()!.Contains("secret-key") &&
                    !v.ToString()!.Contains("Bearer token") &&
                    !v.ToString()!.Contains("session=abc")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private WebhookSignatureMiddleware CreateMiddleware(Func<Task>? nextDelegate = null, bool invokeNext = true)
    {
        RequestDelegate next;

        if (nextDelegate != null)
        {
            next = _ => nextDelegate();
        }
        else if (invokeNext)
        {
            next = _ => Task.CompletedTask;
        }
        else
        {
            next = _ => throw new InvalidOperationException("Next middleware should not be invoked");
        }

        return new WebhookSignatureMiddleware(next, _mockLogger.Object);
    }

    private DefaultHttpContext CreateHttpContext(string method, bool validSignature, bool isHttps = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.IsHttps = isHttps;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"test\":\"data\"}"));
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        // Set up RequestServices with required options
        var services = new ServiceCollection();
        services.AddSingleton(_optionsWrapper);
        context.RequestServices = services.BuildServiceProvider();

        if (validSignature)
        {
            context.Request.Headers[_options.SignatureHeaderName] = "sha256=valid_signature_hash";
        }

        return context;
    }

    #endregion
}
