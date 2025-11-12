// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Versioning;
using Honua.Server.Host.Admin;
using Honua.Server.Host.Authentication;
using Honua.Server.Host.Carto;
using Honua.Server.Host.Csw;
using Honua.Server.Host.Health;
// using Honua.Server.Host.Metadata;  // Commented out due to build error - methods accessed via global namespace
using Honua.Server.Host.Ogc;
using Honua.Server.Host.OpenRosa;
using Honua.Server.Host.Print;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Records;
using Honua.Server.Host.Security;
using Honua.Server.Host.Wcs;
using Honua.Server.Host.Wfs;
using Honua.Server.Host.Wms;
using Honua.Server.Host.Wmts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for mapping versioned API endpoints.
/// </summary>
/// <remarks>
/// This class provides the versioned endpoint registration strategy for the Honua API.
/// All API endpoints are registered under version-specific route groups (e.g., /v1/).
///
/// The versioning strategy uses URL-based versioning:
/// - /v1/ogc/collections (current)
/// - /v1/stac (current)
/// - /v1/api/admin/ingestion (current)
/// - /v2/... (future)
///
/// Legacy non-versioned URLs are handled by LegacyApiRedirectMiddleware which
/// redirects them to the appropriate versioned endpoint.
/// </remarks>
internal static class VersionedEndpointExtensions
{
    /// <summary>
    /// Maps all versioned API endpoints under version-specific route groups.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapVersionedEndpoints(this WebApplication app)
    {
        // Create the v1 route group for all current API endpoints
        var v1 = app.MapGroup($"/{ApiVersioning.CurrentVersion}");

        // Map top-level conformance endpoint (for OGC API compliance)
        v1.MapGet("/conformance", Honua.Server.Host.Ogc.OgcLandingHandlers.GetConformance).AllowAnonymous();

        // Map OGC and geospatial API endpoints under /v1
        var honuaConfig = app.Services.GetService<HonuaConfig>();

        bool IsServiceEnabled(string serviceId, bool defaultValue = true)
        {
            if (honuaConfig?.Services.TryGetValue(serviceId, out var service) == true)
            {
                return service.Enabled;
            }
            return defaultValue;
        }

        if (IsServiceEnabled("ogc-api", true))
        {
            v1.MapOgcEndpoints();
        }

        if (IsServiceEnabled("carto", true))
        {
            v1.MapCartoEndpoints();
        }

        v1.MapOpenRosaEndpoints();

        // Map conditional service endpoints
        v1.MapConditionalServiceEndpoints(honuaConfig);

        // Map administration endpoints under /v1
        v1.MapAdministrationEndpoints();

        // Map security endpoints (CSRF tokens) under /v1
        v1.MapCsrfTokenEndpoints();

        // Map ArcGIS compatibility token service endpoints under /v1
        v1.MapArcGisTokenEndpoints();

        // Note: Health check endpoints are NOT versioned - they remain at /healthz/*
        // This is intentional as they are infrastructure endpoints, not API endpoints

        return app;
    }

    /// <summary>
    /// Maps OGC API endpoints under the versioned route group.
    /// </summary>
    /// <param name="group">The versioned route group.</param>
    /// <returns>The route group for method chaining.</returns>
    private static RouteGroupBuilder MapOgcEndpoints(this RouteGroupBuilder group)
    {
        group.MapOgcApi();
        group.MapOgcRecords();
        return group;
    }

    /// <summary>
    /// Maps Carto SQL API endpoints under the versioned route group.
    /// </summary>
    /// <param name="group">The versioned route group.</param>
    /// <returns>The route group for method chaining.</returns>
    private static RouteGroupBuilder MapCartoEndpoints(this RouteGroupBuilder group)
    {
        group.MapCartoApi();
        return group;
    }

    /// <summary>
    /// Maps OpenRosa form endpoints under the versioned route group.
    /// </summary>
    /// <param name="group">The versioned route group.</param>
    /// <returns>The route group for method chaining.</returns>
    private static RouteGroupBuilder MapOpenRosaEndpoints(this RouteGroupBuilder group)
    {
        group.MapOpenRosa();
        return group;
    }

    /// <summary>
    /// Maps conditional service endpoints under the versioned route group.
    /// </summary>
    /// <param name="group">The versioned route group.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <returns>The route group for method chaining.</returns>
    private static RouteGroupBuilder MapConditionalServiceEndpoints(
        this RouteGroupBuilder group,
        HonuaConfig? honuaConfig)
    {
        bool IsServiceEnabled(string serviceId, bool defaultValue = true)
        {
            if (honuaConfig?.Services.TryGetValue(serviceId, out var service) == true)
            {
                return service.Enabled;
            }
            return defaultValue;
        }

        if (IsServiceEnabled("wfs", true))
        {
            group.MapWfs();
        }

        if (IsServiceEnabled("wms", true))
        {
            group.MapWms();
        }

        if (IsServiceEnabled("print", true))
        {
            group.MapMapFishPrint();
        }

        if (IsServiceEnabled("csw", true))
        {
            group.MapCswEndpoints("v1");
        }

        if (IsServiceEnabled("wcs", true))
        {
            group.MapWcsEndpoints("v1");
        }

        if (IsServiceEnabled("wmts", true))
        {
            group.MapWmtsEndpoints();
        }

        if (IsServiceEnabled("zarr", true))
        {
            group.MapZarrTimeSeriesEndpoints("v1");
        }

        return group;
    }

    /// <summary>
    /// Maps administration endpoints under the versioned route group.
    /// </summary>
    /// <param name="group">The versioned route group.</param>
    /// <returns>The route group for method chaining.</returns>
    private static RouteGroupBuilder MapAdministrationEndpoints(this RouteGroupBuilder group)
    {
        global::Honua.Server.Host.Metadata.MetadataAdministrationEndpointRouteBuilderExtensions.MapMetadataAdministration(group);
        group.MapDataIngestionAdministration();
        // group.MapMigrationAdministration(); // Removed: Legacy migration endpoints deleted
        group.MapRasterTileCacheAdministration();
        group.MapRasterTileCacheStatistics();
        group.MapRasterTileCacheQuota();
        group.MapRasterMosaicEndpoints();
        group.MapRasterAnalyticsEndpoints();
        // group.MapRuntimeConfiguration(); // Removed: Legacy runtime config endpoints deleted
        group.MapLoggingConfiguration();
        group.MapTokenRevocationEndpoints();
        group.MapVectorTilePreseedEndpoints();
        group.MapTracingConfiguration();
        group.MapDegradationStatusEndpoints();
        group.MapGeofenceAlertAdministrationEndpoints();

        // Map alert management endpoints
        group.MapAdminAlertEndpoints();

        // Map feature flag management endpoints
        _ = ((IEndpointRouteBuilder)group).MapAdminFeatureFlagEndpoints();

        // Map audit log endpoints
        _ = ((IEndpointRouteBuilder)group).MapAuditLogEndpoints();

        // Map spatial index diagnostics endpoints
        _ = ((IEndpointRouteBuilder)group).MapSpatialIndexDiagnosticsEndpoints();

        // Map server configuration endpoints (CORS, etc.)
        group.MapAdminServerEndpoints();

        return group;
    }
}
