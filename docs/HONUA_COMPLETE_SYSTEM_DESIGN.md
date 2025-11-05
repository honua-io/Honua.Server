# Honua Complete System Design - Final Specification

**Version:** 1.0
**Date:** November 2025
**Status:** Approved for Implementation

---

## Executive Summary

**Honua: The Intelligent Field GIS Platform**

A standards-based geospatial server with mobile AR capabilities, sensor integration, and hands-free field data collection.

### Vision

Enable field workers to collect geospatial data hands-free using voice commands, smart glasses, and external sensors, with real-time AR visualization of underground utilities and 3D features.

### Key Differentiators

1. **Standards-Based** - Full OGC compliance + SensorThings API
2. **3D Geometry Support** - Underground utilities, elevation, AR-ready
3. **Mobile-First** - Offline-first architecture with intelligent sync
4. **AR Integration** - Meta Ray-Ban + Quest 3 support
5. **Sensor Platform** - IoT/sensor integration via OGC SensorThings API

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Server Components](#2-server-components)
3. [Mobile Application](#3-mobile-application)
4. [AR/Wearable Integration](#4-arwearable-integration)
5. [Sensor Integration](#5-sensor-integration)
6. [Data Flow Examples](#6-data-flow-examples)
7. [Implementation Timeline](#7-implementation-timeline)
8. [Technology Stack](#8-technology-stack)
9. [Success Metrics](#9-success-metrics)

---

## 1. Architecture Overview

### 1.1 System Components

```
┌──────────────────────────────────────────────────────────────┐
│                     HONUA ECOSYSTEM                          │
└──────────────────────────────────────────────────────────────┘

External Sensors            Fixed IoT Sensors
(Bluetooth/WiFi)           (WiFi/Cellular)
     │                            │
     │ WiFi Direct/BLE           │ HTTP/MQTT
     ↓                            ↓
┌──────────────┐         ┌─────────────────┐
│ 📱 Mobile    │◄───────►│ 🥽 Meta Quest 3 │
│    App       │ USB-C/  │    AR App       │
└──────┬───────┘ WiFi    └────────┬────────┘
       │ BT                       │ HTTPS
       ↓                          │
┌──────────────┐                  │
│ 👓 Ray-Ban   │                  │
│   Glasses    │                  │
└──────────────┘                  │
       │ HTTPS                    │
       ↓                          ↓
┌────────────────────────────────────────────┐
│         🌐 HONUA SERVER                    │
│  ┌──────────────────────────────────────┐  │
│  │  Standards-Based APIs                │  │
│  │  • OGC Features API (2D + 3D)       │  │
│  │  • OGC SensorThings API v1.1        │  │
│  │  • OGC Tiles, Processes             │  │
│  │  • STAC API                          │  │
│  │  • WMS, WFS, WCS, WMTS, CSW        │  │
│  └──────────────────────────────────────┘  │
│  ┌──────────────────────────────────────┐  │
│  │  PostgreSQL 16 + PostGIS 3.4        │  │
│  │  • features (2D/3D geometry)        │  │
│  │  • sta_things                        │  │
│  │  • sta_datastreams                   │  │
│  │  • sta_observations (time-series)   │  │
│  └──────────────────────────────────────┘  │
└────────────────────────────────────────────┘
```

### 1.2 Core Principles

1. **Standards-First** - Implement OGC standards properly, not custom APIs
2. **Offline-First** - Mobile app works without connectivity
3. **Protocol-Agnostic** - Server accepts data regardless of transport
4. **3D-Ready** - Support for elevation, underground utilities, AR
5. **Sensor-Enabled** - IoT/sensor integration via standard APIs

---

## 2. Server Components

### 2.1 New Feature: 3D Geometry Support

**Purpose:** Enable AR visualization, underground utilities, elevation data

#### Database Schema

```sql
-- Add 3D geometry support to existing features table
ALTER TABLE features
ADD COLUMN geometry_3d geometry(PointZ, 4326),
ADD COLUMN elevation_m DECIMAL(10,2);

-- Underground utility specific fields
ALTER TABLE features
ADD COLUMN utility_type VARCHAR(50),      -- gas, water, electric, telecom
ADD COLUMN depth_meters DECIMAL(10,2),    -- Negative for underground
ADD COLUMN burial_depth_confidence VARCHAR(20); -- high, medium, low

-- 3D spatial indexes
CREATE INDEX idx_features_geometry_3d
ON features USING GIST(geometry_3d);

CREATE INDEX idx_features_utility_type
ON features (utility_type, depth_meters)
WHERE utility_type IS NOT NULL;

-- Support for 3D geometries (all types)
ALTER TABLE features
ADD COLUMN geometry_type_3d VARCHAR(50); -- PointZ, LineStringZ, PolygonZ, etc.
```

#### API Endpoints

```http
# Query features with 3D coordinates
GET /collections/{collectionId}/items?
  bbox=-122.5,37.8,-122.4,37.9&
  bbox-crs=http://www.opengis.net/def/crs/EPSG/0/4979  # 3D CRS

# Response with Z-coordinate
{
  "type": "Feature",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749, 125.3]  # Lon, Lat, Elevation
  },
  "properties": {
    "utility_type": "gas",
    "depth_meters": -2.3,
    "diameter_inches": 6
  }
}

# Elevation service endpoint
GET /api/elevation?lat=37.8&lon=-122.4
Response: {
  "elevation_m": 125.3,
  "source": "USGS",
  "resolution_m": 10,
  "vertical_datum": "NAVD88"
}

# Underground utility query
GET /collections/utilities/items?
  bbox=-122.5,37.8,-122.4,37.9&
  filter=depth_meters lt -1.0 and utility_type eq 'gas'
```

#### Implementation Notes

- 2D geometry remains primary (backward compatible)
- 3D geometry is optional enhancement
- Both 2D and 3D can coexist
- AR applications use geometry_3d when available

---

### 2.2 New Feature: OGC SensorThings API v1.1

**Purpose:** Standard IoT/sensor data integration

#### Data Model

```
Things → IoT devices/sensors
  ├─ Locations → Geographic locations
  └─ Datastreams → Time-series of measurements
       ├─ Sensor → Instrument metadata
       ├─ ObservedProperty → What's measured
       └─ Observations → Individual readings
            └─ FeatureOfInterest → Link to OGC Features
```

#### Database Schema

```sql
-- SensorThings API entities
CREATE TABLE sta_things (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    properties JSONB,
    feature_id UUID REFERENCES features(id),  -- Link to Features API
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE sta_sensors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100),
    metadata TEXT  -- URL or inline metadata
);

CREATE TABLE sta_observed_properties (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    definition TEXT,  -- URI to standard definition
    description TEXT
);

CREATE TABLE sta_datastreams (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    thing_id UUID REFERENCES sta_things(id) ON DELETE CASCADE,
    sensor_id UUID REFERENCES sta_sensors(id),
    observed_property_id UUID REFERENCES sta_observed_properties(id),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    observation_type VARCHAR(100),  -- OM_Measurement, OM_Observation, etc.
    unit_of_measurement JSONB,  -- { "name": "Celsius", "symbol": "°C", "definition": "..." }
    observed_area geometry(Polygon, 4326),
    phenomenon_time_start TIMESTAMPTZ,
    phenomenon_time_end TIMESTAMPTZ,
    result_time_start TIMESTAMPTZ,
    result_time_end TIMESTAMPTZ,
    properties JSONB,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE sta_observations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    datastream_id UUID REFERENCES sta_datastreams(id) ON DELETE CASCADE,
    feature_of_interest_id UUID,  -- Link to features.id
    phenomenon_time TIMESTAMPTZ NOT NULL,
    result_time TIMESTAMPTZ DEFAULT now(),
    result_value NUMERIC,
    result_string TEXT,
    result_json JSONB,
    result_boolean BOOLEAN,
    result_quality JSONB,
    valid_time TSTZRANGE,
    parameters JSONB
);

CREATE TABLE sta_locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255),
    description TEXT,
    encoding_type VARCHAR(100),
    location geometry(Point, 4326),
    properties JSONB
);

CREATE TABLE sta_things_locations (
    thing_id UUID REFERENCES sta_things(id) ON DELETE CASCADE,
    location_id UUID REFERENCES sta_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (thing_id, location_id)
);

CREATE TABLE sta_features_of_interest (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255),
    description TEXT,
    encoding_type VARCHAR(100),
    feature JSONB,  -- GeoJSON feature
    feature_id UUID REFERENCES features(id)  -- Link to Features API
);

-- Time-series optimized indexes
CREATE INDEX idx_sta_observations_datastream_time
ON sta_observations (datastream_id, phenomenon_time DESC);

CREATE INDEX idx_sta_observations_time_brin
ON sta_observations USING BRIN (phenomenon_time);

CREATE INDEX idx_sta_observations_foi
ON sta_observations (feature_of_interest_id)
WHERE feature_of_interest_id IS NOT NULL;

-- Spatial indexes
CREATE INDEX idx_sta_locations_location
ON sta_locations USING GIST(location);

CREATE INDEX idx_sta_datastreams_area
ON sta_datastreams USING GIST(observed_area)
WHERE observed_area IS NOT NULL;
```

#### API Endpoints

```http
# Core SensorThings API v1.1 endpoints
GET /v1.1/Things
GET /v1.1/Sensors
GET /v1.1/ObservedProperties
GET /v1.1/Datastreams
GET /v1.1/Observations
GET /v1.1/Locations
GET /v1.1/FeaturesOfInterest

# Entity by ID
GET /v1.1/Things(1)
GET /v1.1/Datastreams(5)
GET /v1.1/Observations(100)

# Navigation (relationships)
GET /v1.1/Things(1)/Datastreams
GET /v1.1/Datastreams(5)/Observations
GET /v1.1/Things(1)/Locations

# OData query options
GET /v1.1/Observations?
  $filter=phenomenonTime gt 2025-11-01T00:00:00Z and result gt 20&
  $expand=Datastream,FeatureOfInterest&
  $orderby=phenomenonTime desc&
  $top=100&
  $skip=0&
  $count=true

# Create entity
POST /v1.1/Things
{
  "name": "Weather Station 001",
  "description": "Field weather monitoring station",
  "properties": {
    "manufacturer": "Davis",
    "model": "Vantage Pro2"
  }
}

# Create with deep insert (nested entities)
POST /v1.1/Things
{
  "name": "Temperature Sensor",
  "Locations": [{
    "name": "Field Site A",
    "location": {
      "type": "Point",
      "coordinates": [-122.4, 37.8]
    }
  }],
  "Datastreams": [{
    "name": "Air Temperature",
    "ObservedProperty": {
      "name": "Temperature",
      "definition": "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#Temperature"
    },
    "Sensor": {
      "name": "DS18B20",
      "encodingType": "application/pdf",
      "metadata": "http://example.com/datasheet.pdf"
    },
    "unitOfMeasurement": {
      "name": "Celsius",
      "symbol": "°C",
      "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
    }
  }]
}

# Batch upload observations
POST /v1.1/Observations
[
  {
    "Datastream@iot.id": 1,
    "phenomenonTime": "2025-11-05T14:30:00Z",
    "result": 23.5
  },
  {
    "Datastream@iot.id": 1,
    "phenomenonTime": "2025-11-05T14:31:00Z",
    "result": 23.6
  }
]
```

#### Integration with Features API

```json
# Feature with linked sensor
{
  "type": "Feature",
  "id": "pole-1247",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4, 37.8, 125.3]
  },
  "properties": {
    "asset_type": "utility_pole",
    "sta_thing_id": "550e8400-e29b-41d4-a716-446655440000"
  },
  "links": [
    {
      "rel": "related",
      "href": "/v1.1/Things(550e8400-e29b-41d4-a716-446655440000)",
      "title": "Sensor Datastreams",
      "type": "application/json"
    }
  ]
}

# Query: Get all observations for features in a bbox
SELECT
    f.id AS feature_id,
    f.properties->>'asset_type' AS asset_type,
    o.phenomenon_time,
    o.result_value,
    ds.name AS datastream_name
FROM features f
JOIN sta_things t ON t.feature_id = f.id
JOIN sta_datastreams ds ON ds.thing_id = t.id
JOIN sta_observations o ON o.datastream_id = ds.id
WHERE ST_Intersects(f.geometry, ST_MakeEnvelope(-122.5, 37.8, -122.4, 37.9, 4326))
  AND o.phenomenon_time > NOW() - INTERVAL '1 day'
ORDER BY o.phenomenon_time DESC;
```

---

### 2.3 Sensor Ingestion Methods

**Multiple Protocol Support:**

```
1. REST API (HTTPS POST) - Primary method
   └─ Mobile apps, WiFi sensors, cellular sensors
   └─ POST /v1.1/Observations

2. MQTT (Future - Phase 3)
   └─ Publish to: honua/v1.1/observations/{datastreamId}
   └─ Real-time IoT sensors

3. Batch Upload
   └─ POST /v1.1/Observations (array)
   └─ Efficient for high-volume sensors

4. WebHooks (Server-to-Server)
   └─ External IoT platforms push to Honua
   └─ Weather services, third-party sensors
```

**Note:** Server is protocol-agnostic. Mobile app handles sensor connection complexity (WiFi Direct, Bluetooth, USB, NFC, etc.).

---

## 3. Mobile Application

### 3.1 Architecture

**Technology:** .NET MAUI, C# 12, .NET 8

**Platform Support:**
- iOS 15+ (iPhone, iPad)
- Android 10+ (Phone, Tablet)

**Core Architecture:**

```
.NET MAUI Application
├── Views (XAML)
│   ├── MapView
│   ├── FeatureFormView
│   ├── SettingsView
│   └── ARView (Phase 3)
│
├── ViewModels (MVVM + CommunityToolkit)
│   ├── MapViewModel
│   ├── FeatureFormViewModel
│   └── SensorViewModel
│
├── Services (Dependency Injection)
│   ├── IOgcFeaturesClient - OGC Features API
│   ├── ISensorThingsClient - SensorThings API
│   ├── ILocationService - GPS/GNSS
│   ├── ISensorService - External sensors
│   ├── IWearableService - Meta glasses
│   ├── ISyncService - Offline sync
│   ├── IVoiceService - Speech recognition
│   └── IAIService - Smart suggestions
│
└── Data Layer
    ├── SQLite (local storage)
    ├── NetTopologySuite (spatial operations)
    └── SyncQueue (offline changes)
```

### 3.2 Offline Storage

```sql
-- Local SQLite schema
CREATE TABLE local_features (
    id TEXT PRIMARY KEY,
    collection_id TEXT NOT NULL,
    geometry BLOB,  -- WKB format
    properties TEXT,  -- JSON
    created_at INTEGER,
    modified_at INTEGER,
    sync_status TEXT,  -- pending, synced, conflict
    etag TEXT
);

CREATE TABLE local_observations (
    id TEXT PRIMARY KEY,
    datastream_id TEXT NOT NULL,
    phenomenon_time INTEGER NOT NULL,
    result REAL,
    result_json TEXT,
    sync_status TEXT
);

CREATE TABLE sync_queue (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_type TEXT,  -- feature, observation
    entity_id TEXT,
    operation TEXT,  -- create, update, delete
    payload TEXT,  -- JSON
    created_at INTEGER,
    retry_count INTEGER DEFAULT 0,
    last_error TEXT
);

CREATE TABLE downloaded_collections (
    collection_id TEXT PRIMARY KEY,
    last_sync INTEGER,
    feature_count INTEGER
);

-- Spatial index on geometries
CREATE INDEX idx_local_features_sync
ON local_features(sync_status, modified_at);

CREATE INDEX idx_sync_queue_status
ON sync_queue(entity_type, operation)
WHERE retry_count < 5;
```

### 3.3 Sync Strategy

```csharp
public class SyncService : ISyncService
{
    public async Task<SyncResult> SyncAsync(CancellationToken ct = default)
    {
        var result = new SyncResult();

        // 1. Push local changes to server
        var pendingFeatures = await _localDb.GetPendingFeaturesAsync();
        foreach (var feature in pendingFeatures)
        {
            try
            {
                if (feature.Operation == Operation.Create)
                {
                    await _ogcClient.CreateFeatureAsync(feature.CollectionId, feature);
                }
                else if (feature.Operation == Operation.Update)
                {
                    await _ogcClient.UpdateFeatureAsync(
                        feature.CollectionId,
                        feature.Id,
                        feature,
                        ifMatch: feature.ETag
                    );
                }

                await _localDb.MarkAsSyncedAsync(feature.Id);
                result.FeaturesSynced++;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Conflict detected
                await _localDb.MarkAsConflictAsync(feature.Id);
                result.Conflicts++;
            }
        }

        // 2. Batch upload observations
        var pendingObs = await _localDb.GetPendingObservationsAsync();
        if (pendingObs.Any())
        {
            await _staClient.CreateObservationsBatchAsync(pendingObs);
            await _localDb.MarkObservationsAsSyncedAsync(pendingObs.Select(o => o.Id));
            result.ObservationsSynced += pendingObs.Count;
        }

        // 3. Pull server changes since last sync
        var lastSync = await _localDb.GetLastSyncTimeAsync();
        foreach (var collection in await _localDb.GetDownloadedCollectionsAsync())
        {
            var serverFeatures = await _ogcClient.GetFeaturesAsync(
                collection.Id,
                datetime: $"{lastSync:O}/.."
            );

            foreach (var serverFeature in serverFeatures)
            {
                await _localDb.UpsertFeatureAsync(serverFeature);
            }
        }

        // 4. Update last sync timestamp
        await _localDb.SetLastSyncTimeAsync(DateTime.UtcNow);

        return result;
    }
}
```

### 3.4 External Sensor Integration

```csharp
public interface ISensorService
{
    // Discovery
    Task<IEnumerable<SensorDevice>> DiscoverDevicesAsync();

    // Connection (WiFi Direct preferred, BLE fallback)
    Task<bool> ConnectAsync(string deviceId, ConnectionMethod method);
    Task DisconnectAsync(string deviceId);

    // Data acquisition
    Task<SensorReading> GetReadingAsync(string deviceId);
    IAsyncEnumerable<SensorReading> StreamReadingsAsync(string deviceId);

    // Registration (create SensorThings Thing)
    Task<string> RegisterSensorAsync(SensorDevice device);
}

public class SensorReading
{
    public string SensorId { get; set; }
    public string DatastreamId { get; set; }
    public DateTime Timestamp { get; set; }
    public object Value { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### 3.5 Implementation Phases

**Phase 1: MVP (Months 1-6)**
- Core data collection (points, lines, polygons)
- Offline editing and sync
- Photo attachments
- OGC Features API client
- GPS location

**Phase 2: Intelligence (Months 7-12)**
- External sensor support (GNSS, weather)
- SensorThings API client
- Voice input and commands
- Meta Ray-Ban integration
- AI suggestions

**Phase 3: Innovation (Months 13-18)**
- AR visualization (tethered to Quest 3)
- 3D geometry support
- Plugin system (beta)
- Advanced AI features

---

## 4. AR/Wearable Integration

### 4.1 Meta Ray-Ban Smart Glasses

**Architecture: Phone-Centric**

```
External Sensor → 📱 Phone (Honua App) → 👓 Ray-Ban Glasses
                        ↓
                  🌐 Honua Server
```

**Capabilities:**
- 🎤 5-microphone array (voice input)
- 📷 12MP camera (photo capture)
- 🔊 Open-ear speakers (audio feedback)
- ⚡ ~4 hour battery
- ❌ **No visual display** (audio-only UI)

**Use Case: Hands-Free Inspection**

```
Worker: "Create new inspection"
Ray-Ban mic → Phone processes
Phone → Ray-Ban speakers: "Starting inspection. Asset type?"

Worker: "Utility pole"
Phone records attribute

Worker: "Take photo"
Ray-Ban camera captures
Phone receives photo
Phone → Speakers: "Photo captured"

Worker: "Record weather"
External weather sensor → Phone (WiFi Direct)
Phone → Speakers: "Weather recorded. 18 degrees, 65% humidity"

Phone uploads:
  POST /collections/inspections/items (Feature)
  POST /v1.1/Observations (Sensor readings)
```

**Implementation:**

```csharp
public class MetaGlassesService : IWearableService
{
    private readonly IBluetooth _bluetooth;
    private readonly ISpeechRecognition _speechRecognition;
    private readonly ITextToSpeech _textToSpeech;

    public async Task<string> ListenForCommandAsync()
    {
        var audioData = await _bluetooth.RecordAudioAsync();
        var text = await _speechRecognition.TranscribeAsync(audioData);
        return text;
    }

    public async Task SpeakAsync(string message)
    {
        var audioData = await _textToSpeech.SynthesizeAsync(message);
        await _bluetooth.PlayAudioAsync(audioData);
    }

    public async Task<Photo> CapturePhotoAsync()
    {
        await _bluetooth.SendCommandAsync("TRIGGER_CAMERA");
        return await _bluetooth.ReceivePhotoAsync();
    }
}
```

**Phase 2 Deliverable (Month 16):**
- Bluetooth SDK integration
- Voice command system
- Audio feedback pipeline
- Camera trigger
- Beta with 50 field workers

---

### 4.2 Meta Quest 3

**Architecture Option A: Standalone**

```
External Sensor → 🌐 Honua Server → 🥽 Quest 3 (Unity App)
```

**Architecture Option B: Tethered (Better Battery)**

```
External Sensor → 📱 Phone → 🥽 Quest 3 (Display Only)
                      ↓
                🌐 Honua Server
```

**Capabilities:**
- 🥽 Full-color AR passthrough
- 👐 Hand tracking (no controllers)
- 🎯 6DOF spatial tracking
- 🗣️ Voice commands
- 🔋 2-3 hour battery (standalone)
- ✅ **Visual AR overlay**

**Use Case: Underground Utility Visualization**

```
1. Technician puts on Quest 3
2. Quest 3 queries server:
   GET /collections/utilities/items?
     bbox=-122.5,37.8,-122.4,37.9&
     filter=utility_type eq 'gas'

3. AR View renders:
   • Yellow lines for gas pipes (on ground)
   • Depth labels: "Gas: -2.3m"
   • Safe dig zones (green circles)
   • Live sensor overlay (GPR readings)

4. User pinches in AR: "Mark this location"
5. Quest 3 uploads:
   POST /collections/field_detections/items
   {
     "geometry": { "coordinates": [-122.4, 37.8, -2.3] },
     "properties": { "detection_method": "GPR" }
   }
```

**Unity Implementation:**

```csharp
public class UndergroundUtilityAR : MonoBehaviour
{
    private IOgcFeaturesClient _ogcClient;
    private ISensorThingsClient _staClient;

    async void Update()
    {
        // 1. Query utilities in current view
        var bounds = GetCurrentViewBounds();
        var utilities = await _ogcClient.GetFeaturesAsync(
            "utilities",
            bbox: bounds,
            filter: "utility_type eq 'gas'"
        );

        // 2. Query live sensor readings
        var gprReadings = await _staClient.GetObservationsAsync(
            datastreamId: "gpr-001",
            top: 10,
            orderby: "phenomenonTime desc"
        );

        // 3. Render AR overlay
        foreach (var utility in utilities)
        {
            var worldPos = GpsToArWorld(utility.Geometry.Coordinates);
            var depth = utility.Properties["depth_meters"];

            // Draw colored line based on utility type
            var color = utility.Properties["utility_type"] switch
            {
                "gas" => Color.yellow,
                "water" => Color.blue,
                "electric" => Color.red,
                _ => Color.white
            };

            RenderUtilityLine(worldPos, color, depth);
            RenderDepthLabel(worldPos, $"{depth:F1}m deep");
        }

        // 4. Show live sensor data
        if (gprReadings.Any() && gprReadings.First().Result > 0.8)
        {
            RenderWarning("CAUTION: Buried utility detected");
        }
    }

    // GPS to AR world coordinate transformation
    private Vector3 GpsToArWorld(double[] coordinates)
    {
        var lon = coordinates[0];
        var lat = coordinates[1];
        var alt = coordinates.Length > 2 ? coordinates[2] : 0;

        var questPos = GetQuestPosition();
        var offset = GeoHelper.LatLonToMeters(lat, lon, questPos.Latitude, questPos.Longitude);

        return new Vector3((float)offset.x, (float)alt, (float)offset.y);
    }
}
```

**Phase 3 Deliverable (Month 18):**
- Unity-based Quest 3 app
- AR underground utility overlay
- Hand tracking UI
- Real-time sensor visualization
- 60 FPS performance

---

## 5. Sensor Integration

### 5.1 Sensor Connection Methods

**Mobile App Handles:**

| Method | Range | Pros | Cons | Use Case |
|--------|-------|------|------|----------|
| **WiFi Direct** | 100-200m | Stable, fast | Power hungry | Professional sensors |
| **Bluetooth LE** | 10-30m | Low power | Unreliable | Consumer sensors |
| **USB/Lightning** | 1m | 100% reliable | Tethered | Lab instruments |
| **NFC** | < 4cm | Zero setup | Must touch | Asset tags |
| **Cellular** | Unlimited | Sensor-to-cloud | $$ per month | Fixed stations |

**Server Perspective:** Protocol-agnostic - accepts data via HTTP POST regardless of how sensor connected.

### 5.2 Common Sensors

**Environmental Monitoring:**
- Kestrel 5500 weather meter (temp, humidity, wind, pressure)
- HOBO data loggers (temperature, light)
- YSI water quality (pH, dissolved oxygen, turbidity)

**Positioning:**
- Trimble R1/R2 GNSS (sub-meter accuracy)
- Bad Elf GPS (Bluetooth/Lightning)
- Eos Arrow receivers (< 1m accuracy)

**Measurement:**
- Laser rangefinders (Bosch, Leica)
- FLIR thermal cameras (USB-C)
- Sound level meters (decibels)

**IoT Fixed Sensors:**
- Weather stations (WiFi → Server)
- Soil moisture sensors (LoRaWAN → Gateway → Server)
- Water level monitors (Cellular → Server)

### 5.3 Data Flow

```
1. Mobile Workflow (Portable Sensors)
   Sensor → Phone (WiFi/BLE) → Local SQLite → Server (when online)

2. IoT Workflow (Fixed Sensors)
   Sensor → Server (Cellular/WiFi) → Database → Phone queries

3. Hybrid Workflow
   Fixed Sensor → Server (real-time)
   Phone → Server (feature with link to sensor Thing)
   AR App → Server (displays both)
```

---

## 6. Data Flow Examples

### 6.1 Environmental Survey with Weather Sensors

**Equipment:**
- iPhone 15 Pro
- Meta Ray-Ban glasses
- Kestrel 5500 weather meter (WiFi Direct)
- Bad Elf GPS (Bluetooth fallback)

**Workflow:**

```
Morning Setup:
├─ Connect Kestrel via WiFi Direct
│   POST /v1.1/Things { name: "Kestrel 5500" }
│   POST /v1.1/Datastreams (temp, humidity, wind, pressure)
├─ Pair Ray-Ban glasses
└─ Download offline maps

Field Work (Hands-Free):
├─ Arrive at site
│   GPS: (-122.4567, 37.8901, ±0.5m)
│   Phone → Ray-Ban: "Arrived at site"
│
├─ Voice: "Create new observation"
│   Ray-Ban mic → Phone
│   Phone → Ray-Ban: "Starting observation. Species?"
│
├─ Voice: "Bald Eagle"
│   Phone: AI species lookup
│   Phone → Ray-Ban: "Bald Eagle confirmed. Count?"
│
├─ Voice: "Two adults"
│   Phone records count = 2
│
├─ Voice: "Take photo"
│   Ray-Ban camera captures
│   Phone → Ray-Ban: "Photo captured"
│
├─ Voice: "Record weather"
│   Phone queries Kestrel:
│     • Temperature: 18.5°C
│     • Humidity: 65%
│     • Wind: 3.2 m/s
│     • Pressure: 1013 hPa
│   Phone → Ray-Ban: "Weather recorded. 18 degrees, 65% humidity, light breeze"
│
└─ Auto-save to local SQLite

End of Day (Online):
├─ WiFi connection established
├─ Sync service runs:
│   ├─ POST /collections/observations/items
│   │   {
│   │     "geometry": { "coordinates": [-122.4567, 37.8901, 125.3] },
│   │     "properties": {
│   │       "species": "Bald Eagle",
│   │       "count": 2,
│   │       "observer": "Jane Smith",
│   │       "photo_url": "..."
│   │     }
│   │   }
│   │
│   └─ POST /v1.1/Observations (batch)
│       [
│         { "Datastream@iot.id": 1, "result": 18.5 },  // Temperature
│         { "Datastream@iot.id": 2, "result": 65 },    // Humidity
│         { "Datastream@iot.id": 3, "result": 3.2 },   // Wind speed
│         { "Datastream@iot.id": 4, "result": 1013 }   // Pressure
│       ]
│
└─ Server stores and indexes all data
```

---

### 6.2 Underground Utility Locating with AR

**Equipment:**
- Meta Quest 3
- GPR sensor with cellular modem (direct to server)

**Workflow:**

```
Before Field Work:
├─ GPR sensor deployed on equipment
├─ Configured to auto-upload:
│   POST /v1.1/Observations every 5 seconds
└─ Creates Thing and Datastream on first connect

Technician with Quest 3:
├─ Put on Quest 3, launch Honua AR app
├─ App queries server:
│   GET /collections/utilities/items?
│     bbox=-122.5,37.8,-122.4,37.9&
│     filter=utility_type eq 'gas'
│
│   GET /v1.1/Datastreams(gpr-001)/Observations?
│     $filter=phenomenonTime gt now() - 1 minute
│
├─ AR overlay renders:
│   • Yellow lines on ground (gas pipes)
│   • Depth labels: "-2.3m"
│   • Safe dig zones (green)
│   • Live GPR signal strength
│
├─ GPR detects anomaly at -2.3m
│   POST /v1.1/Observations { result: 2.3, confidence: 0.92 }
│
├─ Quest 3 updates AR in real-time
│   Warning: "STRONG SIGNAL - Buried utility detected"
│
├─ User pinches in AR: "Mark this"
│   POST /collections/field_detections/items
│   {
│     "geometry": { "coordinates": [-122.4123, 37.8456, -2.3] },
│     "properties": {
│       "detection_method": "GPR",
│       "confidence": 0.92,
│       "sta_observation_id": "..."
│     }
│   }
│
└─ Server stores 3D feature, links to observation
    Available to all team members instantly
```

---

## 7. Implementation Timeline

### Phase 1: Server Infrastructure (Months 10-13)

**Month 10: Database & Schema**
- ✅ 3D geometry columns (geometry_3d, elevation_m)
- ✅ Utility-specific fields (utility_type, depth_meters)
- ✅ SensorThings tables (sta_things, sta_datastreams, sta_observations)
- ✅ Indexes (spatial, time-series)
- ✅ Migration scripts

**Month 11: SensorThings API Core**
- ✅ CRUD endpoints for all entities
- ✅ Entity relationships ($expand)
- ✅ Deep insert support
- ✅ Batch operations
- ✅ Link to Features API

**Month 12: Query & Optimization**
- ✅ OData query support ($filter, $orderby, $top, $skip)
- ✅ Time-series query optimization
- ✅ 3D CRS support in Features API
- ✅ Elevation service endpoint
- ✅ Performance testing (10k observations/sec)

**Month 13: Testing & Documentation**
- ✅ OGC SensorThings conformance tests
- ✅ Integration tests
- ✅ API documentation (OpenAPI)
- ✅ Developer examples
- ✅ Production deployment

**Deliverables:**
- 🎯 OGC SensorThings API v1.1 conformant
- 🎯 3D geometry in all OGC APIs
- 🎯 < 100ms query response time
- 🎯 100% test coverage

---

### Phase 2: Mobile Sensors + Ray-Ban (Months 14-16)

**Month 14: Sensor Integration**
- ✅ SensorThings API client (mobile)
- ✅ WiFi Direct support
- ✅ Bluetooth LE fallback
- ✅ Sensor registration UI
- ✅ Offline observation queue

**Month 15: Voice & Ray-Ban**
- ✅ Platform speech recognition (iOS Speech, Android SpeechRecognizer)
- ✅ Meta Ray-Ban SDK integration
- ✅ Voice command processing
- ✅ Text-to-speech feedback
- ✅ Camera trigger

**Month 16: Integration & Testing**
- ✅ End-to-end workflows
- ✅ External sensor testing (GNSS, weather)
- ✅ Voice accuracy tuning
- ✅ Beta deployment (50 users)
- ✅ Feedback iteration

**Deliverables:**
- 🎯 External sensor support (GNSS, weather, custom)
- 🎯 Ray-Ban hands-free operation
- 🎯 Voice recognition > 90% accuracy
- 🎯 50+ beta testers

---

### Phase 3: Quest 3 AR (Months 17-18)

**Month 17: Unity AR App**
- ✅ Unity project setup (Meta XR SDK)
- ✅ OGC Features API client (C#)
- ✅ SensorThings API client (C#)
- ✅ GPS to AR coordinate transform
- ✅ Underground utility renderer
- ✅ Hand tracking UI

**Month 18: Polish & Launch**
- ✅ Real-time sensor overlay
- ✅ Performance optimization (60 FPS)
- ✅ Depth label rendering
- ✅ Safety zone visualization
- ✅ Meta Store submission
- ✅ Production launch

**Deliverables:**
- 🎯 Quest 3 AR app in Meta Store
- 🎯 60 FPS AR rendering
- 🎯 Underground utility visualization
- 🎯 100+ enterprise deployments

---

## 8. Technology Stack

### 8.1 Server

| Component | Technology | Version |
|-----------|-----------|---------|
| **Runtime** | .NET | 8.0 |
| **Framework** | ASP.NET Core | 8.0 |
| **Database** | PostgreSQL | 16+ |
| **Spatial** | PostGIS | 3.4+ |
| **ORM** | Dapper + ADO.NET | - |
| **API Docs** | OpenAPI/Swagger | 3.0 |

### 8.2 Mobile App

| Component | Technology | Version |
|-----------|-----------|---------|
| **Framework** | .NET MAUI | 8.0 |
| **Language** | C# | 12 |
| **UI** | XAML | - |
| **Architecture** | MVVM | CommunityToolkit.Mvvm |
| **Local DB** | SQLite-net | 1.8+ |
| **Spatial** | NetTopologySuite | 2.5+ |
| **Maps** | Mapsui (MIT) | 4.1.9+ |
| **Rendering** | SkiaSharp | 2.88+ |
| **Tiles** | OpenStreetMap, OGC WMTS | - |

### 8.3 AR (Quest 3)

| Component | Technology | Version |
|-----------|-----------|---------|
| **Engine** | Unity | 2023 LTS |
| **Language** | C# | 10+ |
| **AR SDK** | Meta XR SDK | Latest |
| **Platform** | Android (Quest OS) | - |

### 8.4 Standards

| Standard | Version | Status |
|----------|---------|--------|
| **OGC Features API** | Part 1-4 | ✅ Implemented |
| **OGC Tiles API** | 1.0 | ✅ Implemented |
| **OGC Processes API** | 1.0 | ✅ Implemented |
| **OGC SensorThings API** | 1.1 | 🆕 Phase 1 |
| **STAC** | 1.0 | ✅ Implemented |
| **WMS** | 1.3.0 | ✅ Implemented |
| **WFS** | 2.0 | ✅ Implemented |
| **WCS** | 2.0 | ✅ Implemented |
| **WMTS** | 1.0 | ✅ Implemented |
| **CSW** | 3.0 | ✅ Implemented |

---

## 9. Success Metrics

### 9.1 Server Performance (Phase 1)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **SensorThings Conformance** | 100% | OGC test suite |
| **Observation Ingest** | 10,000/sec | Load testing |
| **Query Response** | < 100ms | P95 latency |
| **3D Geometry Support** | All APIs | Feature parity |
| **Uptime** | 99.9% | Monthly average |

### 9.2 Mobile App (Phase 2)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Offline Data Collection** | 90% | Usage analytics |
| **Voice Accuracy** | > 90% | Speech recognition tests |
| **Ray-Ban Users** | 1,000+ | Active installations |
| **Sensor Integrations** | 5+ types | GNSS, weather, custom |
| **Crash-Free Rate** | > 99% | AppCenter/Firebase |

### 9.3 AR Platform (Phase 3)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Quest 3 FPS** | 60 FPS | Profiling |
| **AR Accuracy** | ± 0.5m | GPS + AR transform |
| **Enterprise Deployments** | 100+ | License sales |
| **App Store Rating** | 4.5+ stars | Meta Store reviews |
| **User Adoption** | 25% AR usage | Feature analytics |

---

## 10. Key Design Decisions

### Decision 1: Standards-Based Architecture ✅

**Choice:** Implement OGC SensorThings API (not custom REST)

**Rationale:**
- Maintains Honua's standards-based identity
- Interoperability with IoT platforms (FROST, GOST, 52°North)
- Future-proof for sensor ecosystem growth
- Competitive differentiation vs. ArcGIS/GeoServer

**Trade-offs:**
- ✅ Pro: Industry standard, rich query capabilities
- ✅ Pro: Works with existing tools
- ❌ Con: More complex than simple REST API
- ❌ Con: 2-3 months implementation time

---

### Decision 2: 3D Geometry as Optional Enhancement ✅

**Choice:** Add geometry_3d column, keep 2D geometry as primary

**Rationale:**
- Backward compatible (existing clients unaffected)
- Essential for AR/underground utilities
- Opt-in for collections that need it
- Standard PostGIS 3D support

**Trade-offs:**
- ✅ Pro: No breaking changes
- ✅ Pro: Clients can choose 2D or 3D
- ❌ Con: Storage overhead for 3D-enabled features
- ❌ Con: Dual geometry sync complexity

---

### Decision 3: Phone-Centric Architecture for Ray-Ban ✅

**Choice:** Phone is hub, Ray-Ban is audio/camera peripheral

**Rationale:**
- Ray-Ban has no display (audio-only interface)
- Phone has processing power, storage, connectivity
- Better battery life (phone can charge)
- Simpler architecture than standalone glasses

**Trade-offs:**
- ✅ Pro: Leverages existing mobile app
- ✅ Pro: Reliable Bluetooth connection
- ❌ Con: Requires phone in pocket/pack
- ❌ Con: Not truly standalone

---

### Decision 4: Multi-Protocol Sensor Support ✅

**Choice:** Server accepts HTTP POST (protocol-agnostic)

**Rationale:**
- Mobile app handles connection complexity (WiFi/BLE/USB)
- WiFi sensors post directly to server
- Cellular sensors bypass phone entirely
- Server doesn't care about transport

**Trade-offs:**
- ✅ Pro: Maximum flexibility
- ✅ Pro: Future-proof for new protocols
- ❌ Con: Mobile app more complex
- ❌ Con: Multiple code paths to maintain

---

### Decision 5: Offline-First Mobile Architecture ✅

**Choice:** SQLite local storage with sync queue

**Rationale:**
- Field work often has no connectivity
- Data never lost (queue persists)
- Sync when convenient (WiFi, end of day)
- Conflict resolution built-in (ETag-based)

**Trade-offs:**
- ✅ Pro: Reliable data collection
- ✅ Pro: Works anywhere
- ❌ Con: Sync complexity
- ❌ Con: Storage management needed

---

### Decision 6: Unity for Quest 3 (Not MAUI) ✅

**Choice:** Unity-based Quest 3 app (C# shared code)

**Rationale:**
- Meta XR SDK requires Unity
- Best AR performance (native rendering)
- Mature AR tooling
- Can share C# business logic with mobile app

**Trade-offs:**
- ✅ Pro: Native Quest 3 integration
- ✅ Pro: 60 FPS AR performance
- ❌ Con: Separate codebase from mobile
- ❌ Con: Unity learning curve for team

---

## 11. Risk Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **SensorThings complexity** | Medium | High | Phased rollout, OGC conformance tests, 2-month buffer |
| **AR coordinate accuracy** | Medium | High | GPS + IMU fusion, calibration API, field testing |
| **Ray-Ban SDK limitations** | Low | Medium | Fallback to standard Bluetooth, voice-only mode |
| **Quest 3 performance** | Medium | Medium | Unity profiling, LOD system, 60 FPS target |
| **Sensor connectivity issues** | High | Medium | WiFi Direct > BLE, retry logic, offline queue |

### Operational Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Timeline delays** | Medium | High | Conservative estimates, parallel work, skip Quest 3 if needed |
| **Team capacity** | Low | High | Hire AR specialist early, contract help for Unity |
| **Customer adoption** | Medium | Medium | Beta program, user feedback loops, documentation |
| **Hardware availability** | Low | Medium | Dev kits ordered early, backup devices |

---

## 12. Future Roadmap (Post Phase 3)

### Version 2.0 (Months 19-24)

**Real-Time Features:**
- MQTT support for SensorThings
- WebSocket streaming observations
- Live AR collaboration (multi-user)

**Advanced AI:**
- On-device feature detection (ML.NET)
- Predictive analytics
- Automated QA checks

**Platform Expansion:**
- Apple Vision Pro support
- Future Meta Orion glasses
- Desktop AR (HoloLens 2)

**Ecosystem:**
- Plugin marketplace
- Third-party sensor integrations
- Developer SDK

---

## 13. Conclusion

Honua's complete system design delivers:

1. ✅ **Standards-Based Foundation** - OGC SensorThings API for IoT/sensors
2. ✅ **3D Geometry Support** - Underground utilities, elevation, AR-ready
3. ✅ **Mobile-First Architecture** - Offline-first with intelligent sync
4. ✅ **Hands-Free Operation** - Meta Ray-Ban voice + camera integration
5. ✅ **AR Visualization** - Meta Quest 3 underground utility overlay
6. ✅ **Sensor Platform** - External sensors via WiFi/BLE/cellular

**Competitive Position:**
- Only platform with OGC Features + SensorThings + AR
- Underground utility visualization (unique)
- Hands-free field collection (Meta glasses)
- Standards-based (interoperable)

**Implementation:** 18 months, 3 phases
**Team:** 6-10 people (scaled over time)
**Investment:** Server (Months 10-13), Mobile (Months 14-16), AR (Months 17-18)

---

## Appendices

### A. References

- OGC SensorThings API: https://www.ogc.org/standards/sensorthings
- OGC Features API: https://www.ogc.org/standards/ogcapi-features
- PostGIS 3D: https://postgis.net/docs/using_postgis_dbmanagement.html#RefObject
- .NET MAUI: https://learn.microsoft.com/en-us/dotnet/maui/
- Meta XR SDK: https://developer.oculus.com/documentation/

### B. Glossary

- **SensorThings API** - OGC standard for IoT/sensor observations
- **Thing** - IoT device or sensor in SensorThings model
- **Datastream** - Time-series of sensor observations
- **Observation** - Single sensor reading at a point in time
- **AR** - Augmented Reality (digital overlay on real world)
- **GNSS** - Global Navigation Satellite System (GPS, GLONASS, Galileo)
- **GPR** - Ground Penetrating Radar (detects buried utilities)

---

**Document Status:** ✅ Ready for Implementation
**Next Action:** Begin Phase 1 (Months 10-13) - Server Infrastructure
**Approval Required:** Executive team, engineering lead
