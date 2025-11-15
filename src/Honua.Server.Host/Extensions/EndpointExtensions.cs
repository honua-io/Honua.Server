// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Configuration.V2;
// Enterprise features disabled
// using Honua.Server.Enterprise.Sensors.Extensions;
// using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Host.Admin;
using Honua.Server.Host.Authentication;
using Honua.Server.Host.Carto;
using Honua.Server.Host.Csw;
using Honua.Server.Host.Health;
// using Honua.Server.Host.Metadata;  // Commented out due to build error - methods accessed via global namespace
using Honua.Server.Host.OData;
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

        // Map OGC endpoints at root level for OGC API spec compliance
        // The OGC API specification expects the landing page at /ogc, not /v1/ogc
        var honuaConfig = app.Services.GetService<HonuaConfig>();
        var ogcApiEnabled = honuaConfig?.Services.TryGetValue("ogc-api", out var ogcApiService) == true
            ? ogcApiService.Enabled
            : true;
        if (ogcApiEnabled)
        {
            app.MapOgcEndpoints();
        }

        // Map health check endpoints (NOT versioned - infrastructure endpoints)
        app.MapHonuaHealthCheckEndpoints();

        // Map home redirect to OGC landing page endpoint
        app.MapGet("/", () => Results.Redirect("/ogc"));

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
        var honuaConfig = app.Services.GetService<HonuaConfig>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        // Helper to check if a service is enabled
        bool IsServiceEnabled(string serviceId, bool defaultValue = true)
        {
            if (honuaConfig?.Services.TryGetValue(serviceId, out var service) == true)
            {
                return service.Enabled;
            }
            // If Configuration V2 is active but service is not defined, default to disabled
            if (honuaConfig != null)
            {
                return false;
            }
            return defaultValue;
        }

        // OData v4 endpoints (AOT-compatible implementation)
        var odataEnabled = IsServiceEnabled("odata", true);
        logger.LogWarning("ODATA ENDPOINT REGISTRATION: OData.Enabled = {ODataEnabled}, honuaConfig null = {ConfigNull}",
            odataEnabled, honuaConfig == null);

        if (odataEnabled)
        {
            logger.LogWarning("ODATA ENDPOINT REGISTRATION: Calling MapODataEndpoints()");
            app.MapODataEndpoints();
            logger.LogWarning("ODATA ENDPOINT REGISTRATION: MapODataEndpoints() completed");
        }
        else
        {
            logger.LogWarning("ODATA ENDPOINT REGISTRATION: OData is DISABLED - skipping MapODataEndpoints()");
        }

        if (IsServiceEnabled("wfs", true))
        {
            app.MapWfs();
        }

        if (IsServiceEnabled("wms", true))
        {
            app.MapWms();
        }

        if (IsServiceEnabled("print", true))
        {
            app.MapMapFishPrint();
        }

        if (IsServiceEnabled("csw", true))
        {
            app.MapCswEndpoints();
        }

        if (IsServiceEnabled("wcs", true))
        {
            app.MapWcsEndpoints();
        }

        if (IsServiceEnabled("wmts", true))
        {
            app.MapWmtsEndpoints();
        }

        if (IsServiceEnabled("zarr", true))
        {
            app.MapZarrTimeSeriesEndpoints();
        }

        // OGC SensorThings API v1.1 (conditional on configuration)
        // Enterprise features disabled
        // var sensorThingsConfig = app.Services.GetService<SensorThingsServiceDefinition>();
        // if (sensorThingsConfig?.Enabled ?? false)
        // {
        //     app.MapSensorThingsEndpoints(sensorThingsConfig);
        // }

        // OIDC Authentication endpoints (login, logout, user info)
        app.MapAuthenticationEndpoints();

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
        global::Honua.Server.Host.Metadata.MetadataAdministrationEndpointRouteBuilderExtensions.MapMetadataAdministration(app);
        app.MapDataIngestionAdministration();
        // app.MapMigrationAdministration(); // Removed: Legacy migration endpoints deleted
        app.MapRasterTileCacheAdministration();
        app.MapRasterTileCacheStatistics();
        app.MapRasterTileCacheQuota();
        app.MapRasterMosaicEndpoints();
        app.MapRasterAnalyticsEndpoints();
        // app.MapRuntimeConfiguration(); // Removed: Legacy runtime config endpoints deleted
        app.MapLoggingConfiguration();
        app.MapTokenRevocationEndpoints();

        // Map MapSDK configuration endpoints (visual map builder)
        app.MapMapConfigurationEndpoints();

        // Map geofence alert administration endpoints
        app.MapGeofenceAlertAdministrationEndpoints();

        // Map alert management endpoints
        _ = ((IEndpointRouteBuilder)app).MapAdminAlertEndpoints();

        // TODO: Re-enable MapAdminFeatureFlagEndpoints when the method is implemented
        // Map feature flag management endpoints
        // _ = ((IEndpointRouteBuilder)app).MapAdminFeatureFlagEndpoints();

        // TODO: Re-enable MapAuditLogEndpoints when the method is implemented
        // Map audit log endpoints
        // _ = ((IEndpointRouteBuilder)app).MapAuditLogEndpoints();

        // Map spatial index diagnostics endpoints
        _ = ((IEndpointRouteBuilder)app).MapSpatialIndexDiagnosticsEndpoints();

        // Map server configuration endpoints (CORS, etc.)
        _ = ((IEndpointRouteBuilder)app).MapAdminServerEndpoints();

        // Map GeoETL endpoints (Enterprise feature)
        // Enterprise features disabled
        // app.MapGeoEtlWorkflowEndpoints();
        // app.MapGeoEtlExecutionEndpoints();
        // app.MapGeoEtlAiEndpoints();
        // app.MapGeoEtlTemplateEndpoints();
        // app.MapGeoEtlScheduleEndpoints();

        // Map Admin UI SignalR hub for real-time updates
        app.MapHub<Honua.Server.Host.Admin.Hubs.MetadataChangeNotificationHub>("/admin/hub/metadata");

        // Map GeoEvent SignalR hub for real-time geofence event streaming
        // Enterprise features disabled
        // app.MapHub<Honua.Server.Host.GeoEvent.GeoEventHub>("/hubs/geoevent");

        // Map GeoETL Progress SignalR hub for real-time workflow execution tracking
        // app.MapHub<Honua.Server.Host.GeoEvent.GeoEtlProgressHub>("/hubs/geoetl-progress");

        // Map SensorThings SignalR hub for real-time sensor observation streaming
        // var sensorThingsConfig = app.Services.GetService<Honua.Server.Enterprise.Sensors.Models.SensorThingsServiceDefinition>();
        // if (sensorThingsConfig?.WebSocketStreamingEnabled == true)
        // {
        //     app.MapHub<Honua.Server.Host.SensorThings.SensorObservationHub>("/hubs/sensor-observations");
        // }

        return app;
    }
}
