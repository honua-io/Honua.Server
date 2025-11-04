using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Middleware;

/// <summary>
/// Integration tests for RequestResponseLoggingMiddleware with sensitive data redaction.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class RequestResponseLoggingMiddlewareTests
{
    private readonly Mock<ILogger<RequestResponseLoggingMiddleware>> _mockLogger;
    private readonly List<LogEntry> _logEntries;
    private readonly RequestResponseLoggingOptions _options;

    public RequestResponseLoggingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<RequestResponseLoggingMiddleware>>();
        _logEntries = new List<LogEntry>();

        // Capture all log calls
        _mockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var message = formatter.GetType()
                    .GetMethod("Invoke")
                    .Invoke(formatter, new[] { state, exception }) as string;

                _logEntries.Add(new LogEntry
                {
                    LogLevel = logLevel,
                    Message = message,
                    State = state,
                    Exception = exception
                });
            }));

        _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _options = new RequestResponseLoggingOptions
        {
            LogRequests = true,
            LogResponses = true,
            LogHeaders = true,
            LogRequestBody = true,
            RedactionOptions = new SensitiveDataRedactionOptions()
        };
    }

    [Fact]
    public async Task InvokeAsync_QueryStringWithPassword_RedactsPassword()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/users";
        context.Request.QueryString = new QueryString("?username=john&password=secret123");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var logMessage = GetLogMessage(LogLevel.Information);
        Assert.NotNull(logMessage);
        Assert.Contains("username=john", logMessage);
        Assert.Contains("password=***REDACTED***", logMessage);
        Assert.DoesNotContain("secret123", logMessage);
    }

    [Fact]
    public async Task InvokeAsync_QueryStringWithApiKey_RedactsApiKey()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/data";
        context.Request.QueryString = new QueryString("?api_key=sk_test_12345&format=json");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var logMessage = GetLogMessage(LogLevel.Information);
        Assert.NotNull(logMessage);
        Assert.Contains("api_key=***REDACTED***", logMessage);
        Assert.Contains("format=json", logMessage);
        Assert.DoesNotContain("sk_test_12345", logMessage);
    }

    [Fact]
    public async Task InvokeAsync_QueryStringWithToken_RedactsToken()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/verify";
        context.Request.QueryString = new QueryString("?token=abc123xyz&redirect=dashboard");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var logMessage = GetLogMessage(LogLevel.Information);
        Assert.NotNull(logMessage);
        Assert.Contains("token=***REDACTED***", logMessage);
        Assert.Contains("redirect=dashboard", logMessage);
        Assert.DoesNotContain("abc123xyz", logMessage);
    }

    [Fact]
    public async Task InvokeAsync_AuthorizationHeader_RedactsHeader()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/secure";
        context.Request.Headers["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";
        context.Request.Headers["Content-Type"] = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var headerLog = GetLogMessage(LogLevel.Debug, "Request Headers");
        Assert.NotNull(headerLog);
        Assert.Contains("Authorization: ***REDACTED***", headerLog);
        Assert.Contains("Content-Type: application/json", headerLog);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", headerLog);
    }

    [Fact]
    public async Task InvokeAsync_ApiKeyHeader_RedactsHeader()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/data";
        context.Request.Headers["X-API-Key"] = "sk_live_1234567890abcdef";
        context.Request.Headers["Accept"] = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var headerLog = GetLogMessage(LogLevel.Debug, "Request Headers");
        Assert.NotNull(headerLog);
        Assert.Contains("X-API-Key: ***REDACTED***", headerLog);
        Assert.Contains("Accept: application/json", headerLog);
        Assert.DoesNotContain("sk_live_1234567890abcdef", headerLog);
    }

    [Fact]
    public async Task InvokeAsync_CookieHeader_RedactsHeader()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/profile";
        context.Request.Headers["Cookie"] = "session=abc123; user_id=456";
        context.Request.Headers["User-Agent"] = "Mozilla/5.0";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var headerLog = GetLogMessage(LogLevel.Debug, "Request Headers");
        Assert.NotNull(headerLog);
        Assert.Contains("Cookie: ***REDACTED***", headerLog);
        Assert.Contains("User-Agent: Mozilla/5.0", headerLog);
        Assert.DoesNotContain("session=abc123", headerLog);
    }

    [Fact]
    public async Task InvokeAsync_JsonBodyWithPassword_RedactsPassword()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var json = @"{""username"":""john"",""password"":""secret123""}";
        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/login";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var bodyLog = GetLogMessage(LogLevel.Debug, "Request Body");
        Assert.NotNull(bodyLog);
        Assert.Contains("\"username\":\"john\"", bodyLog.Replace(" ", ""));
        Assert.Contains("\"password\":\"***REDACTED***\"", bodyLog.Replace(" ", ""));
        Assert.DoesNotContain("secret123", bodyLog);
    }

    [Fact]
    public async Task InvokeAsync_JsonBodyWithApiKey_RedactsApiKey()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var json = @"{""apiKey"":""sk_live_12345"",""environment"":""production""}";
        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/config";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var bodyLog = GetLogMessage(LogLevel.Debug, "Request Body");
        Assert.NotNull(bodyLog);
        Assert.Contains("\"apiKey\":\"***REDACTED***\"", bodyLog.Replace(" ", ""));
        Assert.Contains("\"environment\":\"production\"", bodyLog.Replace(" ", ""));
        Assert.DoesNotContain("sk_live_12345", bodyLog);
    }

    [Fact]
    public async Task InvokeAsync_JsonBodyWithNestedSecrets_RedactsAll()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var json = @"{
            ""user"": {
                ""name"": ""John"",
                ""credentials"": {
                    ""password"": ""pass123"",
                    ""apiKey"": ""key456""
                }
            }
        }";
        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/users";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var bodyLog = GetLogMessage(LogLevel.Debug, "Request Body");
        Assert.NotNull(bodyLog);
        Assert.Contains("\"name\":\"John\"", bodyLog.Replace(" ", ""));
        Assert.Contains("\"password\":\"***REDACTED***\"", bodyLog.Replace(" ", ""));
        Assert.Contains("\"apiKey\":\"***REDACTED***\"", bodyLog.Replace(" ", ""));
        Assert.DoesNotContain("pass123", bodyLog);
        Assert.DoesNotContain("key456", bodyLog);
    }

    [Fact]
    public async Task InvokeAsync_MultipleHeadersAndQueryParams_RedactsOnlySensitive()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/action";
        context.Request.QueryString = new QueryString("?user=john&token=secret&page=1");
        context.Request.Headers["Authorization"] = "Bearer token123";
        context.Request.Headers["Content-Type"] = "application/json";
        context.Request.Headers["Accept"] = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var requestLog = GetLogMessage(LogLevel.Information);
        Assert.Contains("user=john", requestLog);
        Assert.Contains("token=***REDACTED***", requestLog);
        Assert.Contains("page=1", requestLog);

        var headerLog = GetLogMessage(LogLevel.Debug, "Request Headers");
        Assert.Contains("Authorization: ***REDACTED***", headerLog);
        Assert.Contains("Content-Type: application/json", headerLog);
        Assert.Contains("Accept: application/json", headerLog);
    }

    [Fact]
    public async Task InvokeAsync_RedactionDisabled_DoesNotRedact()
    {
        // Arrange
        var optionsNoRedaction = new RequestResponseLoggingOptions
        {
            LogRequests = true,
            LogHeaders = true,
            RedactionOptions = new SensitiveDataRedactionOptions
            {
                RedactHeaders = false,
                RedactQueryStrings = false,
                RedactJsonBodies = false
            }
        };

        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            optionsNoRedaction);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Request.QueryString = new QueryString("?password=secret123");
        context.Request.Headers["Authorization"] = "Bearer token123";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var requestLog = GetLogMessage(LogLevel.Information);
        Assert.Contains("password=secret123", requestLog);

        var headerLog = GetLogMessage(LogLevel.Debug, "Request Headers");
        Assert.Contains("Authorization: Bearer token123", headerLog);
    }

    [Fact]
    public async Task InvokeAsync_HealthCheckEndpoint_SkipsLogging()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/healthz";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Empty(_logEntries);
    }

    [Fact]
    public async Task InvokeAsync_NonJsonContent_SkipsBodyLogging()
    {
        // Arrange
        var middleware = new RequestResponseLoggingMiddleware(
            async _ => await Task.CompletedTask,
            _mockLogger.Object,
            _options);

        var context = CreateHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/upload";
        context.Request.ContentType = "multipart/form-data";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("binary data"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var bodyLog = GetLogMessage(LogLevel.Debug, "Request Body");
        Assert.Null(bodyLog);
    }

    private HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private string GetLogMessage(LogLevel level, string contains = null)
    {
        var entry = _logEntries.Find(e =>
            e.LogLevel == level &&
            (contains == null || (e.Message != null && e.Message.Contains(contains))));

        return entry?.Message;
    }

    private class LogEntry
    {
        public LogLevel LogLevel { get; set; }
        public string Message { get; set; }
        public object State { get; set; }
        public Exception Exception { get; set; }
    }
}
