# Azure Event Grid Integration

The Honua.Server Azure Event Grid integration publishes geospatial events from Honua APIs (Features, SensorThings, GeoEvent) to Azure Event Grid using the CloudEvents v1.0 schema with geospatial extensions.

## Features

- **CloudEvents v1.0 Standard**: Industry-standard event schema with geospatial metadata extensions
- **Async Publishing**: Non-blocking event publishing with automatic batching
- **High Performance**: Batching, circuit breaker, and retry logic for reliability
- **Filtering**: Configurable filters by event type, collection, and tenant
- **Multi-API Support**: Hooks for Features API (WFS-T), SensorThings API, and GeoEvent API
- **Production Ready**: Managed Identity support, comprehensive logging, and metrics

## Architecture

```
┌─────────────────────┐
│  OGC Features API   │─┐
│  (WFS-T)            │ │
└─────────────────────┘ │
                        │
┌─────────────────────┐ │    ┌──────────────────┐    ┌──────────────────┐
│  SensorThings API   │─┼───→│  Event Publisher │───→│  Azure Event Grid│
└─────────────────────┘ │    │  (with batching) │    │  Topic/Domain    │
                        │    └──────────────────┘    └──────────────────┘
┌─────────────────────┐ │              │
│  GeoEvent API       │─┘              │
└─────────────────────┘                ↓
                              ┌──────────────────┐
                              │  Background      │
                              │  Flush Service   │
                              └──────────────────┘
```

## Configuration

### appsettings.json

```json
{
  "Honua": {
    "EventGrid": {
      "Enabled": true,
      "TopicEndpoint": "https://your-topic.westus-1.eventgrid.azure.net/api/events",
      "UseManagedIdentity": true,
      "MaxBatchSize": 100,
      "FlushIntervalSeconds": 10,
      "MaxQueueSize": 10000,
      "BackpressureMode": "Drop",

      "EventTypeFilter": [
        "honua.features.created",
        "honua.features.updated",
        "honua.sensor.observation.created",
        "honua.geoevent.geofence.entered"
      ],

      "CollectionFilter": ["parcels", "buildings"],
      "TenantFilter": ["tenant-abc", "tenant-xyz"],

      "Retry": {
        "MaxRetries": 3,
        "InitialDelaySeconds": 1,
        "MaxDelaySeconds": 30
      },

      "CircuitBreaker": {
        "Enabled": true,
        "FailureThreshold": 5,
        "DurationOfBreakSeconds": 60,
        "SamplingDurationSeconds": 30,
        "MinimumThroughput": 10
      }
    }
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable/disable Event Grid publishing |
| `TopicEndpoint` | string | `null` | Azure Event Grid topic endpoint URL |
| `DomainEndpoint` | string | `null` | Azure Event Grid domain endpoint URL (alternative to topic) |
| `TopicKey` | string | `null` | Access key for authentication (alternative to Managed Identity) |
| `UseManagedIdentity` | bool | `true` | Use Azure Managed Identity (recommended for production) |
| `MaxBatchSize` | int | `100` | Maximum events per batch (1-1000) |
| `FlushIntervalSeconds` | int | `10` | Flush interval for batched events |
| `MaxQueueSize` | int | `10000` | Maximum pending events queue size |
| `BackpressureMode` | enum | `Drop` | `Drop` (drop oldest) or `Block` (wait for space) |
| `EventTypeFilter` | string[] | `[]` | Event types to publish (empty = all) |
| `CollectionFilter` | string[] | `[]` | Collections to publish (empty = all) |
| `TenantFilter` | string[] | `[]` | Tenants to publish (empty = all) |

## CloudEvents Schema

### Base CloudEvent Properties

All Honua events follow the CloudEvents v1.0 specification:

```json
{
  "specversion": "1.0",
  "type": "honua.features.created",
  "source": "honua.io/features/parcels",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "time": "2025-11-10T10:30:00Z",
  "datacontenttype": "application/json",
  "subject": "parcel-12345",
  "data": { ... }
}
```

### Honua Geospatial Extensions

Honua adds geospatial metadata as CloudEvents extensions:

| Extension | Type | Description |
|-----------|------|-------------|
| `honuatenantid` | string | Tenant ID (multi-tenancy) |
| `honuabbox` | number[] | Bounding box [minLon, minLat, maxLon, maxLat] |
| `honuacrs` | string | Coordinate Reference System (e.g., EPSG:4326) |
| `honuacollection` | string | Collection or datastream ID |
| `honuaseverity` | string | Event severity (info, warning, error, critical) |

### Event Type Examples

#### 1. Feature Created

```json
{
  "specversion": "1.0",
  "type": "honua.features.created",
  "source": "honua.io/features/parcels",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "time": "2025-11-10T10:30:00Z",
  "subject": "parcel-12345",
  "honuatenantid": "city-sf",
  "honuabbox": [-122.5, 37.7, -122.3, 37.9],
  "honuacrs": "EPSG:4326",
  "honuacollection": "parcels",
  "data": {
    "collection_id": "parcels",
    "feature_id": "parcel-12345",
    "properties": {
      "owner": "John Doe",
      "address": "123 Main St",
      "area_sqm": 500.5
    },
    "geometry": {
      "type": "Polygon",
      "srid": 4326,
      "coordinates_count": 5
    }
  }
}
```

#### 2. Sensor Observation Created

```json
{
  "specversion": "1.0",
  "type": "honua.sensor.observation.created",
  "source": "honua.io/sensorthings/datastreams/temp-sensor-01",
  "id": "660e8400-e29b-41d4-a716-446655440000",
  "time": "2025-11-10T10:35:00Z",
  "subject": "obs-98765",
  "honuatenantid": "city-sf",
  "honuacollection": "temp-sensor-01",
  "data": {
    "datastream_id": "temp-sensor-01",
    "observation_id": "obs-98765",
    "result": 22.5,
    "phenomenon_time": "2025-11-10T10:35:00Z",
    "parameters": {
      "unit": "celsius",
      "quality": "good"
    }
  }
}
```

#### 3. Geofence Entered

```json
{
  "specversion": "1.0",
  "type": "honua.geoevent.geofence.entered",
  "source": "honua.io/geoevent/entities/vehicle-789",
  "id": "770e8400-e29b-41d4-a716-446655440000",
  "time": "2025-11-10T10:40:00Z",
  "subject": "geofence-downtown",
  "honuatenantid": "city-sf",
  "honuabbox": [-122.4194, 37.7749, -122.4194, 37.7749],
  "data": {
    "entity_id": "vehicle-789",
    "geofence_id": "geofence-downtown",
    "geofence_name": "Downtown Zone",
    "location": {
      "type": "Point",
      "coordinates": [-122.4194, 37.7749]
    },
    "properties": {
      "speed": 45.5,
      "heading": 180
    }
  }
}
```

#### 4. Geofence Alert

```json
{
  "specversion": "1.0",
  "type": "honua.geoevent.geofence.alert",
  "source": "honua.io/geoevent/entities/vehicle-789",
  "id": "880e8400-e29b-41d4-a716-446655440000",
  "time": "2025-11-10T10:45:00Z",
  "subject": "geofence-restricted",
  "honuatenantid": "city-sf",
  "honuaseverity": "critical",
  "honuabbox": [-122.4094, 37.7849, -122.4094, 37.7849],
  "data": {
    "entity_id": "vehicle-789",
    "geofence_id": "geofence-restricted",
    "geofence_name": "Restricted Area",
    "alert_type": "unauthorized_entry",
    "severity": "critical",
    "message": "Vehicle entered restricted zone",
    "location": {
      "type": "Point",
      "coordinates": [-122.4094, 37.7849]
    }
  }
}
```

## Event Types Reference

### Features API Events

| Event Type | Description | Data Payload |
|------------|-------------|--------------|
| `honua.features.created` | Feature created | collection_id, feature_id, properties, geometry |
| `honua.features.updated` | Feature updated | collection_id, feature_id, properties, geometry |
| `honua.features.deleted` | Feature deleted | collection_id, feature_id |
| `honua.features.batch.created` | Batch features created | Same as single (multiple events) |
| `honua.features.batch.updated` | Batch features updated | Same as single (multiple events) |
| `honua.features.batch.deleted` | Batch features deleted | Same as single (multiple events) |

### SensorThings API Events

| Event Type | Description | Data Payload |
|------------|-------------|--------------|
| `honua.sensor.observation.created` | Observation created | datastream_id, observation_id, result, phenomenon_time |
| `honua.sensor.observation.batch.created` | Batch observations created | Same as single (multiple events) |
| `honua.sensor.thing.created` | Thing created | thing_id, name, properties |
| `honua.sensor.thing.updated` | Thing updated | thing_id, name, properties |
| `honua.sensor.location.updated` | Location updated | thing_id, location_id, longitude, latitude |
| `honua.sensor.datastream.updated` | Datastream updated | datastream_id, name |

### GeoEvent API Events

| Event Type | Description | Data Payload |
|------------|-------------|--------------|
| `honua.geoevent.geofence.entered` | Entity entered geofence | entity_id, geofence_id, geofence_name, location |
| `honua.geoevent.geofence.exited` | Entity exited geofence | entity_id, geofence_id, geofence_name, location, dwell_time_seconds |
| `honua.geoevent.geofence.alert` | Geofence alert triggered | entity_id, geofence_id, alert_type, severity, message |
| `honua.geoevent.location.evaluated` | Location evaluated (tracking) | entity_id, location, current_geofences, events_generated |

## Usage

### Setup in Program.cs / Startup.cs

```csharp
using Honua.Server.Core.Cloud.EventGrid.DependencyInjection;

// In ConfigureServices / Program.cs
builder.Services.AddEventGridPublisher(builder.Configuration);
```

### Publish Events from Features API

```csharp
using Honua.Server.Core.Cloud.EventGrid.Hooks;

public class MyFeatureHandler
{
    private readonly IFeatureEventPublisher _eventPublisher;

    public MyFeatureHandler(IFeatureEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task CreateFeatureAsync(...)
    {
        // Create feature logic...

        // Publish event (async, non-blocking)
        await _eventPublisher.PublishFeatureCreatedAsync(
            collectionId: "parcels",
            featureId: feature.Id,
            properties: feature.Properties,
            geometry: feature.Geometry,
            tenantId: GetTenantId()
        );
    }
}
```

### Publish Events from SensorThings API

```csharp
using Honua.Server.Core.Cloud.EventGrid.Hooks;

public class MySensorHandler
{
    private readonly ISensorThingsEventPublisher _eventPublisher;

    public MySensorHandler(ISensorThingsEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task CreateObservationAsync(...)
    {
        // Create observation logic...

        // Publish event
        await _eventPublisher.PublishObservationCreatedAsync(
            datastreamId: observation.DatastreamId,
            observationId: observation.Id,
            result: observation.Result,
            phenomenonTime: observation.PhenomenonTime,
            parameters: observation.Parameters
        );
    }
}
```

### Publish Events from GeoEvent API

```csharp
using Honua.Server.Core.Cloud.EventGrid.Hooks;

public class MyGeoEventHandler
{
    private readonly IGeoEventPublisher _eventPublisher;

    public MyGeoEventHandler(IGeoEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task OnGeofenceEnteredAsync(...)
    {
        // Geofence logic...

        // Publish event
        await _eventPublisher.PublishGeofenceEnteredAsync(
            entityId: entity.Id,
            geofenceId: geofence.Id,
            geofenceName: geofence.Name,
            longitude: location.X,
            latitude: location.Y,
            properties: entity.Properties
        );
    }
}
```

## Azure Event Grid Subscription Patterns

### 1. Subscribe to All Events

```bash
az eventgrid event-subscription create \
  --name honua-all-events \
  --source-resource-id /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventGrid/topics/honua-events \
  --endpoint https://myapp.azurewebsites.net/api/events \
  --included-event-types honua.features.created honua.features.updated honua.sensor.observation.created
```

### 2. Subscribe to Feature Events Only

```bash
az eventgrid event-subscription create \
  --name honua-features \
  --source-resource-id /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventGrid/topics/honua-events \
  --endpoint https://myapp.azurewebsites.net/api/feature-events \
  --advanced-filter data.collection_id StringIn parcels buildings
```

### 3. Subscribe to High-Severity Alerts

```bash
az eventgrid event-subscription create \
  --name honua-critical-alerts \
  --source-resource-id /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventGrid/topics/honua-events \
  --endpoint https://myapp.azurewebsites.net/api/alerts \
  --advanced-filter honuaseverity StringIn critical error
```

### 4. Route to Azure Functions

```bash
az eventgrid event-subscription create \
  --name honua-to-function \
  --source-resource-id /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventGrid/topics/honua-events \
  --endpoint-type azurefunction \
  --endpoint /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{function-app}/functions/{function-name}
```

### 5. Route to Event Hubs (for Streaming)

```bash
az eventgrid event-subscription create \
  --name honua-to-eventhub \
  --source-resource-id /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventGrid/topics/honua-events \
  --endpoint-type eventhub \
  --endpoint /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventHub/namespaces/{ns}/eventhubs/{hub}
```

## Performance & Best Practices

### Batching

- Events are batched automatically based on `MaxBatchSize` and `FlushIntervalSeconds`
- Batch size of 100 and flush interval of 10s is optimal for most scenarios
- For high-volume scenarios (>1000 events/sec), increase `MaxQueueSize` and `MaxBatchSize`

### Backpressure

- **Drop mode** (default): Drops oldest events when queue is full (non-blocking)
- **Block mode**: Blocks API requests until queue has space (may slow down APIs)

### Filtering

Use filters to reduce Event Grid costs and improve performance:

```json
{
  "EventTypeFilter": ["honua.features.created", "honua.features.updated"],
  "CollectionFilter": ["parcels", "buildings"],
  "TenantFilter": ["production-tenant"]
}
```

### Monitoring

Get metrics for monitoring:

```csharp
var metrics = _eventGridPublisher.GetMetrics();
Console.WriteLine($"Published: {metrics.EventsPublished}");
Console.WriteLine($"Failed: {metrics.EventsFailed}");
Console.WriteLine($"Dropped: {metrics.EventsDropped}");
Console.WriteLine($"Circuit: {metrics.CircuitBreakerState}");
```

### Error Handling

- Events are published asynchronously and never throw exceptions to API callers
- Failed events are retried with exponential backoff (configurable)
- Circuit breaker opens after consecutive failures to prevent cascading failures
- All errors are logged with structured logging

## Security

### Managed Identity (Recommended)

```json
{
  "EventGrid": {
    "TopicEndpoint": "https://your-topic.westus-1.eventgrid.azure.net/api/events",
    "UseManagedIdentity": true
  }
}
```

Assign **Event Grid Data Sender** role to the Managed Identity:

```bash
az role assignment create \
  --assignee-object-id <managed-identity-object-id> \
  --role "EventGrid Data Sender" \
  --scope /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.EventGrid/topics/honua-events
```

### Access Key (Alternative)

```json
{
  "EventGrid": {
    "TopicEndpoint": "https://your-topic.westus-1.eventgrid.azure.net/api/events",
    "TopicKey": "your-access-key",
    "UseManagedIdentity": false
  }
}
```

⚠️ Store access keys in Azure Key Vault, not in appsettings.json.

## Troubleshooting

### Events not being published

1. Check `Enabled` is `true`
2. Verify Event Grid endpoint is correct
3. Check authentication (Managed Identity role assignment or access key)
4. Review logs for errors
5. Check circuit breaker state: `metrics.CircuitBreakerState`

### High latency

1. Increase `MaxBatchSize` to reduce HTTP requests
2. Decrease `FlushIntervalSeconds` for more frequent publishing
3. Check Event Grid region (should be same as Honua deployment)

### Events being dropped

1. Increase `MaxQueueSize`
2. Switch to `Block` backpressure mode (if acceptable)
3. Add filters to reduce event volume
4. Scale out Honua instances for higher throughput

## License

Copyright (c) 2025 HonuaIO. Licensed under the Elastic License 2.0.
