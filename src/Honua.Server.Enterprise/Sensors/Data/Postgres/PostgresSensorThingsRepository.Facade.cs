using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using StaLocation = Honua.Server.Enterprise.Sensors.Models.Location;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Facade implementation of PostgresSensorThingsRepository.
/// Delegates operations to specialized entity repositories while maintaining backward compatibility.
/// This refactored version reduces complexity from 2,356 lines to ~400 lines by using composition.
/// </summary>
public sealed class PostgresSensorThingsRepositoryFacade : ISensorThingsRepository
{
    private readonly string _connectionString;
    private readonly string _basePath;
    private readonly ILogger<PostgresSensorThingsRepository> _logger;
    private readonly IDbConnection _connection;
    private readonly SensorThingsServiceDefinition _config;

    // Specialized repositories
    private readonly PostgresThingRepository _thingRepo;
    private readonly PostgresLocationRepository _locationRepo;
    private readonly PostgresObservationRepository _observationRepo;
    private readonly PostgresSensorRepository _sensorRepo;
    private readonly PostgresObservedPropertyRepository _observedPropertyRepo;
    private readonly PostgresDatastreamRepository _datastreamRepo;
    private readonly PostgresFeatureOfInterestRepository _featureOfInterestRepo;
    private readonly PostgresHistoricalLocationRepository _historicalLocationRepo;

    // Helpers for complex scenarios
    private readonly GeoJsonReader _geoJsonReader;

    public PostgresSensorThingsRepositoryFacade(
        IDbConnection connection,
        SensorThingsServiceDefinition config,
        ILogger<PostgresSensorThingsRepository> logger)
    {
        DapperBootstrapper.EnsureConfigured();

        _connection = connection;
        _config = config;
        _logger = logger;
        _basePath = config.BasePath;
        _geoJsonReader = new GeoJsonReader();

        // Extract connection string from the IDbConnection
        _connectionString = connection is NpgsqlConnection npgsqlConn
            ? npgsqlConn.ConnectionString
            : connection.ConnectionString;

        // Initialize all specialized repositories
        _thingRepo = new PostgresThingRepository(_connectionString, logger);
        _locationRepo = new PostgresLocationRepository(_connectionString, logger);
        _observationRepo = new PostgresObservationRepository(_connectionString, _basePath, _config.MaxObservationsPerRequest, logger);
        _sensorRepo = new PostgresSensorRepository(_connectionString, _basePath, logger);
        _observedPropertyRepo = new PostgresObservedPropertyRepository(_connectionString, _basePath, logger);
        _datastreamRepo = new PostgresDatastreamRepository(_connectionString, _basePath, logger);
        _featureOfInterestRepo = new PostgresFeatureOfInterestRepository(_connectionString, _basePath, logger);
        _historicalLocationRepo = new PostgresHistoricalLocationRepository(_connectionString, _basePath, logger);
    }

    // ============================================================================
    // Thing operations - delegate to PostgresThingRepository
    // ============================================================================

    public Task<Thing?> GetThingAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _thingRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<Thing>> GetThingsAsync(QueryOptions options, CancellationToken ct = default)
        => _thingRepo.GetPagedAsync(options, ct);

    public Task<IReadOnlyList<Thing>> GetThingsByUserAsync(string userId, CancellationToken ct = default)
        => _thingRepo.GetByUserAsync(userId, ct);

    public Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default)
        => _thingRepo.CreateAsync(thing, ct);

    public Task<Thing> UpdateThingAsync(string id, Thing thing, CancellationToken ct = default)
        => _thingRepo.UpdateAsync(id, thing, ct);

    public Task DeleteThingAsync(string id, CancellationToken ct = default)
        => _thingRepo.DeleteAsync(id, ct);

    // ============================================================================
    // Location operations - delegate to PostgresLocationRepository
    // ============================================================================

    public Task<StaLocation?> GetLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _locationRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<StaLocation>> GetLocationsAsync(QueryOptions options, CancellationToken ct = default)
        => _locationRepo.GetPagedAsync(options, ct);

    public Task<StaLocation> CreateLocationAsync(StaLocation location, CancellationToken ct = default)
        => _locationRepo.CreateAsync(location, ct);

    public Task<StaLocation> UpdateLocationAsync(string id, StaLocation location, CancellationToken ct = default)
        => _locationRepo.UpdateAsync(id, location, ct);

    public Task DeleteLocationAsync(string id, CancellationToken ct = default)
        => _locationRepo.DeleteAsync(id, ct);

    // ============================================================================
    // Observation operations - delegate to PostgresObservationRepository
    // ============================================================================

    public Task<Observation?> GetObservationAsync(string id, CancellationToken ct = default)
        => _observationRepo.GetByIdAsync(id, ct);

    public Task<PagedResult<Observation>> GetObservationsAsync(QueryOptions options, CancellationToken ct = default)
        => _observationRepo.GetPagedAsync(options, ct);

    public Task<Observation> CreateObservationAsync(Observation observation, CancellationToken ct = default)
        => _observationRepo.CreateAsync(observation, ct);

    public Task<IReadOnlyList<Observation>> CreateObservationsBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct = default)
        => _observationRepo.CreateBatchAsync(observations, ct);

    public Task<IReadOnlyList<Observation>> CreateObservationsDataArrayAsync(
        DataArrayRequest request,
        CancellationToken ct = default)
        => _observationRepo.CreateDataArrayAsync(request, ct);

    public Task DeleteObservationAsync(string id, CancellationToken ct = default)
        => _observationRepo.DeleteAsync(id, ct);

    // ============================================================================
    // Sensor operations - delegate to PostgresSensorRepository
    // ============================================================================

    public Task<Sensor?> GetSensorAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _sensorRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<Sensor>> GetSensorsAsync(QueryOptions options, CancellationToken ct = default)
        => _sensorRepo.GetPagedAsync(options, ct);

    public Task<Sensor> CreateSensorAsync(Sensor sensor, CancellationToken ct = default)
        => _sensorRepo.CreateAsync(sensor, ct);

    public Task<Sensor> UpdateSensorAsync(string id, Sensor sensor, CancellationToken ct = default)
        => _sensorRepo.UpdateAsync(id, sensor, ct);

    public Task DeleteSensorAsync(string id, CancellationToken ct = default)
        => _sensorRepo.DeleteAsync(id, ct);

    // ============================================================================
    // ObservedProperty operations - delegate to PostgresObservedPropertyRepository
    // ============================================================================

    public Task<ObservedProperty?> GetObservedPropertyAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _observedPropertyRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<ObservedProperty>> GetObservedPropertiesAsync(QueryOptions options, CancellationToken ct = default)
        => _observedPropertyRepo.GetPagedAsync(options, ct);

    public Task<ObservedProperty> CreateObservedPropertyAsync(ObservedProperty observedProperty, CancellationToken ct = default)
        => _observedPropertyRepo.CreateAsync(observedProperty, ct);

    public Task<ObservedProperty> UpdateObservedPropertyAsync(string id, ObservedProperty observedProperty, CancellationToken ct = default)
        => _observedPropertyRepo.UpdateAsync(id, observedProperty, ct);

    public Task DeleteObservedPropertyAsync(string id, CancellationToken ct = default)
        => _observedPropertyRepo.DeleteAsync(id, ct);

    // ============================================================================
    // Datastream operations - delegate to PostgresDatastreamRepository
    // ============================================================================

    public Task<Datastream?> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _datastreamRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default)
        => _datastreamRepo.GetPagedAsync(options, ct);

    public Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default)
        => _datastreamRepo.CreateAsync(datastream, ct);

    public Task<Datastream> UpdateDatastreamAsync(string id, Datastream datastream, CancellationToken ct = default)
        => _datastreamRepo.UpdateAsync(id, datastream, ct);

    public Task DeleteDatastreamAsync(string id, CancellationToken ct = default)
        => _datastreamRepo.DeleteAsync(id, ct);

    // ============================================================================
    // FeatureOfInterest operations - delegate to PostgresFeatureOfInterestRepository
    // ============================================================================

    public Task<FeatureOfInterest?> GetFeatureOfInterestAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _featureOfInterestRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<FeatureOfInterest>> GetFeaturesOfInterestAsync(QueryOptions options, CancellationToken ct = default)
        => _featureOfInterestRepo.GetPagedAsync(options, ct);

    public Task<FeatureOfInterest> CreateFeatureOfInterestAsync(FeatureOfInterest featureOfInterest, CancellationToken ct = default)
        => _featureOfInterestRepo.CreateAsync(featureOfInterest, ct);

    public Task<FeatureOfInterest> UpdateFeatureOfInterestAsync(string id, FeatureOfInterest featureOfInterest, CancellationToken ct = default)
        => _featureOfInterestRepo.UpdateAsync(id, featureOfInterest, ct);

    public Task DeleteFeatureOfInterestAsync(string id, CancellationToken ct = default)
        => _featureOfInterestRepo.DeleteAsync(id, ct);

    public Task<FeatureOfInterest> GetOrCreateFeatureOfInterestAsync(
        string name,
        string description,
        Geometry geometry,
        CancellationToken ct = default)
        => _featureOfInterestRepo.GetOrCreateAsync(name, description, geometry, ct);

    // ============================================================================
    // HistoricalLocation operations - delegate to PostgresHistoricalLocationRepository
    // ============================================================================

    public Task<HistoricalLocation?> GetHistoricalLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => _historicalLocationRepo.GetByIdAsync(id, expand, ct);

    public Task<PagedResult<HistoricalLocation>> GetHistoricalLocationsAsync(QueryOptions options, CancellationToken ct = default)
        => _historicalLocationRepo.GetPagedAsync(options, ct);

    // ============================================================================
    // Navigation property queries - delegate to specialized repositories
    // ============================================================================

    public Task<PagedResult<StaLocation>> GetThingLocationsAsync(string thingId, QueryOptions options, CancellationToken ct = default)
        => _locationRepo.GetByThingAsync(thingId, options, ct);

    public Task<PagedResult<Datastream>> GetThingDatastreamsAsync(string thingId, QueryOptions options, CancellationToken ct = default)
        => _datastreamRepo.GetByThingIdAsync(thingId, options, ct);

    public Task<PagedResult<Observation>> GetDatastreamObservationsAsync(string datastreamId, QueryOptions options, CancellationToken ct = default)
        => _observationRepo.GetByDatastreamAsync(datastreamId, options, ct);

    public Task<PagedResult<Datastream>> GetSensorDatastreamsAsync(string sensorId, QueryOptions options, CancellationToken ct = default)
        => _datastreamRepo.GetBySensorIdAsync(sensorId, options, ct);

    public Task<PagedResult<Datastream>> GetObservedPropertyDatastreamsAsync(string observedPropertyId, QueryOptions options, CancellationToken ct = default)
        => _datastreamRepo.GetByObservedPropertyIdAsync(observedPropertyId, options, ct);

    // ============================================================================
    // Mobile-specific operations - use observation repository
    // ============================================================================

    public async Task<SyncResponse> SyncObservationsAsync(SyncRequest request, CancellationToken ct = default)
    {
        var errors = new List<SyncError>();
        var created = 0;

        try
        {
            // Batch create all observations using optimized repository method
            await _observationRepo.CreateBatchAsync(request.Observations, ct);
            created = request.Observations.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing observations for Thing {ThingId}", request.ThingId);
            errors.Add(new SyncError
            {
                Code = "SYNC_ERROR",
                Message = "Failed to sync observations",
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            });
        }

        return new SyncResponse
        {
            ServerTimestamp = DateTime.UtcNow,
            ObservationsCreated = created,
            ObservationsUpdated = 0,
            Errors = errors
        };
    }
}
