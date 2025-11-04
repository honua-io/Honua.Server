// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Observability;
using Microsoft.AspNetCore.Http;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Extension methods for tracking API metrics in HTTP handlers.
/// </summary>
public static class ApiMetricsExtensions
{
    /// <summary>
    /// Records the number of features returned in a response.
    /// Extracts service and layer context from the HTTP context.
    /// </summary>
    public static void RecordFeaturesReturned(
        this IApiMetrics metrics,
        HttpContext context,
        string apiProtocol,
        long featureCount)
    {
        if (metrics == null || featureCount <= 0) return;

        var (serviceId, layerId) = ExtractContext(context);
        metrics.RecordFeatureCount(apiProtocol, serviceId, layerId, featureCount);
    }

    /// <summary>
    /// Records the number of features returned with explicit service and layer IDs.
    /// </summary>
    public static void RecordFeaturesReturned(
        this IApiMetrics metrics,
        string apiProtocol,
        string? serviceId,
        string? layerId,
        long featureCount)
    {
        if (metrics == null || featureCount <= 0) return;

        metrics.RecordFeatureCount(apiProtocol, serviceId, layerId, featureCount);
    }

    private static (string? serviceId, string? layerId) ExtractContext(HttpContext context)
    {
        // Try to get from route values first
        var routeValues = context.Request.RouteValues;
        var serviceId = routeValues.GetValueOrDefault("serviceId")?.ToString();
        var layerId = routeValues.GetValueOrDefault("layerId")?.ToString()
                     ?? routeValues.GetValueOrDefault("collectionId")?.ToString();

        // If not in route, try query parameters
        if (serviceId.IsNullOrEmpty() || layerId.IsNullOrEmpty())
        {
            var query = context.Request.Query;
            var typeNames = query["typeNames"].ToString() ?? query["typeName"].ToString();
            var layers = query["layers"].ToString() ?? query["layer"].ToString();

            var identifier = typeNames ?? layers;
            if (!identifier.IsNullOrEmpty())
            {
                var parts = identifier.Split(':', 2);
                if (parts.Length == 2)
                {
                    serviceId ??= parts[0];
                    layerId ??= parts[1];
                }
                else
                {
                    layerId ??= identifier;
                }
            }
        }

        return (serviceId, layerId);
    }
}
