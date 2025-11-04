# Metadata Crosswalk Documentation

**Last Updated:** 2025-01-07

This document describes how Honua's semantic metadata model maps to different API protocol standards, demonstrating that metadata is defined once and projected to multiple output formats without replication.

---

## Overview

Honua uses a **semantic mapping approach** where:
- Metadata is defined once in a protocol-agnostic format
- Each API protocol projects the core metadata to its specific format
- Protocol-specific extensions are available but optional
- No metadata duplication across standards

### Core Principle

```
Single Metadata Definition → Multiple Protocol Outputs
                           ├─→ OGC API Features (JSON)
                           ├─→ Esri REST API (JSON)
                           ├─→ WMS 1.3 (XML)
                           ├─→ WFS 2.0 (GML/XML)
                           ├─→ CSW 2.0.2 (Dublin Core XML)
                           ├─→ WCS 2.0.1 (XML)
                           ├─→ STAC 1.0 (JSON)
                           ├─→ OData v4 (JSON/XML)
                           └─→ Carto SQL API (JSON)
```

---

## Layer Metadata Crosswalk

### Identity & Descriptive Metadata

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.id` | `collection.id` | `layer.id` | `Layer/Name` | `FeatureType/Name` | `dc:identifier` | Universal unique identifier |
| `layer.title` | `collection.title` | `layer.name` | `Layer/Title` | `FeatureType/Title` | `dc:title` | Human-readable name |
| `layer.description` | `collection.description` | `layer.description` | `Layer/Abstract` | `FeatureType/Abstract` | `dct:abstract` | Long-form description |
| `layer.keywords` | `collection.keywords` | - | `Layer/KeywordList` | `FeatureType/Keywords` | `dc:subject` (multiple) | Search/discovery terms |

### Geometry & Spatial Metadata

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.geometryType` | `collection.itemType` | `layer.geometryType` | - | `FeatureType/DefaultGeometryPropertyName` | `dc:type` | Point, LineString, Polygon, etc. |
| `layer.geometryField` | `id_field` convention | `layer.geometryField` | - | Property in schema | - | Name of geometry column |
| `layer.extent.bbox` | `collection.extent.spatial.bbox` | `layer.extent` | `Layer/BoundingBox` | `FeatureType/WGS84BoundingBox` | `ows:BoundingBox` | Spatial extent in WGS84 |
| `layer.extent.crs` | `collection.crs` | `layer.extent.spatialReference` | - | - | `@crs` attribute | CRS for bbox |
| `layer.crs` | `collection.crs` | `layer.spatialReference` | `Layer/CRS` | `FeatureType/DefaultCRS` + `OtherCRS` | - | Supported CRS list |
| `layer.storage.srid` | Used for reprojection | `layer.extent.spatialReference.wkid` | Default CRS EPSG code | Default CRS | - | Native storage CRS |

### Temporal Metadata

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.extent.temporal` | `collection.extent.temporal.interval` | `layer.timeInfo` | `Layer/Dimension[@name="time"]` | - | - | Time extent |
| `layer.temporal.startField` | Query parameter mapping | `timeInfo.startTimeField` | - | - | - | Start time column |
| `layer.temporal.endField` | Query parameter mapping | `timeInfo.endTimeField` | - | - | - | End time column |
| `layer.temporal.defaultValue` | Default `datetime` param | `timeInfo.timeExtent[0]` | Default TIME value | - | - | Default temporal value |

### Attribute Schema

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.idField` | Primary feature identifier | `layer.objectIdField` | - | `gml:id` source | - | Primary key field |
| `layer.displayField` | - | `layer.displayField` | - | - | - | Default label field |
| `layer.fields[].name` | Property name in schema | `field.name` | GetFeatureInfo field | Property element name | - | Field/column name |
| `layer.fields[].alias` | `title` in schema | `field.alias` | - | - | - | Human-readable field name |
| `layer.fields[].dataType` | JSON Schema type | `field.type` | - | XSD type | - | Data type (string, integer, etc.) |
| `layer.fields[].nullable` | `required` in schema | `field.nullable` | - | `nillable` | - | NULL allowed |
| `layer.fields[].editable` | - | `field.editable` | - | - | - | Can be modified |
| `layer.fields[].maxLength` | `maxLength` in schema | `field.length` | - | - | - | String max length |

### Query & Access Control

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.query.maxRecordCount` | `limit` max value | `layer.maxRecordCount` | - | `maxFeatures` limit | `maxRecords` limit | Max features per request |
| `layer.query.autoFilter.cql` | Injected into CQL2 filter | Server-side definition filter | - | Additional filter | - | Pre-filter all queries |
| `layer.minScale` | - | `layer.minScale` | `Layer/MinScaleDenominator` | - | - | Min visibility scale |
| `layer.maxScale` | - | `layer.maxScale` | `Layer/MaxScaleDenominator` | - | - | Max visibility scale |

### Editing Capabilities

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.editing.capabilities.allowAdd` | POST to `/items` enabled | `capabilities` includes "Create" | - | Insert operation | - | Can create features |
| `layer.editing.capabilities.allowUpdate` | PUT/PATCH to `/items/{id}` | `capabilities` includes "Update" | - | Update operation | - | Can modify features |
| `layer.editing.capabilities.allowDelete` | DELETE to `/items/{id}` | `capabilities` includes "Delete" | - | Delete operation | - | Can remove features |
| `layer.editing.constraints.requiredFields` | Validation on POST/PUT | `field.nullable=false` | - | - | - | Required field list |
| `layer.editing.constraints.immutableFields` | - | `field.editable=false` | - | - | - | Cannot be changed after creation |

### Styling

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.defaultStyleId` | `collection.links[rel=styles]` default | `layer.drawingInfo` | `Layer/Style[@isDefault=true]` | - | - | Default rendering style |
| `layer.styleIds` | Multiple style links | - | `Layer/Style` list | - | - | Available styles |
| `style.format` | Determines output (SLD, Mapbox GL) | Converted to Esri renderer | SLD if requested | - | - | Style format |
| `style.renderer` | - | Maps to `layer.drawingInfo.renderer.type` | - | - | - | Renderer type (simple, uniqueValue) |

### Attachments

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.attachments.enabled` | Links in feature if enabled | `layer.hasAttachments=true` | - | - | - | Supports file attachments |
| `layer.attachments.maxSizeMiB` | - | Enforced on upload | - | - | - | Max attachment size |
| `layer.attachments.allowedContentTypes` | - | Enforced on upload | - | - | - | MIME type whitelist |

### Catalog & Discovery

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `layer.catalog.summary` | `collection.description` | `layer.description` | `Layer/Abstract` | - | `dct:abstract` | Brief summary |
| `layer.catalog.keywords` | `collection.keywords` | - | `Layer/KeywordList` | - | Multiple `dc:subject` | Keywords for search |
| `layer.catalog.thumbnail` | Link with `rel=preview` | `layer.thumbnail` (if exposed) | - | - | - | Preview image URL |
| `layer.catalog.spatialExtent.bbox` | `extent.spatial.bbox` | `layer.extent` | `BoundingBox` | `WGS84BoundingBox` | `ows:BoundingBox` | Geographic coverage |
| `layer.links` | `collection.links` | - | `MetadataURL`, `DataURL` | `MetadataURL` | `dct:references` | Related resources |

---

## Service Metadata Crosswalk

### Service Identity

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `service.id` | Part of URL path | `/rest/services/{folderId}/{serviceId}` | - | - | - | Service identifier |
| `service.title` | Landing page title | `service.serviceDescription` | `Service/Title` | `ServiceIdentification/Title` | - | Service name |
| `service.description` | Landing page description | - | `Service/Abstract` | `ServiceIdentification/Abstract` | - | Service description |
| `service.keywords` | Landing page keywords | - | `Service/KeywordList` | `ServiceIdentification/Keywords` | - | Service keywords |
| `service.enabled` | Service available if true | Service accessible | Service in capabilities | Service in capabilities | - | Service activation flag |

### Protocol-Specific Configuration

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | WFS 2.0 | CSW 2.0.2 | Notes |
|---------------|------------------|-----------|---------|---------|-----------|-------|
| `service.ogc.collectionsEnabled` | Collections listed | - | - | - | - | Show in OGC collections |
| `service.ogc.itemLimit` | Default `limit` value | - | - | `maxFeatures` | - | Default page size |
| `service.ogc.defaultCrs` | `crs` default | - | Default CRS | Default CRS | - | Default coordinate system |
| `service.ogc.additionalCrs` | `crs` alternatives | - | Additional `CRS` | `OtherCRS` | - | Supported CRS list |
| `service.ogc.storedQueries` | - | - | - | `StoredQuery` definitions | - | WFS parameterized queries |
| `service.vectorTileOptions` | - | - | - | - | - | MVT generation settings |

---

## Raster Dataset Crosswalk

### Raster Identity & Source

| Core Metadata | OGC API - Tiles | Esri REST (ImageServer) | WMS 1.3 | WCS 2.0.1 | STAC 1.0 | Notes |
|---------------|-----------------|-------------------------|---------|-----------|----------|-------|
| `raster.id` | `tileset.id` | `layer.id` | `Layer/Name` | `coverageId` | `item.id` | Unique identifier |
| `raster.title` | `tileset.title` | `layer.name` | `Layer/Title` | Coverage title | `item.properties.title` | Human-readable name |
| `raster.description` | `tileset.description` | `layer.description` | `Layer/Abstract` | - | `item.properties.description` | Long description |
| `raster.source.type` | - | - | - | - | `asset.type` | Source type (file, COG, etc.) |
| `raster.source.uri` | Tile URL template | Image path | - | Coverage file path | `asset.href` | Data location |

### Raster Spatial Properties

| Core Metadata | OGC API - Tiles | Esri REST (ImageServer) | WMS 1.3 | WCS 2.0.1 | STAC 1.0 | Notes |
|---------------|-----------------|-------------------------|---------|-----------|----------|-------|
| `raster.extent.bbox` | `tileset.bounds` | `layer.extent` | `Layer/BoundingBox` | `Envelope/lowerCorner`, `upperCorner` | `item.bbox` | Spatial extent |
| `raster.crs` | `tileset.crs` | `layer.spatialReference` | `Layer/CRS` | Coverage CRS | `item.properties.proj:epsg` | Coordinate systems |

### Raster Cache & Performance

| Core Metadata | OGC API - Tiles | Esri REST (ImageServer) | WMS 1.3 | WCS 2.0.1 | STAC 1.0 | Notes |
|---------------|-----------------|-------------------------|---------|-----------|----------|-------|
| `raster.cache.enabled` | Tiles cached | - | - | - | - | Enable tile caching |
| `raster.cache.zoomLevels` | `tileset.tileMatrixSetLimits` | `tileInfo.lods` | - | - | - | Cached zoom levels |
| `raster.cache.preseed` | - | - | - | - | - | Pre-generate tiles on startup |

### Raster Temporal Properties

| Core Metadata | OGC API - Tiles | Esri REST (ImageServer) | WMS 1.3 | WCS 2.0.1 | STAC 1.0 | Notes |
|---------------|-----------------|-------------------------|---------|-----------|----------|-------|
| `raster.temporal.defaultValue` | Default TIME param | `timeInfo.timeExtent[0]` | Default TIME dimension | - | `item.properties.datetime` | Default time |
| `raster.temporal.minValue` | Earliest available | `timeInfo.timeExtent[0]` | TIME min | - | `item.properties.start_datetime` | Time range start |
| `raster.temporal.maxValue` | Latest available | `timeInfo.timeExtent[1]` | TIME max | - | `item.properties.end_datetime` | Time range end |

---

## Catalog-Level Crosswalk

### Catalog Discovery Metadata

| Core Metadata | OGC API Features | Esri REST | WMS 1.3 | CSW 2.0.2 | STAC 1.0 | Notes |
|---------------|------------------|-----------|---------|-----------|----------|-------|
| `catalog.id` | Landing page id | - | - | Catalog id | `catalog.id` | Catalog identifier |
| `catalog.title` | Landing page title | - | `Service/Title` | `ows:Title` | `catalog.title` | Catalog name |
| `catalog.description` | Landing page description | - | `Service/Abstract` | `ows:Abstract` | `catalog.description` | Catalog description |
| `catalog.keywords` | Landing page keywords | - | `Service/KeywordList` | - | - | Discovery keywords |
| `catalog.publisher` | - | - | - | - | - | Publishing organization |
| `catalog.contact` | Contact link | - | `Service/ContactInformation` | `ows:ServiceProvider` | - | Contact information |
| `catalog.license` | License link | - | `Service/Fees`, `AccessConstraints` | `ows:Fees`, `AccessConstraints` | `catalog.license` | Usage license |

---

## Data Source Crosswalk

Data sources are **internal** metadata and don't directly map to API outputs, but they inform connection behavior:

| Core Metadata | Runtime Behavior | Notes |
|---------------|------------------|-------|
| `dataSource.id` | Referenced by `service.dataSourceId` | Lookup key |
| `dataSource.provider` | Determines IDataStoreProvider implementation | postgres, sqlite, sqlserver, redshift, bigquery, etc. |
| `dataSource.connectionString` | Database connection string | May use `env:` prefix for secrets |

---

## Protocol-Specific Extensions

### OGC-Specific Extensions

**Location:** `service.ogc.*` and `layer.ogc.*` (implicitly available)

- `ogc.collectionsEnabled` - Whether to list in `/collections`
- `ogc.itemLimit` - Default page size for features
- `ogc.defaultCrs` - Default CRS (e.g., `http://www.opengis.net/def/crs/OGC/1.3/CRS84`)
- `ogc.additionalCrs` - Additional supported CRS URIs
- `ogc.conformanceClasses` - Additional conformance classes advertised
- `ogc.storedQueries` - WFS stored query definitions

**Mapping Example:**
```json
{
  "service": {
    "ogc": {
      "itemLimit": 5000,
      "defaultCrs": "http://www.opengis.net/def/crs/EPSG/0/4326",
      "additionalCrs": [
        "http://www.opengis.net/def/crs/EPSG/0/3857"
      ]
    }
  }
}
```

### Esri-Specific Extensions

**Location:** `service.esri.*` (currently empty object, reserved for future use)

**Current Behavior:**
- Esri REST endpoints derive all metadata from core layer/service definitions
- No Esri-specific metadata required currently
- Planned: `esri.supportsCoordinatesQuantization`, `esri.supportedQueryFormats`, etc.

### STAC-Specific Extensions

**Location:** Separate `RasterDatasetDefinition` entities

**Consideration:** STAC Items are semi-separate from Layers:
- `raster.serviceId` and `raster.layerId` optionally link to core metadata
- STAC collections built from raster datasets via `RasterStacCatalogBuilder`
- Could improve integration by allowing `layer.stac.*` extension block

---

## Validation Requirements by Protocol

### Minimum Required Metadata for Each Protocol

#### OGC API Features
**Required:**
- `layer.id`
- `layer.title`
- `layer.geometryType`
- `layer.geometryField`
- `layer.extent.bbox`

**Recommended:**
- `layer.description`
- `layer.keywords`
- `layer.fields` (for property schema)
- `layer.crs` (defaults to CRS84 if omitted)

#### Esri REST API (FeatureServer)
**Required:**
- `layer.id`
- `layer.title`
- `layer.geometryType`
- `layer.idField` (for objectIdField)
- `layer.geometryField`
- `layer.extent.bbox`
- `layer.storage.srid` (for spatial reference)

**Recommended:**
- `layer.displayField`
- `layer.fields` (full schema)
- `layer.editing.*` (if editable)
- `layer.attachments.*` (if attachments enabled)

#### WMS 1.3
**Required:**
- `layer.id` (for Layer/Name)
- `layer.title`
- `layer.extent.bbox`
- `layer.crs` (for supported CRS list)

**Recommended:**
- `layer.description` (Abstract)
- `layer.keywords`
- `layer.minScale`, `layer.maxScale` (scale denominators)
- `layer.defaultStyleId`

#### WFS 2.0
**Required:**
- `layer.id` (for TypeName)
- `layer.title`
- `layer.geometryType`
- `layer.geometryField`
- `layer.extent.bbox`
- `layer.fields` (for XSD schema)

**Recommended:**
- `layer.crs` (default and other CRS)
- `service.ogc.storedQueries` (if using stored queries)

#### CSW 2.0.2
**Required:**
- `layer.id` (dc:identifier)
- `layer.title` (dc:title)

**Recommended:**
- `layer.description` or `layer.catalog.summary` (dct:abstract)
- `layer.keywords` or `layer.catalog.keywords` (dc:subject)
- `layer.extent.bbox` (ows:BoundingBox)
- `layer.links` (dct:references)

#### WCS 2.0.1
**Required:**
- `raster.id` (coverageId)
- `raster.title`
- `raster.source.uri` (file path)
- `raster.extent.bbox`

**Recommended:**
- `raster.crs`
- `raster.description`

#### STAC 1.0
**Required:**
- `raster.id` (item.id)
- `raster.title`
- `raster.extent.bbox` (item.bbox)
- `raster.source.uri` (asset href)

**Recommended:**
- `raster.description`
- `raster.keywords`
- `raster.temporal.*` (datetime properties)
- `raster.catalog.thumbnail`

---

## Crosswalk Implementation Locations

| Protocol | Projection Code Location | Key Mapping Function |
|----------|-------------------------|---------------------|
| OGC API Features | `OgcFeaturesHandlers.cs` | `BuildCollectionMetadata()` |
| Esri REST API | `GeoservicesRESTMetadataMapper.cs` | `CreateFeatureServiceSummary()`, `CreateLayerDetail()` |
| WMS 1.3 | `WmsHandlers.cs` | `CreateLayerElement()` in GetCapabilities |
| WFS 2.0 | `WfsHandlers.cs` | `CreateFeatureTypeElement()` |
| CSW 2.0.2 | `CswHandlers.cs` | `CreateDublinCoreRecord()` |
| WCS 2.0.1 | `WcsHandlers.cs` | `CreateCoverageSummary()`, `HandleDescribeCoverage()` |
| STAC 1.0 | `StacApiMapper.cs`, `RasterStacCatalogBuilder.cs` | `MapToStacItem()`, `BuildCatalog()` |
| OData v4 | `DynamicODataController.cs` | EDM model generation from `LayerDefinition` |
| Carto SQL API | `CartoHandlers.cs` | Direct SQL passthrough to data source |

---

## Design Principles

### 1. **Single Source of Truth**
Core metadata (`LayerDefinition`, `ServiceDefinition`) is protocol-agnostic.

### 2. **Semantic Projection**
Each protocol handler projects core metadata to its format using semantic equivalence.

### 3. **Optional Protocol Extensions**
Protocol-specific features use nested extension objects (e.g., `service.ogc.*`).

### 4. **No Metadata Replication**
You never define the same metadata twice for different protocols.

### 5. **Graceful Degradation**
If optional metadata is missing, protocols use sensible defaults (e.g., CRS84 for undefined CRS).

### 6. **Validation at Boundaries**
Metadata validation ensures required fields for enabled protocols are present before service activation.

---

## Future Enhancements

### 1. **ISO 19115 Metadata Block**
Add `layer.iso19115.*` for richer CSW metadata beyond Dublin Core:
- `iso19115.pointOfContact`
- `iso19115.maintenanceFrequency`
- `iso19115.spatialRepresentationType`
- `iso19115.distributionInfo`

### 2. **Explicit Crosswalk Validation**
Validate that required fields for each enabled protocol are present:
```csharp
public class ProtocolMetadataValidator
{
    public ValidationResult ValidateForOgcApi(LayerDefinition layer) { ... }
    public ValidationResult ValidateForEsri(LayerDefinition layer) { ... }
    public ValidationResult ValidateForWms(LayerDefinition layer) { ... }
}
```

### 3. **STAC-Layer Integration**
Allow `layer.stac.*` extension to unify vector and raster STAC metadata:
```json
{
  "layer": {
    "stac": {
      "assets": [...],
      "properties": {...}
    }
  }
}
```

### 4. **Protocol Capability Advertising**
Add `layer.protocols` to explicitly declare which protocols a layer supports:
```json
{
  "layer": {
    "protocols": ["ogc-features", "esri-rest", "wms", "wfs"]
  }
}
```

### 5. **Automated Crosswalk Testing**
Test suite that validates metadata crosswalk for each protocol:
- Define metadata fixture
- Generate output for all protocols
- Verify semantic equivalence

---

## Examples

### Example 1: Multi-Protocol Layer

**Input Metadata:**
```json
{
  "layers": [{
    "id": "roads",
    "serviceId": "transportation",
    "title": "Road Network",
    "description": "Comprehensive road centerline data",
    "geometryType": "LineString",
    "idField": "road_id",
    "displayField": "road_name",
    "geometryField": "geom",
    "crs": [
      "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
      "http://www.opengis.net/def/crs/EPSG/0/3857"
    ],
    "extent": {
      "bbox": [[-180, -90, 180, 90]],
      "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
    },
    "keywords": ["transportation", "roads", "infrastructure"],
    "fields": [
      {"name": "road_id", "alias": "Road ID", "dataType": "integer"},
      {"name": "road_name", "alias": "Road Name", "dataType": "string", "maxLength": 100},
      {"name": "road_class", "alias": "Classification", "dataType": "string"}
    ],
    "storage": {
      "table": "roads",
      "srid": 4326
    }
  }]
}
```

**Output Projections:**

**OGC API Features (`/ogc/collections/roads`):**
```json
{
  "id": "roads",
  "title": "Road Network",
  "description": "Comprehensive road centerline data",
  "itemType": "feature",
  "crs": [
    "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
    "http://www.opengis.net/def/crs/EPSG/0/3857"
  ],
  "extent": {
    "spatial": {
      "bbox": [[-180, -90, 180, 90]],
      "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
    }
  },
  "keywords": ["transportation", "roads", "infrastructure"]
}
```

**Esri REST (`/rest/services/transportation/roads/FeatureServer/0`):**
```json
{
  "id": 0,
  "name": "Road Network",
  "description": "Comprehensive road centerline data",
  "geometryType": "esriGeometryPolyline",
  "objectIdField": "road_id",
  "displayField": "road_name",
  "extent": {
    "xmin": -180, "ymin": -90, "xmax": 180, "ymax": 90,
    "spatialReference": {"wkid": 4326}
  },
  "fields": [
    {"name": "road_id", "alias": "Road ID", "type": "esriFieldTypeInteger"},
    {"name": "road_name", "alias": "Road Name", "type": "esriFieldTypeString", "length": 100},
    {"name": "road_class", "alias": "Classification", "type": "esriFieldTypeString"}
  ]
}
```

**WMS GetCapabilities:**
```xml
<Layer>
  <Name>roads</Name>
  <Title>Road Network</Title>
  <Abstract>Comprehensive road centerline data</Abstract>
  <KeywordList>
    <Keyword>transportation</Keyword>
    <Keyword>roads</Keyword>
    <Keyword>infrastructure</Keyword>
  </KeywordList>
  <CRS>EPSG:4326</CRS>
  <CRS>EPSG:3857</CRS>
  <EX_GeographicBoundingBox>
    <westBoundLongitude>-180</westBoundLongitude>
    <eastBoundLongitude>180</eastBoundLongitude>
    <southBoundLatitude>-90</southBoundLatitude>
    <northBoundLatitude>90</northBoundLatitude>
  </EX_GeographicBoundingBox>
</Layer>
```

**CSW GetRecords:**
```xml
<csw:Record>
  <dc:identifier>roads</dc:identifier>
  <dc:title>Road Network</dc:title>
  <dct:abstract>Comprehensive road centerline data</dct:abstract>
  <dc:subject>transportation</dc:subject>
  <dc:subject>roads</dc:subject>
  <dc:subject>infrastructure</dc:subject>
  <dc:type>dataset</dc:type>
  <ows:BoundingBox crs="urn:ogc:def:crs:EPSG::4326">
    <ows:LowerCorner>-90 -180</ows:LowerCorner>
    <ows:UpperCorner>90 180</ows:UpperCorner>
  </ows:BoundingBox>
</csw:Record>
```

---

## Conclusion

Honua's metadata architecture follows a **semantic mapping pattern** that eliminates metadata duplication:

1. **Define once** - Core metadata in protocol-agnostic format
2. **Project many** - Each protocol handler maps to its output format
3. **Extend optionally** - Protocol-specific features use nested extensions
4. **Validate comprehensively** - Ensure required fields for enabled protocols

This approach provides:
- ✅ **Maintainability** - Single source of truth
- ✅ **Interoperability** - Same data, multiple standards
- ✅ **Flexibility** - Easy to add new protocols
- ✅ **Clarity** - Clear mapping from core to protocol-specific
