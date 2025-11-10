// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;

namespace Honua.Server.Enterprise.Sensors.Data;

/// <summary>
/// Repository interface for SensorThings API entities.
/// Provides CRUD operations and query capabilities for all SensorThings entities.
/// </summary>
public interface ISensorThingsRepository
{
    // ============================================================================
    // Thing operations
    // ============================================================================

    /// <summary>
    /// Gets a Thing by ID with optional navigation property expansion.
    /// </summary>
    Task<Thing?> GetThingAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a paged collection of Things with filtering, sorting, and expansion.
    /// </summary>
    Task<PagedResult<Thing>> GetThingsAsync(QueryOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gets Things associated with a specific user ID.
    /// </summary>
    Task<IReadOnlyList<Thing>> GetThingsByUserAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new Thing.
    /// </summary>
    Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing Thing (partial update).
    /// </summary>
    Task<Thing> UpdateThingAsync(string id, Thing thing, CancellationToken ct = default);

    /// <summary>
    /// Deletes a Thing by ID.
    /// </summary>
    Task DeleteThingAsync(string id, CancellationToken ct = default);

    // ============================================================================
    // Location operations
    // ============================================================================

    Task<Location?> GetLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<Location>> GetLocationsAsync(QueryOptions options, CancellationToken ct = default);
    Task<Location> CreateLocationAsync(Location location, CancellationToken ct = default);
    Task<Location> UpdateLocationAsync(string id, Location location, CancellationToken ct = default);
    Task DeleteLocationAsync(string id, CancellationToken ct = default);

    // ============================================================================
    // HistoricalLocation operations
    // ============================================================================

    Task<HistoricalLocation?> GetHistoricalLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<HistoricalLocation>> GetHistoricalLocationsAsync(QueryOptions options, CancellationToken ct = default);

    // ============================================================================
    // Sensor operations
    // ============================================================================

    Task<Sensor?> GetSensorAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<Sensor>> GetSensorsAsync(QueryOptions options, CancellationToken ct = default);
    Task<Sensor> CreateSensorAsync(Sensor sensor, CancellationToken ct = default);
    Task<Sensor> UpdateSensorAsync(string id, Sensor sensor, CancellationToken ct = default);
    Task DeleteSensorAsync(string id, CancellationToken ct = default);

    // ============================================================================
    // ObservedProperty operations
    // ============================================================================

    Task<ObservedProperty?> GetObservedPropertyAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<ObservedProperty>> GetObservedPropertiesAsync(QueryOptions options, CancellationToken ct = default);
    Task<ObservedProperty> CreateObservedPropertyAsync(ObservedProperty observedProperty, CancellationToken ct = default);
    Task<ObservedProperty> UpdateObservedPropertyAsync(string id, ObservedProperty observedProperty, CancellationToken ct = default);
    Task DeleteObservedPropertyAsync(string id, CancellationToken ct = default);

    // ============================================================================
    // Datastream operations
    // ============================================================================

    Task<Datastream?> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default);
    Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default);
    Task<Datastream> UpdateDatastreamAsync(string id, Datastream datastream, CancellationToken ct = default);
    Task DeleteDatastreamAsync(string id, CancellationToken ct = default);

    // ============================================================================
    // FeatureOfInterest operations
    // ============================================================================

    Task<FeatureOfInterest?> GetFeatureOfInterestAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<FeatureOfInterest>> GetFeaturesOfInterestAsync(QueryOptions options, CancellationToken ct = default);
    Task<FeatureOfInterest> CreateFeatureOfInterestAsync(FeatureOfInterest featureOfInterest, CancellationToken ct = default);
    Task<FeatureOfInterest> UpdateFeatureOfInterestAsync(string id, FeatureOfInterest featureOfInterest, CancellationToken ct = default);
    Task DeleteFeatureOfInterestAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets or creates a FeatureOfInterest based on a geometry.
    /// If a FOI with the same geometry exists, returns it; otherwise creates a new one.
    /// </summary>
    Task<FeatureOfInterest> GetOrCreateFeatureOfInterestAsync(
        string name,
        string description,
        NetTopologySuite.Geometries.Geometry geometry,
        CancellationToken ct = default);

    // ============================================================================
    // Observation operations (critical for mobile)
    // ============================================================================

    Task<Observation?> GetObservationAsync(string id, CancellationToken ct = default);
    Task<PagedResult<Observation>> GetObservationsAsync(QueryOptions options, CancellationToken ct = default);

    /// <summary>
    /// Creates a single observation.
    /// </summary>
    Task<Observation> CreateObservationAsync(Observation observation, CancellationToken ct = default);

    /// <summary>
    /// Creates multiple observations in a batch operation.
    /// Optimized for performance using bulk insert strategies.
    /// </summary>
    Task<IReadOnlyList<Observation>> CreateObservationsBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct = default);

    /// <summary>
    /// Creates observations from a DataArray request.
    /// This is the most efficient way to upload bulk observations from mobile devices.
    /// </summary>
    Task<IReadOnlyList<Observation>> CreateObservationsDataArrayAsync(
        DataArrayRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an observation by ID.
    /// </summary>
    Task DeleteObservationAsync(string id, CancellationToken ct = default);

    // ============================================================================
    // Navigation property queries
    // ============================================================================

    /// <summary>
    /// Gets the Locations associated with a Thing.
    /// </summary>
    Task<PagedResult<Location>> GetThingLocationsAsync(string thingId, QueryOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gets the Datastreams associated with a Thing.
    /// </summary>
    Task<PagedResult<Datastream>> GetThingDatastreamsAsync(string thingId, QueryOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gets the Observations associated with a Datastream.
    /// </summary>
    Task<PagedResult<Observation>> GetDatastreamObservationsAsync(string datastreamId, QueryOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gets the Datastreams associated with a Sensor.
    /// </summary>
    Task<PagedResult<Datastream>> GetSensorDatastreamsAsync(string sensorId, QueryOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gets the Datastreams associated with an ObservedProperty.
    /// </summary>
    Task<PagedResult<Datastream>> GetObservedPropertyDatastreamsAsync(string observedPropertyId, QueryOptions options, CancellationToken ct = default);

    // ============================================================================
    // Mobile-specific operations
    // ============================================================================

    /// <summary>
    /// Processes a sync request from a mobile device.
    /// Handles batch upload of observations and conflict resolution.
    /// </summary>
    Task<SyncResponse> SyncObservationsAsync(SyncRequest request, CancellationToken ct = default);
}
