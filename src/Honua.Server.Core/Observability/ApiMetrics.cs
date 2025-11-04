// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Observability;

/// <summary>
/// OpenTelemetry metrics for API operations with service and protocol dimensions.
/// Enables drilling down by service ID and API standard (WFS, WMS, OGC API Features, etc.).
/// </summary>
public interface IApiMetrics
{
    void RecordRequest(string apiProtocol, string? serviceId, string? layerId);
    void RecordRequestDuration(string apiProtocol, string? serviceId, string? layerId, TimeSpan duration, int statusCode);
    void RecordError(string apiProtocol, string? serviceId, string? layerId, string errorType);
    void RecordError(string apiProtocol, string? serviceId, string? layerId, Exception exception, string? additionalContext = null);
    void RecordFeatureCount(string apiProtocol, string? serviceId, string? layerId, long count);
    void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration);
    void RecordHttpError(string method, string endpoint, int statusCode, string errorType);
    void RecordRateLimitHit(string endpoint, string clientIp);
}

/// <summary>
/// Implementation of API metrics using OpenTelemetry.
/// </summary>
public sealed class ApiMetrics : IApiMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _errorCounter;
    private readonly Counter<long> _featureCounter;

    // HTTP-level metrics for comprehensive error tracking
    private readonly Histogram<double> _httpRequestDuration;
    private readonly Counter<long> _httpErrorCounter;
    private readonly Counter<long> _httpRequestCounter;

    // Slow request tracking
    private readonly Counter<long> _slowRequestCounter;

    // Rate limiting metrics
    private readonly Counter<long> _rateLimitCounter;

    private readonly ILogger<ApiMetrics>? _logger;

    public ApiMetrics(ILogger<ApiMetrics>? logger = null)
    {
        _logger = logger;
        _meter = new Meter("Honua.Server.Api", "1.0.0");

        _requestCounter = _meter.CreateCounter<long>(
            "honua.api.requests",
            unit: "{request}",
            description: "Number of API requests by protocol, service, and layer");

        _requestDuration = _meter.CreateHistogram<double>(
            "honua.api.request_duration",
            unit: "ms",
            description: "API request duration by protocol, service, and layer");

        _errorCounter = _meter.CreateCounter<long>(
            "honua.api.errors",
            unit: "{error}",
            description: "Number of API errors by protocol, service, layer, and error type");

        _featureCounter = _meter.CreateCounter<long>(
            "honua.api.features_returned",
            unit: "{feature}",
            description: "Number of features returned by protocol, service, and layer");

        // HTTP-level metrics with OpenTelemetry semantic conventions
        _httpRequestDuration = _meter.CreateHistogram<double>(
            "honua.http.request.duration",
            unit: "ms",
            description: "HTTP request duration by endpoint, method, and status code for calculating p95/p99/p99.9 latencies");

        _httpErrorCounter = _meter.CreateCounter<long>(
            "honua.http.errors.total",
            unit: "{error}",
            description: "HTTP errors by endpoint, method, status code, and error type");

        _httpRequestCounter = _meter.CreateCounter<long>(
            "honua.http.requests.total",
            unit: "{request}",
            description: "Total HTTP requests by endpoint, method, and status code");

        _slowRequestCounter = _meter.CreateCounter<long>(
            "honua.http.slow_requests.total",
            unit: "{request}",
            description: "Count of slow HTTP requests categorized by latency thresholds (>1s, >5s, >10s)");

        _rateLimitCounter = _meter.CreateCounter<long>(
            "honua.rate_limit.hits.total",
            unit: "{hit}",
            description: "Count of rate limit hits by endpoint and client IP");
    }

    public void RecordRequest(string apiProtocol, string? serviceId, string? layerId)
    {
        _requestCounter.Add(1,
            new("api.protocol", NormalizeProtocol(apiProtocol)),
            new("service.id", Normalize(serviceId)),
            new("layer.id", Normalize(layerId)));
    }

    public void RecordRequestDuration(string apiProtocol, string? serviceId, string? layerId, TimeSpan duration, int statusCode)
    {
        _requestDuration.Record(duration.TotalMilliseconds,
            new("api.protocol", NormalizeProtocol(apiProtocol)),
            new("service.id", Normalize(serviceId)),
            new("layer.id", Normalize(layerId)),
            new("http.status_code", statusCode.ToString()));
    }

    public void RecordError(string apiProtocol, string? serviceId, string? layerId, string errorType)
    {
        _errorCounter.Add(1,
            new("api.protocol", NormalizeProtocol(apiProtocol)),
            new("service.id", Normalize(serviceId)),
            new("layer.id", Normalize(layerId)),
            new("error.type", Normalize(errorType)));
    }

    public void RecordError(string apiProtocol, string? serviceId, string? layerId, Exception exception, string? additionalContext = null)
    {
        var errorType = GetErrorType(exception);

        // Record metric
        _errorCounter.Add(1,
            new("api.protocol", NormalizeProtocol(apiProtocol)),
            new("service.id", Normalize(serviceId)),
            new("layer.id", Normalize(layerId)),
            new("error.type", errorType),
            new("error.category", GetErrorCategory(exception)));

        // Log structured error details for alerting
        if (_logger != null)
        {
            var severity = GetErrorSeverity(exception);

            var logLevel = severity switch
            {
                ErrorSeverity.Critical => LogLevel.Critical,
                ErrorSeverity.High => LogLevel.Error,
                ErrorSeverity.Medium => LogLevel.Warning,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, exception,
                "API Error - Protocol: {ApiProtocol}, Service: {ServiceId}, Layer: {LayerId}, " +
                "ErrorType: {ErrorType}, Category: {ErrorCategory}, Severity: {Severity}, Context: {Context}",
                apiProtocol, serviceId ?? "unknown", layerId ?? "unknown",
                errorType, GetErrorCategory(exception), severity, additionalContext ?? "none");
        }
    }

    private static string GetErrorType(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException => "null_argument",
            ArgumentException => "argument_error",
            InvalidOperationException => "invalid_operation",
            UnauthorizedAccessException => "unauthorized",
            TimeoutException => "timeout",
            System.Net.Http.HttpRequestException => "http_error",
            System.Data.Common.DbException => "database_error",
            System.IO.IOException => "io_error",
            OutOfMemoryException => "out_of_memory",
            StackOverflowException => "stack_overflow",
            _ => exception.GetType().Name.Replace("Exception", "").ToLowerInvariant()
        };
    }

    private static string GetErrorCategory(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException => "validation",
            UnauthorizedAccessException => "security",
            TimeoutException => "performance",
            System.Net.Http.HttpRequestException => "network",
            System.Data.Common.DbException => "database",
            System.IO.IOException => "storage",
            OutOfMemoryException or StackOverflowException => "resource",
            _ => "application"
        };
    }

    private static ErrorSeverity GetErrorSeverity(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException or StackOverflowException => ErrorSeverity.Critical,
            System.Data.Common.DbException => ErrorSeverity.High,
            UnauthorizedAccessException => ErrorSeverity.High,
            TimeoutException => ErrorSeverity.Medium,
            System.IO.IOException => ErrorSeverity.Medium,
            ArgumentException or ArgumentNullException => ErrorSeverity.Low,
            _ => ErrorSeverity.Medium
        };
    }

    private enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public void RecordFeatureCount(string apiProtocol, string? serviceId, string? layerId, long count)
    {
        _featureCounter.Add(count,
            new("api.protocol", NormalizeProtocol(apiProtocol)),
            new("service.id", Normalize(serviceId)),
            new("layer.id", Normalize(layerId)));
    }

    public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration)
    {
        var normalizedEndpoint = NormalizeEndpoint(endpoint);
        var statusClass = GetStatusClass(statusCode);
        var errorType = GetErrorTypeFromStatusCode(statusCode);
        var durationMs = duration.TotalMilliseconds;

        // Record histogram for latency percentiles (p95, p99, p99.9)
        _httpRequestDuration.Record(durationMs,
            new("http.method", method.ToUpperInvariant()),
            new("http.endpoint", normalizedEndpoint),
            new("http.status_code", statusCode.ToString()),
            new("http.status_class", statusClass));

        // Record counter for request counts
        _httpRequestCounter.Add(1,
            new("http.method", method.ToUpperInvariant()),
            new("http.endpoint", normalizedEndpoint),
            new("http.status_code", statusCode.ToString()),
            new("http.status_class", statusClass));

        // Track slow requests with categorized thresholds
        if (durationMs > 1000)
        {
            var threshold = durationMs switch
            {
                > 10000 => "10s",
                > 5000 => "5s",
                > 1000 => "1s",
                _ => "normal"
            };

            _slowRequestCounter.Add(1,
                new("http.method", method.ToUpperInvariant()),
                new("http.endpoint", normalizedEndpoint),
                new("latency_threshold", threshold));
        }

        // Record error if status code indicates an error
        if (statusCode >= 400)
        {
            _httpErrorCounter.Add(1,
                new("http.method", method.ToUpperInvariant()),
                new("http.endpoint", normalizedEndpoint),
                new("http.status_code", statusCode.ToString()),
                new("http.status_class", statusClass),
                new("error.type", errorType));
        }
    }

    public void RecordHttpError(string method, string endpoint, int statusCode, string errorType)
    {
        var normalizedEndpoint = NormalizeEndpoint(endpoint);
        var statusClass = GetStatusClass(statusCode);

        _httpErrorCounter.Add(1,
            new("http.method", method.ToUpperInvariant()),
            new("http.endpoint", normalizedEndpoint),
            new("http.status_code", statusCode.ToString()),
            new("http.status_class", statusClass),
            new("error.type", Normalize(errorType)));
    }

    public void RecordRateLimitHit(string endpoint, string clientIp)
    {
        var normalizedEndpoint = NormalizeEndpoint(endpoint);

        _rateLimitCounter.Add(1,
            new("http.endpoint", normalizedEndpoint),
            new("client.ip", MaskIp(clientIp)));

        // Also record as HTTP error for consistency
        RecordHttpError("RATE_LIMITED", endpoint, 429, "rate_limit_exceeded");
    }

    private static string MaskIp(string ip)
    {
        // Mask last octet for privacy (e.g., 192.168.1.100 -> 192.168.1.*)
        if (string.IsNullOrWhiteSpace(ip))
            return "unknown";

        var parts = ip.Split('.');
        if (parts.Length == 4)
        {
            return $"{parts[0]}.{parts[1]}.{parts[2]}.*";
        }

        // For IPv6 or other formats, just return masked
        return "masked";
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static string NormalizeProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
            return "unknown";

        return protocol.ToLowerInvariant() switch
        {
            "wfs" or "web feature service" => "wfs",
            "wms" or "web map service" => "wms",
            "wmts" or "web map tile service" => "wmts",
            "csw" or "catalog service" => "csw",
            "wcs" or "web coverage service" => "wcs",
            "ogc-api-features" or "ogcapi-features" or "oapif" => "ogc-api-features",
            "ogc-api-tiles" or "ogcapi-tiles" => "ogc-api-tiles",
            "stac" or "spatiotemporal asset catalog" => "stac",
            "esri-rest" or "geoservices" or "arcgis-rest" => "esri-rest",
            "odata" => "odata",
            "carto" or "carto-sql" => "carto",
            _ => protocol.ToLowerInvariant()
        };
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return "/unknown";

        // Remove query string
        var pathOnly = endpoint.Split('?')[0];

        // Normalize common patterns - replace IDs with placeholders
        pathOnly = System.Text.RegularExpressions.Regex.Replace(
            pathOnly,
            @"/collections/[^/]+",
            "/collections/{id}");

        pathOnly = System.Text.RegularExpressions.Regex.Replace(
            pathOnly,
            @"/items/[^/]+",
            "/items/{id}");

        pathOnly = System.Text.RegularExpressions.Regex.Replace(
            pathOnly,
            @"/services/[^/]+",
            "/services/{id}");

        pathOnly = System.Text.RegularExpressions.Regex.Replace(
            pathOnly,
            @"/layers/[^/]+",
            "/layers/{id}");

        // Limit cardinality - if path is too long or unusual, use generic
        if (pathOnly.Length > 100 || pathOnly.Split('/').Length > 10)
            return "/other";

        return pathOnly.ToLowerInvariant();
    }

    private static string GetStatusClass(int statusCode)
    {
        return statusCode switch
        {
            >= 100 and < 200 => "1xx",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 and < 600 => "5xx",
            _ => "unknown"
        };
    }

    private static string GetErrorTypeFromStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 => "bad_request",
            401 => "unauthorized",
            403 => "forbidden",
            404 => "not_found",
            405 => "method_not_allowed",
            408 => "request_timeout",
            409 => "conflict",
            422 => "validation_error",
            429 => "rate_limited",
            500 => "internal_server_error",
            502 => "bad_gateway",
            503 => "service_unavailable",
            504 => "gateway_timeout",
            >= 400 and < 500 => "client_error",
            >= 500 and < 600 => "server_error",
            _ => "unknown"
        };
    }
}
