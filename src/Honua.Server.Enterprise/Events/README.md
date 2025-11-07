# GeoEvent / Geofencing Module

Enterprise-tier feature for real-time geospatial event detection and geofencing.

## Overview

The GeoEvent module provides real-time geofencing capabilities, enabling applications to detect when entities (vehicles, assets, people) enter, exit, or dwell within defined geographic boundaries. It combines PostgreSQL/PostGIS spatial queries with efficient state tracking to deliver sub-100ms latency for location evaluations.

**Key Capabilities:**
- Real-time location evaluation against active geofences
- Enter/Exit/Dwell event generation with state tracking
- PostgreSQL/PostGIS spatial indexing for high performance
- Batch location processing (up to 1,000 locations per request)
- Azure Stream Analytics integration (Phase 2)
- Event notification via webhooks and email
- Multi-tenant isolation
- Alert integration for event-driven workflows

## Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Applications                      │
│  (Mobile Apps, IoT Devices, Fleet Management Systems)       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────┐
│                    API Endpoints                             │
│  POST /api/v1/geoevent/evaluate                             │
│  POST /api/v1/geoevent/evaluate/batch                       │
│  GET/POST/PUT/DELETE /api/v1/geofences                      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────┐
│           GeofenceEvaluationService                          │
│  • Load active geofences at point (ST_Contains)             │
│  • Compare with entity's previous state                      │
│  • Generate Enter/Exit events                               │
│  • Calculate dwell time for exits                           │
│  • Persist events to database                               │
└──────┬──────────────────────┬───────────────────────────────┘
       │                      │
       v                      v
┌──────────────┐      ┌──────────────────┐
│ PostgreSQL   │      │ Entity State     │
│ Geofences    │      │ Repository       │
│ (Spatial     │      │ (In-Memory or    │
│  Index)      │      │  PostgreSQL)     │
└──────────────┘      └──────────────────┘
       │
       v
┌─────────────────────────────────────────────────────────────┐
│           Event Notification Pipeline                        │
│  • GeofenceToAlertBridgeService (integrates with Alerts)    │
│  • WebhookNotifier (HTTP callbacks)                         │
│  • EmailNotifier (SMTP notifications)                       │
└─────────────────────────────────────────────────────────────┘
```

### Event Detection Flow

```
1. Client POSTs location
   ↓
2. Parse coordinates (longitude, latitude)
   ↓
3. Query geofences containing point:
   SELECT * FROM geofences
   WHERE ST_Contains(geometry, ST_Point(@lon, @lat))
     AND is_active = true
     AND tenant_id = @tenantId
   ↓
4. Load entity's previous state (HashSet lookup)
   ↓
5. Detect state changes:
   • Enter: In new geofence, not in previous state
   • Exit: In previous state, not in new geofence
   ↓
6. Generate events and update state
   ↓
7. Persist events to database
   ↓
8. Send notifications (webhooks, email, alerts)
   ↓
9. Return response with events and current geofences
```

## Event Types

### 1. ENTER Event

**Triggered:** When an entity enters a geofence (was outside, now inside).

**Example:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "event_type": "Enter",
  "event_time": "2025-11-05T10:30:00Z",
  "geofence_id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "geofence_name": "Downtown District",
  "entity_id": "vehicle-123",
  "entity_type": "vehicle",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "speed": 45.5,
    "heading": 180,
    "driver_id": "D-456"
  },
  "processed_at": "2025-11-05T10:30:00.123Z"
}
```

### 2. EXIT Event

**Triggered:** When an entity exits a geofence (was inside, now outside).

**Includes:** Dwell time calculation (time spent inside geofence).

**Example:**
```json
{
  "id": "650e8400-e29b-41d4-a716-446655440001",
  "event_type": "Exit",
  "event_time": "2025-11-05T11:15:00Z",
  "geofence_id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "geofence_name": "Downtown District",
  "entity_id": "vehicle-123",
  "entity_type": "vehicle",
  "location": {
    "type": "Point",
    "coordinates": [-122.4094, 37.7849]
  },
  "dwell_time_seconds": 2700,
  "processed_at": "2025-11-05T11:15:00.456Z"
}
```

### 3. DWELL Event (Phase 2)

**Triggered:** When an entity remains inside a geofence for a specified duration.

**Use Cases:** Parking enforcement, loitering detection, scheduled delivery windows.

### 4. APPROACH Event (Phase 2)

**Triggered:** When an entity comes within a buffer distance of a geofence.

**Use Cases:** ETA notifications, proximity alerts, smart routing.

## API Endpoints

### Location Evaluation

#### Evaluate Single Location

**Endpoint:** `POST /api/v1/geoevent/evaluate`

**Request:**
```json
{
  "entity_id": "vehicle-123",
  "entity_type": "vehicle",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "event_time": "2025-11-05T10:30:00Z",
  "properties": {
    "speed": 45.5,
    "heading": 180,
    "driver_id": "D-456"
  }
}
```

**Response:**
```json
{
  "entity_id": "vehicle-123",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "event_time": "2025-11-05T10:30:00Z",
  "events_generated": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "event_type": "Enter",
      "geofence_id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "geofence_name": "Downtown District",
      "event_time": "2025-11-05T10:30:00Z"
    }
  ],
  "current_geofences": [
    {
      "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "name": "Downtown District"
    }
  ],
  "processing_time_ms": 42.3
}
```

**Performance Target:** P95 latency < 100ms for 1,000 geofences

#### Batch Location Evaluation

**Endpoint:** `POST /api/v1/geoevent/evaluate/batch`

**Request:**
```json
[
  {
    "entity_id": "vehicle-123",
    "location": { "type": "Point", "coordinates": [-122.4194, 37.7749] }
  },
  {
    "entity_id": "vehicle-456",
    "location": { "type": "Point", "coordinates": [-122.4094, 37.7849] }
  }
]
```

**Response:**
```json
{
  "results": [
    {
      "entity_id": "vehicle-123",
      "events_generated": [/* ... */],
      "current_geofences": [/* ... */]
    },
    {
      "entity_id": "vehicle-456",
      "events_generated": [/* ... */],
      "current_geofences": [/* ... */]
    }
  ],
  "total_processed": 2,
  "success_count": 2,
  "error_count": 0,
  "total_processing_time_ms": 87.6
}
```

**Limits:**
- Maximum 1,000 locations per batch
- Individual failures don't fail entire batch
- Target throughput: 100 events/second sustained

### Geofence Management

#### Create Geofence

**Endpoint:** `POST /api/v1/geofences`

**Request:**
```json
{
  "name": "Downtown District",
  "description": "Main downtown area",
  "geometry": {
    "type": "Polygon",
    "coordinates": [[
      [-122.4194, 37.7749],
      [-122.4094, 37.7749],
      [-122.4094, 37.7849],
      [-122.4194, 37.7849],
      [-122.4194, 37.7749]
    ]]
  },
  "enabled_event_types": ["Enter", "Exit"],
  "is_active": true,
  "properties": {
    "zone_type": "restricted",
    "priority": "high"
  }
}
```

**Response:** `201 Created` with geofence ID

#### Get Geofence

**Endpoint:** `GET /api/v1/geofences/{id}`

**Response:**
```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "Downtown District",
  "description": "Main downtown area",
  "geometry": { /* GeoJSON Polygon */ },
  "enabled_event_types": ["Enter", "Exit"],
  "is_active": true,
  "created_at": "2025-01-15T08:00:00Z",
  "updated_at": "2025-02-10T14:30:00Z",
  "created_by": "admin@acme.com",
  "updated_by": "admin@acme.com"
}
```

#### List Geofences

**Endpoint:** `GET /api/v1/geofences`

**Query Parameters:**
- `is_active` (bool): Filter by active status
- `limit` (int, 1-1000): Results per page (default: 100)
- `offset` (int): Pagination offset (default: 0)

**Response:**
```json
{
  "geofences": [/* array of geofences */],
  "total_count": 150,
  "limit": 100,
  "offset": 0
}
```

#### Update Geofence

**Endpoint:** `PUT /api/v1/geofences/{id}`

**Request:** Same as Create Geofence

**Response:** `204 No Content`

#### Delete Geofence

**Endpoint:** `DELETE /api/v1/geofences/{id}`

**Response:** `204 No Content`

## PostgreSQL Spatial Queries

### Database Schema

```sql
-- Geofences table with spatial index
CREATE TABLE geofences (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    geometry GEOMETRY(Polygon, 4326) NOT NULL,  -- WGS84
    properties JSONB,
    enabled_event_types INTEGER NOT NULL DEFAULT 3,  -- Flags: Enter=1, Exit=2, Dwell=4, Approach=8
    is_active BOOLEAN NOT NULL DEFAULT true,
    tenant_id TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    created_by TEXT,
    updated_by TEXT
);

-- Spatial index for fast point-in-polygon queries
CREATE INDEX idx_geofences_geometry ON geofences USING GIST(geometry);
CREATE INDEX idx_geofences_tenant_active ON geofences(tenant_id, is_active) WHERE is_active = true;

-- Entity state tracking
CREATE TABLE entity_geofence_state (
    entity_id TEXT NOT NULL,
    geofence_id UUID NOT NULL REFERENCES geofences(id) ON DELETE CASCADE,
    is_inside BOOLEAN NOT NULL,
    entered_at TIMESTAMPTZ,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    tenant_id TEXT,
    PRIMARY KEY (entity_id, geofence_id)
);

CREATE INDEX idx_entity_state_entity ON entity_geofence_state(entity_id, tenant_id);

-- Geofence events
CREATE TABLE geofence_events (
    id UUID PRIMARY KEY,
    event_type TEXT NOT NULL,  -- 'Enter', 'Exit', 'Dwell', 'Approach'
    event_time TIMESTAMPTZ NOT NULL,
    geofence_id UUID NOT NULL REFERENCES geofences(id),
    geofence_name TEXT NOT NULL,
    entity_id TEXT NOT NULL,
    entity_type TEXT,
    location GEOMETRY(Point, 4326) NOT NULL,
    properties JSONB,
    dwell_time_seconds INTEGER,
    sensor_things_observation_id UUID,  -- Optional link to SensorThings
    tenant_id TEXT,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_geofence_events_entity ON geofence_events(entity_id, event_time DESC);
CREATE INDEX idx_geofence_events_geofence ON geofence_events(geofence_id, event_time DESC);
CREATE INDEX idx_geofence_events_time ON geofence_events(event_time DESC);
CREATE INDEX idx_geofence_events_tenant ON geofence_events(tenant_id) WHERE tenant_id IS NOT NULL;
```

### Point-in-Polygon Query

```sql
-- Find all active geofences containing a point
SELECT id, name, geometry, enabled_event_types, properties
FROM geofences
WHERE ST_Contains(geometry, ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326))
  AND is_active = true
  AND tenant_id = @tenantId  -- Multi-tenant isolation
ORDER BY name;
```

**Performance:**
- Uses GIST spatial index: O(log n) lookup
- Typical query time: 2-10ms for 1,000 geofences
- Scales to 10,000+ geofences with proper indexing

### Batch Geofence Lookup

```sql
-- Get multiple geofences by ID (avoids N+1 queries)
SELECT id, name, enabled_event_types
FROM geofences
WHERE id = ANY(@geofence_ids)
  AND tenant_id = @tenantId;
```

## Entity State Tracking

### State Storage Options

**1. PostgreSQL (Default):**
```csharp
public class PostgresEntityStateRepository : IEntityStateRepository
{
    public async Task<List<EntityGeofenceState>> GetEntityStatesAsync(
        string entityId,
        string? tenantId,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT entity_id, geofence_id, is_inside, entered_at, last_updated
            FROM entity_geofence_state
            WHERE entity_id = @EntityId
              AND (@TenantId IS NULL OR tenant_id = @TenantId)";

        // Returns current state for all geofences entity is inside
    }
}
```

**2. In-Memory (High Performance):**
```csharp
// Stored in ConcurrentDictionary<string, HashSet<Guid>>
// Key: entity_id, Value: Set of geofence_ids entity is inside
private readonly ConcurrentDictionary<string, HashSet<Guid>> _entityStates = new();
```

### State Transitions

```
Initial State: Entity outside all geofences
    → State: {} (empty set)

Entity moves to (lon: -122.4194, lat: 37.7749)
    → ST_Contains finds geofence A
    → State change: {} → {A}
    → Event: ENTER geofence A

Entity moves to (lon: -122.4094, lat: 37.7849)
    → ST_Contains finds geofences A and B
    → State change: {A} → {A, B}
    → Event: ENTER geofence B

Entity moves to (lon: -122.3994, lat: 37.7949)
    → ST_Contains finds geofence B only
    → State change: {A, B} → {B}
    → Event: EXIT geofence A (with dwell_time_seconds)
```

## Azure Stream Analytics Integration

### Architecture (Phase 2)

```
IoT Hub / Event Hub
    ↓
Azure Stream Analytics
    ↓ (Streaming SQL)
SELECT
    DeviceId as entity_id,
    latitude,
    longitude,
    EventProcessedUtcTime as event_time
FROM IoTInput
    ↓
Azure Function / WebJob
    ↓ (Batch POST)
POST /api/v1/geoevent/evaluate/batch
    ↓
Honua GeoEvent Module
```

### Stream Analytics Query

```sql
-- Tumbling window aggregation (5-second batches)
SELECT
    DeviceId as entity_id,
    'vehicle' as entity_type,
    LAST(latitude) as latitude,
    LAST(longitude) as longitude,
    System.Timestamp() as event_time,
    AVG(speed) as avg_speed,
    COUNT(*) as reading_count
INTO HonuaBatchOutput
FROM IoTInput TIMESTAMP BY EventProcessedUtcTime
GROUP BY DeviceId, TumblingWindow(second, 5)
```

### Azure Function Processor

```csharp
[FunctionName("ProcessGeoEvents")]
public static async Task Run(
    [EventHubTrigger("geoevent-stream", Connection = "EventHubConnection")] EventData[] events,
    ILogger log)
{
    var locations = events.Select(e => new EvaluateLocationRequest
    {
        EntityId = e.Properties["entity_id"].ToString(),
        EntityType = e.Properties["entity_type"].ToString(),
        Location = new GeoJsonPoint
        {
            Type = "Point",
            Coordinates = new[]
            {
                Convert.ToDouble(e.Properties["longitude"]),
                Convert.ToDouble(e.Properties["latitude"])
            }
        },
        EventTime = DateTime.Parse(e.Properties["event_time"].ToString())
    }).ToList();

    // Batch POST to Honua
    var response = await _httpClient.PostAsJsonAsync(
        "https://api.honua.io/api/v1/geoevent/evaluate/batch",
        locations);
}
```

## Performance Optimization

### Spatial Indexing

**GIST Index:** Enable fast point-in-polygon queries
```sql
CREATE INDEX idx_geofences_geometry ON geofences USING GIST(geometry);
```

**Performance Characteristics:**
- 1 geofence: ~0.5ms
- 100 geofences: ~2ms
- 1,000 geofences: ~5-10ms
- 10,000 geofences: ~20-50ms (requires index tuning)

### Query Optimization Strategies

1. **Tenant Filtering:** Always include `WHERE tenant_id = @TenantId` to reduce search space
2. **Active Filter:** Use partial index on `is_active = true`
3. **Batch Queries:** Load exit geofences with single query (avoid N+1)
4. **State Caching:** Use in-memory state store for high-frequency entities

### Connection Pooling

```csharp
"ConnectionStrings": {
  "GeoEvents": "Host=postgres;Database=honua;Pooling=true;MinPoolSize=10;MaxPoolSize=200"
}
```

### Parallel Processing

```csharp
// Process batch locations in parallel
var tasks = requests.Select(req => EvaluateLocationAsync(req));
var results = await Task.WhenAll(tasks);
```

## Event Notifications

### Webhook Notifications

```csharp
public class WebhookNotifier : IGeofenceEventNotifier
{
    public async Task NotifyAsync(GeofenceEvent geofenceEvent, CancellationToken ct)
    {
        var webhook = await GetWebhookForGeofence(geofenceEvent.GeofenceId);
        if (webhook == null) return;

        var payload = new
        {
            event_id = geofenceEvent.Id,
            event_type = geofenceEvent.EventType.ToString(),
            entity_id = geofenceEvent.EntityId,
            geofence_name = geofenceEvent.GeofenceName,
            event_time = geofenceEvent.EventTime,
            location = new
            {
                type = "Point",
                coordinates = new[] { geofenceEvent.Location.X, geofenceEvent.Location.Y }
            }
        };

        await _httpClient.PostAsJsonAsync(webhook.Url, payload);
    }
}
```

### Email Notifications

```csharp
public class EmailNotifier : IGeofenceEventNotifier
{
    public async Task NotifyAsync(GeofenceEvent geofenceEvent, CancellationToken ct)
    {
        var email = await GetEmailForGeofence(geofenceEvent.GeofenceId);
        if (email == null) return;

        var message = new MailMessage
        {
            To = { email },
            Subject = $"Geofence Event: {geofenceEvent.EventType} - {geofenceEvent.GeofenceName}",
            Body = $"Entity {geofenceEvent.EntityId} {geofenceEvent.EventType.ToString().ToLower()}ed geofence '{geofenceEvent.GeofenceName}' at {geofenceEvent.EventTime:yyyy-MM-dd HH:mm:ss UTC}"
        };

        await _smtpClient.SendMailAsync(message);
    }
}
```

### Alert Integration

```csharp
public class GeofenceToAlertBridgeService : IGeofenceToAlertBridgeService
{
    public async Task ProcessGeofenceEventAsync(GeofenceEvent geofenceEvent, CancellationToken ct)
    {
        // Find alert rules configured for this geofence
        var rules = await _alertRuleRepository.GetRulesForGeofenceAsync(
            geofenceEvent.GeofenceId,
            ct);

        foreach (var rule in rules)
        {
            // Check if rule conditions match event
            if (ShouldTriggerAlert(rule, geofenceEvent))
            {
                // Create alert instance
                await _alertService.TriggerAlertAsync(new Alert
                {
                    RuleId = rule.Id,
                    Severity = rule.Severity,
                    Title = $"{geofenceEvent.EntityId} {geofenceEvent.EventType} {geofenceEvent.GeofenceName}",
                    Description = FormatAlertDescription(geofenceEvent),
                    TriggeredAt = DateTime.UtcNow,
                    Source = "GeoFencing",
                    Metadata = new Dictionary<string, object>
                    {
                        ["geofence_event_id"] = geofenceEvent.Id,
                        ["entity_id"] = geofenceEvent.EntityId,
                        ["geofence_id"] = geofenceEvent.GeofenceId
                    }
                }, ct);
            }
        }
    }
}
```

## Usage Examples

### Vehicle Fleet Tracking

```csharp
// Track delivery vehicle entering customer site
POST /api/v1/geoevent/evaluate
{
  "entity_id": "truck-42",
  "entity_type": "delivery_vehicle",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "driver_id": "D-123",
    "delivery_id": "DEL-456",
    "speed_mph": 15
  }
}
```

**Response triggers:**
- Enter event → Alert dispatcher
- Webhook to customer: "Driver arriving"
- Update delivery status: "At customer location"

### Asset Monitoring

```csharp
// Track construction equipment leaving job site
POST /api/v1/geoevent/evaluate
{
  "entity_id": "excavator-7",
  "entity_type": "equipment",
  "location": {
    "type": "Point",
    "coordinates": [-122.4094, 37.7849]
  },
  "properties": {
    "operator_id": "OP-789",
    "job_site_id": "JS-001"
  }
}
```

**Response triggers:**
- Exit event (dwell_time: 28800 seconds = 8 hours)
- Alert site manager: "Equipment leaving site"
- Log usage hours for billing

### Emergency Response

```csharp
// Create emergency geofence around incident
POST /api/v1/geofences
{
  "name": "Emergency Perimeter - Incident #12345",
  "geometry": {
    "type": "Polygon",
    "coordinates": [/* 500m radius around incident */]
  },
  "enabled_event_types": ["Enter", "Exit"],
  "properties": {
    "incident_id": "INC-12345",
    "severity": "high",
    "restricted": true
  }
}
```

**Use case:**
- Track first responders entering/exiting zone
- Alert unauthorized personnel entering restricted area
- Monitor evacuation progress

## Multi-Tenant Isolation

All geofencing operations are tenant-scoped:

```csharp
// Middleware extracts tenant from subdomain or header
var tenantId = HttpContext.GetTenantContext()?.TenantId;

// All queries include tenant filter
SELECT * FROM geofences
WHERE tenant_id = @tenantId AND is_active = true;

// Events stored with tenant ID
INSERT INTO geofence_events (tenant_id, ...)
VALUES (@tenantId, ...);
```

**Isolation Guarantees:**
- Tenant A cannot see Tenant B's geofences
- Tenant A's events are isolated from Tenant B
- State tracking is per-tenant
- Webhooks and alerts scoped to tenant

## Configuration

### Dependency Injection

```csharp
// In Program.cs
services.AddGeoEventServices(configuration);

// Registers:
// - IGeofenceRepository (PostgreSQL)
// - IGeofenceEvaluationService
// - IGeofenceManagementService
// - IEntityStateRepository (PostgreSQL or InMemory)
// - IGeofenceEventRepository
// - GeofenceEventNotificationService
// - WebhookNotifier, EmailNotifier
// - GeofenceToAlertBridgeService
```

### appsettings.json

```json
{
  "GeoEvent": {
    "ConnectionString": "Host=postgres;Database=honua;...",
    "UseInMemoryStateStore": false,
    "EnableNotifications": true,
    "EnableAlertIntegration": true,
    "MaxBatchSize": 1000,
    "PerformanceTargetMs": 100
  },
  "Webhooks": {
    "TimeoutSeconds": 5,
    "MaxRetries": 3,
    "RetryDelaySeconds": 5
  }
}
```

## Monitoring and Analytics

### Key Metrics

```csharp
// Prometheus metrics
private static readonly Histogram EvaluationDuration = Metrics.CreateHistogram(
    "geoevent_evaluation_duration_seconds",
    "Time to evaluate location against geofences");

private static readonly Counter EventsGenerated = Metrics.CreateCounter(
    "geoevent_events_total",
    "Total geofence events generated",
    new CounterConfiguration { LabelNames = new[] { "event_type", "tenant_id" } });

private static readonly Gauge ActiveGeofences = Metrics.CreateGauge(
    "geoevent_active_geofences",
    "Number of active geofences per tenant",
    new GaugeConfiguration { LabelNames = new[] { "tenant_id" } });
```

### Analytics Queries

```sql
-- Event count by type (last 24 hours)
SELECT event_type, COUNT(*) as count
FROM geofence_events
WHERE event_time > NOW() - INTERVAL '24 hours'
GROUP BY event_type;

-- Top geofences by event volume
SELECT geofence_name, COUNT(*) as event_count
FROM geofence_events
WHERE event_time > NOW() - INTERVAL '7 days'
GROUP BY geofence_id, geofence_name
ORDER BY event_count DESC
LIMIT 10;

-- Average dwell time by geofence
SELECT geofence_name, AVG(dwell_time_seconds) / 60.0 as avg_dwell_minutes
FROM geofence_events
WHERE event_type = 'Exit'
  AND dwell_time_seconds IS NOT NULL
GROUP BY geofence_id, geofence_name;
```

## Troubleshooting

### Common Issues

**1. Slow Evaluation Performance**

**Symptom:** Processing time > 100ms

**Solutions:**
- Verify spatial index exists: `\d geofences` in psql
- Check number of active geofences: Reduce if > 1,000
- Enable query logging: `SET log_min_duration_statement = 10;`
- Use in-memory state store for high-frequency entities

**2. Missing Events**

**Symptom:** Expected Enter/Exit events not generated

**Solutions:**
- Verify geofence `is_active = true`
- Check `enabled_event_types` includes desired event type
- Confirm coordinates are in WGS84 (EPSG:4326)
- Validate polygon is closed (first point = last point)

**3. Duplicate Events**

**Symptom:** Multiple Enter events without Exit

**Solutions:**
- Check entity state table for stale entries
- Clear state: `DELETE FROM entity_geofence_state WHERE entity_id = 'entity-123'`
- Ensure location updates are idempotent

## Best Practices

1. **Polygon Validation:** Ensure geofence polygons are valid and closed
2. **Coordinate Format:** Always use [longitude, latitude] (GeoJSON standard)
3. **Event Time:** Provide explicit event_time for historical processing
4. **Batch Processing:** Use batch endpoint for > 10 locations
5. **State Cleanup:** Periodically clean old entity states (> 30 days inactive)
6. **Index Maintenance:** Run `VACUUM ANALYZE geofences` weekly
7. **Monitoring:** Alert on evaluation latency > 100ms P95

## Related Documentation

- [ENTERPRISE_FEATURES.md](/home/user/Honua.Server/src/Honua.Server.Enterprise/ENTERPRISE_FEATURES.md) - Enterprise features overview
- [Multitenancy Module](../Multitenancy/README.md) - Multi-tenant architecture
- [Sensors/IoT Module](../Sensors/README.md) - SensorThings API integration
- Alerts Module - Event-driven alerting system
