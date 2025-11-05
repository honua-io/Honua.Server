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
            .WithName("SensorThings_GetServiceRoot")
            .WithTags("SensorThings");

        // ========================================
        // Things
        // ========================================

        endpoints.MapGet($"{basePath}/Things", SensorThingsHandlers.GetThings)
            .WithName("SensorThings_GetThings")
            .WithTags("Things");

        endpoints.MapGet($"{basePath}/Things({{id}})", SensorThingsHandlers.GetThing)
            .WithName("SensorThings_GetThing")
            .WithTags("Things");

        endpoints.MapPost($"{basePath}/Things", SensorThingsHandlers.CreateThing)
            .WithName("SensorThings_CreateThing")
            .WithTags("Things");

        endpoints.MapPatch($"{basePath}/Things({{id}})", SensorThingsHandlers.UpdateThing)
            .WithName("SensorThings_UpdateThing")
            .WithTags("Things");

        endpoints.MapDelete($"{basePath}/Things({{id}})", SensorThingsHandlers.DeleteThing)
            .WithName("SensorThings_DeleteThing")
            .WithTags("Things");

        // ========================================
        // Locations
        // ========================================

        endpoints.MapGet($"{basePath}/Locations", SensorThingsHandlers.GetLocations)
            .WithName("SensorThings_GetLocations")
            .WithTags("Locations");

        endpoints.MapGet($"{basePath}/Locations({{id}})", SensorThingsHandlers.GetLocation)
            .WithName("SensorThings_GetLocation")
            .WithTags("Locations");

        endpoints.MapPost($"{basePath}/Locations", SensorThingsHandlers.CreateLocation)
            .WithName("SensorThings_CreateLocation")
            .WithTags("Locations");

        endpoints.MapPatch($"{basePath}/Locations({{id}})", SensorThingsHandlers.UpdateLocation)
            .WithName("SensorThings_UpdateLocation")
            .WithTags("Locations");

        endpoints.MapDelete($"{basePath}/Locations({{id}})", SensorThingsHandlers.DeleteLocation)
            .WithName("SensorThings_DeleteLocation")
            .WithTags("Locations");

        // ========================================
        // HistoricalLocations
        // ========================================

        endpoints.MapGet($"{basePath}/HistoricalLocations", SensorThingsHandlers.GetHistoricalLocations)
            .WithName("SensorThings_GetHistoricalLocations")
            .WithTags("HistoricalLocations");

        endpoints.MapGet($"{basePath}/HistoricalLocations({{id}})", SensorThingsHandlers.GetHistoricalLocation)
            .WithName("SensorThings_GetHistoricalLocation")
            .WithTags("HistoricalLocations");

        // HistoricalLocations are read-only (created automatically via trigger)

        // ========================================
        // Sensors
        // ========================================

        endpoints.MapGet($"{basePath}/Sensors", SensorThingsHandlers.GetSensors)
            .WithName("SensorThings_GetSensors")
            .WithTags("Sensors");

        endpoints.MapGet($"{basePath}/Sensors({{id}})", SensorThingsHandlers.GetSensor)
            .WithName("SensorThings_GetSensor")
            .WithTags("Sensors");

        endpoints.MapPost($"{basePath}/Sensors", SensorThingsHandlers.CreateSensor)
            .WithName("SensorThings_CreateSensor")
            .WithTags("Sensors");

        endpoints.MapPatch($"{basePath}/Sensors({{id}})", SensorThingsHandlers.UpdateSensor)
            .WithName("SensorThings_UpdateSensor")
            .WithTags("Sensors");

        endpoints.MapDelete($"{basePath}/Sensors({{id}})", SensorThingsHandlers.DeleteSensor)
            .WithName("SensorThings_DeleteSensor")
            .WithTags("Sensors");

        // ========================================
        // ObservedProperties
        // ========================================

        endpoints.MapGet($"{basePath}/ObservedProperties", SensorThingsHandlers.GetObservedProperties)
            .WithName("SensorThings_GetObservedProperties")
            .WithTags("ObservedProperties");

        endpoints.MapGet($"{basePath}/ObservedProperties({{id}})", SensorThingsHandlers.GetObservedProperty)
            .WithName("SensorThings_GetObservedProperty")
            .WithTags("ObservedProperties");

        endpoints.MapPost($"{basePath}/ObservedProperties", SensorThingsHandlers.CreateObservedProperty)
            .WithName("SensorThings_CreateObservedProperty")
            .WithTags("ObservedProperties");

        endpoints.MapPatch($"{basePath}/ObservedProperties({{id}})", SensorThingsHandlers.UpdateObservedProperty)
            .WithName("SensorThings_UpdateObservedProperty")
            .WithTags("ObservedProperties");

        endpoints.MapDelete($"{basePath}/ObservedProperties({{id}})", SensorThingsHandlers.DeleteObservedProperty)
            .WithName("SensorThings_DeleteObservedProperty")
            .WithTags("ObservedProperties");

        // ========================================
        // Datastreams
        // ========================================

        endpoints.MapGet($"{basePath}/Datastreams", SensorThingsHandlers.GetDatastreams)
            .WithName("SensorThings_GetDatastreams")
            .WithTags("Datastreams");

        endpoints.MapGet($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.GetDatastream)
            .WithName("SensorThings_GetDatastream")
            .WithTags("Datastreams");

        endpoints.MapPost($"{basePath}/Datastreams", SensorThingsHandlers.CreateDatastream)
            .WithName("SensorThings_CreateDatastream")
            .WithTags("Datastreams");

        endpoints.MapPatch($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.UpdateDatastream)
            .WithName("SensorThings_UpdateDatastream")
            .WithTags("Datastreams");

        endpoints.MapDelete($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.DeleteDatastream)
            .WithName("SensorThings_DeleteDatastream")
            .WithTags("Datastreams");

        // ========================================
        // Observations (with DataArray detection)
        // ========================================

        endpoints.MapGet($"{basePath}/Observations", SensorThingsHandlers.GetObservations)
            .WithName("SensorThings_GetObservations")
            .WithTags("Observations");

        endpoints.MapGet($"{basePath}/Observations({{id}})", SensorThingsHandlers.GetObservation)
            .WithName("SensorThings_GetObservation")
            .WithTags("Observations");

        // Standards-compliant: Automatically detects DataArray in POST body
        endpoints.MapPost($"{basePath}/Observations", SensorThingsHandlers.CreateObservation)
            .WithName("SensorThings_CreateObservation")
            .WithTags("Observations")
            .Accepts<Models.Observation>("application/json")
            .Accepts<DataArrayRequest>("application/json");

        endpoints.MapPatch($"{basePath}/Observations({{id}})", SensorThingsHandlers.UpdateObservation)
            .WithName("SensorThings_UpdateObservation")
            .WithTags("Observations");

        endpoints.MapDelete($"{basePath}/Observations({{id}})", SensorThingsHandlers.DeleteObservation)
            .WithName("SensorThings_DeleteObservation")
            .WithTags("Observations");

        // ========================================
        // FeaturesOfInterest
        // ========================================

        endpoints.MapGet($"{basePath}/FeaturesOfInterest", SensorThingsHandlers.GetFeaturesOfInterest)
            .WithName("SensorThings_GetFeaturesOfInterest")
            .WithTags("FeaturesOfInterest");

        endpoints.MapGet($"{basePath}/FeaturesOfInterest({{id}})", SensorThingsHandlers.GetFeatureOfInterest)
            .WithName("SensorThings_GetFeatureOfInterest")
            .WithTags("FeaturesOfInterest");

        endpoints.MapPost($"{basePath}/FeaturesOfInterest", SensorThingsHandlers.CreateFeatureOfInterest)
            .WithName("SensorThings_CreateFeatureOfInterest")
            .WithTags("FeaturesOfInterest");

        endpoints.MapPatch($"{basePath}/FeaturesOfInterest({{id}})", SensorThingsHandlers.UpdateFeatureOfInterest)
            .WithName("SensorThings_UpdateFeatureOfInterest")
            .WithTags("FeaturesOfInterest");

        endpoints.MapDelete($"{basePath}/FeaturesOfInterest({{id}})", SensorThingsHandlers.DeleteFeatureOfInterest)
            .WithName("SensorThings_DeleteFeatureOfInterest")
            .WithTags("FeaturesOfInterest");

        // ========================================
        // Navigation Properties
        // ========================================

        // Thing navigation properties
        endpoints.MapGet($"{basePath}/Things({{id}})/Datastreams", SensorThingsHandlers.GetThingDatastreams)
            .WithName("SensorThings_GetThingDatastreams")
            .WithTags("Things", "Navigation");

        endpoints.MapGet($"{basePath}/Things({{id}})/Locations", SensorThingsHandlers.GetThingLocations)
            .WithName("SensorThings_GetThingLocations")
            .WithTags("Things", "Navigation");

        endpoints.MapGet($"{basePath}/Things({{id}})/HistoricalLocations", SensorThingsHandlers.GetThingHistoricalLocations)
            .WithName("SensorThings_GetThingHistoricalLocations")
            .WithTags("Things", "Navigation");

        // Datastream navigation properties
        endpoints.MapGet($"{basePath}/Datastreams({{id}})/Observations", SensorThingsHandlers.GetDatastreamObservations)
            .WithName("SensorThings_GetDatastreamObservations")
            .WithTags("Datastreams", "Navigation");

        // Location navigation properties
        endpoints.MapGet($"{basePath}/Locations({{id}})/Things", SensorThingsHandlers.GetLocationThings)
            .WithName("SensorThings_GetLocationThings")
            .WithTags("Locations", "Navigation");

        // ========================================
        // Mobile-Optimized Extensions
        // ========================================

        // Custom mobile sync endpoint (if enabled)
        if (config.OfflineSyncEnabled)
        {
            endpoints.MapPost($"{basePath}/Sync", SensorThingsHandlers.SyncObservations)
                .WithName("SensorThings_SyncObservations")
                .WithTags("Mobile", "Sync")
                .RequireAuthorization(); // Requires authenticated user
        }

        return endpoints;
    }
}
