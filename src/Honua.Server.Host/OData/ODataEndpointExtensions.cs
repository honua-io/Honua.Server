// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Host.OData;

/// <summary>
/// Extension methods for registering OData v4 endpoints without Microsoft dependencies.
/// Provides AOT-compatible OData protocol implementation.
/// </summary>
public static class ODataEndpointExtensions
{
    /// <summary>
    /// Maps OData v4 endpoints for feature data.
    /// Supports full CRUD operations and spatial queries.
    /// </summary>
    public static IEndpointRouteBuilder MapODataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var logger = endpoints.ServiceProvider.GetService<ILogger<FeatureLayerODataHandlers>>();

        logger?.LogDebug("Registering OData v4 endpoints");

        // Service root and metadata
        logger?.LogDebug("Registering endpoint: GET /odata (service document)");
        endpoints.MapGet("/odata", FeatureLayerODataHandlers.GetServiceDocument)
            .WithName("OData_ServiceDocument")
            .WithTags("OData");

        logger?.LogDebug("Registering endpoint: GET /odata/$metadata (metadata)");
        endpoints.MapGet("/odata/$metadata", FeatureLayerODataHandlers.GetMetadata)
            .WithName("OData_Metadata")
            .WithTags("OData");

        // Collection operations
        logger?.LogDebug("Registering endpoint: GET /odata/{{entitySetName}} (collection)");
        endpoints.MapGet("/odata/{entitySetName}", FeatureLayerODataHandlers.GetFeatureCollection)
            .WithName("OData_GetCollection")
            .WithTags("OData");

        logger?.LogDebug("Registering endpoint: GET /odata/{{entitySetName}}/$count (count)");
        endpoints.MapGet("/odata/{entitySetName}/$count", FeatureLayerODataHandlers.GetCollectionCount)
            .WithName("OData_GetCount")
            .WithTags("OData");

        logger?.LogDebug("Registering endpoint: POST /odata/{{entitySetName}} (create)");
        endpoints.MapPost("/odata/{entitySetName}", FeatureLayerODataHandlers.CreateFeature)
            .WithName("OData_CreateFeature")
            .WithTags("OData");

        // Single entity operations
        logger?.LogDebug("Registering endpoint: GET /odata/{{entitySetName}}({{id}}) (get single)");
        endpoints.MapGet("/odata/{entitySetName}({id})", FeatureLayerODataHandlers.GetFeature)
            .WithName("OData_GetFeature")
            .WithTags("OData");

        logger?.LogDebug("Registering endpoint: PATCH /odata/{{entitySetName}}({{id}}) (update)");
        endpoints.MapPatch("/odata/{entitySetName}({id})", FeatureLayerODataHandlers.UpdateFeature)
            .WithName("OData_UpdateFeature")
            .WithTags("OData");

        logger?.LogDebug("Registering endpoint: DELETE /odata/{{entitySetName}}({{id}}) (delete)");
        endpoints.MapDelete("/odata/{entitySetName}({id})", FeatureLayerODataHandlers.DeleteFeature)
            .WithName("OData_DeleteFeature")
            .WithTags("OData");

        logger?.LogInformation("Successfully registered 8 OData v4 endpoints");
        return endpoints;
    }
}
