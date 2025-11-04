# Temporal Raster Support

Honua Server provides comprehensive support for time series rasters across all major OGC protocols and APIs. This enables serving dynamic datasets like weather forecasts, satellite imagery time series, climate data, and environmental monitoring.

## Overview

Temporal rasters allow serving the same geographic area across different time dimensions. The server supports:

- **Fixed discrete timestamps** - Specific acquisition dates/times
- **Continuous time ranges** - Min/max bounds with optional intervals
- **Default time values** - Fallback when no TIME parameter provided
- **ISO 8601 format** - Standard datetime representation

## Configuration

Add temporal metadata to your raster dataset configuration:

```json
{
  "rasters": [
    {
      "id": "sea_surface_temperature",
      "title": "Sea Surface Temperature",
      "source": {
        "type": "file",
        "uri": "/data/sst/{time}.tif"
      },
      "temporal": {
        "enabled": true,
        "defaultValue": "2024-01-15T00:00:00Z",
        "minValue": "2024-01-01T00:00:00Z",
        "maxValue": "2024-12-31T23:59:59Z",
        "period": "P1D"
      }
    }
  ]
}
```

### Temporal Configuration Options

| Property | Type | Description |
|----------|------|-------------|
| `enabled` | boolean | Enable temporal dimension (default: false) |
| `defaultValue` | string | ISO 8601 timestamp to use when TIME not specified |
| `fixedValues` | string[] | List of discrete timestamps (mutually exclusive with min/max) |
| `minValue` | string | Earliest timestamp in the dataset |
| `maxValue` | string | Latest timestamp in the dataset |
| `period` | string | ISO 8601 duration between timesteps (e.g., "P1D" = 1 day) |

### Configuration Patterns

**Pattern 1: Discrete Timestamps**
```json
{
  "temporal": {
    "enabled": true,
    "defaultValue": "2024-01-15T00:00:00Z",
    "fixedValues": [
      "2024-01-01T00:00:00Z",
      "2024-01-15T00:00:00Z",
      "2024-02-01T00:00:00Z",
      "2024-02-15T00:00:00Z"
    ]
  }
}
```

**Pattern 2: Continuous Range with Interval**
```json
{
  "temporal": {
    "enabled": true,
    "defaultValue": "2024-06-15T12:00:00Z",
    "minValue": "2024-01-01T00:00:00Z",
    "maxValue": "2024-12-31T23:59:59Z",
    "period": "PT6H"
  }
}
```

**Pattern 3: Open-Ended (Current/Latest)**
```json
{
  "temporal": {
    "enabled": true,
    "defaultValue": "current",
    "minValue": "2020-01-01T00:00:00Z"
  }
}
```

## Protocol Support

### WMS 1.3.0 - TIME Parameter

The TIME parameter follows WMS 1.3.0 specification.

**GetCapabilities Advertises Temporal Dimension:**
```xml
<Layer>
  <Name>sea_surface_temperature</Name>
  <Title>Sea Surface Temperature</Title>
  <Dimension name="time" units="ISO8601" default="2024-01-15T00:00:00Z">
    2024-01-01T00:00:00Z/2024-12-31T23:59:59Z/P1D
  </Dimension>
</Layer>
```

**GetMap Request with TIME:**
```
GET /wms?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap
  &LAYERS=sea_surface_temperature
  &TIME=2024-06-15T00:00:00Z
  &CRS=EPSG:4326&BBOX=-180,-90,180,90
  &WIDTH=800&HEIGHT=400&FORMAT=image/png
```

**TIME Parameter Formats:**
- Specific instant: `TIME=2024-01-15T00:00:00Z`
- Date only: `TIME=2024-01-15`
- Range: `TIME=2024-01-01/2024-01-31`
- Multiple: `TIME=2024-01-01,2024-01-15,2024-02-01`

### WMTS 1.0.0 - Temporal Dimension

WMTS advertises the temporal dimension in GetCapabilities and includes TIME in tile URLs.

**GetCapabilities Dimension:**
```xml
<Layer>
  <ows:Identifier>sea_surface_temperature</ows:Identifier>
  <Dimension>
    <ows:Identifier>Time</ows:Identifier>
    <Default>2024-01-15T00:00:00Z</Default>
    <Value>2024-01-01T00:00:00Z</Value>
    <Value>2024-12-31T23:59:59Z</Value>
    <UOM>ISO8601</UOM>
  </Dimension>
  <ResourceURL format="image/png" resourceType="tile"
    template="/wmts?layer=sst&time={Time}&TileMatrix={TileMatrix}..." />
</Layer>
```

**GetTile Request with TIME:**
```
GET /wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0
  &LAYER=sea_surface_temperature
  &TIME=2024-06-15T00:00:00Z
  &TILEMATRIXSET=WorldWebMercatorQuad
  &TILEMATRIX=8&TILEROW=123&TILECOL=456
  &FORMAT=image/png
```

### WCS 2.0.1 - Temporal Subsetting

WCS uses the `subset` parameter with time dimension.

**DescribeCoverage Temporal Domain:**
```xml
<CoverageDescription>
  <CoverageId>sea_surface_temperature</CoverageId>
  <DomainSet>
    <TimePeriod gml:id="time-period">
      <beginPosition>2024-01-01T00:00:00Z</beginPosition>
      <endPosition>2024-12-31T23:59:59Z</endPosition>
    </TimePeriod>
  </DomainSet>
</CoverageDescription>
```

**GetCoverage Request with Temporal Subset:**
```
GET /wcs?SERVICE=WCS&VERSION=2.0.1&REQUEST=GetCoverage
  &COVERAGEID=sea_surface_temperature
  &SUBSET=time("2024-06-15T00:00:00Z")
  &FORMAT=image/tiff
```

**Subset Syntax:**
- Single instant: `subset=time("2024-01-15T00:00:00Z")`
- Range: `subset=time("2024-01-01","2024-01-31")`

### OGC API - Tiles - datetime Parameter

OGC API uses the `datetime` query parameter (RFC 3339).

**Request Tile with datetime:**
```
GET /ogc/collections/sea_surface_temperature/
    tiles/WorldWebMercatorQuad/8/123/456?datetime=2024-06-15T00:00:00Z
```

**datetime Parameter Formats:**
- Instant: `datetime=2024-01-15T00:00:00Z`
- Range: `datetime=2024-01-01T00:00:00Z/2024-01-31T23:59:59Z`
- Open start: `datetime=../2024-01-31T23:59:59Z`
- Open end: `datetime=2024-01-01T00:00:00Z/..`

### STAC - Temporal Extent

STAC collections advertise temporal extent in collection metadata.

**Collection Temporal Extent:**
```json
{
  "extent": {
    "temporal": {
      "interval": [
        ["2024-01-01T00:00:00Z", "2024-12-31T23:59:59Z"]
      ]
    }
  }
}
```

## File Organization Strategies

### Strategy 1: Templated File Paths

Use placeholders in the source URI:

```json
{
  "source": {
    "type": "file",
    "uri": "/data/sst/{time}.tif"
  },
  "temporal": {
    "enabled": true,
    "defaultValue": "2024-01-15"
  }
}
```

The `{time}` placeholder is replaced with the requested timestamp (formatted as needed).

### Strategy 2: Subdirectories by Date

```
/data/sst/
  2024-01-01/sst.tif
  2024-01-02/sst.tif
  2024-01-03/sst.tif
```

```json
{
  "source": {
    "type": "file",
    "uri": "/data/sst/{time:yyyy-MM-dd}/sst.tif"
  }
}
```

### Strategy 3: Cloud Object Storage

For S3/Azure Blob/GCS:

```json
{
  "source": {
    "type": "cloud",
    "uri": "s3://my-bucket/sst/{time:yyyy/MM/dd}/sst.tif",
    "credentialsId": "aws-creds"
  }
}
```

### Strategy 4: Single Multidimensional File

For NetCDF or HDF5 with time dimension:

```json
{
  "source": {
    "type": "file",
    "uri": "/data/sst_timeseries.nc",
    "subDataset": "NETCDF:\"/data/sst_timeseries.nc\":sea_surface_temp"
  },
  "temporal": {
    "enabled": true,
    "minValue": "2024-01-01",
    "maxValue": "2024-12-31",
    "period": "P1D"
  }
}
```

## Use Cases

### Weather Forecasting

```json
{
  "id": "temperature_forecast",
  "title": "Temperature Forecast",
  "source": {
    "uri": "/data/forecast/temp_{time}.tif"
  },
  "temporal": {
    "enabled": true,
    "defaultValue": "current",
    "period": "PT3H",
    "minValue": "2024-01-01T00:00:00Z"
  }
}
```

### Satellite Imagery Time Series

```json
{
  "id": "landsat_ndvi",
  "title": "Landsat NDVI",
  "source": {
    "uri": "s3://landsat-data/{time:yyyy/MM/dd}/ndvi.tif"
  },
  "temporal": {
    "enabled": true,
    "fixedValues": [
      "2024-01-10",
      "2024-01-26",
      "2024-02-11",
      "2024-02-27"
    ],
    "defaultValue": "2024-02-27"
  }
}
```

### Climate Data

```json
{
  "id": "sea_ice_concentration",
  "title": "Sea Ice Concentration",
  "source": {
    "type": "cloud",
    "uri": "https://data.server.com/seaice/{time}.tif"
  },
  "temporal": {
    "enabled": true,
    "minValue": "1979-01-01",
    "maxValue": "2024-12-31",
    "period": "P1M",
    "defaultValue": "2024-01-01"
  }
}
```

## Validation Rules

The server validates TIME/datetime parameters against the temporal configuration:

1. **Fixed Values** - Must exactly match one of the listed timestamps
2. **Range** - Must be between `minValue` and `maxValue` (inclusive)
3. **Default Fallback** - If TIME not provided, uses `defaultValue`
4. **ISO 8601 Required** - All timestamps must be valid ISO 8601 format

**Example Validation Error:**
```
InvalidOperationException: TIME value '2024-06-15' is outside the valid range: 2024-01-01 to 2024-05-31
```

## Performance Considerations

### Caching

Temporal rasters can be cached per timestamp:

```json
{
  "cache": {
    "enabled": true,
    "preseed": false,
    "zoomLevels": [0, 1, 2, 3, 4, 5, 6]
  }
}
```

Cache keys include the TIME parameter, so different timestamps are cached separately.

### Pre-seeding

For frequently accessed timestamps, consider pre-seeding the cache:

```bash
# Warm up cache for common time values
for date in 2024-01-{01..31}; do
  curl "/wms?TIME=$date&..." > /dev/null
done
```

### Storage Optimization

- **Compression** - Use LZW or DEFLATE compression in GeoTIFFs
- **Overviews** - Include pyramid levels for faster zoomed-out rendering
- **Cloud Optimized** - Use COG (Cloud Optimized GeoTIFF) format
- **Tiling** - Internal tiling improves partial read performance

## Testing

Test temporal parameters across protocols:

```bash
# WMS with TIME
curl "http://localhost:5000/wms?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=sst&TIME=2024-01-15&..."

# WMTS with TIME
curl "http://localhost:5000/wmts?SERVICE=WMTS&REQUEST=GetTile&LAYER=sst&TIME=2024-01-15&..."

# WCS with subset
curl "http://localhost:5000/wcs?SERVICE=WCS&REQUEST=GetCoverage&COVERAGEID=sst&SUBSET=time(\"2024-01-15\")&..."

# OGC API with datetime
curl "http://localhost:5000/ogc/collections/sst/tiles/WorldCrs84Quad/8/123/456?datetime=2024-01-15T00:00:00Z"
```

## Troubleshooting

### TIME parameter ignored

- Check `temporal.enabled` is `true`
- Verify TIME format matches ISO 8601
- Ensure TIME value is within allowed range/values

### Cache not working with temporal

- Cache keys include TIME, so each timestamp creates separate cache entries
- Monitor cache size for temporal datasets
- Consider limiting cache to specific zoom levels

### File not found errors

- Verify file path template matches actual file organization
- Check `{time}` placeholder replacement logic
- Ensure file permissions for all temporal files

## Next Steps

- [Raster Configuration](./RASTER_CONFIGURATION.md)
- [OGC Protocols](./OGC_PROTOCOLS.md)
- [Caching Strategy](./CACHING.md)
