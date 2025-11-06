using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Honua.Server.Enterprise.Sensors.Handlers;
using Honua.Server.Enterprise.Sensors.Models;

namespace Honua.Server.Enterprise.Sensors.Extensions;

/// <summary>
/// Extension methods for mapping OGC SensorThings API v1.1 endpoints.
/// Implements all required entity types and navigation properties.
/// </summary>
public static class SensorThingsEndpoints
{
    /// <summary>
    /// Maps all OGC SensorThings API v1.1 endpoints to the application.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="config">SensorThings service configuration</param>
    /// <returns>The endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapSensorThingsEndpoints(
        this IEndpointRouteBuilder endpoints,
        SensorThingsServiceDefinition config)
    {
        if (!config.Enabled)
            return endpoints;

        var basePath = config.BasePath;

        // ========================================
        // Service Root
        // ========================================

        endpoints.MapGet(basePath, SensorThingsHandlers.GetServiceRoot)
            .WithName("SensorThings_GetServiceRoot");

        // ========================================
        // Things
        // ========================================

        endpoints.MapGet($"{basePath}/Things", SensorThingsHandlers.GetThings)
            .WithName("SensorThings_GetThings");

        endpoints.MapGet($"{basePath}/Things({{id}})", SensorThingsHandlers.GetThing)
            .WithName("SensorThings_GetThing");

        endpoints.MapPost($"{basePath}/Things", SensorThingsHandlers.CreateThing)
            .WithName("SensorThings_CreateThing");

        endpoints.MapPatch($"{basePath}/Things({{id}})", SensorThingsHandlers.UpdateThing)
            .WithName("SensorThings_UpdateThing");

        endpoints.MapDelete($"{basePath}/Things({{id}})", SensorThingsHandlers.DeleteThing)
            .WithName("SensorThings_DeleteThing");

        // ========================================
        // Locations
        // ========================================

        endpoints.MapGet($"{basePath}/Locations", SensorThingsHandlers.GetLocations)
            .WithName("SensorThings_GetLocations");

        endpoints.MapGet($"{basePath}/Locations({{id}})", SensorThingsHandlers.GetLocation)
            .WithName("SensorThings_GetLocation");

        endpoints.MapPost($"{basePath}/Locations", SensorThingsHandlers.CreateLocation)
            .WithName("SensorThings_CreateLocation");

        endpoints.MapPatch($"{basePath}/Locations({{id}})", SensorThingsHandlers.UpdateLocation)
            .WithName("SensorThings_UpdateLocation");

        endpoints.MapDelete($"{basePath}/Locations({{id}})", SensorThingsHandlers.DeleteLocation)
            .WithName("SensorThings_DeleteLocation");

        // ========================================
        // HistoricalLocations
        // ========================================

        endpoints.MapGet($"{basePath}/HistoricalLocations", SensorThingsHandlers.GetHistoricalLocations)
            .WithName("SensorThings_GetHistoricalLocations");

        endpoints.MapGet($"{basePath}/HistoricalLocations({{id}})", SensorThingsHandlers.GetHistoricalLocation)
            .WithName("SensorThings_GetHistoricalLocation");

        // HistoricalLocations are read-only (created automatically via trigger)

        // ========================================
        // Sensors
        // ========================================

        endpoints.MapGet($"{basePath}/Sensors", SensorThingsHandlers.GetSensors)
            .WithName("SensorThings_GetSensors");

        endpoints.MapGet($"{basePath}/Sensors({{id}})", SensorThingsHandlers.GetSensor)
            .WithName("SensorThings_GetSensor");

        endpoints.MapPost($"{basePath}/Sensors", SensorThingsHandlers.CreateSensor)
            .WithName("SensorThings_CreateSensor");

        endpoints.MapPatch($"{basePath}/Sensors({{id}})", SensorThingsHandlers.UpdateSensor)
            .WithName("SensorThings_UpdateSensor");

        endpoints.MapDelete($"{basePath}/Sensors({{id}})", SensorThingsHandlers.DeleteSensor)
            .WithName("SensorThings_DeleteSensor");

        // ========================================
        // ObservedProperties
        // ========================================

        endpoints.MapGet($"{basePath}/ObservedProperties", SensorThingsHandlers.GetObservedProperties)
            .WithName("SensorThings_GetObservedProperties");

        endpoints.MapGet($"{basePath}/ObservedProperties({{id}})", SensorThingsHandlers.GetObservedProperty)
            .WithName("SensorThings_GetObservedProperty");

        endpoints.MapPost($"{basePath}/ObservedProperties", SensorThingsHandlers.CreateObservedProperty)
            .WithName("SensorThings_CreateObservedProperty");

        endpoints.MapPatch($"{basePath}/ObservedProperties({{id}})", SensorThingsHandlers.UpdateObservedProperty)
            .WithName("SensorThings_UpdateObservedProperty");

        endpoints.MapDelete($"{basePath}/ObservedProperties({{id}})", SensorThingsHandlers.DeleteObservedProperty)
            .WithName("SensorThings_DeleteObservedProperty");

        // ========================================
        // Datastreams
        // ========================================

        endpoints.MapGet($"{basePath}/Datastreams", SensorThingsHandlers.GetDatastreams)
            .WithName("SensorThings_GetDatastreams");

        endpoints.MapGet($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.GetDatastream)
            .WithName("SensorThings_GetDatastream");

        endpoints.MapPost($"{basePath}/Datastreams", SensorThingsHandlers.CreateDatastream)
            .WithName("SensorThings_CreateDatastream");

        endpoints.MapPatch($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.UpdateDatastream)
            .WithName("SensorThings_UpdateDatastream");

        endpoints.MapDelete($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.DeleteDatastream)
            .WithName("SensorThings_DeleteDatastream");

        // ========================================
        // Observations (with DataArray detection)
        // ========================================

        endpoints.MapGet($"{basePath}/Observations", SensorThingsHandlers.GetObservations)
            .WithName("SensorThings_GetObservations");

        endpoints.MapGet($"{basePath}/Observations({{id}})", SensorThingsHandlers.GetObservation)
            .WithName("SensorThings_GetObservation");

        // Standards-compliant: Automatically detects DataArray in POST body
        endpoints.MapPost($"{basePath}/Observations", SensorThingsHandlers.CreateObservation)
            .WithName("SensorThings_CreateObservation");

        endpoints.MapDelete($"{basePath}/Observations({{id}})", SensorThingsHandlers.DeleteObservation)
            .WithName("SensorThings_DeleteObservation");

        // ========================================
        // FeaturesOfInterest
        // ========================================

        endpoints.MapGet($"{basePath}/FeaturesOfInterest", SensorThingsHandlers.GetFeaturesOfInterest)
            .WithName("SensorThings_GetFeaturesOfInterest");

        endpoints.MapGet($"{basePath}/FeaturesOfInterest({{id}})", SensorThingsHandlers.GetFeatureOfInterest)
            .WithName("SensorThings_GetFeatureOfInterest");

        endpoints.MapPost($"{basePath}/FeaturesOfInterest", SensorThingsHandlers.CreateFeatureOfInterest)
            .WithName("SensorThings_CreateFeatureOfInterest");

        endpoints.MapPatch($"{basePath}/FeaturesOfInterest({{id}})", SensorThingsHandlers.UpdateFeatureOfInterest)
            .WithName("SensorThings_UpdateFeatureOfInterest");

        endpoints.MapDelete($"{basePath}/FeaturesOfInterest({{id}})", SensorThingsHandlers.DeleteFeatureOfInterest)
            .WithName("SensorThings_DeleteFeatureOfInterest");

        // ========================================
        // Navigation Properties
        // ========================================

        // Thing navigation properties
        endpoints.MapGet($"{basePath}/Things({{id}})/Datastreams", SensorThingsHandlers.GetThingDatastreams)
            .WithName("SensorThings_GetThingDatastreams");

        endpoints.MapGet($"{basePath}/Things({{id}})/Locations", SensorThingsHandlers.GetThingLocations)
            .WithName("SensorThings_GetThingLocations");

        // Datastream navigation properties
        endpoints.MapGet($"{basePath}/Datastreams({{id}})/Observations", SensorThingsHandlers.GetDatastreamObservations)
            .WithName("SensorThings_GetDatastreamObservations");

        // Location navigation properties

        // ========================================
        // Mobile-Optimized Extensions
        // ========================================

        // Custom mobile sync endpoint (if enabled)
        if (config.OfflineSyncEnabled)
        {
            endpoints.MapPost($"{basePath}/Sync", SensorThingsHandlers.SyncObservations)
                .WithName("SensorThings_SyncObservations")
                .RequireAuthorization(); // Requires authenticated user
        }

        return endpoints;
    }
}
