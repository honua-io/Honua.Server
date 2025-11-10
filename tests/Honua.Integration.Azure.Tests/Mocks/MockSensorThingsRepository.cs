// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;

namespace Honua.Integration.Azure.Tests.Mocks;

/// <summary>
/// In-memory mock implementation of ISensorThingsRepository for E2E testing.
/// Provides full CRUD operations without requiring a database.
/// </summary>
public class MockSensorThingsRepository : ISensorThingsRepository
{
    private readonly Dictionary<string, Thing> _things = new();
    private readonly Dictionary<string, Location> _locations = new();
    private readonly Dictionary<string, HistoricalLocation> _historicalLocations = new();
    private readonly Dictionary<string, Sensor> _sensors = new();
    private readonly Dictionary<string, ObservedProperty> _observedProperties = new();
    private readonly Dictionary<string, Datastream> _datastreams = new();
    private readonly Dictionary<string, FeatureOfInterest> _featuresOfInterest = new();
    private readonly Dictionary<string, Observation> _observations = new();

    // Navigation mappings
    private readonly Dictionary<string, List<string>> _thingLocations = new(); // ThingId -> LocationIds
    private readonly Dictionary<string, List<string>> _thingDatastreams = new(); // ThingId -> DatastreamIds
    private readonly Dictionary<string, List<string>> _datastreamObservations = new(); // DatastreamId -> ObservationIds

    public Task ClearAllAsync()
    {
        _things.Clear();
        _locations.Clear();
        _historicalLocations.Clear();
        _sensors.Clear();
        _observedProperties.Clear();
        _datastreams.Clear();
        _featuresOfInterest.Clear();
        _observations.Clear();
        _thingLocations.Clear();
        _thingDatastreams.Clear();
        _datastreamObservations.Clear();
        return Task.CompletedTask;
    }

    // ============================================================================
    // Thing operations
    // ============================================================================

    public Task<Thing?> GetThingAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _things.TryGetValue(id, out var thing);
        return Task.FromResult(thing);
    }

    public Task<PagedResult<Thing>> GetThingsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var things = _things.Values.AsEnumerable();

        // Apply filter
        if (!string.IsNullOrEmpty(options.Filter))
        {
            things = ApplyFilter(things, options.Filter);
        }

        // Apply ordering
        if (!string.IsNullOrEmpty(options.OrderBy))
        {
            things = ApplyOrderBy(things, options.OrderBy);
        }

        // Apply paging
        var count = things.Count();
        var skip = options.Skip ?? 0;
        var top = options.Top ?? 100;

        var pagedThings = things.Skip(skip).Take(top).ToList();

        return Task.FromResult(new PagedResult<Thing>
        {
            Values = pagedThings,
            Count = count,
            NextLink = skip + top < count ? $"?$skip={skip + top}" : null
        });
    }

    public Task<IReadOnlyList<Thing>> GetThingsByUserAsync(string userId, CancellationToken ct = default)
    {
        var things = _things.Values
            .Where(t => t.Properties.ContainsKey("userId") && t.Properties["userId"]?.ToString() == userId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Thing>>(things);
    }

    public Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(thing.Id))
        {
            thing = thing with { Id = Guid.NewGuid().ToString() };
        }

        _things[thing.Id] = thing;
        _thingDatastreams[thing.Id] = new List<string>();
        return Task.FromResult(thing);
    }

    public Task<Thing> UpdateThingAsync(string id, Thing thing, CancellationToken ct = default)
    {
        if (!_things.ContainsKey(id))
        {
            throw new KeyNotFoundException($"Thing with ID {id} not found");
        }

        var updated = thing with { Id = id };
        _things[id] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteThingAsync(string id, CancellationToken ct = default)
    {
        _things.Remove(id);
        _thingDatastreams.Remove(id);
        return Task.CompletedTask;
    }

    // ============================================================================
    // Location operations
    // ============================================================================

    public Task<Location?> GetLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _locations.TryGetValue(id, out var location);
        return Task.FromResult(location);
    }

    public Task<PagedResult<Location>> GetLocationsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var locations = _locations.Values.ToList();
        return Task.FromResult(new PagedResult<Location>
        {
            Values = locations,
            Count = locations.Count
        });
    }

    public Task<Location> CreateLocationAsync(Location location, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(location.Id))
        {
            location = location with { Id = Guid.NewGuid().ToString() };
        }

        _locations[location.Id] = location;
        return Task.FromResult(location);
    }

    public Task<Location> UpdateLocationAsync(string id, Location location, CancellationToken ct = default)
    {
        var updated = location with { Id = id };
        _locations[id] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteLocationAsync(string id, CancellationToken ct = default)
    {
        _locations.Remove(id);
        return Task.CompletedTask;
    }

    // ============================================================================
    // HistoricalLocation operations
    // ============================================================================

    public Task<HistoricalLocation?> GetHistoricalLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _historicalLocations.TryGetValue(id, out var hl);
        return Task.FromResult(hl);
    }

    public Task<PagedResult<HistoricalLocation>> GetHistoricalLocationsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var hls = _historicalLocations.Values.ToList();
        return Task.FromResult(new PagedResult<HistoricalLocation>
        {
            Values = hls,
            Count = hls.Count
        });
    }

    // ============================================================================
    // Sensor operations
    // ============================================================================

    public Task<Sensor?> GetSensorAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _sensors.TryGetValue(id, out var sensor);
        return Task.FromResult(sensor);
    }

    public Task<PagedResult<Sensor>> GetSensorsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sensors = _sensors.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(options.Filter))
        {
            sensors = ApplyFilter(sensors, options.Filter);
        }

        var sensorList = sensors.ToList();
        return Task.FromResult(new PagedResult<Sensor>
        {
            Values = sensorList,
            Count = sensorList.Count
        });
    }

    public Task<Sensor> CreateSensorAsync(Sensor sensor, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sensor.Id))
        {
            sensor = sensor with { Id = Guid.NewGuid().ToString() };
        }

        _sensors[sensor.Id] = sensor;
        return Task.FromResult(sensor);
    }

    public Task<Sensor> UpdateSensorAsync(string id, Sensor sensor, CancellationToken ct = default)
    {
        var updated = sensor with { Id = id };
        _sensors[id] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteSensorAsync(string id, CancellationToken ct = default)
    {
        _sensors.Remove(id);
        return Task.CompletedTask;
    }

    // ============================================================================
    // ObservedProperty operations
    // ============================================================================

    public Task<ObservedProperty?> GetObservedPropertyAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _observedProperties.TryGetValue(id, out var op);
        return Task.FromResult(op);
    }

    public Task<PagedResult<ObservedProperty>> GetObservedPropertiesAsync(QueryOptions options, CancellationToken ct = default)
    {
        var ops = _observedProperties.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(options.Filter))
        {
            ops = ApplyFilter(ops, options.Filter);
        }

        var opList = ops.ToList();
        return Task.FromResult(new PagedResult<ObservedProperty>
        {
            Values = opList,
            Count = opList.Count
        });
    }

    public Task<ObservedProperty> CreateObservedPropertyAsync(ObservedProperty observedProperty, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(observedProperty.Id))
        {
            observedProperty = observedProperty with { Id = Guid.NewGuid().ToString() };
        }

        _observedProperties[observedProperty.Id] = observedProperty;
        return Task.FromResult(observedProperty);
    }

    public Task<ObservedProperty> UpdateObservedPropertyAsync(string id, ObservedProperty observedProperty, CancellationToken ct = default)
    {
        var updated = observedProperty with { Id = id };
        _observedProperties[id] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteObservedPropertyAsync(string id, CancellationToken ct = default)
    {
        _observedProperties.Remove(id);
        return Task.CompletedTask;
    }

    // ============================================================================
    // Datastream operations
    // ============================================================================

    public Task<Datastream?> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _datastreams.TryGetValue(id, out var ds);
        return Task.FromResult(ds);
    }

    public Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var datastreams = _datastreams.Values.ToList();
        return Task.FromResult(new PagedResult<Datastream>
        {
            Values = datastreams,
            Count = datastreams.Count
        });
    }

    public Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(datastream.Id))
        {
            datastream = datastream with { Id = Guid.NewGuid().ToString() };
        }

        _datastreams[datastream.Id] = datastream;

        // Track navigation
        if (!_thingDatastreams.ContainsKey(datastream.ThingId))
        {
            _thingDatastreams[datastream.ThingId] = new List<string>();
        }
        _thingDatastreams[datastream.ThingId].Add(datastream.Id);
        _datastreamObservations[datastream.Id] = new List<string>();

        return Task.FromResult(datastream);
    }

    public Task<Datastream> UpdateDatastreamAsync(string id, Datastream datastream, CancellationToken ct = default)
    {
        var updated = datastream with { Id = id };
        _datastreams[id] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteDatastreamAsync(string id, CancellationToken ct = default)
    {
        _datastreams.Remove(id);
        _datastreamObservations.Remove(id);
        return Task.CompletedTask;
    }

    // ============================================================================
    // FeatureOfInterest operations
    // ============================================================================

    public Task<FeatureOfInterest?> GetFeatureOfInterestAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        _featuresOfInterest.TryGetValue(id, out var foi);
        return Task.FromResult(foi);
    }

    public Task<PagedResult<FeatureOfInterest>> GetFeaturesOfInterestAsync(QueryOptions options, CancellationToken ct = default)
    {
        var fois = _featuresOfInterest.Values.ToList();
        return Task.FromResult(new PagedResult<FeatureOfInterest>
        {
            Values = fois,
            Count = fois.Count
        });
    }

    public Task<FeatureOfInterest> CreateFeatureOfInterestAsync(FeatureOfInterest featureOfInterest, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(featureOfInterest.Id))
        {
            featureOfInterest = featureOfInterest with { Id = Guid.NewGuid().ToString() };
        }

        _featuresOfInterest[featureOfInterest.Id] = featureOfInterest;
        return Task.FromResult(featureOfInterest);
    }

    public Task<FeatureOfInterest> UpdateFeatureOfInterestAsync(string id, FeatureOfInterest featureOfInterest, CancellationToken ct = default)
    {
        var updated = featureOfInterest with { Id = id };
        _featuresOfInterest[id] = updated;
        return Task.FromResult(updated);
    }

    public Task DeleteFeatureOfInterestAsync(string id, CancellationToken ct = default)
    {
        _featuresOfInterest.Remove(id);
        return Task.CompletedTask;
    }

    public Task<FeatureOfInterest> GetOrCreateFeatureOfInterestAsync(
        string name,
        string description,
        NetTopologySuite.Geometries.Geometry geometry,
        CancellationToken ct = default)
    {
        var existing = _featuresOfInterest.Values.FirstOrDefault(f => f.Name == name);
        if (existing != null)
        {
            return Task.FromResult(existing);
        }

        var foi = new FeatureOfInterest
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            EncodingType = "application/geo+json",
            Feature = geometry
        };

        _featuresOfInterest[foi.Id] = foi;
        return Task.FromResult(foi);
    }

    // ============================================================================
    // Observation operations
    // ============================================================================

    public Task<Observation?> GetObservationAsync(string id, CancellationToken ct = default)
    {
        _observations.TryGetValue(id, out var obs);
        return Task.FromResult(obs);
    }

    public Task<PagedResult<Observation>> GetObservationsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var observations = _observations.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(options.OrderBy))
        {
            observations = ApplyOrderBy(observations, options.OrderBy);
        }

        var obsList = observations.ToList();
        return Task.FromResult(new PagedResult<Observation>
        {
            Values = obsList,
            Count = obsList.Count
        });
    }

    public Task<Observation> CreateObservationAsync(Observation observation, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(observation.Id))
        {
            observation = observation with { Id = Guid.NewGuid().ToString() };
        }

        _observations[observation.Id] = observation;

        // Track navigation
        if (_datastreamObservations.ContainsKey(observation.DatastreamId))
        {
            _datastreamObservations[observation.DatastreamId].Add(observation.Id);
        }

        return Task.FromResult(observation);
    }

    public Task<IReadOnlyList<Observation>> CreateObservationsBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct = default)
    {
        var created = new List<Observation>();

        foreach (var obs in observations)
        {
            var createdObs = CreateObservationAsync(obs, ct).Result;
            created.Add(createdObs);
        }

        return Task.FromResult<IReadOnlyList<Observation>>(created);
    }

    public Task<IReadOnlyList<Observation>> CreateObservationsDataArrayAsync(
        DataArrayRequest request,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteObservationAsync(string id, CancellationToken ct = default)
    {
        _observations.Remove(id);
        return Task.CompletedTask;
    }

    // ============================================================================
    // Navigation property queries
    // ============================================================================

    public Task<PagedResult<Location>> GetThingLocationsAsync(string thingId, QueryOptions options, CancellationToken ct = default)
    {
        var locationIds = _thingLocations.GetValueOrDefault(thingId) ?? new List<string>();
        var locations = locationIds.Select(id => _locations[id]).ToList();

        return Task.FromResult(new PagedResult<Location>
        {
            Values = locations,
            Count = locations.Count
        });
    }

    public Task<PagedResult<Datastream>> GetThingDatastreamsAsync(string thingId, QueryOptions options, CancellationToken ct = default)
    {
        var datastreamIds = _thingDatastreams.GetValueOrDefault(thingId) ?? new List<string>();
        var datastreams = datastreamIds
            .Where(id => _datastreams.ContainsKey(id))
            .Select(id => _datastreams[id])
            .AsEnumerable();

        if (!string.IsNullOrEmpty(options.Filter))
        {
            datastreams = ApplyFilter(datastreams, options.Filter);
        }

        var dsList = datastreams.ToList();
        return Task.FromResult(new PagedResult<Datastream>
        {
            Values = dsList,
            Count = dsList.Count
        });
    }

    public Task<PagedResult<Observation>> GetDatastreamObservationsAsync(string datastreamId, QueryOptions options, CancellationToken ct = default)
    {
        var observationIds = _datastreamObservations.GetValueOrDefault(datastreamId) ?? new List<string>();
        var observations = observationIds.Select(id => _observations[id]).ToList();

        return Task.FromResult(new PagedResult<Observation>
        {
            Values = observations,
            Count = observations.Count
        });
    }

    public Task<PagedResult<Datastream>> GetSensorDatastreamsAsync(string sensorId, QueryOptions options, CancellationToken ct = default)
    {
        var datastreams = _datastreams.Values.Where(ds => ds.SensorId == sensorId).ToList();
        return Task.FromResult(new PagedResult<Datastream>
        {
            Values = datastreams,
            Count = datastreams.Count
        });
    }

    public Task<PagedResult<Datastream>> GetObservedPropertyDatastreamsAsync(string observedPropertyId, QueryOptions options, CancellationToken ct = default)
    {
        var datastreams = _datastreams.Values.Where(ds => ds.ObservedPropertyId == observedPropertyId).ToList();
        return Task.FromResult(new PagedResult<Datastream>
        {
            Values = datastreams,
            Count = datastreams.Count
        });
    }

    // ============================================================================
    // Mobile-specific operations
    // ============================================================================

    public Task<SyncResponse> SyncObservationsAsync(SyncRequest request, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    // ============================================================================
    // Helper methods for filtering and ordering
    // ============================================================================

    private IEnumerable<T> ApplyFilter<T>(IEnumerable<T> items, string filter)
    {
        // Simplified filter implementation for testing
        // In real implementation, this would parse OData filter syntax

        if (filter.Contains("properties/deviceId eq"))
        {
            var deviceId = ExtractFilterValue(filter);
            return items.Where(item =>
            {
                if (item is Thing thing)
                {
                    return thing.Properties.ContainsKey("deviceId") &&
                           thing.Properties["deviceId"]?.ToString() == deviceId;
                }
                return true;
            });
        }

        if (filter.Contains("properties/telemetryField eq"))
        {
            var fieldName = ExtractFilterValue(filter);
            return items.Where(item =>
            {
                if (item is Datastream ds)
                {
                    return ds.Properties.ContainsKey("telemetryField") &&
                           ds.Properties["telemetryField"]?.ToString() == fieldName;
                }
                if (item is Sensor sensor)
                {
                    return sensor.Properties.ContainsKey("telemetryField") &&
                           sensor.Properties["telemetryField"]?.ToString() == fieldName;
                }
                return true;
            });
        }

        if (filter.Contains("properties/tenantId eq"))
        {
            var tenantId = ExtractFilterValue(filter);
            return items.Where(item =>
            {
                if (item is Thing thing)
                {
                    return thing.Properties.ContainsKey("tenantId") &&
                           thing.Properties["tenantId"]?.ToString() == tenantId;
                }
                return true;
            });
        }

        if (filter.Contains("name eq"))
        {
            var name = ExtractFilterValue(filter);
            return items.Where(item =>
            {
                if (item is Sensor sensor) return sensor.Name == name;
                if (item is ObservedProperty op) return op.Name == name;
                return true;
            });
        }

        return items;
    }

    private IEnumerable<T> ApplyOrderBy<T>(IEnumerable<T> items, string orderBy)
    {
        if (orderBy.Contains("phenomenonTime"))
        {
            var ascending = orderBy.Contains("asc");
            return items.Cast<Observation>()
                .OrderBy(o => ascending ? o.PhenomenonTime : DateTime.MaxValue)
                .ThenByDescending(o => ascending ? DateTime.MaxValue : o.PhenomenonTime)
                .Cast<T>();
        }

        return items;
    }

    private string ExtractFilterValue(string filter)
    {
        var startIndex = filter.IndexOf('\'') + 1;
        var endIndex = filter.LastIndexOf('\'');
        if (startIndex > 0 && endIndex > startIndex)
        {
            return filter.Substring(startIndex, endIndex - startIndex);
        }
        return string.Empty;
    }
}
