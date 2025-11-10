# 3D Features Support in Honua.Server

## Overview

Honua.Server now supports 3D GeoJSON output with elevation (Z) coordinates, enabling 3D building visualization, terrain context, and underground utility mapping for smart cities applications.

## Features

- **3D Coordinates**: Returns GeoJSON with `[lon, lat, z]` coordinates instead of `[lon, lat]`
- **Building Heights**: Supports building extrusion with base elevation + height
- **Flexible Elevation Sources**: Pluggable elevation providers (database columns, external APIs, etc.)
- **Backward Compatible**: 3D output is optional and controlled by query parameter

## Quick Start

### Enable 3D Output

Add the `include3D=true` query parameter to any OGC API Features request:

```
GET /ogc/collections/buildings/items?include3D=true
```

### Response Example

**2D Response (default)**:
```json
{
  "type": "Feature",
  "id": 1,
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "name": "Transamerica Pyramid"
  }
}
```

**3D Response (with `include3D=true`)**:
```json
{
  "type": "Feature",
  "id": 1,
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749, 260.0]
  },
  "properties": {
    "name": "Transamerica Pyramid",
    "height": 85.0
  }
}
```

## Configuration

### Layer-Level Elevation Configuration

Add elevation configuration to your layer metadata using the `extensions` property:

```yaml
layers:
  - id: buildings
    title: "Buildings"
    type: table
    source: database1
    schema: public
    table: buildings
    extensions:
      elevation:
        source: "attribute"           # Required: "attribute" or "external"
        elevationAttribute: "base_elevation"  # Column containing elevation
        heightAttribute: "building_height"    # Column containing building height
        defaultElevation: 0.0         # Default if no elevation data
        verticalOffset: 0.0           # Offset to apply to all elevations
        includeHeight: true           # Include height for 3D extrusion
```

### Configuration Options

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `source` | string | Yes | Elevation source: `"attribute"` for database columns, `"external"` for future API support |
| `elevationAttribute` | string | Conditional | Name of database column containing elevation (required for `"attribute"` source) |
| `heightAttribute` | string | No | Name of database column containing building height for extrusion |
| `defaultElevation` | number | No | Default elevation in meters if no data available (default: 0) |
| `verticalOffset` | number | No | Vertical offset in meters to add to all elevations (default: 0) |
| `includeHeight` | boolean | No | Whether to include height property for 3D extrusion (default: false) |

## Elevation Sources

### Attribute-Based (Database Column)

The most common source. Reads elevation from a database column:

```yaml
extensions:
  elevation:
    source: "attribute"
    elevationAttribute: "elevation"
```

Supports common column names out-of-the-box (no configuration needed):
- `elevation`
- `elev`
- `height`
- `z`
- `altitude`

### Default Elevation

If no configuration is provided, features default to elevation 0.0. You can override this:

```yaml
extensions:
  elevation:
    defaultElevation: 10.0  # All features at 10m elevation
```

### Future: External APIs

Support for external elevation APIs (USGS, SRTM, etc.) is planned:

```yaml
extensions:
  elevation:
    source: "external"
    externalServiceUrl: "https://api.usgs.gov/elevation"
```

## Building Extrusion

For 3D building visualization, configure both base elevation and height:

```yaml
extensions:
  elevation:
    source: "attribute"
    elevationAttribute: "ground_elevation"
    heightAttribute: "building_height"
    includeHeight: true
```

This will:
1. Set geometry coordinates to `[lon, lat, ground_elevation]`
2. Add `height` property to feature properties
3. Enable clients (like deck.gl) to extrude buildings in 3D

## Supported Geometry Types

All GeoJSON geometry types support 3D coordinates:

- **Point**: Single `[lon, lat, z]` coordinate
- **LineString**: Array of `[lon, lat, z]` coordinates
- **Polygon**: Rings of `[lon, lat, z]` coordinates
- **MultiPoint**: Multiple `[lon, lat, z]` coordinates
- **MultiLineString**: Multiple LineStrings with Z
- **MultiPolygon**: Multiple Polygons with Z

## Client Integration

### deck.gl Example

```javascript
import {GeoJsonLayer} from '@deck.gl/layers';

const layer = new GeoJsonLayer({
  id: 'buildings-3d',
  data: '/ogc/collections/buildings/items?include3D=true',
  extruded: true,
  wireframe: true,
  getElevation: f => f.geometry.coordinates[2] || 0,
  getFillColor: [160, 160, 180, 200],
  getLineColor: [255, 255, 255],
  pickable: true
});
```

With building heights for extrusion:

```javascript
const layer = new GeoJsonLayer({
  id: 'buildings-3d',
  data: '/ogc/collections/buildings/items?include3D=true',
  extruded: true,
  getElevation: f => f.properties.height || 10,  // Extrude by height
  elevationScale: 1,
  getFillColor: [160, 160, 180, 200]
});
```

## OGC API Compliance

The 3D features implementation follows OGC standards:

- **Coordinate Reference Systems**: Elevations are in meters above mean sea level
- **GeoJSON Specification**: Compatible with GeoJSON specification for 3D coordinates
- **Backward Compatibility**: 3D is an optional extension, default behavior unchanged

## Performance Considerations

### Database-Based Elevation

- **Attribute source**: Fast, reads directly from database columns
- **No additional queries**: Elevation data fetched with feature data
- **Indexed columns**: Ensure elevation columns are indexed if used in filters

### Large Datasets

- Use pagination (`limit` parameter) for large 3D datasets
- Consider tile-based rendering for city-scale visualizations
- Enable server-side caching for frequently accessed collections

## Examples

### Basic 3D Points

```bash
curl "http://localhost:5000/ogc/collections/poi/items?include3D=true&limit=10"
```

### 3D Buildings with Heights

```bash
curl "http://localhost:5000/ogc/collections/buildings/items?include3D=true&bbox=-122.5,37.7,-122.3,37.8"
```

### Underground Utilities

```bash
curl "http://localhost:5000/ogc/collections/utilities/items?include3D=true&filter=utility_type='sewer'"
```

### Terrain Contours

```bash
curl "http://localhost:5000/ogc/collections/contours/items?include3D=true"
```

## Testing

Comprehensive tests are included in:
- `tests/Honua.Server.Core.Tests/Elevation/AttributeElevationServiceTests.cs`
- `tests/Honua.Server.Core.Tests/Elevation/GeoJsonElevationEnricherTests.cs`

Run tests:
```bash
dotnet test --filter "Category=Elevation"
```

## Troubleshooting

### Elevations Always Zero

- Check that `include3D=true` is in the URL
- Verify elevation configuration in layer metadata
- Ensure database column names match configuration

### Missing Height Property

- Set `includeHeight: true` in elevation configuration
- Verify `heightAttribute` points to correct column
- Check that column contains numeric values

### Performance Issues

- Index elevation and height columns in database
- Use spatial filtering (`bbox`) to reduce dataset size
- Enable response caching for static datasets

## Future Enhancements

- [ ] External elevation API support (USGS, SRTM)
- [ ] Vertical CRS transformation
- [ ] 3D spatial queries (volume intersection)
- [ ] Terrain-following line strings
- [ ] 3D distance calculations

## API Reference

### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `include3D` | boolean | false | Include Z coordinates in geometry output |

### Elevation Service Interface

```csharp
public interface IElevationService
{
    Task<double?> GetElevationAsync(
        double longitude,
        double latitude,
        ElevationContext context,
        CancellationToken cancellationToken = default);

    Task<double?[]> GetElevationsAsync(
        IReadOnlyList<(double Longitude, double Latitude)> coordinates,
        ElevationContext context,
        CancellationToken cancellationToken = default);

    bool CanProvideElevation(ElevationContext context);
}
```

## Contributing

To add a custom elevation provider:

1. Implement `IElevationService` interface
2. Register in DI container using `AddElevationProvider<T>()`
3. Add provider to composite service in priority order

Example:

```csharp
services.AddElevationProvider<MyCustomElevationService>();
```

## References

- [GeoJSON Specification - Position](https://datatracker.ietf.org/doc/html/rfc7946#section-3.1.1)
- [OGC API - Features](https://ogcapi.ogc.org/features/)
- [deck.gl 3D Visualization](https://deck.gl/docs/get-started/using-3d-positions)
