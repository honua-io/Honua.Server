// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses with timing and performance metrics.
/// Includes sensitive data redaction to prevent credential leakage.
/// </summary>
public sealed class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly RequestResponseLoggingOptions _options;
    private readonly SensitiveDataRedactor _redactor;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        RequestResponseLoggingOptions options)
    {
        _next = Guard.NotNull(next);
        _logger = Guard.NotNull(logger);
        _options = Guard.NotNull(options);
        _redactor = new SensitiveDataRedactor(options.RedactionOptions);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health checks and metrics endpoints to avoid noise
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;

        // Log request
        if (_options.LogRequests)
        {
            await LogRequestAsync(context, requestId).ConfigureAwait(false);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            if (_options.LogResponses)
            {
                LogResponse(context, requestId, stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        var user = context.User.Identity?.Name ?? "anonymous";

        // Redact sensitive query string parameters
        var queryString = _options.RedactionOptions.RedactQueryStrings
            ? _redactor.RedactQueryString(request.QueryString.ToString())
            : request.QueryString.ToString();

        _logger.LogInformation(
            "HTTP {Method} {Path}{QueryString} - User: {User} - RequestId: {RequestId} - IP: {RemoteIp}",
            request.Method,
            request.Path,
            queryString,
            user,
            requestId,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        // Log request headers if configured
        if (_options.LogHeaders && _logger.IsEnabled(LogLevel.Debug))
        {
            var headers = new StringBuilder();
            foreach (var (key, value) in request.Headers)
            {
                // Redact sensitive headers
                if (_options.RedactionOptions.RedactHeaders && _redactor.IsSensitiveField(key))
                {
                    headers.AppendLine($"  {key}: ***REDACTED***");
                }
                else
                {
                    headers.AppendLine($"  {key}: {value}");
                }
            }

            _logger.LogDebug("Request Headers - RequestId: {RequestId}\n{Headers}", requestId, headers.ToString());
        }

        // Log request body if configured (JSON only)
        if (_options.LogRequestBody && _logger.IsEnabled(LogLevel.Debug))
        {
            await LogRequestBodyAsync(context, requestId).ConfigureAwait(false);
        }
    }

    private async Task LogRequestBodyAsync(HttpContext context, string requestId)
    {
        var request = context.Request;

        // Only log if it's JSON content
        if (!request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? true)
            return;

        if (_options.MaxRequestBodyLogSize <= 0)
        {
            return;
        }

        var bufferLimit = (int)Math.Min(_options.MaxRequestBodyLogSize, int.MaxValue);

        // Enable buffering to allow multiple reads
        request.EnableBuffering(bufferThreshold: 1024 * 16, bufferLimit: bufferLimit);

        var truncated = false;
        StringBuilder? builder = null;
        char[]? rented = null;
        try
        {
            using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            builder = new StringBuilder();
            rented = ArrayPool<char>.Shared.Rent(4096);

            while (true)
            {
                var memory = rented.AsMemory(0, 4096);
                int read;
                try
                {
                    read = await reader.ReadAsync(memory).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    truncated = true;
                    break;
                }
                catch (InvalidOperationException)
                {
                    truncated = true;
                    break;
                }

                if (read == 0)
                {
                    break;
                }

                builder.Append(memory.Span.Slice(0, read));
            }

            request.Body.Position = 0;

            if (builder.Length > 0)
            {
                var body = builder.ToString();

                if (_options.RedactionOptions.RedactJsonBodies)
                {
                    body = _redactor.RedactJson(body);
                }

                if (truncated)
                {
                    body += "\n[Body truncated due to logging size limit]";
                }

                _logger.LogDebug("Request Body - RequestId: {RequestId}\n{Body}", requestId, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for logging - RequestId: {RequestId}", requestId);
            // Reset position on error
            if (request.Body.CanSeek)
                request.Body.Position = 0;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private void LogResponse(HttpContext context, string requestId, long elapsedMs)
    {
        var response = context.Response;

        var logLevel = response.StatusCode >= 500 ? LogLevel.Error :
                      response.StatusCode >= 400 ? LogLevel.Warning :
                      LogLevel.Information;

        _logger.Log(
            logLevel,
            "HTTP {Method} {Path} - Status: {StatusCode} - Duration: {ElapsedMs}ms - RequestId: {RequestId}",
            context.Request.Method,
            context.Request.Path,
            response.StatusCode,
            elapsedMs,
            requestId);

        // Log slow requests
        if (elapsedMs > _options.SlowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected - {Method} {Path} - Duration: {ElapsedMs}ms - RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                elapsedMs,
                requestId);
        }

        // Log response headers if configured
        if (_options.LogHeaders && _logger.IsEnabled(LogLevel.Debug))
        {
            var headers = new StringBuilder();
            foreach (var (key, value) in response.Headers)
            {
                headers.AppendLine($"  {key}: {value}");
            }

            _logger.LogDebug("Response Headers - RequestId: {RequestId}\n{Headers}", requestId, headers.ToString());
        }
    }

    private bool ShouldSkipLogging(PathString path)
    {
        // Skip health checks and metrics to avoid log noise
        return path.StartsWithSegments("/healthz") ||
               path.StartsWithSegments("/metrics") ||
               path.StartsWithSegments("/favicon.ico");
    }
}

/// <summary>
/// Configuration options for request/response logging middleware.
/// </summary>
public sealed class RequestResponseLoggingOptions
{
    /// <summary>
    /// Enable logging of HTTP requests (default: true).
    /// </summary>
    public bool LogRequests { get; set; } = true;

    /// <summary>
    /// Enable logging of HTTP responses (default: true).
    /// </summary>
    public bool LogResponses { get; set; } = true;

    /// <summary>
    /// Enable logging of request/response headers (default: false).
    /// Only logged at Debug level to avoid excessive logging.
    /// </summary>
    public bool LogHeaders { get; set; } = false;

    /// <summary>
    /// Enable logging of request body (default: false).
    /// Only logged at Debug level and only for JSON content.
    /// </summary>
    public bool LogRequestBody { get; set; } = false;

    /// <summary>
    /// Maximum number of bytes to buffer and log for request bodies (default: 256KB).
    /// </summary>
    public long MaxRequestBodyLogSize { get; set; } = ApiLimitsAndConstants.MaxRequestBodyLogBytes;

    /// <summary>
    /// Threshold in milliseconds for logging slow requests (default: 5000ms).
    /// Requests exceeding this threshold will be logged as warnings.
    /// </summary>
    public long SlowRequestThresholdMs { get; set; } = 5000;

    /// <summary>
    /// Options for sensitive data redaction.
    /// </summary>
    public SensitiveDataRedactionOptions RedactionOptions { get; set; } = new();
}

/// <summary>
/// Extension methods for registering request/response logging middleware.
/// </summary>
public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLogging(
        this IApplicationBuilder app,
        Action<RequestResponseLoggingOptions>? configure = null)
    {
        var options = new RequestResponseLoggingOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<RequestResponseLoggingMiddleware>(options);
    }
}
