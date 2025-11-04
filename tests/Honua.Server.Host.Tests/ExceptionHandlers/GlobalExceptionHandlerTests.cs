using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Host.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Host.Tests.ExceptionHandlers;

public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler;
    private readonly FakeHostEnvironment _environment;

    public GlobalExceptionHandlerTests()
    {
        _environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        _handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, _environment);
    }

    [Fact]
    public async Task TryHandleAsync_FeatureNotFoundException_Returns404()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new FeatureNotFoundException("test-feature-id");

        // Act
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        // Verify ProblemDetails response
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Resource Not Found", problemDetails.Title);
        Assert.NotNull(problemDetails.Extensions["traceId"]);
        Assert.NotNull(problemDetails.Extensions["timestamp"]);
    }

    [Fact]
    public async Task TryHandleAsync_ServiceUnavailableException_Returns503WithTransientFlag()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new ServiceUnavailableException("test-service", "Connection timeout");

        // Act
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);

        // Verify transient flag
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.Equal(503, problemDetails.Status);
        Assert.True(problemDetails.Extensions.ContainsKey("isTransient"));
        Assert.True((bool)problemDetails.Extensions["isTransient"]!);
    }

    [Fact]
    public async Task TryHandleAsync_ServiceThrottledException_AddsRetryAfterHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var retryAfter = TimeSpan.FromSeconds(30);
        var exception = new ServiceThrottledException("test-service", retryAfter);

        // Act
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.True(context.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal("30", context.Response.Headers["Retry-After"]);
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new ArgumentException("Invalid parameter");

        // Act
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Returns401()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new UnauthorizedAccessException();

        // Act
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_Returns500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new InvalidCastException("Unexpected error");

        // Act
        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_DevelopmentMode_IncludesDetailedInformation()
    {
        // Arrange
        var devEnvironment = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var devHandler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, devEnvironment);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new FeatureNotFoundException("test-feature");

        // Act
        await devHandler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey("exceptionType"));
        Assert.True(problemDetails.Extensions.ContainsKey("stackTrace"));
        Assert.NotNull(problemDetails.Detail); // Detailed message in dev
    }

    [Fact]
    public async Task TryHandleAsync_ProductionMode_DoesNotLeakDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("Internal database connection failed");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.False(problemDetails.Extensions.ContainsKey("exceptionType"));
        Assert.False(problemDetails.Extensions.ContainsKey("stackTrace"));
        // Detail should be generic, not the actual exception message
        Assert.NotEqual("Internal database connection failed", problemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_CircuitBreakerOpen_IncludesBreakDurationAndServiceName()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var breakDuration = TimeSpan.FromSeconds(30);
        var exception = new CircuitBreakerOpenException("external-api", breakDuration);

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);

        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Extensions.ContainsKey("breakDuration"));
        Assert.True(problemDetails.Extensions.ContainsKey("serviceName"));
        Assert.Equal(30.0, problemDetails.Extensions["breakDuration"]);
        Assert.Equal("external-api", problemDetails.Extensions["serviceName"]);
    }

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Honua.Server.Host.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
