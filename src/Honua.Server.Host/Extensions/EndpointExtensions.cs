// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;
using Honua.Server.Enterprise.Sensors.Extensions;
using Honua.Server.Enterprise.Sensors.Models;
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
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for mapping application endpoints.
/// </summary>
internal static class EndpointExtensions
{
    /// <summary>
    /// Maps all Honua API endpoints including OGC services, administration, and health checks.
    /// Uses versioned endpoints with backward compatibility for legacy URLs.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapHonuaEndpoints(this WebApplication app)
    {
        // Map MVC controllers and Razor Pages
        app.MapControllers();
        app.MapRazorPages();

        // Map versioned API endpoints (all endpoints under /v1/)
        app.MapVersionedEndpoints();

        // Map health check endpoints (NOT versioned - infrastructure endpoints)
        app.MapHonuaHealthCheckEndpoints();

        // Map home redirect to versioned OGC endpoint
        app.MapGet("/", () => Results.Redirect("/v1/ogc"));

        return app;
    }

    /// <summary>
    /// Maps OGC API endpoints including Features and Records.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapOgcEndpoints(this WebApplication app)
    {
        app.MapOgcApi();
        app.MapOgcRecords();

        return app;
    }

    /// <summary>
    /// Maps Carto SQL API endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapCartoEndpoints(this WebApplication app)
    {
        app.MapCartoApi();

        return app;
    }

    /// <summary>
    /// Maps OpenRosa form endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapOpenRosaEndpoints(this WebApplication app)
    {
        app.MapOpenRosa();

        return app;
    }

    /// <summary>
    /// Maps service endpoints that can be conditionally enabled/disabled via configuration.
    /// Includes WFS, WMS, WMTS, WCS, CSW, and Print services.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapConditionalServiceEndpoints(this WebApplication app)
    {
        var configurationService = app.Services.GetService<IHonuaConfigurationService>();

        if (configurationService?.Current.Services.Wfs.Enabled ?? true)
        {
            app.MapWfs();
        }

        if (configurationService?.Current.Services.Wms.Enabled ?? true)
        {
            app.MapWms();
        }

        if (configurationService?.Current.Services.Print.Enabled ?? true)
        {
            app.MapMapFishPrint();
        }

        if (configurationService?.Current.Services.Csw.Enabled ?? true)
        {
            app.MapCswEndpoints();
        }

        if (configurationService?.Current.Services.Wcs.Enabled ?? true)
        {
            app.MapWcsEndpoints();
        }

        if (configurationService?.Current.Services.Wmts.Enabled ?? true)
        {
            app.MapWmtsEndpoints();
        }

        if (configurationService?.Current.Services.Zarr.Enabled ?? true)
        {
            app.MapZarrTimeSeriesEndpoints();
        }

        // OGC SensorThings API v1.1 (conditional on configuration)
        var sensorThingsConfig = app.Services.GetService<SensorThingsServiceDefinition>();
        if (sensorThingsConfig?.Enabled ?? false)
        {
            app.MapSensorThingsEndpoints(sensorThingsConfig);
        }

        return app;
    }

    /// <summary>
    /// Maps health check endpoints for Kubernetes probes and monitoring.
    /// Provides both /health and /healthz style endpoints for compatibility.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapHonuaHealthCheckEndpoints(this WebApplication app)
    {
        // Use the comprehensive health check setup from WebApplicationExtensions
        // This configures /health, /health/ready, and /health/live endpoints
        app.UseHonuaHealthChecks();

        // Also map Kubernetes-style /healthz endpoints for compatibility
        app.MapHealthChecks("/healthz/startup", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("startup"),
            ResponseWriter = HealthResponseWriter.WriteResponse
        });

        app.MapHealthChecks("/healthz/live", new HealthCheckOptions
        {
            Predicate = _ => false, // Just return 200 if app is running
            ResponseWriter = HealthResponseWriter.WriteResponse
        });

        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = HealthResponseWriter.WriteResponse
        });

        return app;
    }

    /// <summary>
    /// Maps administration endpoints for metadata, data ingestion, migration, and runtime configuration.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication MapAdministrationEndpoints(this WebApplication app)
    {
        app.MapMetadataAdministration();
        app.MapDataIngestionAdministration();
        app.MapMigrationAdministration();
        app.MapRasterTileCacheAdministration();
        app.MapRasterTileCacheStatistics();
        app.MapRasterTileCacheQuota();
        app.MapRasterMosaicEndpoints();
        app.MapRasterAnalyticsEndpoints();
        app.MapRuntimeConfiguration();
        app.MapLoggingConfiguration();
        app.MapTokenRevocationEndpoints();

        // Map MapSDK configuration endpoints (visual map builder)
        app.MapMapConfigurationEndpoints();

        // Map GeoETL endpoints (Enterprise feature)
        app.MapGeoEtlWorkflowEndpoints();
        app.MapGeoEtlExecutionEndpoints();
        app.MapGeoEtlAiEndpoints();
        app.MapGeoEtlTemplateEndpoints();

        // Map Admin UI SignalR hub for real-time updates
        app.MapHub<Honua.Server.Host.Admin.Hubs.MetadataChangeNotificationHub>("/admin/hub/metadata");

        // Map GeoEvent SignalR hub for real-time geofence event streaming
        app.MapHub<Honua.Server.Host.GeoEvent.GeoEventHub>("/hubs/geoevent");

        // Map GeoETL Progress SignalR hub for real-time workflow execution tracking
        app.MapHub<Honua.Server.Host.GeoEvent.GeoEtlProgressHub>("/hubs/geoetl-progress");

        return app;
    }
}
