# GeoEvent API Usage Guide

This guide provides practical examples and best practices for using the Honua GeoEvent geofencing API.

## Overview

The GeoEvent API provides real-time geofencing capabilities for tracking entities (vehicles, assets, people) as they move through geographic boundaries. The API consists of two main controllers:

- **Geofences Controller** (`/api/v1/geofences`) - CRUD operations for managing geofences
- **GeoEvent Controller** (`/api/v1/geoevent`) - Real-time location evaluation and event generation

## Quick Start

### 1. Create a Geofence

Define a geographic boundary that will trigger events:

```bash
POST /api/v1/geofences
Authorization: Bearer YOUR_TOKEN
Content-Type: application/json

{
  "name": "Downtown Delivery Zone",
  "description": "Main downtown delivery area",
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
    "zone_type": "delivery",
    "priority": "high",
    "speed_limit": 25
  }
}
```

**Response (201 Created):**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "Downtown Delivery Zone",
  "description": "Main downtown delivery area",
  "geometry": { ... },
  "enabled_event_types": ["Enter", "Exit"],
  "is_active": true,
  "properties": { ... },
  "created_at": "2025-11-05T10:00:00Z",
  "updated_at": "2025-11-05T10:00:00Z"
}
```

### 2. Evaluate a Location

Send location updates to check if an entity has entered or exited geofences:

```bash
POST /api/v1/geoevent/evaluate
Authorization: Bearer YOUR_TOKEN
Content-Type: application/json

{
  "entity_id": "vehicle-123",
  "entity_type": "delivery_truck",
  "location": {
    "type": "Point",
    "coordinates": [-122.4144, 37.7799]
  },
  "event_time": "2025-11-05T10:30:00Z",
  "properties": {
    "speed": 35.5,
    "heading": 180,
    "driver_id": "D-456",
    "temperature": 72
  }
}
```

**Response (200 OK):**
```json
{
  "entity_id": "vehicle-123",
  "location": {
    "type": "Point",
    "coordinates": [-122.4144, 37.7799]
  },
  "event_time": "2025-11-05T10:30:00Z",
  "events_generated": [
    {
      "id": "e1f2g3h4-i5j6-7890-abcd-ef1234567890",
      "event_type": "Enter",
      "geofence_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "geofence_name": "Downtown Delivery Zone",
      "event_time": "2025-11-05T10:30:00Z",
      "dwell_time_seconds": null
    }
  ],
  "current_geofences": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "name": "Downtown Delivery Zone"
    }
  ],
  "processing_time_ms": 45.3
}
```

### 3. Exit Event

When the vehicle leaves the geofence:

```bash
POST /api/v1/geoevent/evaluate
{
  "entity_id": "vehicle-123",
  "location": {
    "type": "Point",
    "coordinates": [-122.4000, 37.7700]
  }
}
```

**Response includes Exit event with dwell time:**
```json
{
  "events_generated": [
    {
      "event_type": "Exit",
      "geofence_name": "Downtown Delivery Zone",
      "dwell_time_seconds": 1800
    }
  ],
  "current_geofences": []
}
```

## Use Cases

### Fleet Management

Track delivery vehicles entering/exiting service areas:

```javascript
// Send location update every 30 seconds
setInterval(async () => {
  const position = await getCurrentPosition();

  const response = await fetch('/api/v1/geoevent/evaluate', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      entity_id: vehicleId,
      entity_type: 'delivery_truck',
      location: {
        type: 'Point',
        coordinates: [position.longitude, position.latitude]
      },
      properties: {
        speed: position.speed,
        heading: position.heading,
        driver_id: driverId
      }
    })
  });

  const result = await response.json();

  // Handle enter/exit events
  result.events_generated.forEach(event => {
    if (event.event_type === 'Enter') {
      console.log(`Vehicle entered ${event.geofence_name}`);
      notifyDispatch('ENTERED', event);
    } else if (event.event_type === 'Exit') {
      console.log(`Vehicle exited ${event.geofence_name} after ${event.dwell_time_seconds}s`);
      notifyDispatch('EXITED', event);
    }
  });
}, 30000);
```

### Asset Tracking

Monitor equipment entering restricted zones:

```python
import requests
from datetime import datetime

def check_asset_location(asset_id, lon, lat):
    """Check if asset has entered/exited any geofences."""
    response = requests.post(
        'https://api.example.com/api/v1/geoevent/evaluate',
        headers={'Authorization': f'Bearer {api_token}'},
        json={
            'entity_id': asset_id,
            'entity_type': 'equipment',
            'location': {
                'type': 'Point',
                'coordinates': [lon, lat]
            },
            'event_time': datetime.utcnow().isoformat() + 'Z',
            'properties': {
                'asset_type': 'forklift',
                'operator_id': 'OP-789'
            }
        }
    )

    result = response.json()

    # Alert on restricted zone entry
    for event in result['events_generated']:
        if event['event_type'] == 'Enter':
            geofence_props = get_geofence_properties(event['geofence_id'])
            if geofence_props.get('zone_type') == 'restricted':
                send_alert(f"Asset {asset_id} entered restricted zone!")

    return result
```

### Batch Processing

Process historical GPS tracks or bulk updates:

```bash
POST /api/v1/geoevent/evaluate/batch
Content-Type: application/json

[
  {
    "entity_id": "vehicle-123",
    "location": { "type": "Point", "coordinates": [-122.4194, 37.7749] },
    "event_time": "2025-11-05T10:00:00Z"
  },
  {
    "entity_id": "vehicle-123",
    "location": { "type": "Point", "coordinates": [-122.4144, 37.7799] },
    "event_time": "2025-11-05T10:00:30Z"
  },
  {
    "entity_id": "vehicle-456",
    "location": { "type": "Point", "coordinates": [-122.4094, 37.7849] },
    "event_time": "2025-11-05T10:01:00Z"
  }
]
```

**Response:**
```json
{
  "results": [ ... ],
  "total_processed": 3,
  "success_count": 3,
  "error_count": 0,
  "total_processing_time_ms": 127.5
}
```

## Geofence Management

### List All Active Geofences

```bash
GET /api/v1/geofences?is_active=true&limit=50&offset=0
```

### Update Geofence

```bash
PUT /api/v1/geofences/{id}
Content-Type: application/json

{
  "name": "Updated Name",
  "description": "Updated description",
  "geometry": { ... },
  "is_active": false
}
```

### Deactivate Geofence

Instead of deleting, deactivate geofences to preserve historical event data:

```bash
PUT /api/v1/geofences/{id}
{
  "is_active": false
}
```

### Delete Geofence

Permanently delete a geofence:

```bash
DELETE /api/v1/geofences/{id}
```

## Best Practices

### 1. **Location Update Frequency**

- **Real-time tracking**: 10-30 second intervals
- **Asset monitoring**: 1-5 minute intervals
- **Historical replay**: Batch processing (up to 1000 locations)

### 2. **Geofence Design**

- **Simple polygons**: Use 4-10 vertices for optimal performance
- **Avoid overlaps**: Minimize overlapping geofences when possible
- **Size matters**: Very small geofences (<10 meters) may cause frequent enter/exit events

### 3. **Error Handling**

Always handle errors gracefully:

```javascript
try {
  const response = await evaluateLocation(entityId, location);
  if (!response.ok) {
    console.error('Evaluation failed:', response.statusText);
    // Retry with exponential backoff
  }
} catch (error) {
  console.error('Network error:', error);
  // Queue for retry
}
```

### 4. **Performance Optimization**

- Use batch endpoint for bulk operations (up to 1000 locations)
- Monitor `processing_time_ms` in responses (target < 100ms)
- Keep total geofence count under 1,000 for optimal performance

### 5. **State Management**

The service automatically tracks entity state:
- First location update → Enter event (if inside geofence)
- Subsequent updates inside → No event
- Moving outside → Exit event with dwell time
- Moving back inside → New Enter event

## Coordinate Systems

All coordinates must be in **WGS84 (EPSG:4326)** format:
- Longitude: -180 to 180 (X axis)
- Latitude: -90 to 90 (Y axis)
- Order: **[longitude, latitude]** (GeoJSON standard)

Example:
```json
{
  "type": "Point",
  "coordinates": [-122.4194, 37.7749]
}
```

## Event Types

### Current (MVP)
- **Enter**: Entity enters a geofence
- **Exit**: Entity exits a geofence (includes dwell_time_seconds)

### Future (Phase 2)
- **Dwell**: Entity remains inside geofence for specified duration
- **Approach**: Entity comes within specified distance of geofence

## Rate Limits

- Single evaluation: Target P95 < 100ms
- Batch evaluation: Max 1000 locations per request
- Sustained throughput: 100 events/second target

## Integration Examples

### Azure Stream Analytics (Phase 2)

Configure Stream Analytics to call evaluation endpoint:

```sql
SELECT
    deviceId as entity_id,
    location.lon as longitude,
    location.lat as latitude,
    EventEnqueuedUtcTime as event_time
INTO
    [honua-geoevent]
FROM
    [iothub-input]
```

### SensorThings API (Phase 2)

Link geofence events to SensorThings observations:

```json
{
  "entity_id": "sensor-123",
  "location": { ... },
  "sensorthings_observation_id": "obs-uuid-here"
}
```

## Troubleshooting

### No Events Generated

1. **Check geofence is active**: `GET /api/v1/geofences/{id}`
2. **Verify coordinates**: Ensure [longitude, latitude] order
3. **Check geometry**: Use tool like geojson.io to visualize

### Slow Performance

1. **Monitor processing_time_ms**: Should be < 100ms
2. **Reduce geofence count**: Keep under 1,000 active geofences
3. **Simplify geometries**: Use fewer vertices

### Coordinate Errors

Common mistake: reversing lat/lon order
- ❌ Wrong: `[37.7749, -122.4194]` (latitude first)
- ✅ Correct: `[-122.4194, 37.7749]` (longitude first)

## API Reference

For complete API documentation with interactive examples, visit:
- Swagger UI: `https://your-server/swagger`
- OpenAPI JSON: `https://your-server/swagger/v1/swagger.json`

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io/geoevent
