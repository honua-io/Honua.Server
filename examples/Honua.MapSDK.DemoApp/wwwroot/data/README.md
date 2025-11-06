# Sample Data for Honua MapSDK Demo App

This directory contains sample GeoJSON and data files used by the demo application.

## Required Data Files

### Property Data
- **parcels.geojson** - Property parcel polygons with attributes
  - Properties: id, address, landUse, assessedValue, sqft, zoneType, yearBuilt
  - ~1,500 features covering San Francisco

### Environmental Monitoring
- **sensors.geojson** - Air quality sensor locations
  - Properties: id, sensorType, location, temperature, airQuality, co2Level, timestamp
  - ~50 sensor points

### Vehicle Tracking
- **vehicles.geojson** - Fleet vehicle locations
  - Properties: id, vehicleId, status, speed, heading, driver, lastUpdate
  - ~30 vehicle points

### Emergency Response
- **incidents.geojson** - Emergency incident locations
  - Properties: id, incidentId, incidentType, priority, address, status, timeReported, timeDispatched, unitsAssigned, eta, description
  - ~50 incident points

- **fire-stations.geojson** - Fire station locations
  - Properties: id, name, address, unitsAvailable
  - ~15 station points

- **police-stations.geojson** - Police station locations
  - Properties: id, name, address, unitsAvailable
  - ~20 station points

- **hospitals.geojson** - Hospital locations
  - Properties: id, name, address, emergencyRoom, traumaLevel
  - ~12 hospital points

### Urban Planning
- **zones.geojson** - Zoning district polygons
  - Properties: id, zoneName, zoneType, area, maxHeight, far, status, description
  - ~200 zone polygons

- **buildings.geojson** - Building footprints
  - Properties: id, buildingName, height, floors, yearBuilt, use
  - ~5,000 building polygons

### Asset Management
- **assets.geojson** - Infrastructure asset locations
  - Properties: id, assetId, assetName, assetType, condition, installDate, lastMaintenance, nextMaintenance, replacementValue
  - ~3,847 asset points and lines

### City Planning Dashboard
- **city-zones.geojson** - City zoning data
  - Properties: id, zoneName, zoneType, area, maxHeight, far, status
  - ~300 polygons

### Large Datasets (for performance testing)
- **large-dataset.geojson** - Large dataset for AttributeTable demo
  - 10,000+ features with multiple attributes
  - Used to demonstrate virtualization and performance

## Data Format

All GeoJSON files should follow this structure:

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Point|Polygon|LineString",
        "coordinates": [...]
      },
      "properties": {
        "id": "unique-id",
        ...
      }
    }
  ]
}
```

## Generating Sample Data

Sample data can be generated using:
1. **Mockaroo** (https://mockaroo.com) - Generate random realistic data
2. **GeoJSON.io** (https://geojson.io) - Draw and edit GeoJSON features
3. **QGIS** - Export real data as GeoJSON
4. **Python/JavaScript scripts** - Generate programmatically

## Example Python Script to Generate Sample Data

```python
import json
import random
from datetime import datetime, timedelta

def generate_parcels(count=1500):
    features = []
    land_uses = ["Residential", "Commercial", "Industrial", "Mixed Use"]

    for i in range(count):
        feature = {
            "type": "Feature",
            "geometry": {
                "type": "Polygon",
                "coordinates": [[
                    [-122.5 + random.random() * 0.2, 37.7 + random.random() * 0.15],
                    [-122.5 + random.random() * 0.2, 37.7 + random.random() * 0.15],
                    [-122.5 + random.random() * 0.2, 37.7 + random.random() * 0.15],
                    [-122.5 + random.random() * 0.2, 37.7 + random.random() * 0.15],
                    [-122.5 + random.random() * 0.2, 37.7 + random.random() * 0.15]
                ]]
            },
            "properties": {
                "id": f"parcel-{i:04d}",
                "address": f"{random.randint(100, 9999)} Market St",
                "landUse": random.choice(land_uses),
                "assessedValue": random.randint(300000, 5000000),
                "sqft": random.randint(1000, 10000),
                "yearBuilt": random.randint(1900, 2024)
            }
        }
        features.append(feature)

    return {
        "type": "FeatureCollection",
        "features": features
    }

# Generate and save
data = generate_parcels()
with open('parcels.geojson', 'w') as f:
    json.dump(data, f, indent=2)
```

## Data Privacy & Licensing

- All sample data should be fictional or publicly available
- Do not include real personal information (PII)
- Respect data licensing requirements
- Attribute data sources appropriately

## Notes

- Coordinate system: WGS84 (EPSG:4326)
- Bounding box: San Francisco area (~-122.5, 37.7 to -122.35, 37.85)
- All timestamps in ISO 8601 format
- Monetary values in USD
- Distances in meters or feet (specified in properties)
