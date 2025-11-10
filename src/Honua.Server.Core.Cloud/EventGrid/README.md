# Azure Event Grid Integration for Honua.Server

Production-ready Azure Event Grid publisher that publishes geospatial events from Honua APIs using CloudEvents v1.0 schema.

## Quick Start

### 1. Configure Event Grid

Add to `appsettings.json`:

```json
{
  "Honua": {
    "EventGrid": {
      "Enabled": true,
      "TopicEndpoint": "https://your-topic.westus-1.eventgrid.azure.net/api/events",
      "UseManagedIdentity": true,
      "MaxBatchSize": 100,
      "FlushIntervalSeconds": 10
    }
  }
}
```

### 2. Register Services

In `Program.cs`:

```csharp
using Honua.Server.Core.Cloud.EventGrid.DependencyInjection;

builder.Services.AddEventGridPublisher(builder.Configuration);
```

### 3. Publish Events

```csharp
using Honua.Server.Core.Cloud.EventGrid.Hooks;

public class MyHandler
{
    private readonly IFeatureEventPublisher _eventPublisher;

    public async Task CreateFeatureAsync(...)
    {
        // Your feature creation logic...

        // Publish event (async, non-blocking, batched)
        await _eventPublisher.PublishFeatureCreatedAsync(
            collectionId: "parcels",
            featureId: feature.Id,
            properties: feature.Properties,
            geometry: feature.Geometry
        );
    }
}
```

## Features

✅ **CloudEvents v1.0** - Industry standard event schema
✅ **Geospatial Extensions** - Bounding box, CRS, tenant ID
✅ **Async Batching** - Non-blocking with automatic batching
✅ **Resilience** - Circuit breaker + exponential backoff retry
✅ **Filtering** - By event type, collection, tenant
✅ **Multi-API** - Features, SensorThings, GeoEvent
✅ **Managed Identity** - Azure AD authentication
✅ **Production Ready** - Comprehensive logging & metrics

## Architecture

- **Publisher Service**: Core async publisher with batching and queuing
- **Background Service**: Periodic batch flushing (configurable interval)
- **Event Hooks**: Integration points for Features, SensorThings, GeoEvent APIs
- **Resilience Pipeline**: Polly-based retry and circuit breaker
- **CloudEvents**: Standard schema with Honua geospatial extensions

## Event Types

### Features API (OGC API - Features)
- `honua.features.created`
- `honua.features.updated`
- `honua.features.deleted`
- `honua.features.batch.*`

### SensorThings API (OGC SensorThings)
- `honua.sensor.observation.created`
- `honua.sensor.thing.created/updated`
- `honua.sensor.location.updated`
- `honua.sensor.datastream.updated`

### GeoEvent API (Geofencing)
- `honua.geoevent.geofence.entered`
- `honua.geoevent.geofence.exited`
- `honua.geoevent.geofence.alert`
- `honua.geoevent.location.evaluated`

## Documentation

See [Azure Event Grid Integration Guide](../../../../docs/integrations/azure-event-grid.md) for:
- Complete configuration reference
- CloudEvents schema examples
- Azure Event Grid subscription patterns
- Performance tuning
- Security best practices
- Troubleshooting

## Project Structure

```
EventGrid/
├── Configuration/
│   └── EventGridOptions.cs          # Configuration model with validation
├── Models/
│   └── HonuaCloudEvent.cs          # CloudEvents v1.0 model + builder
├── Services/
│   ├── IEventGridPublisher.cs      # Publisher interface
│   ├── EventGridPublisher.cs       # Core publisher implementation
│   └── EventGridBackgroundPublisher.cs  # Background flush service
├── Hooks/
│   ├── IFeatureEventPublisher.cs   # Features API hook interface
│   ├── FeatureEventPublisher.cs    # Features API hook implementation
│   ├── ISensorThingsEventPublisher.cs
│   ├── SensorThingsEventPublisher.cs
│   ├── IGeoEventPublisher.cs
│   └── GeoEventPublisher.cs
├── DependencyInjection/
│   └── EventGridServiceCollectionExtensions.cs
└── README.md
```

## Testing

Unit tests are located in:
- `/tests/Honua.Server.Core.Tests.Infrastructure/EventGrid/`

Run tests:
```bash
dotnet test tests/Honua.Server.Core.Tests.Infrastructure
```

## Performance

- **Batching**: Up to 100 events per batch (configurable)
- **Throughput**: 1000+ events/sec per instance
- **Latency**: < 10ms to queue (non-blocking)
- **Queue Size**: 10,000 pending events (configurable)
- **Backpressure**: Drop oldest or block (configurable)

## License

Copyright (c) 2025 HonuaIO. Licensed under the Elastic License 2.0.
