// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Diagnostics;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Middleware that enriches HTTP requests with API protocol and service context for metrics.
/// Captures service ID and API protocol from request paths and query parameters.
/// </summary>
public sealed class ApiMetricsMiddleware
{
    private const string CollectionIdSeparator = "::";
    private readonly RequestDelegate _next;
    private readonly IApiMetrics _metrics;
    private readonly ILogger<ApiMetricsMiddleware> _logger;

    public ApiMetricsMiddleware(
        RequestDelegate next,
        IApiMetrics metrics,
        ILogger<ApiMetricsMiddleware> logger)
    {
        _next = Guard.NotNull(next);
        _metrics = Guard.NotNull(metrics);
        _logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        // Time the request for HTTP-level metrics
        var stopwatch = Stopwatch.StartNew();
        int statusCode = 500; // Default to server error if something goes wrong
        string? errorType = null;

        try
        {
            // Extract API protocol from path
            var apiProtocol = ExtractApiProtocol(path);

            if (apiProtocol != null)
            {
                // Extract service and layer context
                var (serviceId, layerId) = ExtractServiceContext(context);

                // Record request
                _metrics.RecordRequest(apiProtocol, serviceId, layerId);

                await _next(context);

                stopwatch.Stop();
                statusCode = context.Response.StatusCode;

                // Record API-level metrics
                _metrics.RecordRequestDuration(
                    apiProtocol,
                    serviceId,
                    layerId,
                    stopwatch.Elapsed,
                    statusCode);

                // Record HTTP-level metrics for all requests (including non-errors)
                var normalizedPath = NormalizePath(context);
                _metrics.RecordHttpRequest(method, normalizedPath, statusCode, stopwatch.Elapsed);

                // Record error if status code indicates failure
                if (statusCode >= 400)
                {
                    errorType = DetermineErrorType(statusCode, null);
                    _metrics.RecordError(apiProtocol, serviceId, layerId, errorType);
                }
            }
            else
            {
                // Not an API request, but still track HTTP metrics
                await _next(context);
                stopwatch.Stop();
                statusCode = context.Response.StatusCode;

                // Record HTTP-level metrics for non-API requests too
                var normalizedPath = NormalizePath(context);
                _metrics.RecordHttpRequest(method, normalizedPath, statusCode, stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            statusCode = 500;
            errorType = DetermineErrorType(statusCode, ex);

            // Extract API protocol for error recording
            var apiProtocol = ExtractApiProtocol(path);

            if (apiProtocol != null)
            {
                var (serviceId, layerId) = ExtractServiceContext(context);

                _metrics.RecordError(
                    apiProtocol,
                    serviceId,
                    layerId,
                    ex.GetType().Name);

                _metrics.RecordRequestDuration(
                    apiProtocol,
                    serviceId,
                    layerId,
                    stopwatch.Elapsed,
                    statusCode);
            }

            // Always record HTTP-level metrics
            var normalizedPath = NormalizePath(context);
            _metrics.RecordHttpRequest(method, normalizedPath, statusCode, stopwatch.Elapsed);
            _metrics.RecordHttpError(method, normalizedPath, statusCode, errorType);

            throw;
        }
    }

    private static string? ExtractApiProtocol(string path)
    {
        // Normalize path
        path = path.TrimStart('/').ToLowerInvariant();

        if (path.StartsWith("wfs")) return "wfs";
        if (path.StartsWith("wms")) return "wms";
        if (path.StartsWith("wmts")) return "wmts";
        if (path.StartsWith("csw")) return "csw";
        if (path.StartsWith("wcs")) return "wcs";
        if (path.StartsWith("ogc/collections") || path.StartsWith("ogc") || path.StartsWith("collections")) return "ogc-api-features";
        if (path.StartsWith("ogc/tiles") || path.StartsWith("tiles")) return "ogc-api-tiles";
        if (path.StartsWith("stac")) return "stac";
        if (path.StartsWith("rest/services") || path.StartsWith("services")) return "esri-rest";
        if (path.StartsWith("odata")) return "odata";
        if (path.StartsWith("carto") || path.StartsWith("api/v1/sql")) return "carto";

        return null; // Not an API request
    }

    private static string NormalizePath(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint)
        {
            var template = routeEndpoint.RoutePattern.RawText;
            if (template.HasValue())
            {
                return template;
            }
        }

        var rawPath = context.Request.Path.Value;
        if (rawPath.IsNullOrWhiteSpace())
        {
            return "/";
        }

        var segments = rawPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (segment.Length == 0)
            {
                continue;
            }

            if (Guid.TryParse(segment, out _))
            {
                segments[i] = "{guid}";
                continue;
            }

            if (int.TryParse(segment, out _) || long.TryParse(segment, out _))
            {
                segments[i] = "{int}";
                continue;
            }

            if (ContainsMixedAlphaNumeric(segment) || segment.Length > 24)
            {
                segments[i] = "{value}";
            }
        }

        return "/" + string.Join('/', segments);
    }

    private static bool ContainsMixedAlphaNumeric(string value)
    {
        var hasLetter = false;
        var hasDigit = false;

        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
            {
                hasLetter = true;
            }
            else if (char.IsDigit(ch))
            {
                hasDigit = true;
            }

            if (hasLetter && hasDigit)
            {
                return true;
            }
        }

        return false;
    }

    private static (string? serviceId, string? layerId) ExtractServiceContext(HttpContext context)
    {
        string? serviceId = null;
        string? layerId = null;

        var path = context.Request.Path.Value ?? string.Empty;
        var query = context.Request.Query;

        // Try to extract from path segments
        // Pattern: /api/{serviceId}/layers/{layerId}
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Equals("services", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
            {
                serviceId = segments[i + 1];
            }
            else if (segments[i].Equals("layers", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
            {
                layerId = segments[i + 1];
            }
            else if (segments[i].Equals("collections", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
            {
                // OGC API uses collection ID which may include serviceId:layerId
                var collectionId = segments[i + 1];
                (serviceId, layerId) = ParseCompositeIdentifier(collectionId);
            }
        }

        // Try to extract from query parameters
        if (serviceId.IsNullOrEmpty())
        {
            // WFS/WMS often use typeNames or layers parameter
            var typeNames = query["typeNames"].ToString() ?? query["typeName"].ToString();
            var layers = query["layers"].ToString() ?? query["layer"].ToString();

            var identifier = typeNames ?? layers;
            if (!identifier.IsNullOrEmpty())
            {
                (serviceId, layerId) = ParseCompositeIdentifier(identifier);
            }
        }

        return (serviceId, layerId);
    }

    private static (string? serviceId, string? layerId) ParseCompositeIdentifier(string identifier)
    {
        if (identifier.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var doubleColonParts = identifier.Split(new[] { CollectionIdSeparator }, StringSplitOptions.None);
        if (doubleColonParts.Length == 2)
        {
            return (doubleColonParts[0], doubleColonParts[1]);
        }

        var singleColonParts = identifier.Split(':', 2, StringSplitOptions.TrimEntries);
        if (singleColonParts.Length == 2)
        {
            return (singleColonParts[0], singleColonParts[1]);
        }

        return (null, identifier);
    }

    private static string DetermineErrorType(int statusCode, Exception? exception)
    {
        if (exception != null)
        {
            return exception switch
            {
                ArgumentException or ArgumentNullException => "validation",
                UnauthorizedAccessException => "auth",
                TimeoutException => "timeout",
                InvalidOperationException => "invalid_operation",
                System.Data.Common.DbException => "database",
                System.IO.IOException => "storage",
                System.Net.Http.HttpRequestException => "network",
                OutOfMemoryException => "out_of_memory",
                _ => "server"
            };
        }

        return statusCode switch
        {
            400 => "validation",
            401 => "auth",
            403 => "auth",
            404 => "not_found",
            405 => "method_not_allowed",
            408 => "timeout",
            409 => "conflict",
            422 => "validation",
            429 => "rate_limit",
            >= 400 and < 500 => "client",
            >= 500 and < 600 => "server",
            _ => "unknown"
        };
    }
}
