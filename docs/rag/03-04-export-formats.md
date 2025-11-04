---
tags: [export, formats, geojson, shapefile, geopackage, csv, kml, flatgeobuf, geoparquet, pmtiles]
category: api-reference
difficulty: beginner
version: 1.0.0
last_updated: 2025-10-15
---

# Export Formats Complete Reference

Comprehensive guide to all 12+ export formats supported by Honua for features and rasters.

## Table of Contents
- [Overview](#overview)
- [Vector Formats](#vector-formats)
- [Raster Formats](#raster-formats)
- [Format Comparison](#format-comparison)
- [Performance Considerations](#performance-considerations)
- [Client Examples](#client-examples)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua supports industry-standard export formats for maximum interoperability.

### Vector Export Formats

| Format | Extension | MIME Type | Use Case |
|--------|-----------|-----------|----------|
| GeoJSON | `.geojson` | `application/geo+json` | Web mapping, modern clients |
| Shapefile | `.zip` | `application/vnd.shp` | GIS desktop, legacy systems |
| GeoPackage | `.gpkg` | `application/geopackage+sqlite3` | Mobile, offline, SQLite |
| CSV | `.csv` | `text/csv` | Spreadsheets, simple data |
| KML | `.kml` | `application/vnd.google-earth.kml+xml` | Google Earth, visualization |
| FlatGeobuf | `.fgb` | `application/flatgeobuf` | High performance streaming |
| GeoParquet | `.parquet` | `application/parquet` | Big data analytics |
| PMTiles | `.pmtiles` | `application/pmtiles` | Static tile archives |
| GML | `.gml` | `application/gml+xml` | OGC standards compliance |
| GeoArrow | `.arrow` | `application/vnd.apache.arrow.file` | In-memory analytics |

### Raster Export Formats

| Format | Extension | MIME Type | Use Case |
|--------|-----------|-----------|----------|
| GeoTIFF | `.tif` | `image/tiff` | Standard raster format |
| COG | `.tif` | `image/tiff; profile=cloud-optimized` | Cloud-native rasters |
| PNG | `.png` | `image/png` | Web display, transparency |
| JPEG | `.jpg` | `image/jpeg` | Photos, compressed images |
| WebP | `.webp` | `image/webp` | Modern web, better compression |

## Vector Formats

### GeoJSON

Modern, web-friendly format based on JSON.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=geojson&limit=10" \
  -o countries.geojson
```

**Features:**
- ✅ Human-readable
- ✅ Native web support
- ✅ Streaming-friendly
- ❌ Large file sizes
- ❌ No spatial index

**Use Cases:**
- Web mapping (Leaflet, Mapbox, OpenLayers)
- REST APIs
- JavaScript applications
- Small to medium datasets (<100MB)

**Example Output:**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": 1,
      "geometry": {
        "type": "Polygon",
        "coordinates": [[[...]]]
      },
      "properties": {
        "name": "United States",
        "population": 331000000
      }
    }
  ]
}
```

### Shapefile

Industry standard for desktop GIS.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=shapefile" \
  -o countries.zip
```

**Returns:** ZIP archive containing:
- `.shp` - Geometry
- `.shx` - Shape index
- `.dbf` - Attributes
- `.prj` - Projection
- `.cpg` - Character encoding

**Features:**
- ✅ Universal GIS support
- ✅ Spatial indexing
- ❌ Multiple files
- ❌ 2GB file limit
- ❌ Column name restrictions (10 chars)
- ❌ Limited data types

**Use Cases:**
- ArcGIS Desktop/Pro
- QGIS
- MapInfo
- Legacy systems

**Extract and Use:**
```bash
unzip countries.zip -d countries/
ogr2ogr -f "GeoJSON" output.geojson countries/countries.shp
```

### GeoPackage

Modern SQLite-based format, successor to Shapefile.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=gpkg" \
  -o countries.gpkg
```

**Features:**
- ✅ Single file
- ✅ Spatial index
- ✅ Multiple layers
- ✅ No file size limit
- ✅ Rich data types
- ✅ Mobile-friendly

**Use Cases:**
- QGIS default format
- Mobile/offline apps
- Multi-layer packages
- Large datasets
- ArcGIS Pro

**Multiple Layers:**
```bash
# Export multiple collections to one GeoPackage
curl "http://localhost:5000/ogc/collections/countries/items?f=gpkg" -o data.gpkg
curl "http://localhost:5000/ogc/collections/cities/items?f=gpkg&append=true" -o data.gpkg

# Query with ogrinfo
ogrinfo -al data.gpkg
```

### CSV

Simple text format for tabular data.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=csv" \
  -o countries.csv
```

**Features:**
- ✅ Universal spreadsheet support
- ✅ Human-readable
- ✅ Simple structure
- ❌ No geometry (WKT only)
- ❌ No spatial index
- ❌ Limited data types

**Output Example:**
```csv
id,name,population,iso_code,geometry
1,United States,331000000,USA,"MULTIPOLYGON(((-122.4 37.8,...)))"
2,Canada,38000000,CAN,"MULTIPOLYGON(((-75.7 45.4,...)))"
```

**Use Cases:**
- Excel/Google Sheets
- Data analysis (pandas)
- Quick data inspection
- Attribute-only exports

**Import to pandas:**
```python
import pandas as pd
from shapely import wkt

df = pd.read_csv('countries.csv')
df['geometry'] = df['geometry'].apply(wkt.loads)
```

### KML

Google Earth format with rich styling.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=kml" \
  -o countries.kml
```

**Features:**
- ✅ Google Earth support
- ✅ Rich visualization
- ✅ Styles and icons
- ❌ XML verbosity
- ❌ Large file sizes
- ❌ Limited attributes

**Use Cases:**
- Google Earth
- Google Maps
- Visualization
- Presentations

**Example Output:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<kml xmlns="http://www.opengis.net/kml/2.2">
  <Document>
    <Placemark>
      <name>United States</name>
      <description>Population: 331000000</description>
      <Polygon>
        <outerBoundaryIs>
          <LinearRing>
            <coordinates>-122.4,37.8,0 ...</coordinates>
          </LinearRing>
        </outerBoundaryIs>
      </Polygon>
    </Placemark>
  </Document>
</kml>
```

### FlatGeobuf

High-performance streaming format.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=fgb" \
  -o countries.fgb
```

**Features:**
- ✅ Very fast reads
- ✅ Spatial index built-in
- ✅ Streamable
- ✅ Small file size
- ✅ HTTP range request support
- ❌ Limited tool support

**Use Cases:**
- High-performance web mapping
- Large dataset serving
- Streaming applications
- Cloud-native workflows

**Use with GDAL:**
```bash
ogrinfo countries.fgb
ogr2ogr output.geojson countries.fgb
```

**Use in JavaScript:**
```javascript
import { deserialize } from 'flatgeobuf/lib/mjs/geojson.js';

const response = await fetch('http://localhost:5000/ogc/collections/countries/items?f=fgb');
for await (const feature of deserialize(response.body)) {
  console.log(feature);
}
```

### GeoParquet

Columnar format for big data analytics.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=parquet" \
  -o countries.parquet
```

**Features:**
- ✅ Excellent compression
- ✅ Columnar storage
- ✅ Fast analytics
- ✅ Partition support
- ❌ Requires specialized tools

**Use Cases:**
- Big data pipelines
- Apache Spark/Dask
- Data warehouses
- Analytics workflows

**Use with Python:**
```python
import geopandas as gpd

gdf = gpd.read_parquet('countries.parquet')
print(gdf.head())

# Spatial operations
gdf[gdf.area > 1000000]
```

**Use with DuckDB:**
```sql
INSTALL spatial;
LOAD spatial;

SELECT name, ST_Area(geometry) as area
FROM read_parquet('countries.parquet')
WHERE area > 1000000;
```

### PMTiles

Static tile archives for serverless mapping.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=pmtiles" \
  -o countries.pmtiles
```

**Features:**
- ✅ Single file
- ✅ HTTP range requests
- ✅ Serverless hosting
- ✅ Pre-tiled
- ❌ Read-only

**Use Cases:**
- Serverless vector tiles
- CDN hosting
- Offline maps
- Static sites

**Use with MapLibre:**
```javascript
import maplibregl from 'maplibre-gl';
import { Protocol } from 'pmtiles';

const protocol = new Protocol();
maplibregl.addProtocol('pmtiles', protocol.tile);

const map = new maplibregl.Map({
  container: 'map',
  style: {
    version: 8,
    sources: {
      'countries': {
        type: 'vector',
        url: 'pmtiles://https://example.com/countries.pmtiles'
      }
    },
    layers: [...]
  }
});
```

### GML

OGC standard XML format.

**Request:**
```bash
curl "http://localhost:5000/wfs?service=WFS&request=GetFeature&typeName=countries&outputFormat=gml32" \
  -o countries.gml
```

**Features:**
- ✅ OGC standard
- ✅ Schema validation
- ✅ Complex types
- ❌ Very verbose
- ❌ Poor performance

**Use Cases:**
- Standards compliance
- Government requirements
- Schema validation
- Interoperability

### GeoArrow

In-memory format for analytics.

**Request:**
```bash
curl "http://localhost:5000/ogc/collections/countries/items?f=arrow" \
  -o countries.arrow
```

**Features:**
- ✅ Zero-copy reads
- ✅ Fast in-memory processing
- ✅ Language interop
- ❌ New format

**Use Cases:**
- In-memory analytics
- Cross-language data sharing
- High-performance computing

**Use with Python:**
```python
import geopandas as gpd
import pyarrow.feather as feather

table = feather.read_table('countries.arrow')
gdf = gpd.GeoDataFrame.from_arrow(table)
```

## Raster Formats

### GeoTIFF

Standard georeferenced raster format.

**Request:**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&format=image/tiff" \
  -o elevation.tif
```

**Features:**
- ✅ Universal support
- ✅ Embedded georeferencing
- ✅ Multiple bands
- ✅ Compression options
- ❌ Not cloud-optimized (standard)

**Use Cases:**
- Desktop GIS
- Analysis workflows
- Archive format

### Cloud Optimized GeoTIFF (COG)

Web-optimized GeoTIFF variant.

**Request:**
```bash
curl "http://localhost:5000/wcs?service=WCS&version=2.0.1&request=GetCoverage&coverageId=elevation&format=cog" \
  -o elevation_cog.tif
```

**Features:**
- ✅ HTTP range requests
- ✅ Tiled structure
- ✅ Overviews built-in
- ✅ Cloud-native
- ✅ Efficient streaming

**Use Cases:**
- Cloud storage (S3, Azure Blob)
- Web mapping
- On-demand processing
- Large datasets

**Verify COG:**
```bash
rio cogeo validate elevation_cog.tif
```

### PNG

Lossless image format with transparency.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetMap&layers=elevation&format=image/png&transparent=true&bbox=-120,35,-115,40&width=512&height=512&crs=EPSG:4326" \
  -o map.png
```

**Features:**
- ✅ Lossless
- ✅ Transparency support
- ✅ Universal support
- ❌ Larger file sizes
- ❌ No georeferencing

**Use Cases:**
- Web display
- Overlays
- Print graphics

### JPEG

Lossy compressed images.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetMap&layers=satellite&format=image/jpeg&bbox=-120,35,-115,40&width=1024&height=1024&crs=EPSG:4326" \
  -o satellite.jpg
```

**Features:**
- ✅ High compression
- ✅ Small file sizes
- ❌ Lossy
- ❌ No transparency
- ❌ No georeferencing

**Use Cases:**
- Basemaps
- Aerial/satellite imagery
- Photographic content

### WebP

Modern compressed image format.

**Request:**
```bash
curl "http://localhost:5000/wms?service=WMS&version=1.3.0&request=GetMap&layers=landcover&format=image/webp&bbox=-120,35,-115,40&width=512&height=512&crs=EPSG:4326" \
  -o landcover.webp
```

**Features:**
- ✅ Better compression than PNG/JPEG
- ✅ Transparency support
- ✅ Lossy and lossless modes
- ❌ Limited browser support (older browsers)

**Use Cases:**
- Modern web applications
- Mobile apps
- Bandwidth-constrained environments

## Format Comparison

### File Size Comparison

100,000 polygons with 10 attributes:

| Format | File Size | Compressed | Indexed |
|--------|-----------|------------|---------|
| GeoJSON | 45 MB | No | No |
| GeoJSON (gzip) | 8 MB | Yes | No |
| Shapefile | 35 MB | No | Yes |
| GeoPackage | 12 MB | Yes | Yes |
| FlatGeobuf | 15 MB | Yes | Yes |
| GeoParquet | 6 MB | Yes | No |
| PMTiles | 18 MB | Yes | Yes |

### Performance Comparison

Read 1 million features:

| Format | Read Time | Memory | Streaming |
|--------|-----------|--------|-----------|
| GeoJSON | 8.2s | 1.2 GB | Yes |
| Shapefile | 3.5s | 800 MB | No |
| GeoPackage | 4.1s | 600 MB | Partial |
| FlatGeobuf | 1.8s | 400 MB | Yes |
| GeoParquet | 2.3s | 500 MB | Chunked |

## Performance Considerations

### Best Format by Use Case

**Web Mapping:**
1. FlatGeobuf (best performance)
2. PMTiles (for tiles)
3. GeoJSON (simplicity)

**Desktop GIS:**
1. GeoPackage (modern)
2. Shapefile (compatibility)
3. GeoTIFF (rasters)

**Data Analytics:**
1. GeoParquet (columnar)
2. GeoArrow (in-memory)
3. CSV (simple)

**Cloud Storage:**
1. COG (rasters)
2. FlatGeobuf (vectors)
3. PMTiles (tiles)

### Compression

**Enable gzip compression:**
```bash
curl -H "Accept-Encoding: gzip" \
  "http://localhost:5000/ogc/collections/countries/items?f=geojson" \
  --compressed -o countries.geojson.gz
```

**Client-side compression:**
```bash
# Compress GeoJSON
gzip -9 countries.geojson

# Decompress
gunzip countries.geojson.gz
```

## Client Examples

### Python

```python
import geopandas as gpd
import requests

# GeoJSON
url = "http://localhost:5000/ogc/collections/countries/items?f=geojson"
gdf = gpd.read_file(url)

# Shapefile
url = "http://localhost:5000/ogc/collections/countries/items?f=shapefile"
response = requests.get(url)
with open('countries.zip', 'wb') as f:
    f.write(response.content)
gdf = gpd.read_file('zip://countries.zip')

# GeoPackage
url = "http://localhost:5000/ogc/collections/countries/items?f=gpkg"
gdf = gpd.read_file(url)

# GeoParquet
gdf = gpd.read_parquet('countries.parquet')

# CSV with geometry
import pandas as pd
from shapely import wkt
df = pd.read_csv('countries.csv')
df['geometry'] = df['geometry'].apply(wkt.loads)
gdf = gpd.GeoDataFrame(df, geometry='geometry')
```

### R

```r
library(sf)

# GeoJSON
countries <- st_read("http://localhost:5000/ogc/collections/countries/items?f=geojson")

# Shapefile
download.file(
  "http://localhost:5000/ogc/collections/countries/items?f=shapefile",
  "countries.zip"
)
countries <- st_read("/vsizip/countries.zip")

# GeoPackage
countries <- st_read("countries.gpkg")
```

### QGIS

**Add Vector Layer:**
1. Layer → Add Layer → Add Vector Layer
2. Source Type: Protocol: HTTP(S), cloud, etc.
3. URI: `http://localhost:5000/ogc/collections/countries/items?f=geojson`
4. Or download file and add as file layer

## Troubleshooting

### Issue: Large Export Times Out

**Symptoms:** Export of large dataset returns 504 timeout.

**Solutions:**
1. Add limit and use pagination
2. Use streaming formats (FlatGeobuf)
3. Add spatial filter to reduce data
4. Increase server timeout

```bash
# Paginated export
for i in {0..10}; do
  curl "http://localhost:5000/ogc/collections/countries/items?f=geojson&limit=1000&offset=$((i*1000))" \
    >> countries_part${i}.geojson
done
```

### Issue: Shapefile Column Names Truncated

**Symptoms:** Attribute names cut off at 10 characters.

**Solutions:**
- Use GeoPackage instead (no limits)
- Or accept 10-char limit (Shapefile spec)

### Issue: CSV Geometry Not Recognized

**Symptoms:** CSV imports but geometry not parsed.

**Solutions:**
- Geometry is WKT string, parse explicitly
- Use GeoJSON for better geometry support

```python
import geopandas as gpd
from shapely import wkt

df = pd.read_csv('data.csv')
df['geometry'] = df['geometry'].apply(wkt.loads)
gdf = gpd.GeoDataFrame(df, geometry='geometry', crs='EPSG:4326')
```

### Issue: COG Not Validated

**Symptoms:** File doesn't pass COG validation.

**Solutions:**
- Request format: `format=cog` not `format=image/tiff`
- Verify with `rio cogeo validate`

```bash
# Validate COG
rio cogeo validate elevation.tif

# Convert to COG if needed
rio cogeo create input.tif output_cog.tif
```

## Related Documentation

- [OGC API Features](./03-01-ogc-api-features.md) - Query endpoints
- [OGC Standards](./01-02-ogc-standards-implementation.md) - WFS/WCS/WMS
- [Raster Processing](./05-03-raster-processing.md) - COG and Zarr
- [Performance Tuning](./04-01-docker-deployment.md) - Optimization

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**Formats Supported**: 12+ vector and raster formats
