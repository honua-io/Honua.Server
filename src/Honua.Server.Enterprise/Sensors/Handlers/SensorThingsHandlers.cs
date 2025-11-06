using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;

namespace Honua.Server.Enterprise.Sensors.Handlers;

/// <summary>
/// HTTP handlers for OGC SensorThings API v1.1 endpoints.
/// Implements the core specification with mobile-optimized extensions.
/// </summary>
public static class SensorThingsHandlers
{
    // ========================================
    // Service Root
    // ========================================

    public static Task<IResult> GetServiceRoot(
        HttpContext context,
        SensorThingsServiceDefinition config)
    {
        var serviceRoot = new
        {
            value = new[]
            {
                new { name = "Things", url = $"{config.BasePath}/Things" },
                new { name = "Locations", url = $"{config.BasePath}/Locations" },
                new { name = "HistoricalLocations", url = $"{config.BasePath}/HistoricalLocations" },
                new { name = "Datastreams", url = $"{config.BasePath}/Datastreams" },
                new { name = "Sensors", url = $"{config.BasePath}/Sensors" },
                new { name = "ObservedProperties", url = $"{config.BasePath}/ObservedProperties" },
                new { name = "Observations", url = $"{config.BasePath}/Observations" },
                new { name = "FeaturesOfInterest", url = $"{config.BasePath}/FeaturesOfInterest" }
            },
            serverSettings = new
            {
                conformance = new[]
                {
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/core",
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/dataArray",
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/create-update-delete",
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/batch-request"
                }
            }
        };

        return Task.FromResult(Results.Json(serviceRoot, CreateJsonOptions()));
    }

    // ========================================
    // Things
    // ========================================

    public static async Task<IResult> GetThings(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetThingsAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Things",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetThing(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var thing = await repository.GetThingAsync(id, expandOptions, ct);

        if (thing == null)
            return Results.NotFound(new { error = $"Thing {id} not found" });

        return Results.Json(thing, CreateJsonOptions());
    }

    public static async Task<IResult> CreateThing(
        HttpContext context,
        ISensorThingsRepository repository,
        Thing thing,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(thing.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (string.IsNullOrWhiteSpace(thing.Description))
            return Results.BadRequest(new { error = "Description is required" });

        // Add user context from JWT if authenticated
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            var properties = thing.Properties ?? new Dictionary<string, object>();
            properties["userId"] = userId;
            thing = thing with { Properties = properties };
        }

        var created = await repository.CreateThingAsync(thing, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> UpdateThing(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        Thing thing,
        CancellationToken ct = default)
    {
        var updated = await repository.UpdateThingAsync(id, thing, ct);
        return Results.Ok(updated);
    }

    public static async Task<IResult> DeleteThing(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteThingAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // Locations
    // ========================================

    public static async Task<IResult> GetLocations(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetLocationsAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Locations",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetLocation(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var location = await repository.GetLocationAsync(id, expandOptions, ct);

        if (location == null)
            return Results.NotFound(new { error = $"Location {id} not found" });

        return Results.Json(location, CreateJsonOptions());
    }

    public static async Task<IResult> CreateLocation(
        ISensorThingsRepository repository,
        Location location,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(location.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (string.IsNullOrWhiteSpace(location.Description))
            return Results.BadRequest(new { error = "Description is required" });

        if (location.Geometry == null)
            return Results.BadRequest(new { error = "Location geometry is required" });

        var created = await repository.CreateLocationAsync(location, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> UpdateLocation(
        ISensorThingsRepository repository,
        string id,
        Location location,
        CancellationToken ct = default)
    {
        var updated = await repository.UpdateLocationAsync(id, location, ct);
        return Results.Ok(updated);
    }

    public static async Task<IResult> DeleteLocation(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteLocationAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // HistoricalLocations
    // ========================================

    public static async Task<IResult> GetHistoricalLocations(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetHistoricalLocationsAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#HistoricalLocations",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetHistoricalLocation(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var historicalLocation = await repository.GetHistoricalLocationAsync(id, expandOptions, ct);

        if (historicalLocation == null)
            return Results.NotFound(new { error = $"HistoricalLocation {id} not found" });

        return Results.Json(historicalLocation, CreateJsonOptions());
    }

    // ========================================
    // Sensors
    // ========================================

    public static async Task<IResult> GetSensors(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetSensorsAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Sensors",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetSensor(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var sensor = await repository.GetSensorAsync(id, expandOptions, ct);

        if (sensor == null)
            return Results.NotFound(new { error = $"Sensor {id} not found" });

        return Results.Json(sensor, CreateJsonOptions());
    }

    public static async Task<IResult> CreateSensor(
        ISensorThingsRepository repository,
        Sensor sensor,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sensor.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (string.IsNullOrWhiteSpace(sensor.Description))
            return Results.BadRequest(new { error = "Description is required" });

        if (string.IsNullOrWhiteSpace(sensor.EncodingType))
            return Results.BadRequest(new { error = "EncodingType is required" });

        var created = await repository.CreateSensorAsync(sensor, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> UpdateSensor(
        ISensorThingsRepository repository,
        string id,
        Sensor sensor,
        CancellationToken ct = default)
    {
        var updated = await repository.UpdateSensorAsync(id, sensor, ct);
        return Results.Ok(updated);
    }

    public static async Task<IResult> DeleteSensor(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteSensorAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // ObservedProperties
    // ========================================

    public static async Task<IResult> GetObservedProperties(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetObservedPropertiesAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#ObservedProperties",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetObservedProperty(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var observedProperty = await repository.GetObservedPropertyAsync(id, expandOptions, ct);

        if (observedProperty == null)
            return Results.NotFound(new { error = $"ObservedProperty {id} not found" });

        return Results.Json(observedProperty, CreateJsonOptions());
    }

    public static async Task<IResult> CreateObservedProperty(
        ISensorThingsRepository repository,
        ObservedProperty observedProperty,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(observedProperty.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (string.IsNullOrWhiteSpace(observedProperty.Description))
            return Results.BadRequest(new { error = "Description is required" });

        if (string.IsNullOrWhiteSpace(observedProperty.Definition))
            return Results.BadRequest(new { error = "Definition is required" });

        var created = await repository.CreateObservedPropertyAsync(observedProperty, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> UpdateObservedProperty(
        ISensorThingsRepository repository,
        string id,
        ObservedProperty observedProperty,
        CancellationToken ct = default)
    {
        var updated = await repository.UpdateObservedPropertyAsync(id, observedProperty, ct);
        return Results.Ok(updated);
    }

    public static async Task<IResult> DeleteObservedProperty(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteObservedPropertyAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // Datastreams
    // ========================================

    public static async Task<IResult> GetDatastreams(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetDatastreamsAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Datastreams",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetDatastream(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var datastream = await repository.GetDatastreamAsync(id, expandOptions, ct);

        if (datastream == null)
            return Results.NotFound(new { error = $"Datastream {id} not found" });

        return Results.Json(datastream, CreateJsonOptions());
    }

    public static async Task<IResult> CreateDatastream(
        ISensorThingsRepository repository,
        Datastream datastream,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(datastream.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (string.IsNullOrWhiteSpace(datastream.Description))
            return Results.BadRequest(new { error = "Description is required" });

        if (string.IsNullOrWhiteSpace(datastream.ObservationType))
            return Results.BadRequest(new { error = "ObservationType is required" });

        if (datastream.UnitOfMeasurement == null)
            return Results.BadRequest(new { error = "UnitOfMeasurement is required" });

        if (string.IsNullOrWhiteSpace(datastream.ThingId))
            return Results.BadRequest(new { error = "Thing reference is required" });

        if (string.IsNullOrWhiteSpace(datastream.SensorId))
            return Results.BadRequest(new { error = "Sensor reference is required" });

        if (string.IsNullOrWhiteSpace(datastream.ObservedPropertyId))
            return Results.BadRequest(new { error = "ObservedProperty reference is required" });

        var created = await repository.CreateDatastreamAsync(datastream, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> UpdateDatastream(
        ISensorThingsRepository repository,
        string id,
        Datastream datastream,
        CancellationToken ct = default)
    {
        var updated = await repository.UpdateDatastreamAsync(id, datastream, ct);
        return Results.Ok(updated);
    }

    public static async Task<IResult> DeleteDatastream(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteDatastreamAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // Observations (with DataArray detection)
    // ========================================

    public static async Task<IResult> GetObservations(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetObservationsAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Observations",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetObservation(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var observation = await repository.GetObservationAsync(id, ct);

        if (observation == null)
            return Results.NotFound(new { error = $"Observation {id} not found" });

        return Results.Json(observation, CreateJsonOptions());
    }

    /// <summary>
    /// Creates observation(s) - automatically detects DataArray format.
    /// Standards-compliant implementation per OGC SensorThings API v1.1.
    /// </summary>
    public static async Task<IResult> CreateObservation(
        HttpContext context,
        ISensorThingsRepository repository,
        JsonElement body,
        CancellationToken ct = default)
    {
        // Detect if this is a DataArray request by checking for "dataArray" property
        if (body.TryGetProperty("dataArray", out _))
        {
            // Route to DataArray handler
            var dataArrayRequest = JsonSerializer.Deserialize<DataArrayRequest>(body.GetRawText(), CreateJsonOptions());
            if (dataArrayRequest == null)
                return Results.BadRequest(new { error = "Invalid DataArray request" });

            return await CreateObservationsDataArray(context, repository, dataArrayRequest, ct);
        }

        // Single observation creation
        var observation = JsonSerializer.Deserialize<Observation>(body.GetRawText(), CreateJsonOptions());
        if (observation == null)
            return Results.BadRequest(new { error = "Invalid observation" });

        if (string.IsNullOrWhiteSpace(observation.DatastreamId))
            return Results.BadRequest(new { error = "Datastream reference is required" });

        if (observation.Result == null)
            return Results.BadRequest(new { error = "Result is required" });

        var created = await repository.CreateObservationAsync(observation, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> DeleteObservation(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteObservationAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // FeaturesOfInterest
    // ========================================

    public static async Task<IResult> GetFeaturesOfInterest(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetFeaturesOfInterestAsync(options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#FeaturesOfInterest",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetFeatureOfInterest(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$expand")] string? expand,
        CancellationToken ct = default)
    {
        var expandOptions = string.IsNullOrEmpty(expand) ? null : ExpandOptions.Parse(expand);
        var featureOfInterest = await repository.GetFeatureOfInterestAsync(id, expandOptions, ct);

        if (featureOfInterest == null)
            return Results.NotFound(new { error = $"FeatureOfInterest {id} not found" });

        return Results.Json(featureOfInterest, CreateJsonOptions());
    }

    public static async Task<IResult> CreateFeatureOfInterest(
        ISensorThingsRepository repository,
        FeatureOfInterest featureOfInterest,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(featureOfInterest.Name))
            return Results.BadRequest(new { error = "Name is required" });

        if (string.IsNullOrWhiteSpace(featureOfInterest.Description))
            return Results.BadRequest(new { error = "Description is required" });

        if (featureOfInterest.Feature == null)
            return Results.BadRequest(new { error = "Feature geometry is required" });

        var created = await repository.CreateFeatureOfInterestAsync(featureOfInterest, ct);
        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> UpdateFeatureOfInterest(
        ISensorThingsRepository repository,
        string id,
        FeatureOfInterest featureOfInterest,
        CancellationToken ct = default)
    {
        var updated = await repository.UpdateFeatureOfInterestAsync(id, featureOfInterest, ct);
        return Results.Ok(updated);
    }

    public static async Task<IResult> DeleteFeatureOfInterest(
        ISensorThingsRepository repository,
        string id,
        CancellationToken ct = default)
    {
        await repository.DeleteFeatureOfInterestAsync(id, ct);
        return Results.NoContent();
    }

    // ========================================
    // Navigation Properties
    // ========================================

    public static async Task<IResult> GetThingDatastreams(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, null, null, orderby, top, skip, count);
        var result = await repository.GetThingDatastreamsAsync(id, options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Datastreams",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetThingLocations(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, null, null, orderby, top, skip, count);
        var result = await repository.GetThingLocationsAsync(id, options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Locations",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    public static async Task<IResult> GetDatastreamObservations(
        HttpContext context,
        ISensorThingsRepository repository,
        string id,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false,
        CancellationToken ct = default)
    {
        var options = QueryOptionsParser.Parse(filter, null, null, orderby, top, skip, count);
        var result = await repository.GetDatastreamObservationsAsync(id, options, ct);

        return Results.Json(new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Observations",
            count = count ? result.TotalCount : null,
            value = result.Items
        }, CreateJsonOptions());
    }

    // ========================================
    // Mobile-Optimized Extensions
    // ========================================

    /// <summary>
    /// DataArray extension handler - called internally by CreateObservation when dataArray detected.
    /// Per OGC SensorThings v1.1 DataArray extension specification.
    /// </summary>
    private static async Task<IResult> CreateObservationsDataArray(
        HttpContext context,
        ISensorThingsRepository repository,
        DataArrayRequest request,
        CancellationToken ct = default)
    {
        if (request.Datastream?.Id == null)
            return Results.BadRequest(new { error = "Datastream reference required" });

        if (request.Components == null || request.DataArray == null)
            return Results.BadRequest(new { error = "Components and dataArray required" });

        var created = await repository.CreateObservationsDataArrayAsync(request, ct);

        return Results.Created($"{GetBaseUrl(context)}/Datastreams({request.Datastream.Id})/Observations", new
        {
            context = $"{GetBaseUrl(context)}/$metadata#Observations",
            value = created
        });
    }

    /// <summary>
    /// Custom mobile sync endpoint for offline data synchronization.
    /// Requires authentication.
    /// </summary>
    public static async Task<IResult> SyncObservations(
        HttpContext context,
        ISensorThingsRepository repository,
        SyncRequest request,
        CancellationToken ct = default)
    {
        // Validate user owns the Thing
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Results.Unauthorized();

        var thing = await repository.GetThingAsync(request.ThingId, ct: ct);

        if (thing?.Properties?.GetValueOrDefault("userId")?.ToString() != userId)
            return Results.Forbid();

        // Process sync
        var response = await repository.SyncObservationsAsync(request, ct);

        return Results.Ok(response);
    }

    // ========================================
    // Helpers
    // ========================================

    private static string GetBaseUrl(HttpContext context)
    {
        return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
}
