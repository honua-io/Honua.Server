# Azure Smart Cities & Digital Twins Integration Guide

**Last Updated**: 2025-11-10  
**Status**: Planning Document  
**Target**: Honua.Server v2.0 Smart Cities Features

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Azure Digital Twins](#azure-digital-twins)
3. [Azure IoT Hub](#azure-iot-hub)
4. [Azure IoT Central](#azure-iot-central)
5. [Azure Maps](#azure-maps)
6. [Azure Event Hubs & Event Grid](#azure-event-hubs--event-grid)
7. [Power BI](#power-bi)
8. [Azure Time Series Insights](#azure-time-series-insights)
9. [Integration Architecture](#integration-architecture)
10. [Implementation Roadmap](#implementation-roadmap)
11. [Authentication & Security](#authentication--security)
12. [Use Cases](#use-cases)

---

## Executive Summary

### What is Azure's Smart City Stack?

Microsoft Azure provides a comprehensive platform for building smart city and digital twin solutions through an integrated ecosystem of IoT, spatial, analytics, and visualization services.

**Core Components:**
- **Azure Digital Twins** - Platform for creating digital representations of physical environments
- **Azure IoT Hub/Central** - Device connectivity and telemetry ingestion
- **Azure Maps** - Geospatial services and indoor mapping
- **Event Hubs/Event Grid** - Event streaming and routing
- **Power BI** - Analytics and visualization
- **Time Series Insights** - Time-series data storage and analysis

### Honua.Server's Role

Honua.Server complements Microsoft's stack by providing:
- **OGC-compliant geospatial APIs** that integrate with Azure Digital Twins
- **Bi-directional sync** between Honua's PostGIS database and Azure Digital Twins graphs
- **Standards-based access** to digital twin data via WFS, OGC API Features, STAC
- **Multi-cloud flexibility** with Azure integration as one deployment option
- **Open architecture** avoiding vendor lock-in while leveraging Azure capabilities

### Value Proposition

**For Cities Using Azure:**
- Access digital twin data through industry-standard OGC APIs
- Export digital twins to GeoJSON, Shapefile, GeoPackage for GIS tools
- Query spatial relationships using CQL2 and SQL
- Integrate with non-Microsoft mapping tools (QGIS, ArcGIS, Leaflet)

**For Honua.Server Users:**
- Optional Azure Digital Twins integration for advanced scenarios
- Leverage Azure's IoT device management without lock-in
- Publish geospatial data to Azure Maps for consumption
- Stream sensor data to Power BI dashboards

---

## Azure Digital Twins

### What is Azure Digital Twins?

Azure Digital Twins (ADT) is a Platform-as-a-Service (PaaS) offering that creates live digital representations of physical environments using semantic models, twin graphs, and live data integration.

**Key Concepts:**

1. **DTDL Models (Digital Twins Definition Language)**
   - JSON-LD schema defining entity types
   - Based on JSON-LD for semantic interoperability
   - Supports properties, telemetry, relationships, commands
   - Extensible via interfaces and components

2. **Digital Twins**
   - Individual instances of DTDL models
   - Represent specific physical entities (buildings, sensors, rooms)
   - Store current state as property values
   - Connected via relationships

3. **Twin Graphs**
   - Network of twins connected by relationships
   - Queryable using ADT's query language (similar to SQL)
   - Represents organizational hierarchies and spatial containment

4. **Event Routes**
   - Send twin updates to Event Hubs, Event Grid, Service Bus
   - Enable downstream processing and notifications
   - Support filtering and transformation

### Azure Digital Twins APIs

**REST API Endpoints:**

```http
# Models API
GET    https://{instance}.api.wus2.digitaltwins.azure.net/models
POST   https://{instance}.api.wus2.digitaltwins.azure.net/models
GET    https://{instance}.api.wus2.digitaltwins.azure.net/models/{modelId}
DELETE https://{instance}.api.wus2.digitaltwins.azure.net/models/{modelId}

# Twins API
GET    https://{instance}.api.wus2.digitaltwins.azure.net/digitaltwins/{twinId}
PUT    https://{instance}.api.wus2.digitaltwins.azure.net/digitaltwins/{twinId}
PATCH  https://{instance}.api.wus2.digitaltwins.azure.net/digitaltwins/{twinId}
DELETE https://{instance}.api.wus2.digitaltwins.azure.net/digitaltwins/{twinId}

# Query API
POST   https://{instance}.api.wus2.digitaltwins.azure.net/query
```

**Authentication:**
- Azure AD OAuth 2.0
- Managed Identity support
- RBAC roles: Digital Twins Data Reader, Data Owner

**SDKs Available:**
- .NET (Azure.DigitalTwins.Core)
- Python (azure-digitaltwins-core)
- JavaScript/TypeScript (@azure/digital-twins-core)
- Java (azure-digitaltwins-core)

### Smart Cities Ontology

Microsoft maintains an open-source DTDL ontology for smart cities:
- Repository: https://github.com/Azure/opendigitaltwins-smartcities
- Based on ETSI NGSI-LD and OASC standards
- Includes models for:
  - Urban infrastructure (poles, streetlights, traffic signals)
  - Buildings and spaces
  - Environmental sensors
  - Mobility (parking, vehicles, transit)
  - Utilities (water, energy)

### How Honua.Server Integrates with Azure Digital Twins

#### Integration Pattern 1: Bi-Directional Sync

**Honua → Azure Digital Twins (Export)**

```
Honua PostGIS → Honua API → Azure Function → Azure Digital Twins
```

- Export Honua features as digital twins
- Map OGC Features to DTDL twins
- Sync on schedule or via webhooks
- Maintain relationship mappings

**Azure Digital Twins → Honua (Import)**

```
Azure Digital Twins → Event Grid → Azure Function → Honua API
```

- Subscribe to twin change notifications
- Update Honua features when twins change
- Preserve ADT twin IDs in Honua metadata
- Support spatial queries on synchronized data

#### Integration Pattern 2: Federated Query

- Query Azure Digital Twins from Honua APIs
- Combine ADT data with Honua's PostGIS data
- Present unified OGC API Features response
- Cache ADT data in Redis for performance

#### Integration Pattern 3: Event Streaming

- Stream Honua sensor observations to ADT
- Use Honua's SensorThings API as data source
- Update ADT telemetry in real-time
- Leverage Honua's anomaly detection before sending

### Sample DTDL Model

```json
{
  "@id": "dtmi:com:honua:smartcity:Sensor;1",
  "@type": "Interface",
  "displayName": "Smart City Sensor",
  "@context": "dtmi:dtdl:context;2",
  "contents": [
    {
      "@type": "Property",
      "name": "location",
      "schema": {
        "@type": "Object",
        "fields": [
          {
            "name": "latitude",
            "schema": "double"
          },
          {
            "name": "longitude",
            "schema": "double"
          },
          {
            "name": "elevation",
            "schema": "double"
          }
        ]
      }
    },
    {
      "@type": "Telemetry",
      "name": "temperature",
      "schema": "double"
    },
    {
      "@type": "Relationship",
      "name": "installedAt",
      "target": "dtmi:com:honua:smartcity:Building;1"
    }
  ]
}
```

---

## Azure IoT Hub

### What is Azure IoT Hub?

Azure IoT Hub is a managed service for bi-directional communication between IoT devices and Azure cloud services.

**Core Capabilities:**
- **Device Connectivity**: MQTT, AMQP, HTTPS protocols
- **Device Twins**: JSON documents storing device metadata and state
- **Message Routing**: Route telemetry to multiple endpoints
- **Direct Methods**: Invoke commands on devices
- **Device Provisioning Service (DPS)**: Zero-touch device onboarding

### Device Twins vs. Azure Digital Twins

| Feature | IoT Hub Device Twins | Azure Digital Twins |
|---------|---------------------|---------------------|
| **Purpose** | Device state management | Semantic modeling of environments |
| **Scope** | Single device | Multi-entity graphs with relationships |
| **Schema** | Free-form JSON | DTDL-based semantic models |
| **Query** | SQL-like device queries | Graph queries across relationships |
| **Typical Use** | Device configuration, shadow state | Building models, city infrastructure |

**Common Pattern**: IoT Hub device twins feed data into Azure Digital Twins for higher-level modeling.

### Integration with Azure Digital Twins

**Data Flow:**

```
IoT Devices → IoT Hub → Azure Function → Azure Digital Twins
```

**Azure Function Implementation:**

```csharp
[FunctionName("IoTHubToDigitalTwins")]
public static async Task Run(
    [EventGridTrigger] EventGridEvent eventGridEvent,
    [DigitalTwins] DigitalTwinsClient client,
    ILogger log)
{
    var deviceMessage = JsonSerializer.Deserialize<DeviceMessage>(
        eventGridEvent.Data.ToString());
    
    var twinId = $"sensor-{deviceMessage.DeviceId}";
    
    var patch = new JsonPatchDocument();
    patch.AppendReplace("/temperature", deviceMessage.Temperature);
    patch.AppendReplace("/lastUpdate", DateTime.UtcNow);
    
    await client.UpdateDigitalTwinAsync(twinId, patch);
}
```

### How Honua.Server Integrates with IoT Hub

#### Option 1: Honua as Consumer

**Use Case**: Ingest IoT Hub telemetry into Honua's SensorThings API

```
IoT Hub → Event Hub → Honua Intake API → SensorThings Observations
```

**Benefits:**
- Store device telemetry in PostGIS for spatial queries
- Expose via OGC SensorThings API
- Enable standards-based access to IoT data
- Leverage Honua's anomaly detection

**Implementation:**

```csharp
// Honua.Server.Intake: IoTHubTelemetryHandler.cs
public class IoTHubTelemetryHandler
{
    private readonly ISensorThingsService _sensorThings;
    
    public async Task ProcessTelemetryAsync(EventData eventData)
    {
        var deviceId = eventData.SystemProperties["iothub-connection-device-id"];
        var body = Encoding.UTF8.GetString(eventData.Body);
        var telemetry = JsonSerializer.Deserialize<DeviceTelemetry>(body);
        
        // Map to SensorThings Observation
        var observation = new Observation
        {
            DatastreamId = MapDeviceIdToDatastream(deviceId),
            ResultTime = telemetry.Timestamp,
            Result = telemetry.Value,
            FeatureOfInterest = await ResolveFeatureOfInterest(deviceId)
        };
        
        await _sensorThings.CreateObservationAsync(observation);
    }
}
```

#### Option 2: Honua as Source

**Use Case**: Send Honua sensor observations to IoT Hub

```
Honua SensorThings API → Azure Function → IoT Hub
```

**Benefits:**
- Leverage IoT Hub's device management
- Route to Azure Stream Analytics for processing
- Integrate with Azure Digital Twins

---

## Azure IoT Central

### What is Azure IoT Central?

Azure IoT Central is a fully managed SaaS platform for building IoT solutions without writing code.

**Key Features:**
- **Pre-built Templates**: Smart city apps for waste management, water quality, parking
- **No-Code Device Management**: Visual device provisioning and monitoring
- **Built-in Dashboards**: Configurable visualization without coding
- **Rules Engine**: Automated alerts and actions
- **Data Export**: Continuous export to Event Hubs, Blob Storage, Webhook

### IoT Central vs. IoT Hub

| Aspect | IoT Central | IoT Hub |
|--------|-------------|---------|
| **Deployment** | SaaS (fully managed) | PaaS (requires config) |
| **Code Required** | No (templates + UI) | Yes (custom functions) |
| **Customization** | Limited to templates | Full control |
| **Cost** | Per-device pricing | Per-message pricing |
| **Best For** | Rapid prototyping, citizen-facing apps | Enterprise, custom integration |

### Smart City Templates

Microsoft provides government templates for:
- **Water Quality Monitoring**: Track pH, turbidity, contamination
- **Waste Management**: Monitor bin fill levels, optimize routes
- **Air Quality**: Track PM2.5, CO2, NO2
- **Smart Parking**: Occupancy sensors, availability maps

### How Honua.Server Integrates with IoT Central

#### Option 1: Consume IoT Central Data

**Use Case**: Export IoT Central sensor data to Honua for spatial analysis

```
IoT Central → Continuous Data Export → Event Hub → Honua Intake
```

**Configuration (IoT Central):**
```json
{
  "type": "webhook@v1",
  "displayName": "Honua Intake",
  "url": "https://honua.example.com/intake/iot-central",
  "headers": {
    "Authorization": "Bearer ${HONUA_API_KEY}"
  },
  "filter": {
    "devices": ["air-quality-*"]
  }
}
```

**Honua Handler:**
```csharp
[HttpPost("/intake/iot-central")]
public async Task<IActionResult> ReceiveIoTCentralData(
    [FromBody] IoTCentralMessage message)
{
    // Extract telemetry
    var deviceId = message.Device.Id;
    var telemetry = message.Telemetry;
    
    // Map to SensorThings
    var observation = new Observation
    {
        DatastreamId = await GetOrCreateDatastream(deviceId),
        ResultTime = message.EnqueuedTime,
        Result = telemetry["value"],
        // IoT Central provides device location
        FeatureOfInterest = CreatePointFeature(
            message.Device.Properties.Location.Lat,
            message.Device.Properties.Location.Lon
        )
    };
    
    await _sensorThings.CreateObservationAsync(observation);
    return Ok();
}
```

#### Option 2: Visualize Honua Data in IoT Central

**Use Case**: Display Honua features in IoT Central dashboards

- Create custom tiles using Honua APIs
- Embed OGC API Features queries
- Show geofences from Honua GeoEvent API

---

## Azure Maps

### What is Azure Maps?

Azure Maps provides geospatial services including geocoding, routing, mapping, and indoor maps.

**Key Services:**
- **Search**: Geocoding, reverse geocoding, POI search
- **Routing**: Turn-by-turn navigation, route optimization
- **Render**: Map tiles, satellite imagery
- **Indoor Maps**: Floorplans, wayfinding, facility management
- **Spatial Operations**: Geofencing, proximity queries
- **Weather**: Current and forecast data

### Azure Maps Creator

Creator extends Azure Maps with custom indoor map capabilities:

- **Indoor Map Data**: Upload CAD, GeoJSON floorplans
- **Dataset Service**: Manage facility data
- **Tileset Service**: Generate vector tiles from datasets
- **Feature State Service**: Update map styling based on live data
- **WFS API**: Query indoor map data (OGC-compliant!)

### Integration with Azure Digital Twins

**Pattern**: Visualize digital twin data on indoor maps

```
Azure Digital Twins → Azure Function → Azure Maps Feature State Service
```

**Example**: Change room colors based on occupancy from digital twin

```csharp
public async Task UpdateRoomOccupancy(string roomTwinId, int occupancy)
{
    // Get room twin
    var roomTwin = await _adtClient.GetDigitalTwinAsync<Room>(roomTwinId);
    
    // Determine color based on capacity
    var capacity = roomTwin.Capacity;
    var utilizationPercent = (occupancy / (double)capacity) * 100;
    
    var color = utilizationPercent switch
    {
        < 50 => "#00FF00", // Green
        < 80 => "#FFFF00", // Yellow
        _    => "#FF0000"  // Red
    };
    
    // Update Azure Maps feature state
    var stateUpdate = new FeatureStateUpdate
    {
        States = new[]
        {
            new FeatureState
            {
                KeyName = "occupancy",
                Value = occupancy,
                StyleProperty = "fillColor",
                StyleValue = color
            }
        }
    };
    
    await _mapsClient.UpdateFeatureStateAsync(
        featureId: roomTwin.MapFeatureId,
        statesetId: _config.StatesetId,
        stateUpdate: stateUpdate
    );
}
```

### How Honua.Server Integrates with Azure Maps

#### Option 1: Publish Honua Tiles to Azure Maps

**Use Case**: Serve Honua map tiles through Azure Maps CDN

```
Honua WMTS → Azure Blob Storage → Azure Maps Custom Tileset
```

**Steps:**
1. Generate tiles using Honua's tile service
2. Upload to Azure Blob Storage
3. Register as Azure Maps custom tileset
4. Reference in Azure Maps applications

**Benefit**: Leverage Azure's global CDN while keeping Honua as authoritative source

#### Option 2: Consume Azure Maps in Honua MapSDK

**Use Case**: Display Azure Maps basemaps in Honua's Blazor map builder

```csharp
// Honua.MapSDK: AzureMapsLayerProvider.cs
public class AzureMapsLayerProvider : IBaseMapProvider
{
    public MapLayer CreateBasemap(AzureMapsConfig config)
    {
        return new MapLayer
        {
            Type = "raster",
            Source = new RasterSource
            {
                Type = "raster",
                Tiles = new[]
                {
                    $"https://atlas.microsoft.com/map/tile?api-version=2.0" +
                    $"&tilesetId=microsoft.base.road" +
                    $"&zoom={{z}}&x={{x}}&y={{y}}" +
                    $"&subscription-key={config.SubscriptionKey}"
                },
                TileSize = 256
            }
        };
    }
}
```

#### Option 3: Indoor Maps Integration

**Use Case**: Overlay Honua features on Azure Maps indoor maps

**Scenario**: Smart building with Azure Maps floorplans + Honua sensor data

```javascript
// Client-side integration
const map = new atlas.Map('map', {
    authOptions: {
        authType: 'subscriptionKey',
        subscriptionKey: azureMapsKey
    }
});

// Load indoor map from Azure Maps Creator
await map.maps.indoor.setFacility('facility-12345');

// Overlay Honua sensor features
const honuaDataSource = new atlas.source.DataSource();
const honuaSensors = await fetch(
    'https://honua.example.com/ogc/collections/sensors/items'
);
honuaDataSource.add(await honuaSensors.json());
map.sources.add(honuaDataSource);

// Style sensors based on readings
const sensorLayer = new atlas.layer.BubbleLayer(honuaDataSource, null, {
    radius: 10,
    color: ['get', 'status-color'], // From Honua anomaly detection
    strokeWidth: 2
});
map.layers.add(sensorLayer);
```

---

## Azure Event Hubs & Event Grid

### Event Hubs

**Purpose**: High-throughput event streaming for millions of events per second

**Use Cases:**
- Telemetry ingestion from thousands of sensors
- Real-time analytics pipelines
- Event replay and time-travel debugging

**Key Features:**
- Partitioned for parallel processing
- Retention: 1-90 days
- Capture to Blob Storage / Data Lake
- Kafka-compatible endpoint

### Event Grid

**Purpose**: Event-driven architectures with pub/sub messaging

**Use Cases:**
- React to Azure resource events (blob uploaded, twin updated)
- Serverless workflows
- Webhook notifications

**Key Features:**
- 24-hour retry with exponential backoff
- Dead-letter queue support
- Built-in filtering
- Schema validation

### When to Use Which?

| Scenario | Use Event Hubs | Use Event Grid |
|----------|----------------|----------------|
| High-volume telemetry | ✅ Yes | ❌ No (5MB/sec limit) |
| React to Azure events | ❌ No | ✅ Yes |
| Event replay required | ✅ Yes (retention) | ❌ No (at-most-once) |
| Serverless integration | ⚠️ Possible | ✅ Ideal |
| Complex routing | ⚠️ Manual | ✅ Built-in |

### Integration with Azure Digital Twins

**Event Grid Pattern:**

```
Azure Digital Twins → Event Grid Topic → [Subscribers]
                                        ├─ Azure Function
                                        ├─ Logic App
                                        └─ Webhook
```

**Event Types:**
- `Microsoft.DigitalTwins.Twin.Create`
- `Microsoft.DigitalTwins.Twin.Update`
- `Microsoft.DigitalTwins.Twin.Delete`
- `Microsoft.DigitalTwins.Relationship.Create`
- `Microsoft.DigitalTwins.Relationship.Delete`

**Event Hub Pattern:**

```
Azure Digital Twins → Event Hub → Stream Analytics → Power BI
                                ├─ Azure Function → Cosmos DB
                                └─ Databricks → Delta Lake
```

### How Honua.Server Integrates

#### Option 1: Honua Receives ADT Events

**Use Case**: Sync Azure Digital Twins changes to Honua database

```csharp
// Azure Function triggered by Event Grid
[FunctionName("ADTToHonua")]
public static async Task Run(
    [EventGridTrigger] EventGridEvent eventGridEvent,
    ILogger log)
{
    var twinUpdate = JsonSerializer.Deserialize<TwinUpdateEvent>(
        eventGridEvent.Data.ToString());
    
    // Map to Honua feature
    var feature = new Feature
    {
        Id = twinUpdate.TwinId,
        Properties = new Dictionary<string, object>
        {
            ["adt_model"] = twinUpdate.ModelId,
            ["last_updated"] = twinUpdate.UpdateTime
        },
        Geometry = ExtractGeometry(twinUpdate.Properties)
    };
    
    // Update via Honua WFS-T API
    await _honuaClient.UpdateFeatureAsync("digital-twins", feature);
}
```

#### Option 2: Honua Publishes Events to Event Grid

**Use Case**: Notify Azure services when Honua features change

**Configuration:**

```json
// appsettings.json
{
  "EventGrid": {
    "Enabled": true,
    "TopicEndpoint": "https://honua-events.westus-1.eventgrid.azure.net/api/events",
    "TopicKey": "${EVENTGRID_TOPIC_KEY}",
    "PublishOn": ["feature.created", "feature.updated", "anomaly.detected"]
  }
}
```

**Implementation:**

```csharp
public class EventGridPublisher
{
    private readonly EventGridPublisherClient _client;
    
    public async Task PublishFeatureUpdateAsync(Feature feature)
    {
        var eventData = new EventGridEvent(
            subject: $"features/{feature.CollectionId}/{feature.Id}",
            eventType: "Honua.Features.Updated",
            dataVersion: "1.0",
            data: new
            {
                featureId = feature.Id,
                collectionId = feature.CollectionId,
                geometry = feature.Geometry,
                properties = feature.Properties,
                updatedBy = _currentUser.Id,
                updatedAt = DateTime.UtcNow
            }
        );
        
        await _client.SendEventAsync(eventData);
    }
}
```

---

## Power BI

### What is Power BI?

Power BI is Microsoft's business intelligence platform for creating interactive dashboards and reports.

**Components:**
- **Power BI Desktop**: Windows app for creating reports
- **Power BI Service**: Cloud-based report hosting and sharing
- **Power BI Mobile**: iOS/Android/Windows apps
- **Power BI Embedded**: Embed reports in custom applications

### Power BI Connectors

**Built-in Azure Connectors:**
- Azure SQL Database
- Azure Synapse Analytics
- Azure Data Lake Storage
- Azure Analysis Services
- Azure Blob Storage

**Custom Connectors:**
- REST API connector
- OData connector (Honua supports OData!)
- Python/R scripts

### Real-Time Datasets

Power BI supports streaming datasets with sub-second refresh:

- **Streaming Dataset**: In-memory only, 200K rows max
- **PushDataset**: Persisted, full refresh on reload
- **Hybrid Dataset**: Streaming + historical storage

### How Honua.Server Integrates with Power BI

#### Option 1: OData Connector

**Use Case**: Query Honua features directly in Power BI

**Power BI Setup:**
1. Get Data → OData Feed
2. Enter URL: `https://honua.example.com/odata/features/buildings`
3. Authenticate with API key
4. Load data into Power BI Desktop

**Honua Configuration:**
```json
{
  "OData": {
    "Enabled": true,
    "MaxPageSize": 1000,
    "EnableCount": true,
    "EnableFilter": true
  }
}
```

**Benefits:**
- Live queries (no ETL needed)
- Leverage Honua's spatial indexes
- Filter in Power BI translates to efficient SQL

#### Option 2: REST API Connector

**Use Case**: Build custom queries using OGC API Features

**Power Query M Code:**
```m
let
    // Query Honua for high-priority parcels
    Source = Json.Document(Web.Contents(
        "https://honua.example.com/ogc/collections/parcels/items",
        [
            Query=[
                filter="priority eq 'high'",
                limit="1000",
                bbox="-122.5,37.7,-122.3,37.9"
            ],
            Headers=[
                #"Authorization"="Bearer " & ApiKey
            ]
        ]
    )),
    
    // Extract features
    Features = Source[features],
    
    // Convert to table
    Table = Table.FromList(Features, Splitter.SplitByNothing()),
    Expanded = Table.ExpandRecordColumn(
        Table,
        "Column1",
        {"id", "properties", "geometry"}
    )
in
    Expanded
```

#### Option 3: Streaming via Azure Function

**Use Case**: Real-time sensor dashboard

**Architecture:**
```
Honua SensorThings → SignalR Hub → Azure Function → Power BI Streaming Dataset
```

**Azure Function:**
```csharp
[FunctionName("HonuaToPowerBI")]
public static async Task Run(
    [SignalRTrigger("HonuaServer", "sensor-observations")] 
    InvocationContext invocationContext,
    ILogger log)
{
    var observation = invocationContext.Arguments[0] as SensorObservation;
    
    // Power BI REST API
    var powerBIClient = new HttpClient();
    var dataset = "https://api.powerbi.com/v1.0/myorg/datasets/{datasetId}/rows";
    
    var row = new
    {
        timestamp = observation.ResultTime,
        sensor_id = observation.DatastreamId,
        value = observation.Result,
        latitude = observation.Location.Latitude,
        longitude = observation.Location.Longitude
    };
    
    await powerBIClient.PostAsJsonAsync(dataset, new { rows = new[] { row } });
}
```

#### Option 4: Azure Synapse Link

**Use Case**: Large-scale analytics on historical data

**Architecture:**
```
Honua PostGIS → Azure Data Factory → Azure Synapse → Power BI
```

**Azure Data Factory Pipeline:**
1. Query Honua via OGC API Features
2. Write to Parquet in Data Lake
3. Create external table in Synapse
4. Connect Power BI to Synapse

**Benefits:**
- Separate analytics from production database
- Leverage Synapse's distributed query engine
- Join Honua data with other Azure datasets

---

## Azure Time Series Insights

### What is Azure Time Series Insights?

Azure Time Series Insights (TSI) is a fully managed analytics, storage, and visualization service for time-series data.

**Note**: TSI is in maintenance mode as of March 2023. Microsoft recommends **Azure Data Explorer** for new projects.

**Key Capabilities (Legacy):**
- Ingest millions of events/second
- Interactive explorer with drill-down
- Built-in time-series analytics
- Long-term storage (warm + cold tiers)
- REST API for queries

### Integration with IoT Hub

**Direct Integration:**
```
IoT Hub → TSI Event Source (automatic)
```

**Configuration:**
- Consumer group: tsi-consumer-group
- Timestamp property: deviceMessage.enqueuedTime
- Partitioning: by device ID

### Migration Path: Azure Data Explorer

**Recommended Replacement:**

```
IoT Hub → Event Hub → Azure Data Explorer
```

**Benefits over TSI:**
- Better query performance (Kusto Query Language)
- More flexible schema
- Integration with Power BI, Grafana
- Advanced analytics (ML, anomaly detection)

### How Honua.Server Integrates

#### Option 1: Export to TSI/ADX

**Use Case**: Store Honua sensor observations for long-term analysis

**Azure Function:**
```csharp
[FunctionName("HonuaToADX")]
public static async Task Run(
    [EventHubTrigger("honua-observations")] EventData[] events,
    ILogger log)
{
    var kustoClient = new KustoQueuedIngestClient(
        KustoConnectionStringBuilder.WithAadApplicationKeyAuthentication(
            $"https://{cluster}.{region}.kusto.windows.net",
            appId, appKey, tenantId
        )
    );
    
    var observations = events.Select(e =>
    {
        var obs = JsonSerializer.Deserialize<SensorObservation>(e.Body);
        return new
        {
            Timestamp = obs.ResultTime,
            DatastreamId = obs.DatastreamId,
            Value = obs.Result,
            Latitude = obs.Location?.Latitude,
            Longitude = obs.Location?.Longitude
        };
    });
    
    await kustoClient.IngestFromStreamAsync(
        ToJsonStream(observations),
        new KustoIngestionProperties("SensorObservations")
    );
}
```

#### Option 2: Query TSI/ADX from Honua

**Use Case**: Provide historical sensor data via SensorThings API

**Implementation:**
```csharp
public class AzureDataExplorerBackend : ISensorThingsBackend
{
    private readonly KustoQueryClient _client;
    
    public async Task<IEnumerable<Observation>> GetObservationsAsync(
        string datastreamId,
        DateTime start,
        DateTime end)
    {
        var query = $@"
            SensorObservations
            | where DatastreamId == '{datastreamId}'
            | where Timestamp between (datetime({start:yyyy-MM-ddTHH:mm:ss}) .. datetime({end:yyyy-MM-ddTHH:mm:ss}))
            | order by Timestamp asc
        ";
        
        var results = await _client.ExecuteQueryAsync(query);
        
        return results.Select(row => new Observation
        {
            ResultTime = row.GetDateTime("Timestamp"),
            Result = row.GetDouble("Value"),
            // ... map other properties
        });
    }
}
```

---

## Integration Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Smart City Applications                      │
│  Web Dashboards · Mobile Apps · QGIS · ArcGIS · Power BI      │
└────────┬────────────────────────────────────────────────────────┘
         │
         ├─────────────────────┬───────────────────────────────────┐
         │                     │                                   │
    ┌────▼─────────┐  ┌───────▼──────────┐         ┌─────────────▼────┐
    │ Honua.Server │  │ Azure Digital    │         │   Power BI       │
    │ (OGC APIs)   │  │ Twins (ADT)      │         │   Dashboards     │
    └────┬─────────┘  └───────┬──────────┘         └─────────────┬────┘
         │                     │                                  │
         │     ┌───────────────┴────────────────┐                │
         │     │                                │                │
    ┌────▼─────▼─────┐              ┌──────────▼──────────┐     │
    │  Event Grid/   │              │   Azure Maps        │     │
    │  Event Hubs    │◄─────────────│   (Indoor Maps)     │     │
    └────┬───────────┘              └─────────────────────┘     │
         │                                                       │
         │   ┌────────────────────────────────────────┐         │
         │   │                                        │         │
    ┌────▼───▼────┐    ┌──────────────┐   ┌─────────▼────┐    │
    │  IoT Hub    │    │ Azure Maps   │   │  Azure       │    │
    │  (Devices)  │    │ Creator      │   │  Synapse     │◄───┘
    └─────────────┘    └──────────────┘   └──────────────┘
```

### Pattern 1: Azure-Centric with Honua Gateway

**Use Case**: Organization standardized on Azure, wants OGC compatibility

**Architecture:**
```
IoT Devices → IoT Hub → Azure Digital Twins (source of truth)
                             ↓
                        Event Grid
                             ↓
                        Azure Function
                             ↓
                        Honua.Server (read-only mirror)
                             ↓
                        OGC API Features, WFS, STAC
```

**Benefits:**
- Azure Digital Twins as authoritative source
- Honua provides standards-based access
- No vendor lock-in for data consumers

**Implementation:**
1. Configure ADT event route to Event Grid
2. Deploy Azure Function to transform ADT events
3. Update Honua features via WFS-T API
4. Applications query via OGC APIs

### Pattern 2: Honua-Centric with Azure Integration

**Use Case**: Organization with existing Honua deployment, wants Azure analytics

**Architecture:**
```
Field Apps → Honua.Server (source of truth)
                 ↓
            PostGIS Database
                 ↓
            Event Grid Publisher
                 ↓
            [Azure Services]
            ├─ Azure Digital Twins (semantic overlay)
            ├─ Power BI (dashboards)
            ├─ Azure Synapse (analytics)
            └─ Azure Maps (visualization)
```

**Benefits:**
- Honua remains authoritative source
- Leverage Azure for specialized tasks
- Multi-cloud flexibility maintained

**Implementation:**
1. Enable Honua Event Grid publisher
2. Subscribe Azure Functions to events
3. Sync selected features to ADT
4. Export to Synapse for analytics

### Pattern 3: Federated Query

**Use Case**: Query across Honua and Azure Digital Twins without sync

**Architecture:**
```
Client Application
       ↓
  Honua Gateway
       ↓
   [Query Router]
   ├─ Honua PostGIS (features, geometry)
   └─ Azure Digital Twins (semantic relationships, real-time telemetry)
       ↓
   [Merge & Return]
```

**Benefits:**
- No data duplication
- Always current data
- Flexible query routing

**Implementation:**
```csharp
public class FederatedQueryHandler
{
    private readonly IDataStoreProvider _honua;
    private readonly DigitalTwinsClient _adt;
    
    public async Task<FeatureCollection> QueryBuildingsWithOccupancy(
        BoundingBox bbox)
    {
        // Query Honua for building geometries
        var buildings = await _honua.QueryFeaturesAsync(
            "buildings",
            new CqlFilter($"ST_Intersects(geometry, {bbox})")
        );
        
        // Query ADT for occupancy (parallel)
        var occupancyTasks = buildings.Select(async building =>
        {
            var twinId = building.Properties["adt_twin_id"];
            var twin = await _adt.GetDigitalTwinAsync<BuildingTwin>(twinId);
            return (building.Id, twin.CurrentOccupancy);
        });
        
        var occupancies = await Task.WhenAll(occupancyTasks);
        
        // Merge data
        foreach (var (buildingId, occupancy) in occupancies)
        {
            var building = buildings.First(b => b.Id == buildingId);
            building.Properties["current_occupancy"] = occupancy;
        }
        
        return new FeatureCollection { Features = buildings };
    }
}
```

### Pattern 4: Event-Driven Microservices

**Use Case**: Large-scale smart city with distributed services

**Architecture:**
```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│ IoT Devices │────▶│  IoT Hub     │────▶│ Event Hub   │
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                 │
       ┌─────────────────────────────────────────┼───────────────┐
       │                                         │               │
  ┌────▼────────┐         ┌──────────────┐  ┌──▼───────────┐   │
  │ ADT Updater │         │ Honua Intake │  │ Anomaly      │   │
  │ Function    │         │ Function     │  │ Detector     │   │
  └────┬────────┘         └──────┬───────┘  └──┬───────────┘   │
       │                         │              │               │
       ▼                         ▼              ▼               │
┌──────────────┐         ┌──────────────┐  ┌─────────────┐    │
│Azure Digital │         │Honua.Server  │  │Event Grid   │    │
│Twins         │         │(SensorThings)│  │(Alerts)     │    │
└──────────────┘         └──────────────┘  └─────────────┘    │
       │                                                        │
       └────────────────────────────────────────────────────────┘
                              │
                         ┌────▼──────┐
                         │ Power BI  │
                         │ Dashboard │
                         └───────────┘
```

---

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)

**Goal**: Establish authentication and basic connectivity

**Tasks:**
- [ ] Set up Azure AD application registration
- [ ] Implement Managed Identity for Honua
- [ ] Create Azure Digital Twins instance (dev environment)
- [ ] Deploy Azure Function for event routing
- [ ] Test bi-directional authentication

**Deliverables:**
- Honua can authenticate to Azure Digital Twins
- Azure Functions can call Honua APIs
- Documentation for credentials setup

### Phase 2: IoT Hub Integration (Weeks 5-8)

**Goal**: Ingest IoT Hub telemetry into Honua SensorThings API

**Tasks:**
- [ ] Create Event Hub consumer for IoT Hub
- [ ] Implement telemetry mapping to SensorThings
- [ ] Add device provisioning workflow
- [ ] Configure message routing in IoT Hub
- [ ] Test with simulated devices

**Deliverables:**
- Honua receives IoT Hub messages
- SensorThings Observations created automatically
- Admin UI for device-to-datastream mapping

### Phase 3: Azure Digital Twins Sync (Weeks 9-14)

**Goal**: Bi-directional sync between Honua and ADT

**Tasks:**
- [ ] Design DTDL models for Honua entities
- [ ] Implement Honua → ADT export function
- [ ] Implement ADT → Honua import function
- [ ] Add conflict resolution strategy
- [ ] Create sync status dashboard

**Deliverables:**
- Honua features synced to ADT twins
- ADT changes reflected in Honua
- Admin UI for managing sync mappings

### Phase 4: Event Streaming (Weeks 15-18)

**Goal**: Real-time event publishing and consumption

**Tasks:**
- [ ] Configure Event Grid topic for Honua events
- [ ] Implement event publishers in Honua
- [ ] Subscribe to ADT events via Event Grid
- [ ] Add event filtering and transformation
- [ ] Monitor event latency and throughput

**Deliverables:**
- Honua publishes feature updates to Event Grid
- Azure services react to Honua events
- Monitoring dashboard for event flows

### Phase 5: Azure Maps Integration (Weeks 19-22)

**Goal**: Visualize Honua data with Azure Maps

**Tasks:**
- [ ] Create Azure Maps account
- [ ] Implement custom tileset upload
- [ ] Add Azure Maps layer to MapSDK
- [ ] Test indoor maps integration
- [ ] Create sample applications

**Deliverables:**
- Honua tiles available in Azure Maps
- MapSDK supports Azure Maps basemaps
- Sample smart building dashboard

### Phase 6: Power BI Connector (Weeks 23-26)

**Goal**: Enable Power BI dashboards with Honua data

**Tasks:**
- [ ] Optimize OData performance
- [ ] Create Power BI template files
- [ ] Build streaming dataset pipeline
- [ ] Document Power Query examples
- [ ] Test with large datasets

**Deliverables:**
- Power BI template for smart city metrics
- OData connector documentation
- Sample dashboards

### Phase 7: Production Hardening (Weeks 27-30)

**Goal**: Prepare for production deployment

**Tasks:**
- [ ] Security audit (penetration testing)
- [ ] Performance benchmarking
- [ ] Disaster recovery testing
- [ ] Documentation review
- [ ] Training materials

**Deliverables:**
- Production deployment guide
- Security compliance report
- Performance baseline metrics
- Admin training videos

---

## Authentication & Security

### Azure AD (Microsoft Entra ID)

**Authentication Flow:**

```
1. Honua.Server → Azure AD: Request token
2. Azure AD: Validates Managed Identity
3. Azure AD → Honua: Returns access token
4. Honua → Azure Digital Twins: API call with token
5. ADT: Validates token, authorizes request
```

**Implementation:**

```csharp
// Startup.cs
services.AddAzureClients(builder =>
{
    // Use Managed Identity (preferred for production)
    builder.AddDigitalTwinsClient(
        new Uri(Configuration["AzureDigitalTwins:InstanceUrl"]))
        .WithCredential(new DefaultAzureCredential());
    
    // Alternative: Client secret (development)
    // builder.AddDigitalTwinsClient(...)
    //     .WithCredential(new ClientSecretCredential(
    //         tenantId, clientId, clientSecret));
});
```

### Managed Identity

**Types:**
1. **System-Assigned**: Lifecycle tied to resource
2. **User-Assigned**: Independent lifecycle, reusable

**Recommended Approach:**

```bash
# Enable system-assigned MI on App Service / Container App
az webapp identity assign --name honua-server --resource-group honua-rg

# Grant ADT permissions
az dt role-assignment create \
  --dt-name honua-adt-instance \
  --assignee $(az webapp identity show --name honua-server --query principalId -o tsv) \
  --role "Azure Digital Twins Data Owner"
```

### RBAC Roles

**Azure Digital Twins Roles:**
- `Azure Digital Twins Data Reader` - Read twins, relationships, query
- `Azure Digital Twins Data Owner` - Full access (create, update, delete)

**IoT Hub Roles:**
- `IoT Hub Data Reader` - Read device data
- `IoT Hub Registry Contributor` - Manage device registry

**Azure Maps Roles:**
- `Azure Maps Data Reader` - Read map data, tilesets
- `Azure Maps Data Contributor` - Upload datasets, manage tilesets

### Secrets Management

**Azure Key Vault Integration:**

```csharp
// Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential()
);

// appsettings.json
{
  "AzureDigitalTwins": {
    "InstanceUrl": "https://honua-adt.api.wus2.digitaltwins.azure.net",
    // No credentials stored here!
  },
  "IoTHub": {
    "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://honua-kv.vault.azure.net/secrets/IoTHubConnectionString/)"
  }
}
```

### Network Security

**Private Endpoints:**

```bash
# Create private endpoint for ADT
az network private-endpoint create \
  --name honua-adt-pe \
  --resource-group honua-rg \
  --vnet-name honua-vnet \
  --subnet honua-subnet \
  --private-connection-resource-id $(az dt show --dt-name honua-adt --query id -o tsv) \
  --group-id API \
  --connection-name honua-adt-connection
```

**Benefits:**
- ADT not exposed to internet
- Traffic stays within Azure backbone
- Compliant with enterprise security policies

### API Security Best Practices

1. **Always use HTTPS** for Azure service communication
2. **Rotate credentials** every 90 days (automate with Key Vault)
3. **Least privilege**: Grant minimum required permissions
4. **Audit logging**: Enable Azure Monitor for all services
5. **Network isolation**: Use private endpoints in production
6. **Token validation**: Verify Azure AD tokens on both sides

---

## Use Cases

### Use Case 1: Smart Building Management

**Scenario**: University campus with 50 buildings, 5,000 IoT sensors

**Requirements:**
- Track HVAC, lighting, occupancy sensors
- Visualize building floor plans
- Alert on equipment failures
- Energy consumption analytics
- Integration with existing BMS (Building Management System)

**Architecture:**

```
BMS Devices → IoT Hub → [Branching]
                        ├─ Azure Digital Twins (building semantic model)
                        ├─ Honua SensorThings (spatial queries)
                        └─ Azure Data Explorer (long-term analytics)
                                ↓
                           Power BI Dashboard
```

**Implementation:**

1. **Model in ADT:**
   ```json
   {
     "@id": "dtmi:honua:campus:Building;1",
     "contents": [
       { "name": "name", "schema": "string" },
       { "name": "floors", "schema": "integer" },
       { "@type": "Relationship", "name": "contains", "target": "Room" }
     ]
   }
   ```

2. **Store in Honua:**
   - Buildings, rooms as OGC Features
   - Floor plans as GeoJSON geometries
   - Sensors as SensorThings entities

3. **Visualize:**
   - Azure Maps for indoor wayfinding
   - Power BI for energy dashboard
   - Honua MapSDK for facility management

**Benefits:**
- Spatial queries: "Find all rooms with CO2 > 1000ppm within 100m"
- BIM integration via IFC → DTDL conversion
- Export to CAD tools via GeoPackage

### Use Case 2: Smart Traffic Management

**Scenario**: City with 500 intersections, 2,000 traffic sensors

**Requirements:**
- Real-time traffic flow monitoring
- Incident detection and routing
- Historical pattern analysis
- Integration with existing traffic control system
- Public-facing traffic map

**Architecture:**

```
Traffic Sensors → IoT Hub → Event Hub → [Branching]
                                        ├─ Stream Analytics (real-time rules)
                                        ├─ Honua (spatial database)
                                        └─ ADT (intersection digital twins)
                                                ↓
                                           Event Grid
                                                ↓
                                        ┌───────┴────────┐
                                        │                │
                                  Logic App       Azure Maps
                                  (Alerts)       (Public Map)
```

**Implementation:**

1. **Ingest Data:**
   ```csharp
   // Azure Function: Traffic sensor → Honua
   [FunctionName("TrafficToHonua")]
   public static async Task Run(
       [EventHubTrigger("traffic-events")] EventData[] events)
   {
       foreach (var e in events)
       {
           var reading = Parse<TrafficReading>(e.Body);
           
           await _honua.CreateObservationAsync(new Observation
           {
               DatastreamId = $"traffic-{reading.SensorId}",
               ResultTime = reading.Timestamp,
               Result = reading.VehicleCount,
               FeatureOfInterest = GetIntersection(reading.SensorId)
           });
       }
   }
   ```

2. **Detect Incidents:**
   ```sql
   -- Stream Analytics query
   SELECT
       System.Timestamp() AS EventTime,
       IntersectionId,
       AVG(Speed) AS AvgSpeed
   INTO
       AnomalyOutputStream
   FROM
       TrafficInput TIMESTAMP BY EventTime
   GROUP BY
       IntersectionId, TumblingWindow(minute, 5)
   HAVING
       AVG(Speed) < 10 AND COUNT(*) > 100  -- Gridlock
   ```

3. **Visualize:**
   - Azure Maps with real-time traffic layer
   - Public-facing map using Honua WMTS tiles
   - Power BI dashboard for traffic operations center

**Benefits:**
- Standards-based access for third-party apps
- Historical queries: "Compare traffic patterns year-over-year"
- Open data portal via OGC API Features

### Use Case 3: Environmental Monitoring Network

**Scenario**: Regional air quality monitoring with 200 stations

**Requirements:**
- Track PM2.5, PM10, O3, NO2, CO
- Hourly data collection
- Public API for researchers
- Integration with health alerts system
- Predictive modeling (ML)

**Architecture:**

```
Air Quality Sensors → IoT Central → Event Hub → [Branching]
                                                ├─ Honua (SensorThings API)
                                                ├─ ADX (time-series storage)
                                                └─ Azure ML (forecasting)
                                                       ↓
                                                  Event Grid
                                                       ↓
                                                 Health Alert System
```

**Implementation:**

1. **Configure IoT Central:**
   - Use "Air Quality Monitoring" template
   - Configure continuous data export to Event Hub

2. **Ingest to Honua:**
   ```csharp
   // Expose via SensorThings API
   GET /sensorthings/v1.1/Datastreams?$filter=ObservedProperty/name eq 'PM2.5'
   GET /sensorthings/v1.1/Observations?$filter=result gt 35.0
   ```

3. **Public API:**
   - OGC SensorThings for standards-based access
   - OGC API EDR for environmental data retrieval
   - STAC catalog for historical datasets

4. **Machine Learning:**
   - Export to Azure ML via Synapse Link
   - Train forecasting models
   - Publish predictions back to Honua

**Benefits:**
- Standardized API for researchers (SensorThings)
- Integration with EPA reporting systems
- Spatial queries: "Stations within 10km of school district"

### Use Case 4: Digital Twin of Entire City

**Scenario**: Medium-sized city (population 500K) creating comprehensive digital twin

**Requirements:**
- 3D city model (buildings, terrain, infrastructure)
- Integration with GIS, BIM, CAD systems
- Real-time data from utilities, transit, traffic
- Scenario modeling and simulation
- Public engagement platform

**Architecture:**

```
┌─────────────────────────────────────────────────────────┐
│              Data Sources                               │
├─────────────────────────────────────────────────────────┤
│ GIS (Honua) · BIM (Revit) · CAD · IoT · Utilities      │
└────────┬────────────────────────────────────────────────┘
         │
    ┌────▼─────────────┐
    │  Data Integration │
    │  (Azure Functions)│
    └────┬─────────────┘
         │
         ├─────────────────┬──────────────────────┐
         ▼                 ▼                      ▼
┌─────────────────┐ ┌──────────────────┐ ┌─────────────┐
│ Azure Digital   │ │  Honua.Server    │ │ Azure Maps  │
│ Twins (Semantic)│ │  (Spatial/OGC)   │ │ (Visual)    │
└────────┬────────┘ └────────┬─────────┘ └──────┬──────┘
         │                   │                   │
         └───────────────────┴───────────────────┘
                            │
              ┌─────────────┴────────────┐
              ▼                          ▼
    ┌──────────────────┐      ┌──────────────────┐
    │  Web Portal      │      │  Unity/Unreal    │
    │  (Blazor)        │      │  (3D Viewer)     │
    └──────────────────┘      └──────────────────┘
```

**Implementation:**

1. **Honua: Spatial Foundation**
   - Store all city geodata (parcels, buildings, infrastructure)
   - 3D geometries with elevation
   - WFS for editing workflows
   - OGC 3D Tiles for visualization

2. **Azure Digital Twins: Semantic Layer**
   - Model relationships (Building → Owner, Parcel → ZoningCode)
   - Store non-spatial attributes
   - Query complex relationships

3. **Azure Maps: Public Facing**
   - Indoor maps for city buildings
   - Custom tilesets from Honua
   - Mobile-friendly wayfinding

4. **Integration Points:**
   ```csharp
   // Federated query: Join spatial + semantic
   var query = @"
       SELECT
           t.buildingId,
           t.owner,
           t.yearBuilt,
           h.geometry,
           h.elevation
       FROM digitaltwins t
       JOIN honua.buildings h ON t.buildingId = h.id
       WHERE
           t.yearBuilt < 1950 AND
           ST_Within(h.geometry, @historic_district)
   ";
   ```

**Benefits:**
- Single source of truth for city data
- Scenario planning: "What if we add light rail?"
- Public transparency: Open data portal via OGC APIs
- Emergency response: Query critical infrastructure

---

## Next Steps

### For Honua.Server Development Team

1. **Prioritize Integration Patterns**
   - Start with IoT Hub → SensorThings (Phase 2)
   - High value, low complexity

2. **Create Azure SDK Package**
   ```
   Honua.Integration.Azure/
   ├─ DigitalTwins/
   │  ├─ AdtSyncService.cs
   │  └─ DtdlConverter.cs
   ├─ IoTHub/
   │  ├─ TelemetryIngestion.cs
   │  └─ DeviceProvisioning.cs
   ├─ Maps/
   │  ├─ TilesetPublisher.cs
   │  └─ IndoorMapsIntegration.cs
   └─ EventGrid/
      ├─ EventPublisher.cs
      └─ EventSubscriber.cs
   ```

3. **Documentation**
   - Create Azure deployment guide
   - Document DTDL mapping conventions
   - Provide Terraform templates

### For Pilot Customers

1. **Evaluate Fit**
   - Do you need semantic modeling (ADT)?
   - Or just spatial + IoT (Honua alone)?

2. **Start Small**
   - Pilot with single building or neighborhood
   - Validate integration patterns
   - Measure performance and cost

3. **Measure Success**
   - Query latency (target: <100ms P95)
   - Sync lag (target: <5 seconds)
   - Cost per 1M events (target: <$50)

### Cost Estimates

**Small Deployment (10 buildings, 500 sensors):**
- Azure Digital Twins: $200/month
- IoT Hub: $25/month (Basic tier)
- Event Hub: $20/month
- Azure Functions: $10/month
- **Total Azure**: ~$255/month

**Medium Deployment (100 buildings, 5,000 sensors):**
- Azure Digital Twins: $2,000/month
- IoT Hub: $250/month (Standard tier)
- Event Hub: $100/month
- Azure Functions: $50/month
- Azure Data Explorer: $500/month
- **Total Azure**: ~$2,900/month

**Note**: These are Azure costs only. Add Honua.Server licensing separately.

---

## References

### Microsoft Documentation

- [Azure Digital Twins Overview](https://learn.microsoft.com/en-us/azure/digital-twins/overview)
- [IoT Hub Documentation](https://learn.microsoft.com/en-us/azure/iot-hub/)
- [Azure Maps Documentation](https://learn.microsoft.com/en-us/azure/azure-maps/)
- [Event Grid Documentation](https://learn.microsoft.com/en-us/azure/event-grid/)
- [DTDL Specification](https://github.com/Azure/opendigitaltwins-dtdl)

### Open Source Resources

- [Azure Digital Twins Smart Cities Ontology](https://github.com/Azure/opendigitaltwins-smartcities)
- [Azure Digital Twins Samples](https://github.com/Azure-Samples/digital-twins-samples)
- [Azure IoT SDK for .NET](https://github.com/Azure/azure-iot-sdk-csharp)

### OGC Standards

- [OGC API - Features](https://ogcapi.ogc.org/features/)
- [OGC SensorThings API](https://www.ogc.org/standards/sensorthings)
- [OGC 3D Tiles](https://www.ogc.org/standards/3DTiles)

---

**Document Status**: Draft for review  
**Feedback**: Share with product team and pilot customers  
**Next Review**: After Phase 1 completion
