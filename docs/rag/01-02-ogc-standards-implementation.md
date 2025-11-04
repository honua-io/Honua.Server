---
tags: [ogc, wfs, wms, wmts, wcs, csw, standards, protocols, xml, getcapabilities]
category: architecture
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# OGC Standards Implementation Complete Reference

Comprehensive guide to Honua's implementation of classic OGC web services: WFS, WMS, WMTS, WCS, and CSW.

## Table of Contents
- [Overview](#overview)
- [WFS 2.0 - Web Feature Service](#wfs-20---web-feature-service)
- [WMS 1.3.0 - Web Map Service](#wms-130---web-map-service)
- [WMTS 1.0 - Web Map Tile Service](#wmts-10---web-map-tile-service)
- [WCS 2.0.1 - Web Coverage Service](#wcs-201---web-coverage-service)
- [CSW 2.0.2 - Catalog Service for the Web](#csw-202---catalog-service-for-the-web)
- [Common Patterns](#common-patterns)
- [Error Handling](#error-handling)
- [Performance Optimization](#performance-optimization)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua implements all major OGC web service standards, providing compatibility with existing GIS clients and tools.

### Supported Standards

| Standard | Version | Purpose | Endpoint |
|----------|---------|---------|----------|
| WFS | 2.0.0 | Feature access | `/wfs` |
| WMS | 1.3.0 | Map rendering | `/wms` |
| WMTS | 1.0.0 | Tile service | `/wmts` |
| WCS | 2.0.1 | Coverage data | `/wcs` |
| CSW | 2.0.2 | Catalog search | `/csw` |

### Base URL Structure

```
http://localhost:5000/wfs    # Web Feature Service
http://localhost:5000/wms    # Web Map Service
http://localhost:5000/wmts   # Web Map Tile Service
http://localhost:5000/wcs    # Web Coverage Service
http://localhost:5000/csw    # Catalog Service
```

### Why Classic OGC Standards?

While Honua implements modern OGC API standards (Features, Tiles, etc.), classic OGC services remain critical for:
- **Legacy compatibility**: Desktop GIS tools (QGIS, ArcGIS)
- **Industry standards**: Required by government agencies
- **Interoperability**: Broad ecosystem support
- **Proven reliability**: Battle-tested protocols

## WFS 2.0 - Web Feature Service

Web Feature Service provides transactional access to geographic features.

### Conformance Classes

Honua implements these WFS 2.0 conformance classes:
- Simple WFS
- Transaction WFS
- Locking WFS
- Stored Queries

### WFS Operations

#### GetCapabilities

Get service metadata and available feature types.

**Request:**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=GetCapabilities"
```

**Response Structure:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<wfs:WFS_Capabilities version="2.0.0"
    xmlns:wfs="http://www.opengis.net/wfs/2.0"
    xmlns:ows="http://www.opengis.net/ows/1.1"
    xmlns:gml="http://www.opengis.net/gml/3.2">
  <ows:ServiceIdentification>
    <ows:Title>Honua WFS Service</ows:Title>
    <ows:ServiceType>WFS</ows:ServiceType>
    <ows:ServiceTypeVersion>2.0.0</ows:ServiceTypeVersion>
  </ows:ServiceIdentification>
  <ows:OperationsMetadata>
    <ows:Operation name="GetCapabilities"/>
    <ows:Operation name="DescribeFeatureType"/>
    <ows:Operation name="GetFeature"/>
    <ows:Operation name="Transaction"/>
    <ows:Operation name="LockFeature"/>
  </ows:OperationsMetadata>
  <wfs:FeatureTypeList>
    <wfs:FeatureType>
      <wfs:Name>countries</wfs:Name>
      <wfs:Title>World Countries</wfs:Title>
      <ows:WGS84BoundingBox>
        <ows:LowerCorner>-180 -90</ows:LowerCorner>
        <ows:UpperCorner>180 90</ows:UpperCorner>
      </ows:WGS84BoundingBox>
    </wfs:FeatureType>
  </wfs:FeatureTypeList>
</wfs:WFS_Capabilities>
```

#### DescribeFeatureType

Get schema definition for feature types.

**Request:**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=DescribeFeatureType&typeName=countries"
```

**Response:** XSD schema definition of feature attributes.

#### GetFeature

Query and retrieve features.

**Basic Request:**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&count=10"
```

**With Filter (CQL):**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&filter=population>10000000"
```

**Output Formats:**
- `application/gml+xml; version=3.2` (default)
- `application/geo+json` (GeoJSON)
- `text/csv` (CSV)
- `application/vnd.shp` (Shapefile)

**GeoJSON Output:**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&outputFormat=application/geo%2Bjson&count=2"
```

#### Transaction

Create, update, or delete features.

**Insert Feature (POST):**
```xml
<wfs:Transaction service="WFS" version="2.0.0"
    xmlns:wfs="http://www.opengis.net/wfs/2.0"
    xmlns:gml="http://www.opengis.net/gml/3.2">
  <wfs:Insert>
    <countries>
      <name>New Country</name>
      <population>1000000</population>
      <geometry>
        <gml:Point srsName="EPSG:4326">
          <gml:pos>10.0 20.0</gml:pos>
        </gml:Point>
      </geometry>
    </countries>
  </wfs:Insert>
</wfs:Transaction>
```

**Update Feature:**
```xml
<wfs:Transaction service="WFS" version="2.0.0"
    xmlns:wfs="http://www.opengis.net/wfs/2.0"
    xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="countries">
    <wfs:Property>
      <wfs:ValueReference>population</wfs:ValueReference>
      <wfs:Value>1500000</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>id</fes:ValueReference>
        <fes:Literal>123</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
```

**Delete Feature:**
```xml
<wfs:Transaction service="WFS" version="2.0.0"
    xmlns:wfs="http://www.opengis.net/wfs/2.0"
    xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Delete typeName="countries">
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>id</fes:ValueReference>
        <fes:Literal>123</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Delete>
</wfs:Transaction>
```

#### LockFeature

Lock features for editing.

**Request:**
```xml
<wfs:LockFeature service="WFS" version="2.0.0" expiry="5"
    xmlns:wfs="http://www.opengis.net/wfs/2.0"
    xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Query typeNames="countries">
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>id</fes:ValueReference>
        <fes:Literal>123</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Query>
</wfs:LockFeature>
```

Default lock duration: 5 minutes.

### WFS Examples in QGIS

**Add WFS Layer:**
1. Layer → Add Layer → Add WFS Layer
2. Create New Connection
3. URL: `http://localhost:5000/wfs`
4. WFS Version: 2.0.0
5. Connect and select layers

## WMS 1.3.0 - Web Map Service

Web Map Service provides rendered map images.

### WMS Operations

#### GetCapabilities

Get service metadata and available layers.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&request=GetCapabilities"
```

#### GetMap

Retrieve a rendered map image.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetMap&layers=countries&styles=&crs=EPSG:4326&bbox=-180,-90,180,90&width=800&height=400&format=image/png" \
  -o map.png
```

**Parameters:**
- `layers`: Comma-separated layer names
- `styles`: Style names (empty for default)
- `crs`: Coordinate reference system
- `bbox`: Bounding box (minx,miny,maxx,maxy)
- `width`: Image width in pixels
- `height`: Image height in pixels
- `format`: Output format

**Supported Formats:**
- `image/png` (default)
- `image/jpeg`
- `image/webp`
- `image/tiff`

**Multi-Layer Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetMap&layers=countries,cities&styles=,&crs=EPSG:3857&bbox=-20037508,-20037508,20037508,20037508&width=512&height=512&format=image/png" \
  -o multi-layer.png
```

#### GetFeatureInfo

Query map features at a point.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetFeatureInfo&layers=countries&query_layers=countries&crs=EPSG:4326&bbox=-180,-90,180,90&width=800&height=400&i=400&j=200&info_format=application/json"
```

**Parameters:**
- `query_layers`: Layers to query
- `i`, `j`: Pixel coordinates (x, y)
- `info_format`: Response format (JSON, XML, GML, HTML)

**Response:**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "properties": {
        "id": 1,
        "name": "United States",
        "population": 331000000
      },
      "geometry": {
        "type": "MultiPolygon",
        "coordinates": [...]
      }
    }
  ]
}
```

#### GetLegendGraphic

Get legend image for a layer.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetLegendGraphic&layer=countries&format=image/png" \
  -o legend.png
```

### WMS in Web Clients

**OpenLayers:**
```javascript
import TileLayer from 'ol/layer/Tile';
import TileWMS from 'ol/source/TileWMS';

const wmsLayer = new TileLayer({
  source: new TileWMS({
    url: 'http://localhost:5000/wms',
    params: {
      'LAYERS': 'countries',
      'TILED': true
    },
    serverType: 'geoserver'
  })
});
```

**Leaflet:**
```javascript
L.tileLayer.wms('http://localhost:5000/wms', {
  layers: 'countries',
  format: 'image/png',
  transparent: true,
  version: '1.3.0',
  crs: L.CRS.EPSG4326
}).addTo(map);
```

## WMTS 1.0 - Web Map Tile Service

Web Map Tile Service provides pre-rendered or cached map tiles.

### WMTS Conformance Classes

- Core
- GetCapabilities operation
- GetTile operation
- KVP (Key-Value-Pair) encoding

### WMTS Operations

#### GetCapabilities

**Request:**
```bash
curl "http://localhost:5000/wmts?service=WMTS&request=GetCapabilities"
```

**Response Includes:**
- Available layers
- Tile matrix sets
- Image formats
- Tile dimensions

#### GetTile

Retrieve a specific map tile.

**Request:**
```bash
curl "http://localhost:5000/wmts?service=WMTS&request=GetTile&layer=countries&style=default&tilematrixset=WebMercatorQuad&tilematrix=5&tilerow=12&tilecol=8&format=image/png" \
  -o tile.png
```

**Parameters:**
- `layer`: Layer name
- `style`: Style identifier
- `tilematrixset`: Tile matrix set (tiling scheme)
- `tilematrix`: Zoom level
- `tilerow`: Row index
- `tilecol`: Column index
- `format`: Image format

**Supported Tile Matrix Sets:**
- `WebMercatorQuad` (EPSG:3857)
- `WorldCRS84Quad` (EPSG:4326)
- Custom tile matrix sets

### WMTS in OpenLayers

```javascript
import WMTS from 'ol/source/WMTS';
import WMTSTileGrid from 'ol/tilegrid/WMTS';
import {get as getProjection} from 'ol/proj';

const projection = getProjection('EPSG:3857');
const projectionExtent = projection.getExtent();
const size = getExtent(projectionExtent)[2] / 256;
const resolutions = new Array(20);
const matrixIds = new Array(20);
for (let z = 0; z < 20; ++z) {
  resolutions[z] = size / Math.pow(2, z);
  matrixIds[z] = z;
}

const wmtsSource = new WMTS({
  url: 'http://localhost:5000/wmts',
  layer: 'countries',
  matrixSet: 'WebMercatorQuad',
  format: 'image/png',
  projection: projection,
  tileGrid: new WMTSTileGrid({
    origin: [-20037508.34, 20037508.34],
    resolutions: resolutions,
    matrixIds: matrixIds
  }),
  style: 'default'
});
```

## WCS 2.0.1 - Web Coverage Service

Web Coverage Service provides access to raw raster data (coverages).

### WCS Operations

#### GetCapabilities

**Request:**
```bash
curl "http://localhost:5000/wcs?service=WCS&request=GetCapabilities"
```

#### DescribeCoverage

Get detailed metadata about coverages.

**Request:**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=DescribeCoverage&coverageId=elevation"
```

**Response Includes:**
- Coverage extent (bounding box)
- Grid dimensions
- Supported CRS
- Band information
- Data types

#### GetCoverage

Download raster data.

**Request:**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&format=image/tiff&subset=Lat(-90,90)&subset=Long(-180,180)" \
  -o coverage.tif
```

**Subset by Bounding Box:**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&format=image/tiff&subset=Lat(40,50)&subset=Long(-120,-110)" \
  -o subset.tif
```

**Resample to Specific Size:**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&format=image/tiff&size=Lat(512)&size=Long(512)" \
  -o resampled.tif
```

**Supported Output Formats:**
- `image/tiff` (GeoTIFF)
- `image/png`
- `image/jpeg`
- `application/x-netcdf` (NetCDF)

## CSW 2.0.2 - Catalog Service for the Web

Catalog Service provides metadata discovery and search.

### CSW Operations

#### GetCapabilities

**Request:**
```bash
curl "http://localhost:5000/csw?service=CSW&request=GetCapabilities"
```

#### DescribeRecord

Get information about queryable metadata schemas.

**Request:**
```bash
curl "http://localhost:5000/csw?service=CSW&request=DescribeRecord"
```

#### GetRecords

Search catalog for records.

**GET Request:**
```bash
curl "http://localhost:5000/csw?service=CSW&version=2.0.2&request=GetRecords&resultType=results&elementSetName=full&maxRecords=10"
```

**POST Request (with filter):**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<csw:GetRecords service="CSW" version="2.0.2"
    xmlns:csw="http://www.opengis.net/cat/csw/2.0.2"
    xmlns:ogc="http://www.opengis.net/ogc">
  <csw:Query typeNames="csw:Record">
    <csw:ElementSetName>full</csw:ElementSetName>
    <csw:Constraint version="1.1.0">
      <ogc:Filter>
        <ogc:PropertyIsLike wildCard="%" singleChar="_" escapeChar="\">
          <ogc:PropertyName>dc:title</ogc:PropertyName>
          <ogc:Literal>%elevation%</ogc:Literal>
        </ogc:PropertyIsLike>
      </ogc:Filter>
    </csw:Constraint>
  </csw:Query>
</csw:GetRecords>
```

**Spatial Filter:**
```xml
<ogc:Filter>
  <ogc:BBOX>
    <ogc:PropertyName>ows:BoundingBox</ogc:PropertyName>
    <gml:Envelope>
      <gml:lowerCorner>-180 -90</gml:lowerCorner>
      <gml:upperCorner>180 90</gml:upperCorner>
    </gml:Envelope>
  </ogc:BBOX>
</ogc:Filter>
```

#### GetRecordById

Retrieve specific metadata record.

**Request:**
```bash
curl "http://localhost:5000/csw?service=CSW&version=2.0.2&request=GetRecordById&id=countries&elementSetName=full"
```

**Output Schemas:**
- Dublin Core (default)
- ISO 19115/19139
- FGDC

**ISO 19139 Format:**
```bash
curl "http://localhost:5000/csw?service=CSW&version=2.0.2&request=GetRecordById&id=countries&elementSetName=full&outputSchema=http://www.isotc211.org/2005/gmd"
```

## Common Patterns

### Authentication

All OGC services respect Honua's authentication configuration.

**With API Key:**
```bash
curl -H "X-API-Key: your-api-key" \
  "http://localhost:5000/wfs?service=WFS&request=GetCapabilities"
```

**With JWT Bearer Token:**
```bash
curl -H "Authorization: Bearer eyJhbGc..." \
  "http://localhost:5000/wms?service=WMS&request=GetCapabilities"
```

### CORS Configuration

Enable CORS in `metadata.yaml`:

```yaml
catalog:
  cors:
    allowedOrigins:
      - "https://your-app.com"
    allowCredentials: true
```

### Pagination

Large WFS responses are automatically paginated:

```bash
# First page
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&count=100"

# Second page
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&count=100&startIndex=100"
```

### Coordinate Reference Systems

**Supported CRS:**
- EPSG:4326 (WGS 84)
- EPSG:3857 (Web Mercator)
- EPSG:4269 (NAD83)
- Custom CRS via PROJ definitions

**Transform on Request:**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&srsName=EPSG:3857"
```

## Error Handling

### OGC Exception Reports

All services return standard OGC exception reports:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<ows:ExceptionReport version="2.0.0"
    xmlns:ows="http://www.opengis.net/ows/2.0">
  <ows:Exception exceptionCode="InvalidParameterValue" locator="service">
    <ows:ExceptionText>Parameter 'service' must be set to 'WFS'</ows:ExceptionText>
  </ows:Exception>
</ows:ExceptionReport>
```

**Common Exception Codes:**
- `MissingParameterValue`: Required parameter missing
- `InvalidParameterValue`: Parameter value invalid
- `OperationNotSupported`: Operation not implemented
- `NoApplicableCode`: Generic error

### HTTP Status Codes

| Status | Meaning |
|--------|---------|
| 200 | Success |
| 400 | Bad request (client error) |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Resource not found |
| 500 | Server error |

## Performance Optimization

### WMS/WMTS Caching

Enable tile caching in `appsettings.json`:

```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "provider": "FileSystem",
      "basePath": "./data/cache",
      "maxSizeMb": 10240
    }
  }
}
```

### WFS Response Size Limits

Configure in `metadata.yaml`:

```yaml
services:
  - id: wfs
    maxFeatures: 10000
    defaultPageSize: 100
```

### WCS Processing

For large coverages, use subsets:
```bash
# Instead of full coverage
curl "http://localhost:5000/wcs?...&subset=Lat(-90,90)&subset=Long(-180,180)"

# Use smaller subset
curl "http://localhost:5000/wcs?...&subset=Lat(40,45)&subset=Long(-120,-115)"
```

## Troubleshooting

### Issue: WFS Returns Empty Results

**Symptoms:** GetFeature returns no features.

**Solutions:**
1. Check bounding box: `bbox=-180,-90,180,90`
2. Verify feature type name is correct
3. Check filter syntax
4. Verify data exists in database

```bash
# Debug: Get feature count
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&resultType=hits"
```

### Issue: WMS Returns Blank Image

**Symptoms:** GetMap returns transparent/blank image.

**Solutions:**
1. Verify layer name matches capabilities
2. Check bbox matches data extent
3. Verify CRS is supported
4. Check style exists

```bash
# Get layer extent from capabilities
curl "http://localhost:5000/wms?service=WMS&request=GetCapabilities" | grep -A 10 "countries"
```

### Issue: WMTS Tiles Not Loading

**Symptoms:** 404 errors for tile requests.

**Solutions:**
1. Verify tile matrix set name
2. Check zoom level (tilematrix) is valid
3. Verify tile coordinates are in bounds
4. Check tile cache is enabled

```bash
# Validate tile coordinates
curl "http://localhost:5000/wmts?service=WMTS&request=GetCapabilities" | grep TileMatrixSet -A 20
```

### Issue: WCS Request Timeout

**Symptoms:** Large coverage requests timeout.

**Solutions:**
1. Use smaller spatial subsets
2. Reduce output resolution
3. Increase request timeout
4. Use COG format for efficient access

```bash
# Efficient subsetting
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&format=image/tiff&subset=Lat(40,42)&subset=Long(-120,-118)&size=Lat(512)&size=Long(512)"
```

### Issue: CSW Returns No Results

**Symptoms:** GetRecords returns empty result set.

**Solutions:**
1. Check filter syntax
2. Verify metadata exists
3. Check queryable properties
4. Use broader search terms

```bash
# List all records
curl "http://localhost:5000/csw?service=CSW&version=2.0.2&request=GetRecords&resultType=results&elementSetName=brief"
```

## Related Documentation

- [OGC API Features](./03-01-ogc-api-features.md) - Modern REST API
- [Configuration Reference](./02-01-configuration-reference.md) - Service configuration
- [Authentication Setup](./02-02-authentication-setup.md) - Security configuration
- [Docker Deployment](./04-01-docker-deployment.md) - Deployment guides
- [Common Issues](./05-02-common-issues.md) - Troubleshooting

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**OGC Compliance**: WFS 2.0, WMS 1.3.0, WMTS 1.0, WCS 2.0.1, CSW 2.0.2
