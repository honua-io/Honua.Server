using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Host.ExceptionHandlers;
using Honua.Server.Observability.CorrelationId;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Host.Tests.ExceptionHandlers;

/// <summary>
/// Tests for correlation ID integration in exception handlers.
/// Ensures correlation IDs are included in all error responses and logs.
/// </summary>
public class CorrelationIdInExceptionHandlersTests
{
    private readonly GlobalExceptionHandler _handler;
    private readonly FakeHostEnvironment _environment;

    public CorrelationIdInExceptionHandlersTests()
    {
        _environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        _handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, _environment);
    }

    [Fact]
    public async Task TryHandleAsync_WithCorrelationIdInContext_IncludesItInResponse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var expectedCorrelationId = "correlation12345678901234567890";
        CorrelationIdUtilities.SetCorrelationId(context, expectedCorrelationId);

        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.Equal(expectedCorrelationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_WithoutCorrelationId_FallsBackToTraceIdentifier()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var expectedTraceId = context.TraceIdentifier;

        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.Equal(expectedTraceId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_IncludesTraceIdForBackwardCompatibility()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var correlationId = "correlation99999999999999999999";
        CorrelationIdUtilities.SetCorrelationId(context, correlationId);

        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        // Both correlationId and traceId should be present
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.True(problemDetails.Extensions.ContainsKey("traceId"));
        Assert.Equal(correlationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
        Assert.Equal(context.TraceIdentifier, problemDetails.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_DifferentExceptionTypes_AllIncludeCorrelationId()
    {
        // Test ArgumentException
        await VerifyCorrelationIdInException(new ArgumentException("Invalid argument"));

        // Test UnauthorizedAccessException
        await VerifyCorrelationIdInException(new UnauthorizedAccessException());

        // Test ServiceUnavailableException
        await VerifyCorrelationIdInException(new ServiceUnavailableException("test-service", "Connection failed"));

        // Test ServiceThrottledException
        await VerifyCorrelationIdInException(new ServiceThrottledException("test-service", TimeSpan.FromSeconds(30)));

        // Test CircuitBreakerOpenException
        await VerifyCorrelationIdInException(new CircuitBreakerOpenException("test-service", TimeSpan.FromSeconds(60)));

        // Test generic exception
        await VerifyCorrelationIdInException(new InvalidOperationException("Test error"));
    }

    private async Task VerifyCorrelationIdInException(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var expectedCorrelationId = $"test{Guid.NewGuid().ToString("N")[..28]}";
        CorrelationIdUtilities.SetCorrelationId(context, expectedCorrelationId);

        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey),
            $"CorrelationId missing for exception type: {exception.GetType().Name}");
        Assert.Equal(expectedCorrelationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_TransientException_IncludesCorrelationIdAndTransientFlag()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var correlationId = "transient123456789012345678901";
        CorrelationIdUtilities.SetCorrelationId(context, correlationId);

        var exception = new ServiceUnavailableException("test-service", "Temporary failure");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.Equal(correlationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
        Assert.True(problemDetails.Extensions.ContainsKey("isTransient"));
        Assert.True((bool)problemDetails.Extensions["isTransient"]!);
    }

    [Fact]
    public async Task TryHandleAsync_DevelopmentMode_IncludesCorrelationIdWithDebugInfo()
    {
        // Arrange
        var devEnvironment = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var devHandler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, devEnvironment);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var correlationId = "development123456789012345678";
        CorrelationIdUtilities.SetCorrelationId(context, correlationId);

        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await devHandler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        // Should have correlation ID
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.Equal(correlationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
        // Should also have debug info in development
        Assert.True(problemDetails.Extensions.ContainsKey("exceptionType"));
        Assert.True(problemDetails.Extensions.ContainsKey("stackTrace"));
    }

    [Fact]
    public async Task TryHandleAsync_ProductionMode_IncludesCorrelationIdWithoutDebugInfo()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var correlationId = "production12345678901234567890";
        CorrelationIdUtilities.SetCorrelationId(context, correlationId);

        var exception = new InvalidOperationException("Internal error details");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        // Should have correlation ID
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.Equal(correlationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
        // Should NOT have debug info in production
        Assert.False(problemDetails.Extensions.ContainsKey("exceptionType"));
        Assert.False(problemDetails.Extensions.ContainsKey("stackTrace"));
    }

    [Fact]
    public async Task TryHandleAsync_W3CCorrelationId_PreservesFormat()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        // W3C trace-id format (32 hex chars)
        var w3cCorrelationId = "0af7651916cd43dd8448eb211c80319c";
        CorrelationIdUtilities.SetCorrelationId(context, w3cCorrelationId);

        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey(CorrelationIdConstants.ProblemDetailsExtensionKey));
        Assert.Equal(w3cCorrelationId, problemDetails.Extensions[CorrelationIdConstants.ProblemDetailsExtensionKey]?.ToString());
    }

    [Fact]
    public async Task TryHandleAsync_CorrelationIdConstant_MatchesActualKey()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var correlationId = "constant123456789012345678901";
        CorrelationIdUtilities.SetCorrelationId(context, correlationId);

        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        // Verify the constant is correct
        Assert.Equal("correlationId", CorrelationIdConstants.ProblemDetailsExtensionKey);
        Assert.True(problemDetails.Extensions.ContainsKey("correlationId"));
        Assert.Equal(correlationId, problemDetails.Extensions["correlationId"]?.ToString());
    }

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Honua.Server.Host.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
