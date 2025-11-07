// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Versioning;
using Honua.Server.Host.Admin;
using Honua.Server.Host.Authentication;
using Honua.Server.Host.Carto;
using Honua.Server.Host.Csw;
using Honua.Server.Host.Health;
using Honua.Server.Host.Metadata;
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
        var configService = app.Services.GetService<IHonuaConfigurationService>();

        if (configService?.Current.Services.OgcApi.Enabled ?? true)
        {
            v1.MapOgcEndpoints();
        }

        if (configService?.Current.Services.Carto.Enabled ?? true)
        {
            v1.MapCartoEndpoints();
        }

        v1.MapOpenRosaEndpoints();

        // Map conditional service endpoints
        v1.MapConditionalServiceEndpoints(configService);

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
        IHonuaConfigurationService? configurationService)
    {
        if (configurationService?.Current.Services.Wfs.Enabled ?? true)
        {
            group.MapWfs();
        }

        if (configurationService?.Current.Services.Wms.Enabled ?? true)
        {
            group.MapWms();
        }

        if (configurationService?.Current.Services.Print.Enabled ?? true)
        {
            group.MapMapFishPrint();
        }

        if (configurationService?.Current.Services.Csw.Enabled ?? true)
        {
            group.MapCswEndpoints();
        }

        if (configurationService?.Current.Services.Wcs.Enabled ?? true)
        {
            group.MapWcsEndpoints();
        }

        if (configurationService?.Current.Services.Wmts.Enabled ?? true)
        {
            group.MapWmtsEndpoints();
        }

        if (configurationService?.Current.Services.Zarr.Enabled ?? true)
        {
            group.MapZarrTimeSeriesEndpoints();
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
        group.MapMetadataAdministration();
        group.MapDataIngestionAdministration();
        group.MapMigrationAdministration();
        group.MapRasterTileCacheAdministration();
        group.MapRasterTileCacheStatistics();
        group.MapRasterTileCacheQuota();
        group.MapRasterMosaicEndpoints();
        group.MapRasterAnalyticsEndpoints();
        group.MapRuntimeConfiguration();
        group.MapLoggingConfiguration();
        group.MapTokenRevocationEndpoints();
        group.MapVectorTilePreseedEndpoints();
        group.MapTracingConfiguration();
        group.MapDegradationStatusEndpoints();
        group.MapGeofenceAlertAdministrationEndpoints();

        return group;
    }
}
