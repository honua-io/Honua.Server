# Vector STAC Integration

## Overview

The HonuaIO STAC API now provides comprehensive support for vector data through automatic STAC collection and item generation from vector layer definitions. This integration enables discovery and access to vector datasets through the standard STAC API alongside raster datasets.

## Features

### Automatic STAC Collection Generation
- **Metadata Mapping**: Vector layer metadata is automatically mapped to STAC collection properties
- **Spatial Extent**: Bounding boxes from layer definitions are converted to STAC spatial extents
- **Temporal Extent**: Temporal intervals from layer definitions are mapped to STAC temporal extents
- **Keywords & Themes**: Layer keywords, service keywords, and catalog keywords are aggregated
- **Providers**: STAC provider information can be configured per layer
- **License Information**: Configurable SPDX license identifiers

### Vector Asset Generation
Vector STAC items automatically include links to multiple data formats:

- **GeoJSON** (`application/geo+json`): Standard web-friendly format
- **FlatGeobuf** (`application/vnd.flatgeobuf`): Efficient binary format for large datasets
- **Vector Tiles (MVT)** (`application/vnd.mapbox-vector-tile`): Tiled vector data for web mapping
- **WFS** (`application/gml+xml`): OGC Web Feature Service endpoint (when enabled)
- **Thumbnails** (`image/png`, etc.): Preview images when configured

### Rich Metadata Properties
Vector STAC items include Honua-specific properties:

- `honua:serviceId`: Parent service identifier
- `honua:layerId`: Source layer identifier
- `honua:geometryType`: Geometry type (Point, LineString, Polygon, etc.)
- `honua:idField`: Primary key field name
- `honua:geometryField`: Geometry column name
- `honua:fields`: Array of available field names

## Configuration

### Enabling STAC for Vector Layers

Add STAC metadata to your layer configuration:

```yaml
services:
  - id: cadastre
    title: Cadastral Data
    serviceType: ogc
    dataSourceId: postgres-main
    layers:
      - id: parcels
        title: Land Parcels
        geometryType: Polygon
        idField: parcel_id
        geometryField: geom
        fields:
          - name: parcel_id
            dataType: int
          - name: address
            dataType: string
          - name: area_sqm
            dataType: double
        extent:
          bbox:
            - [-122.5, 37.5, -122.0, 38.0]
          temporal:
            - start: "2024-01-01T00:00:00Z"
        stac:
          enabled: true
          collectionId: sf-parcels
          license: CC-BY-4.0
          providers:
            - name: San Francisco Planning
              roles: [producer, licensor]
              url: https://sf-planning.example.com
          summaries:
            land_use: [residential, commercial, industrial, open_space]
          itemAssets:
            data:
              title: Parcel Features
              type: application/geo+json
              roles: [data]
          stacExtensions:
            - https://stac-extensions.github.io/projection/v1.0.0/schema.json
          additionalProperties:
            custom:property: "value"
```

### Configuration Options

#### `stac.enabled` (boolean, default: `true`)
Enable or disable STAC catalog generation for this layer.

#### `stac.collectionId` (string, optional)
Custom STAC collection identifier. If not specified, the layer ID is used.

#### `stac.license` (string, optional)
SPDX license identifier (e.g., `CC-BY-4.0`, `ODbL-1.0`, `proprietary`).

#### `stac.providers` (array, optional)
Information about data providers:
- `name` (required): Provider organization name
- `description` (optional): Provider description
- `roles` (optional): Array of roles (`producer`, `licensor`, `processor`, `host`)
- `url` (optional): Provider website URL

#### `stac.summaries` (object, optional)
Collection-level property summaries. Useful for faceted search:
```yaml
summaries:
  road_class: [highway, arterial, collector, local]
  surface_type: [asphalt, concrete, gravel]
```

#### `stac.assets` (object, optional)
Collection-level assets (metadata documents, etc.):
```yaml
assets:
  metadata:
    title: ISO 19115 Metadata
    type: application/xml
    roles: [metadata]
    href: https://example.com/metadata/parcels.xml
```

#### `stac.itemAssets` (object, optional)
Template for item-level assets. Used when custom assets are needed:
```yaml
itemAssets:
  data:
    title: Feature Data
    type: application/geo+json
    roles: [data]
    href: https://example.com/data/{collection}/{item}
```

#### `stac.stacExtensions` (array, optional)
STAC extension URLs:
```yaml
stacExtensions:
  - https://stac-extensions.github.io/projection/v1.0.0/schema.json
  - https://stac-extensions.github.io/version/v1.0.0/schema.json
```

#### `stac.itemIdTemplate` (string, optional)
Template for generating item IDs. If not specified, defaults to `{collectionId}-overview`.

#### `stac.additionalProperties` (object, optional)
Custom properties to include in the STAC collection:
```yaml
additionalProperties:
  update_frequency: monthly
  quality_level: high
```

## API Endpoints

### List Collections
```
GET /stac/collections
```

Returns all STAC collections, including both raster and vector collections.

### Get Collection
```
GET /stac/collections/{collectionId}
```

Returns metadata for a specific collection.

**Example Response:**
```json
{
  "stac_version": "1.0.0",
  "type": "Collection",
  "id": "sf-parcels",
  "title": "Land Parcels",
  "description": "San Francisco land parcel boundaries",
  "license": "CC-BY-4.0",
  "extent": {
    "spatial": {
      "bbox": [[-122.5, 37.5, -122.0, 38.0]]
    },
    "temporal": {
      "interval": [["2024-01-01T00:00:00Z", null]]
    }
  },
  "links": [
    {
      "rel": "self",
      "href": "https://api.example.com/stac/collections/sf-parcels"
    },
    {
      "rel": "items",
      "href": "https://api.example.com/stac/collections/sf-parcels/items"
    }
  ],
  "providers": [
    {
      "name": "San Francisco Planning",
      "roles": ["producer", "licensor"],
      "url": "https://sf-planning.example.com"
    }
  ],
  "summaries": {
    "land_use": ["residential", "commercial", "industrial", "open_space"]
  }
}
```

### Get Collection Items
```
GET /stac/collections/{collectionId}/items
```

Returns STAC items for a collection.

**Example Response:**
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "stac_version": "1.0.0",
      "id": "sf-parcels-overview",
      "collection": "sf-parcels",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[
          [-122.5, 37.5],
          [-122.0, 37.5],
          [-122.0, 38.0],
          [-122.5, 38.0],
          [-122.5, 37.5]
        ]]
      },
      "bbox": [-122.5, 37.5, -122.0, 38.0],
      "properties": {
        "datetime": "2024-01-01T00:00:00Z",
        "honua:serviceId": "cadastre",
        "honua:layerId": "parcels",
        "honua:geometryType": "Polygon",
        "honua:idField": "parcel_id",
        "honua:geometryField": "geom",
        "honua:fields": ["parcel_id", "address", "area_sqm"]
      },
      "assets": {
        "geojson": {
          "href": "https://api.example.com/ogc/collections/cadastre:parcels/items?f=json",
          "title": "Land Parcels - GeoJSON",
          "type": "application/geo+json",
          "roles": ["data"]
        },
        "flatgeobuf": {
          "href": "https://api.example.com/ogc/collections/cadastre:parcels/items?f=flatgeobuf",
          "title": "Land Parcels - FlatGeobuf",
          "type": "application/vnd.flatgeobuf",
          "roles": ["data"]
        },
        "tiles": {
          "href": "https://api.example.com/vector-tiles/cadastre/parcels/{z}/{x}/{y}.pbf",
          "title": "Land Parcels - Vector Tiles",
          "type": "application/vnd.mapbox-vector-tile",
          "roles": ["tiles"]
        },
        "wfs": {
          "href": "https://api.example.com/services/cadastre/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=parcels",
          "title": "Land Parcels - WFS",
          "type": "application/gml+xml",
          "roles": ["data", "wfs"]
        },
        "thumbnail": {
          "href": "https://example.com/thumbnails/parcels.png",
          "title": "Land Parcels - Thumbnail",
          "type": "image/png",
          "roles": ["thumbnail"]
        }
      },
      "links": [
        {
          "rel": "self",
          "href": "https://api.example.com/stac/collections/sf-parcels/items/sf-parcels-overview"
        },
        {
          "rel": "collection",
          "href": "https://api.example.com/stac/collections/sf-parcels"
        }
      ]
    }
  ],
  "links": [
    {
      "rel": "self",
      "href": "https://api.example.com/stac/collections/sf-parcels/items"
    }
  ]
}
```

## Synchronization

Vector STAC catalogs are automatically synchronized when:

1. **Server Startup**: All STAC-enabled vector layers are synchronized on application startup
2. **Metadata Changes**: Vector layer changes trigger automatic re-synchronization
3. **Manual Trigger**: Use the CLI or API to manually synchronize specific layers

### Automatic Synchronization

The `VectorStacCatalogSynchronizer` service automatically:
- Creates or updates STAC collections from layer metadata
- Generates STAC items with vector asset links
- Prunes removed items from the catalog
- Coordinates with raster STAC synchronization for unified catalogs

### Service Integration

Vector STAC synchronization is integrated with the hosted service lifecycle:

```csharp
public StacCatalogSynchronizationHostedService(
    IHonuaConfigurationService configurationService,
    IMetadataRegistry metadataRegistry,
    IRasterStacCatalogSynchronizer rasterSynchronizer,
    ILogger<StacCatalogSynchronizationHostedService> logger,
    IVectorStacCatalogSynchronizer? vectorSynchronizer = null)
```

Vector synchronization is optional (backward compatible) but automatically enabled when available.

## Architecture

### Components

1. **VectorStacCatalogBuilder**: Converts vector layer metadata to STAC collections and items
   - Maps layer properties to STAC metadata
   - Generates vector asset definitions
   - Handles spatial/temporal extent conversion

2. **VectorStacCatalogSynchronizer**: Manages synchronization workflow
   - Implements `IVectorStacCatalogSynchronizer` interface
   - Coordinates with `IStacCatalogStore` for persistence
   - Handles incremental updates and pruning

3. **StacCatalogSynchronizationHostedService**: Background service orchestration
   - Triggers synchronization on startup
   - Responds to metadata change events
   - Coordinates raster and vector synchronization

### Data Flow

```
Vector Layer Metadata
        ↓
VectorStacCatalogBuilder.Build()
        ↓
StacCollectionRecord + StacItemRecords
        ↓
VectorStacCatalogSynchronizer.SynchronizeLayerAsync()
        ↓
IStacCatalogStore.UpsertCollectionAsync()
IStacCatalogStore.UpsertItemAsync()
        ↓
STAC Catalog Database (SQLite, PostgreSQL, or MySQL)
        ↓
STAC API Endpoints
```

## Limitations

1. **Single Item Per Collection**: Currently, each vector layer generates one STAC item representing the entire dataset. Future versions may support feature-level items for large datasets.

2. **Asset URL Generation**: Asset URLs are generated based on configured base URI. In distributed environments, ensure proper URL configuration.

3. **Geometry Types**: Vector tile assets are only generated for compatible geometry types (Point, LineString, Polygon, and Multi* variants).

4. **WFS Requirement**: WFS assets are only included if WFS is enabled for the parent service.

## Best Practices

1. **Use Descriptive Collection IDs**: Choose meaningful collection identifiers that align with your data catalog structure.

2. **Provide Rich Metadata**: Include comprehensive descriptions, keywords, and provider information to improve discoverability.

3. **Configure Spatial Extents**: Ensure bbox and temporal extent are accurate for better spatial search performance.

4. **Add Thumbnails**: Provide thumbnail images for visual preview in STAC browsers and clients.

5. **Use Summaries**: Define property summaries for faceted search and filtering capabilities.

6. **Set Appropriate Licenses**: Clearly specify data licenses using SPDX identifiers.

7. **Test with STAC Validators**: Validate generated STAC documents using tools like `stac-validator` to ensure compliance.

## Testing

Run vector STAC tests:

```bash
dotnet test tests/Honua.Server.Core.Tests --filter "VectorStacCatalogBuilderTests"
```

## References

- [STAC Specification](https://github.com/radiantearth/stac-spec)
- [STAC Extensions](https://stac-extensions.github.io/)
- [SPDX License List](https://spdx.org/licenses/)
- [OGC API - Features](https://ogcapi.ogc.org/features/)
- [FlatGeobuf Format](https://flatgeobuf.org/)
- [Mapbox Vector Tile Specification](https://github.com/mapbox/vector-tile-spec)
