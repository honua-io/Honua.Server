# Smart City Digital Twin Example

This example demonstrates how to set up Azure Digital Twins integration for a smart city use case with traffic sensors, parking lots, and zones.

## Scenario

We have a smart city with:
- Traffic sensors that monitor vehicle flow
- Parking lots with occupancy sensors
- Traffic zones that contain sensors

## Step 1: Configure Layer Mappings

```json
{
  "AzureDigitalTwins": {
    "InstanceUrl": "https://smartcity-dt.api.wus2.digitaltwins.azure.net",
    "UseManagedIdentity": true,
    "DefaultNamespace": "dtmi:com:smartcity",
    "UseNgsiLdOntology": true,
    "Sync": {
      "Enabled": true,
      "Direction": "Bidirectional",
      "ConflictStrategy": "LastWriteWins",
      "EnableRealTimeSync": true,
      "BatchSyncIntervalMinutes": 15
    },
    "LayerMappings": [
      {
        "ServiceId": "smart-city",
        "LayerId": "traffic-sensors",
        "ModelId": "dtmi:com:smartcity:traffic:sensor;1",
        "TwinIdTemplate": "sensor-{featureId}",
        "PropertyMappings": {
          "sensor_id": "sensorId",
          "installation_date": "installationDate",
          "location_name": "locationName"
        },
        "Relationships": [
          {
            "ForeignKeyColumn": "zone_id",
            "RelationshipName": "locatedInZone",
            "TargetModelId": "dtmi:com:smartcity:traffic:zone;1",
            "TargetTwinIdTemplate": "zone-{targetFeatureId}"
          }
        ],
        "AutoGenerateModel": true
      },
      {
        "ServiceId": "smart-city",
        "LayerId": "parking-lots",
        "ModelId": "dtmi:com:smartcity:parking:lot;1",
        "TwinIdTemplate": "parking-{featureId}",
        "PropertyMappings": {
          "lot_name": "name",
          "total_spaces": "capacity",
          "occupied_spaces": "occupancy"
        },
        "AutoGenerateModel": true
      },
      {
        "ServiceId": "smart-city",
        "LayerId": "traffic-zones",
        "ModelId": "dtmi:com:smartcity:traffic:zone;1",
        "TwinIdTemplate": "zone-{featureId}",
        "PropertyMappings": {
          "zone_name": "name",
          "speed_limit": "speedLimit"
        },
        "AutoGenerateModel": true
      }
    ]
  }
}
```

## Step 2: Create Honua Layers

### Traffic Sensors Layer Schema

```sql
CREATE TABLE traffic_sensors (
    id SERIAL PRIMARY KEY,
    sensor_id VARCHAR(50) UNIQUE NOT NULL,
    location_name VARCHAR(255),
    installation_date DATE,
    zone_id INTEGER REFERENCES traffic_zones(id),
    vehicle_count INTEGER DEFAULT 0,
    average_speed DOUBLE PRECISION,
    temperature DOUBLE PRECISION,
    is_active BOOLEAN DEFAULT true,
    last_reading TIMESTAMP,
    geometry GEOMETRY(Point, 4326)
);
```

Honua metadata:

```json
{
  "title": "Traffic Sensors",
  "description": "IoT sensors monitoring traffic flow in real-time",
  "properties": {
    "sensor_id": {
      "type": "string",
      "title": "Sensor ID",
      "description": "Unique sensor identifier"
    },
    "location_name": {
      "type": "string",
      "title": "Location",
      "description": "Human-readable location name"
    },
    "installation_date": {
      "type": "date",
      "title": "Installation Date"
    },
    "vehicle_count": {
      "type": "integer",
      "title": "Vehicle Count",
      "description": "Vehicles detected in current interval"
    },
    "average_speed": {
      "type": "double",
      "title": "Average Speed (km/h)"
    },
    "temperature": {
      "type": "double",
      "title": "Temperature (°C)"
    },
    "is_active": {
      "type": "boolean",
      "title": "Active Status"
    },
    "last_reading": {
      "type": "datetime",
      "title": "Last Reading Time"
    }
  }
}
```

### Parking Lots Layer Schema

```sql
CREATE TABLE parking_lots (
    id SERIAL PRIMARY KEY,
    lot_name VARCHAR(255) NOT NULL,
    total_spaces INTEGER NOT NULL,
    occupied_spaces INTEGER DEFAULT 0,
    hourly_rate DOUBLE PRECISION,
    is_open BOOLEAN DEFAULT true,
    geometry GEOMETRY(Polygon, 4326)
);
```

### Traffic Zones Layer Schema

```sql
CREATE TABLE traffic_zones (
    id SERIAL PRIMARY KEY,
    zone_name VARCHAR(255) NOT NULL,
    speed_limit INTEGER,
    zone_type VARCHAR(50),
    geometry GEOMETRY(Polygon, 4326)
);
```

## Step 3: Generate and Upload DTDL Models

Use the API to generate models from layer schemas:

```bash
# Generate Traffic Sensor Model
curl -X POST https://your-honua-server/api/azure/digital-twins/models/from-layer/smart-city/traffic-sensors \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Traffic Sensors",
    "properties": {
      "sensor_id": {"type": "string"},
      "location_name": {"type": "string"},
      "vehicle_count": {"type": "integer"},
      "average_speed": {"type": "double"},
      "temperature": {"type": "double"},
      "is_active": {"type": "boolean"},
      "last_reading": {"type": "datetime"}
    }
  }'

# Generate Parking Lot Model
curl -X POST https://your-honua-server/api/azure/digital-twins/models/from-layer/smart-city/parking-lots \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Parking Lots",
    "properties": {
      "lot_name": {"type": "string"},
      "total_spaces": {"type": "integer"},
      "occupied_spaces": {"type": "integer"},
      "hourly_rate": {"type": "double"},
      "is_open": {"type": "boolean"}
    }
  }'

# Generate Traffic Zone Model
curl -X POST https://your-honua-server/api/azure/digital-twins/models/from-layer/smart-city/traffic-zones \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Traffic Zones",
    "properties": {
      "zone_name": {"type": "string"},
      "speed_limit": {"type": "integer"},
      "zone_type": {"type": "string"}
    }
  }'
```

## Step 4: Perform Initial Batch Sync

```bash
# Sync all traffic zones first (referenced by sensors)
curl -X POST https://your-honua-server/api/azure/digital-twins/sync/layer/smart-city/traffic-zones

# Then sync traffic sensors
curl -X POST https://your-honua-server/api/azure/digital-twins/sync/layer/smart-city/traffic-sensors

# Finally sync parking lots
curl -X POST https://your-honua-server/api/azure/digital-twins/sync/layer/smart-city/parking-lots
```

## Step 5: Query Digital Twins

### Find all active sensors in a zone

```bash
curl -X POST https://your-honua-server/api/azure/digital-twins/twins/query \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT sensor FROM DIGITALTWINS sensor JOIN zone RELATED sensor.locatedInZone WHERE zone.$dtId = '\''zone-downtown'\'' AND sensor.is_active = true",
    "maxResults": 100
  }'
```

### Find parking lots with high occupancy

```bash
curl -X POST https://your-honua-server/api/azure/digital-twins/twins/query \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('\''dtmi:com:smartcity:parking:lot;1'\'') AND (occupied_spaces * 1.0 / total_spaces) > 0.9",
    "maxResults": 50
  }'
```

### Get sensor with relationships

```bash
curl https://your-honua-server/api/azure/digital-twins/twins/sensor-TRAFFIC001
```

## Step 6: Real-Time Updates

When a sensor reading changes in Honua:

```bash
curl -X POST https://your-honua-server/api/azure/digital-twins/sync/feature/smart-city/traffic-sensors/TRAFFIC001 \
  -H "Content-Type: application/json" \
  -d '{
    "sensor_id": "TRAFFIC001",
    "vehicle_count": 87,
    "average_speed": 45.2,
    "temperature": 22.5,
    "is_active": true,
    "last_reading": "2025-11-10T14:30:00Z"
  }'
```

## Step 7: Visualize in Azure Digital Twins Explorer

1. Open Azure Digital Twins Explorer
2. Navigate to your instance
3. View the twin graph:
   - Traffic zones (polygons)
   - Traffic sensors (points) with `locatedInZone` relationships
   - Parking lots (polygons)
4. Query twins using ADT query language
5. Update twins and see changes sync back to Honua

## Step 8: Set Up Event-Driven Bidirectional Sync

Configure Event Grid webhook to receive ADT changes:

```bash
# Create Event Grid subscription
az eventgrid event-subscription create \
  --name honua-adt-sync \
  --source-resource-id /subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.DigitalTwins/digitalTwinsInstances/smartcity-dt \
  --endpoint https://your-honua-server/webhooks/adt-events \
  --included-event-types Microsoft.DigitalTwins.Twin.Update Microsoft.DigitalTwins.Twin.Create Microsoft.DigitalTwins.Twin.Delete
```

## Example Queries for Smart City Analytics

### Traffic Congestion Analysis

```sql
SELECT sensor, zone
FROM DIGITALTWINS sensor
JOIN zone RELATED sensor.locatedInZone
WHERE sensor.average_speed < 20
  AND sensor.vehicle_count > 50
```

### Parking Availability Dashboard

```sql
SELECT lot,
       lot.occupied_spaces,
       lot.total_spaces,
       (lot.total_spaces - lot.occupied_spaces) as available_spaces,
       (lot.occupied_spaces * 100.0 / lot.total_spaces) as occupancy_percent
FROM DIGITALTWINS lot
WHERE IS_OF_MODEL('dtmi:com:smartcity:parking:lot;1')
  AND lot.is_open = true
ORDER BY occupancy_percent DESC
```

### Zone Performance Metrics

```sql
SELECT zone,
       AVG(sensor.average_speed) as avg_speed,
       SUM(sensor.vehicle_count) as total_vehicles,
       COUNT(sensor) as sensor_count
FROM DIGITALTWINS zone
JOIN sensor RELATED zone.locatedInZone-1
WHERE IS_OF_MODEL(zone, 'dtmi:com:smartcity:traffic:zone;1')
GROUP BY zone
```

## Integration with Azure Services

### Stream Analytics for Real-Time Processing

```sql
-- Stream Analytics Query
SELECT
    sensor.sensorId,
    sensor.vehicle_count,
    sensor.average_speed,
    System.Timestamp() AS EventTime
INTO
    [AlertsOutput]
FROM
    [ADTTelemetry] sensor
WHERE
    sensor.vehicle_count > 100
    AND sensor.average_speed < 15
```

### Power BI Dashboard

Connect Power BI to Azure Digital Twins using the REST API or Azure Synapse Analytics for real-time dashboards.

### Azure Maps Integration

Display sensor locations and zones on Azure Maps with real-time data from ADT.

## Monitoring and Alerts

Set up alerts for:
- High traffic congestion (low speed + high volume)
- Sensor offline (is_active = false for > 5 minutes)
- Parking lot full (occupancy > 95%)
- Temperature anomalies

## Cost Optimization

- Use batch sync during off-peak hours
- Implement delta sync (only changed features)
- Cache frequently queried twins
- Use ADT's built-in caching for query results
