# Map Styling and Symbolization

Keywords: styling, symbolization, renderer, colors, symbols, cartography, visualization, map-styling, simple-renderer, unique-value-renderer, rule-based-renderer, symbology, legend, sld, drawing-info

## Overview

Honua provides a comprehensive map styling system that controls how geographic features and raster data are visualized across multiple service protocols (OGC API Features, WMS, Geoservices REST a.k.a. Esri REST). The styling architecture supports three primary renderer types: **simple**, **unique value**, and **rule-based**, each designed for different cartographic use cases.

### Key Capabilities

- **Multi-Protocol Support**: Styles are converted automatically to SLD (OGC), GeoServices DrawingInfo format (REST), and KML formats
- **Geometry Type Specific**: Point, line, polygon, and raster symbolization
- **Categorical Classification**: Unique value rendering based on feature attributes
- **Scale-Dependent Rendering**: Control visibility and symbolization by map scale
- **Filter-Based Rules**: Apply different symbols based on CQL filter expressions
- **Fallback Resolution**: Automatic style resolution with configurable defaults

### Style Format

Honua uses the **mvp-style** format (Minimal Viable Product Style), a JSON-based format designed for simplicity and broad compatibility. Styles are defined in the metadata configuration and referenced by layers and raster datasets.

```json
{
  "id": "unique-style-id",
  "title": "Human-Readable Style Name",
  "renderer": "simple|uniqueValue",
  "format": "mvp-style",
  "geometryType": "point|line|polygon|raster"
}
```

## Styling System Architecture

### Style Definition Location

Styles are defined in the `styles` array of your metadata configuration:

```json
{
  "catalog": { ... },
  "folders": [ ... ],
  "dataSources": [ ... ],
  "services": [ ... ],
  "layers": [ ... ],
  "rasterDatasets": [ ... ],
  "styles": [
    {
      "id": "my-style",
      "title": "My Custom Style",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "polygon",
      "simple": {
        "fillColor": "#4A90E2FF",
        "strokeColor": "#1F364DFF",
        "strokeWidth": 1.5
      }
    }
  ]
}
```

### Style Resolution and Fallback Logic

Honua uses a sophisticated resolution algorithm to determine which style to apply:

#### Layer Style Resolution

1. **Requested Style ID** - If a client explicitly requests a style by ID
2. **"default" Keyword** - Resolves to the layer's `defaultStyleId`
3. **Layer Default Style** - The style specified in `layer.defaultStyleId`
4. **First Available Style** - The first style in `layer.styleIds` array
5. **No Style** - Returns null (feature rendered without styling)

```csharp
// StyleResolutionHelper.cs implementation
public static StyleDefinition? ResolveStyleForLayer(
    MetadataSnapshot snapshot,
    LayerDefinition layer,
    string? requestedStyleId)
{
    // Try requested style by ID
    if (!string.IsNullOrWhiteSpace(requestedStyleId))
    {
        if (snapshot.TryGetStyle(requestedStyleId, out var direct))
            return direct;

        // Handle "default" keyword
        if (string.Equals(requestedStyleId, "default", StringComparison.OrdinalIgnoreCase))
            return snapshot.TryGetStyle(layer.DefaultStyleId, out var defaultStyle)
                ? defaultStyle : null;
    }

    // Try layer's default style
    if (!string.IsNullOrWhiteSpace(layer.DefaultStyleId))
        return snapshot.TryGetStyle(layer.DefaultStyleId, out var fallback)
            ? fallback : null;

    // Try first available style
    foreach (var candidate in layer.StyleIds)
    {
        if (snapshot.TryGetStyle(candidate, out var style))
            return style;
    }

    return null;
}
```

#### Raster Dataset Style Resolution

1. **Requested Style ID** - Client-specified style
2. **Dataset Default Style** - `rasterDataset.styles.defaultStyleId`
3. **First Available Style** - First style in `rasterDataset.styles.styleIds`
4. **Dataset ID Fallback** - Uses the dataset ID as the style identifier

### Layer and Dataset Style References

Layers reference styles through two properties:

```json
{
  "id": "roads-primary",
  "serviceId": "roads",
  "title": "Primary Roads",
  "geometryType": "Polyline",
  "defaultStyleId": "highway-style",
  "styleIds": ["highway-style", "roads-alternate"],
  "idField": "road_id",
  "geometryField": "geom"
}
```

Raster datasets use a nested `styles` object:

```json
{
  "id": "aerial-imagery",
  "title": "Aerial Imagery",
  "source": { ... },
  "styles": {
    "defaultStyleId": "natural-color",
    "styleIds": ["natural-color", "infrared", "ndvi"]
  }
}
```

## Renderer Types

### Simple Renderer

The **simple renderer** applies a single symbol to all features in a layer. This is the most common renderer for uniform datasets.

#### Simple Renderer Schema

```json
{
  "id": "parcels-style",
  "title": "Parcels",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "polygon",
  "simple": {
    "symbolType": "polygon",
    "fillColor": "#FFFFCCFF",
    "strokeColor": "#666666FF",
    "strokeWidth": 1.0,
    "strokeStyle": "solid",
    "opacity": 0.8,
    "size": 12.0,
    "iconHref": null,
    "label": "Parcel Boundary",
    "description": "Standard parcel symbolization"
  }
}
```

#### Simple Renderer Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `symbolType` | string | "shape" | Symbol type: "shape", "point", "line", "polygon" |
| `fillColor` | string | "#4A90E2FF" | Fill color in hex format (RRGGBBAA or RRGGBB) |
| `strokeColor` | string | "#1F364DFF" | Stroke/outline color in hex format |
| `strokeWidth` | number | varies* | Width of stroke in pixels |
| `strokeStyle` | string | "solid" | Stroke style: "solid", "dash", "dot" |
| `opacity` | number | 1.0 | Overall opacity (0.0-1.0), overrides alpha in colors |
| `size` | number | 12.0 | Size for point symbols in pixels |
| `iconHref` | string? | null | URL to icon for point symbols |
| `label` | string? | null | Human-readable label for legend |
| `description` | string? | null | Detailed description for legend |

*Default stroke width: 1.5 for polygons, 2.0 for lines, 1.5 for point outlines

#### Simple Renderer Examples

**Polygon Fill (Land Parcels)**

```json
{
  "id": "parcels-fill",
  "title": "Land Parcels",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "polygon",
  "simple": {
    "fillColor": "#F0E68C",
    "strokeColor": "#8B7355",
    "strokeWidth": 1.5
  }
}
```

**Line Symbol (Roads)**

```json
{
  "id": "roads-simple",
  "title": "Roads",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "line",
  "simple": {
    "strokeColor": "#808080",
    "strokeWidth": 2.5
  }
}
```

**Point Symbol (Cities)**

```json
{
  "id": "cities-simple",
  "title": "Cities",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "point",
  "simple": {
    "fillColor": "#FF6347",
    "strokeColor": "#8B0000",
    "strokeWidth": 1.0,
    "size": 16.0
  }
}
```

**Raster Symbol (Imagery)**

```json
{
  "id": "natural-color",
  "title": "Natural Color",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "raster",
  "simple": {
    "fillColor": "#5AA06EFF",
    "strokeColor": "#FFFFFFFF",
    "strokeWidth": 1.5,
    "opacity": 0.85
  }
}
```

### Unique Value Renderer

The **unique value renderer** applies different symbols based on a feature attribute value, enabling categorical classification (e.g., different colors for each road type or land use category).

#### Unique Value Renderer Schema

```json
{
  "id": "roads-classified",
  "title": "Roads by Type",
  "renderer": "uniqueValue",
  "format": "mvp-style",
  "geometryType": "line",
  "uniqueValue": {
    "field": "road_type",
    "defaultSymbol": {
      "strokeColor": "#999999",
      "strokeWidth": 1.5
    },
    "classes": [
      {
        "value": "highway",
        "symbol": {
          "strokeColor": "#FF6B35",
          "strokeWidth": 4.0
        }
      },
      {
        "value": "arterial",
        "symbol": {
          "strokeColor": "#F7931E",
          "strokeWidth": 3.0
        }
      },
      {
        "value": "local",
        "symbol": {
          "strokeColor": "#CCCCCC",
          "strokeWidth": 1.5
        }
      }
    ]
  }
}
```

#### Unique Value Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `field` | string | Yes | Attribute field name for classification |
| `defaultSymbol` | SimpleStyleDefinition | No | Symbol for unmatched values |
| `classes` | array | Yes | Array of value-symbol pairs (minimum 1) |
| `classes[].value` | string | Yes | Attribute value to match |
| `classes[].symbol` | SimpleStyleDefinition | Yes | Symbol for this value |

#### Unique Value Examples

**Land Use Classification**

```json
{
  "id": "landuse-classified",
  "title": "Land Use",
  "renderer": "uniqueValue",
  "format": "mvp-style",
  "geometryType": "polygon",
  "uniqueValue": {
    "field": "zoning",
    "defaultSymbol": {
      "fillColor": "#E0E0E0",
      "strokeColor": "#666666",
      "strokeWidth": 1.0
    },
    "classes": [
      {
        "value": "residential",
        "symbol": {
          "fillColor": "#FFFFE0AA",
          "strokeColor": "#B8860B",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "commercial",
        "symbol": {
          "fillColor": "#FF6347AA",
          "strokeColor": "#8B0000",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "industrial",
        "symbol": {
          "fillColor": "#9370DBAA",
          "strokeColor": "#4B0082",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "park",
        "symbol": {
          "fillColor": "#90EE90AA",
          "strokeColor": "#228B22",
          "strokeWidth": 1.0
        }
      }
    ]
  }
}
```

**Point Features by Priority**

```json
{
  "id": "facilities-priority",
  "title": "Facilities by Priority",
  "renderer": "uniqueValue",
  "format": "mvp-style",
  "geometryType": "point",
  "uniqueValue": {
    "field": "priority",
    "defaultSymbol": {
      "fillColor": "#808080",
      "strokeColor": "#404040",
      "size": 8.0
    },
    "classes": [
      {
        "value": "critical",
        "symbol": {
          "fillColor": "#FF0000",
          "strokeColor": "#8B0000",
          "size": 16.0,
          "strokeWidth": 2.0
        }
      },
      {
        "value": "high",
        "symbol": {
          "fillColor": "#FFA500",
          "strokeColor": "#FF8C00",
          "size": 12.0,
          "strokeWidth": 1.5
        }
      },
      {
        "value": "medium",
        "symbol": {
          "fillColor": "#FFFF00",
          "strokeColor": "#FFD700",
          "size": 10.0,
          "strokeWidth": 1.0
        }
      }
    ]
  }
}
```

### Rule-Based Renderer

The **rule-based renderer** applies symbols based on filter expressions and scale ranges, enabling complex cartographic symbolization with multiple conditions.

#### Rule-Based Renderer Schema

```json
{
  "id": "roads-rules",
  "title": "Roads (Multi-Scale)",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "line",
  "rules": [
    {
      "id": "highways-large-scale",
      "label": "Highways (Large Scale)",
      "filter": {
        "field": "road_type",
        "value": "highway"
      },
      "minScale": 0,
      "maxScale": 100000,
      "isDefault": false,
      "symbolizer": {
        "strokeColor": "#FF6B35",
        "strokeWidth": 6.0
      }
    },
    {
      "id": "highways-small-scale",
      "label": "Highways (Small Scale)",
      "filter": {
        "field": "road_type",
        "value": "highway"
      },
      "minScale": 100000,
      "maxScale": 1000000,
      "symbolizer": {
        "strokeColor": "#FF6B35",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "default-roads",
      "label": "All Other Roads",
      "isDefault": true,
      "symbolizer": {
        "strokeColor": "#999999",
        "strokeWidth": 1.5
      }
    }
  ]
}
```

#### Rule Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | string | Yes | Unique identifier for this rule |
| `label` | string | No | Human-readable label for legend |
| `filter` | RuleFilter | No | Attribute filter (field and value) |
| `filter.field` | string | Yes* | Field name to filter on |
| `filter.value` | string | Yes* | Value to match (string comparison) |
| `minScale` | number | No | Minimum scale denominator (inclusive) |
| `maxScale` | number | No | Maximum scale denominator (exclusive) |
| `isDefault` | boolean | No | True if this is the default/fallback rule |
| `symbolizer` | SimpleStyleDefinition | Yes | Symbol for features matching this rule |

*Required if `filter` is present

#### Scale-Dependent Rendering

Scale values in Honua use **scale denominators** (e.g., 1:100,000 = 100000). Smaller scale denominators represent larger map scales (more zoomed in).

- **minScale**: Feature is visible when map scale >= minScale (zoomed out to this level)
- **maxScale**: Feature is visible when map scale < maxScale (zoomed in past this level)
- **No scale limits**: Feature is always visible

Example scale ranges:
- **Large scale (zoomed in)**: 0 - 100,000
- **Medium scale**: 100,000 - 500,000
- **Small scale (zoomed out)**: 500,000+

#### Rule-Based Examples

**Multi-Scale Road Rendering**

```json
{
  "id": "roads-multiscale",
  "title": "Roads (Scale-Dependent)",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "line",
  "rules": [
    {
      "id": "highways-close",
      "label": "Highways (Close)",
      "filter": {
        "field": "type",
        "value": "highway"
      },
      "maxScale": 50000,
      "symbolizer": {
        "strokeColor": "#E74C3C",
        "strokeWidth": 8.0
      }
    },
    {
      "id": "highways-medium",
      "label": "Highways (Medium)",
      "filter": {
        "field": "type",
        "value": "highway"
      },
      "minScale": 50000,
      "maxScale": 250000,
      "symbolizer": {
        "strokeColor": "#E74C3C",
        "strokeWidth": 4.0
      }
    },
    {
      "id": "highways-far",
      "label": "Highways (Far)",
      "filter": {
        "field": "type",
        "value": "highway"
      },
      "minScale": 250000,
      "symbolizer": {
        "strokeColor": "#E74C3C",
        "strokeWidth": 1.5
      }
    },
    {
      "id": "local-close",
      "label": "Local Roads",
      "filter": {
        "field": "type",
        "value": "local"
      },
      "maxScale": 100000,
      "symbolizer": {
        "strokeColor": "#95A5A6",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "default",
      "label": "Other Roads",
      "isDefault": true,
      "symbolizer": {
        "strokeColor": "#BDC3C7",
        "strokeWidth": 1.0
      }
    }
  ]
}
```

**Combined Filter and Scale Rules**

```json
{
  "id": "buildings-complex",
  "title": "Buildings (Complex)",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "polygon",
  "rules": [
    {
      "id": "hospitals-large",
      "label": "Hospitals (Detailed)",
      "filter": {
        "field": "building_type",
        "value": "hospital"
      },
      "maxScale": 10000,
      "symbolizer": {
        "fillColor": "#E74C3CAA",
        "strokeColor": "#C0392B",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "schools-large",
      "label": "Schools (Detailed)",
      "filter": {
        "field": "building_type",
        "value": "school"
      },
      "maxScale": 10000,
      "symbolizer": {
        "fillColor": "#3498DBAA",
        "strokeColor": "#2980B9",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "all-buildings-medium",
      "label": "All Buildings (Medium)",
      "minScale": 10000,
      "maxScale": 50000,
      "symbolizer": {
        "fillColor": "#95A5A6AA",
        "strokeColor": "#7F8C8D",
        "strokeWidth": 1.0
      }
    }
  ]
}
```

## Symbol Types and Properties

### Point Symbols

Point symbols represent discrete locations with markers or icons.

**Circle Marker**

```json
{
  "symbolType": "point",
  "fillColor": "#FF4500",
  "strokeColor": "#8B0000",
  "strokeWidth": 1.5,
  "size": 12.0
}
```

**Custom Icon**

```json
{
  "symbolType": "point",
  "iconHref": "https://example.org/icons/marker-blue.png",
  "size": 24.0
}
```

**Converted to Geoservices REST a.k.a. Esri REST (Simple Marker Symbol - SMS)**

```json
{
  "type": "esriSMS",
  "style": "esriSMSCircle",
  "color": [255, 69, 0, 255],
  "size": 12.0,
  "outline": {
    "type": "esriSLS",
    "style": "esriSLSSolid",
    "color": [139, 0, 0, 255],
    "width": 1.5
  }
}
```

### Line Symbols

Line symbols represent linear features like roads, rivers, and boundaries.

**Solid Line**

```json
{
  "symbolType": "line",
  "strokeColor": "#2E86AB",
  "strokeWidth": 3.0,
  "strokeStyle": "solid"
}
```

**Dashed Line**

```json
{
  "symbolType": "line",
  "strokeColor": "#A23B72",
  "strokeWidth": 2.0,
  "strokeStyle": "dash"
}
```

**Converted to Geoservices REST a.k.a. Esri REST (Simple Line Symbol - SLS)**

```json
{
  "type": "esriSLS",
  "style": "esriSLSSolid",
  "color": [46, 134, 171, 255],
  "width": 3.0
}
```

**Converted to SLD (LineSymbolizer)**

```xml
<LineSymbolizer>
  <Stroke>
    <CssParameter name="stroke">#2E86AB</CssParameter>
    <CssParameter name="stroke-width">3.0</CssParameter>
    <CssParameter name="stroke-opacity">1.0</CssParameter>
  </Stroke>
</LineSymbolizer>
```

### Polygon Symbols

Polygon symbols represent area features with fill and outline.

**Solid Fill with Outline**

```json
{
  "symbolType": "polygon",
  "fillColor": "#90EE90",
  "strokeColor": "#228B22",
  "strokeWidth": 1.5,
  "opacity": 0.7
}
```

**Transparent Fill**

```json
{
  "symbolType": "polygon",
  "fillColor": "#00000000",
  "strokeColor": "#FF0000",
  "strokeWidth": 2.0
}
```

**Converted to Geoservices REST a.k.a. Esri REST (Simple Fill Symbol - SFS)**

```json
{
  "type": "esriSFS",
  "style": "esriSFSSolid",
  "color": [144, 238, 144, 178],
  "outline": {
    "type": "esriSLS",
    "style": "esriSLSSolid",
    "color": [34, 139, 34, 255],
    "width": 1.5
  }
}
```

**Converted to SLD (PolygonSymbolizer)**

```xml
<PolygonSymbolizer>
  <Fill>
    <CssParameter name="fill">#90EE90</CssParameter>
    <CssParameter name="fill-opacity">0.7</CssParameter>
  </Fill>
  <Stroke>
    <CssParameter name="stroke">#228B22</CssParameter>
    <CssParameter name="stroke-width">1.5</CssParameter>
    <CssParameter name="stroke-opacity">1.0</CssParameter>
  </Stroke>
</PolygonSymbolizer>
```

### Raster Symbols

Raster symbols control the rendering of imagery and continuous data.

**Natural Color**

```json
{
  "symbolType": "raster",
  "fillColor": "#5AA06EFF",
  "opacity": 0.85
}
```

**Converted to SLD (RasterSymbolizer)**

```xml
<RasterSymbolizer>
  <Opacity>0.85</Opacity>
  <ColorMap>
    <ColorMapEntry color="#5AA06E" opacity="0.85" quantity="0"/>
  </ColorMap>
</RasterSymbolizer>
```

## Color Specification

### Color Formats

Honua supports multiple hex color formats:

**6-Character Hex (RGB)**
```json
{
  "fillColor": "#FF6347"
}
```
Implicit alpha: 255 (fully opaque)

**8-Character Hex (RGBA)**
```json
{
  "fillColor": "#FF6347AA"
}
```
Explicit alpha: AA = 170 (67% opaque)

**Case Insensitive**
```json
{
  "fillColor": "#ff6347",
  "strokeColor": "#FF6347"
}
```

### Opacity and Transparency

Opacity can be controlled in two ways:

1. **Alpha channel in color**: `#RRGGBBAA` format
2. **Opacity property**: Overrides alpha channel (0.0 to 1.0)

```json
{
  "fillColor": "#FF6347FF",
  "opacity": 0.5
}
```
Result: 50% opacity (opacity property overrides FF alpha)

### Default Colors

If colors are not specified, Honua uses these defaults:

- **Default Fill**: `#4A90E2FF` (blue)
- **Default Stroke**: `#1F364DFF` (dark blue/gray)

### Color Conversion

**Hex to Geoservices REST a.k.a. Esri REST (RGBA array)**

Hex: `#FF6347AA`
Esri: `[255, 99, 71, 170]`

**Hex to SLD (RGB hex + opacity)**

Hex: `#FF6347AA`
SLD:
```xml
<CssParameter name="fill">#FF6347</CssParameter>
<CssParameter name="fill-opacity">0.667</CssParameter>
```

**Opacity Calculation**
```
opacity = alpha / 255
0xAA = 170
170 / 255 = 0.667
```

## Integration with Services

### Layer Style References

Layers reference styles through `defaultStyleId` and `styleIds`:

```json
{
  "layers": [
    {
      "id": "parcels",
      "serviceId": "cadastral",
      "title": "Land Parcels",
      "geometryType": "Polygon",
      "defaultStyleId": "parcels-standard",
      "styleIds": ["parcels-standard", "parcels-zoning", "parcels-value"],
      "idField": "parcel_id",
      "geometryField": "geom"
    }
  ],
  "styles": [
    {
      "id": "parcels-standard",
      "title": "Standard Parcels",
      "renderer": "simple",
      "geometryType": "polygon",
      "simple": {
        "fillColor": "#FFFFCC80",
        "strokeColor": "#666666",
        "strokeWidth": 1.0
      }
    },
    {
      "id": "parcels-zoning",
      "title": "Parcels by Zoning",
      "renderer": "uniqueValue",
      "geometryType": "polygon",
      "uniqueValue": {
        "field": "zoning",
        "classes": [ /* ... */ ]
      }
    }
  ]
}
```

### Raster Dataset Styles

Raster datasets use a nested `styles` configuration:

```json
{
  "rasterDatasets": [
    {
      "id": "aerial-2024",
      "title": "Aerial Imagery 2024",
      "source": {
        "type": "cog",
        "uri": "s3://bucket/aerial.tif"
      },
      "styles": {
        "defaultStyleId": "natural-color",
        "styleIds": ["natural-color", "infrared", "ndvi"]
      }
    }
  ],
  "styles": [
    {
      "id": "natural-color",
      "title": "Natural Color",
      "renderer": "simple",
      "geometryType": "raster",
      "simple": {
        "fillColor": "#5AA06EFF",
        "opacity": 1.0
      }
    },
    {
      "id": "infrared",
      "title": "Color Infrared",
      "renderer": "simple",
      "geometryType": "raster",
      "simple": {
        "fillColor": "#FF5733FF",
        "opacity": 0.9
      }
    }
  ]
}
```

### OGC API Features Styling

OGC API Features endpoints support style queries:

```
GET /ogc/collections/{serviceId}::{layerId}/items?style={styleId}
```

Example:
```
GET /ogc/collections/cadastral::parcels/items?style=parcels-zoning
```

### WMS GetMap Styling

WMS GetMap requests use the `STYLES` parameter:

```
GET /wms?SERVICE=WMS&REQUEST=GetMap&LAYERS=aerial-2024&STYLES=infrared&...
```

If no style is specified, the default style is used.

### Geoservices REST a.k.a. Esri REST Renderer Conversion

Honua automatically converts styles to GeoServices DrawingInfo format format for REST services:

**Request**
```
GET /rest/services/cadastral/FeatureServer/0?f=json
```

**Response (DrawingInfo)**
```json
{
  "drawingInfo": {
    "renderer": {
      "type": "uniqueValue",
      "field1": "zoning",
      "defaultSymbol": {
        "type": "esriSFS",
        "style": "esriSFSSolid",
        "color": [224, 224, 224, 255]
      },
      "uniqueValueInfos": [
        {
          "value": "residential",
          "symbol": {
            "type": "esriSFS",
            "style": "esriSFSSolid",
            "color": [255, 255, 224, 170]
          }
        }
      ]
    }
  }
}
```

### SLD Export

Styles can be exported as OGC Styled Layer Descriptor (SLD) for use in other systems:

```csharp
var sld = StyleFormatConverter.CreateSld(style, "LayerName", "polygon");
```

**Generated SLD**
```xml
<StyledLayerDescriptor version="1.0.0"
  xmlns="http://www.opengis.net/sld"
  xmlns:ogc="http://www.opengis.net/ogc">
  <NamedLayer>
    <Name>LayerName</Name>
    <UserStyle>
      <Name>parcels-standard</Name>
      <Title>Standard Parcels</Title>
      <FeatureTypeStyle>
        <Rule>
          <Name>default</Name>
          <PolygonSymbolizer>
            <Fill>
              <CssParameter name="fill">#FFFFCC</CssParameter>
              <CssParameter name="fill-opacity">0.502</CssParameter>
            </Fill>
            <Stroke>
              <CssParameter name="stroke">#666666</CssParameter>
              <CssParameter name="stroke-width">1.0</CssParameter>
              <CssParameter name="stroke-opacity">1.0</CssParameter>
            </Stroke>
          </PolygonSymbolizer>
        </Rule>
      </FeatureTypeStyle>
    </UserStyle>
  </NamedLayer>
</StyledLayerDescriptor>
```

## Complete Style Examples

### Example 1: Multi-Class Land Use

```json
{
  "id": "landuse-detailed",
  "title": "Detailed Land Use",
  "renderer": "uniqueValue",
  "format": "mvp-style",
  "geometryType": "polygon",
  "uniqueValue": {
    "field": "landuse_code",
    "defaultSymbol": {
      "fillColor": "#E0E0E080",
      "strokeColor": "#808080",
      "strokeWidth": 0.5
    },
    "classes": [
      {
        "value": "R1",
        "symbol": {
          "fillColor": "#FFFFE0CC",
          "strokeColor": "#DAA520",
          "strokeWidth": 1.0,
          "label": "Single-Family Residential"
        }
      },
      {
        "value": "R2",
        "symbol": {
          "fillColor": "#FFE4B5CC",
          "strokeColor": "#CD853F",
          "strokeWidth": 1.0,
          "label": "Multi-Family Residential"
        }
      },
      {
        "value": "C1",
        "symbol": {
          "fillColor": "#FFB6C1CC",
          "strokeColor": "#C71585",
          "strokeWidth": 1.0,
          "label": "Commercial"
        }
      },
      {
        "value": "I1",
        "symbol": {
          "fillColor": "#E6E6FACC",
          "strokeColor": "#9370DB",
          "strokeWidth": 1.0,
          "label": "Light Industrial"
        }
      },
      {
        "value": "OS",
        "symbol": {
          "fillColor": "#98FB98CC",
          "strokeColor": "#228B22",
          "strokeWidth": 1.0,
          "label": "Open Space"
        }
      }
    ]
  }
}
```

### Example 2: Scale-Dependent Transportation

```json
{
  "id": "transportation-network",
  "title": "Transportation Network",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "line",
  "rules": [
    {
      "id": "interstate-close",
      "label": "Interstate (1:1 - 1:50k)",
      "filter": {
        "field": "functional_class",
        "value": "interstate"
      },
      "minScale": 0,
      "maxScale": 50000,
      "symbolizer": {
        "strokeColor": "#E74C3C",
        "strokeWidth": 10.0,
        "strokeStyle": "solid"
      }
    },
    {
      "id": "interstate-medium",
      "label": "Interstate (1:50k - 1:250k)",
      "filter": {
        "field": "functional_class",
        "value": "interstate"
      },
      "minScale": 50000,
      "maxScale": 250000,
      "symbolizer": {
        "strokeColor": "#E74C3C",
        "strokeWidth": 5.0
      }
    },
    {
      "id": "interstate-far",
      "label": "Interstate (1:250k+)",
      "filter": {
        "field": "functional_class",
        "value": "interstate"
      },
      "minScale": 250000,
      "symbolizer": {
        "strokeColor": "#E74C3C",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "arterial-close",
      "label": "Arterial (1:1 - 1:100k)",
      "filter": {
        "field": "functional_class",
        "value": "arterial"
      },
      "maxScale": 100000,
      "symbolizer": {
        "strokeColor": "#F39C12",
        "strokeWidth": 6.0
      }
    },
    {
      "id": "arterial-far",
      "label": "Arterial (1:100k - 1:500k)",
      "filter": {
        "field": "functional_class",
        "value": "arterial"
      },
      "minScale": 100000,
      "maxScale": 500000,
      "symbolizer": {
        "strokeColor": "#F39C12",
        "strokeWidth": 2.5
      }
    },
    {
      "id": "local-visible",
      "label": "Local Streets",
      "filter": {
        "field": "functional_class",
        "value": "local"
      },
      "maxScale": 50000,
      "symbolizer": {
        "strokeColor": "#95A5A6",
        "strokeWidth": 2.0
      }
    },
    {
      "id": "default",
      "label": "Other Roads",
      "isDefault": true,
      "symbolizer": {
        "strokeColor": "#BDC3C7",
        "strokeWidth": 1.0
      }
    }
  ]
}
```

### Example 3: Point Features with Icons

```json
{
  "id": "facilities-icons",
  "title": "Public Facilities",
  "renderer": "uniqueValue",
  "format": "mvp-style",
  "geometryType": "point",
  "uniqueValue": {
    "field": "facility_type",
    "defaultSymbol": {
      "fillColor": "#808080",
      "strokeColor": "#404040",
      "strokeWidth": 1.5,
      "size": 10.0
    },
    "classes": [
      {
        "value": "school",
        "symbol": {
          "iconHref": "https://example.org/icons/school.png",
          "size": 20.0
        }
      },
      {
        "value": "hospital",
        "symbol": {
          "iconHref": "https://example.org/icons/hospital.png",
          "size": 22.0
        }
      },
      {
        "value": "fire_station",
        "symbol": {
          "iconHref": "https://example.org/icons/fire.png",
          "size": 18.0
        }
      },
      {
        "value": "police_station",
        "symbol": {
          "iconHref": "https://example.org/icons/police.png",
          "size": 18.0
        }
      },
      {
        "value": "library",
        "symbol": {
          "fillColor": "#3498DB",
          "strokeColor": "#2980B9",
          "strokeWidth": 1.5,
          "size": 14.0
        }
      }
    ]
  }
}
```

### Example 4: Raster Dataset Styles

```json
{
  "rasterDatasets": [
    {
      "id": "landsat-scene",
      "title": "Landsat 8 Scene",
      "source": {
        "type": "cog",
        "uri": "s3://satellite-data/landsat8.tif"
      },
      "styles": {
        "defaultStyleId": "true-color",
        "styleIds": ["true-color", "false-color", "ndvi"]
      }
    }
  ],
  "styles": [
    {
      "id": "true-color",
      "title": "True Color (RGB)",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "raster",
      "simple": {
        "fillColor": "#FFFFFFFF",
        "opacity": 1.0,
        "description": "Natural color composite (Bands 4-3-2)"
      }
    },
    {
      "id": "false-color",
      "title": "False Color (Infrared)",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "raster",
      "simple": {
        "fillColor": "#FF5733FF",
        "opacity": 0.95,
        "description": "False color infrared (Bands 5-4-3)"
      }
    },
    {
      "id": "ndvi",
      "title": "NDVI (Vegetation Index)",
      "renderer": "simple",
      "format": "mvp-style",
      "geometryType": "raster",
      "simple": {
        "fillColor": "#00FF00FF",
        "opacity": 0.85,
        "description": "Normalized Difference Vegetation Index"
      }
    }
  ]
}
```

## Migration from ArcGIS/GeoServices REST

### Converting GeoServices Simple Renderer

**GeoServices DrawingInfo format**
```json
{
  "renderer": {
    "type": "simple",
    "symbol": {
      "type": "esriSFS",
      "style": "esriSFSSolid",
      "color": [144, 238, 144, 178],
      "outline": {
        "type": "esriSLS",
        "style": "esriSLSSolid",
        "color": [34, 139, 34, 255],
        "width": 1.5
      }
    }
  }
}
```

**Honua Style**
```json
{
  "id": "parcels-simple",
  "title": "Parcels",
  "renderer": "simple",
  "format": "mvp-style",
  "geometryType": "polygon",
  "simple": {
    "fillColor": "#90EE90B2",
    "strokeColor": "#228B22FF",
    "strokeWidth": 1.5
  }
}
```

**Color Conversion**
```
Esri: [144, 238, 144, 178]
Hex: #90EE90B2
  R = 144 = 0x90
  G = 238 = 0xEE
  B = 144 = 0x90
  A = 178 = 0xB2
```

### Converting GeoServices Unique Value Renderer

**GeoServices DrawingInfo format**
```json
{
  "renderer": {
    "type": "uniqueValue",
    "field1": "zoning",
    "defaultSymbol": {
      "type": "esriSFS",
      "color": [224, 224, 224, 255]
    },
    "uniqueValueInfos": [
      {
        "value": "R1",
        "symbol": {
          "type": "esriSFS",
          "color": [255, 255, 224, 170]
        }
      },
      {
        "value": "C1",
        "symbol": {
          "type": "esriSFS",
          "color": [255, 182, 193, 170]
        }
      }
    ]
  }
}
```

**Honua Style**
```json
{
  "id": "zoning-classified",
  "title": "Zoning",
  "renderer": "uniqueValue",
  "format": "mvp-style",
  "geometryType": "polygon",
  "uniqueValue": {
    "field": "zoning",
    "defaultSymbol": {
      "fillColor": "#E0E0E0FF",
      "strokeColor": "#808080FF",
      "strokeWidth": 1.0
    },
    "classes": [
      {
        "value": "R1",
        "symbol": {
          "fillColor": "#FFFFE0AA",
          "strokeColor": "#DAA520FF",
          "strokeWidth": 1.0
        }
      },
      {
        "value": "C1",
        "symbol": {
          "fillColor": "#FFB6C1AA",
          "strokeColor": "#C71585FF",
          "strokeWidth": 1.0
        }
      }
    ]
  }
}
```

## Troubleshooting

### Style Not Rendering

**Problem**: Features render without styling or use default blue color.

**Solutions**:

1. **Check style reference**: Verify `defaultStyleId` or `styleIds` reference an existing style
   ```json
   "defaultStyleId": "my-style"  // Must exist in styles array
   ```

2. **Verify style ID**: Ensure style ID matches exactly (case-insensitive)
   ```json
   { "id": "parcels-style" }  // matches "parcels-style", "PARCELS-STYLE"
   ```

3. **Check geometry type**: Style `geometryType` must match layer `geometryType`
   ```json
   // Layer
   "geometryType": "Polygon"

   // Style (must match)
   "geometryType": "polygon"  // Case-insensitive: polygon, Polygon, POLYGON
   ```

4. **Validate renderer configuration**:
   - Simple renderer requires `simple` property
   - Unique value renderer requires `uniqueValue.field` and `uniqueValue.classes`
   - Rule-based renderer requires `rules` array

### Colors Not Displaying Correctly

**Problem**: Colors appear different than expected or are fully transparent.

**Solutions**:

1. **Check hex format**: Use 6 or 8 character hex with # prefix
   ```json
   "fillColor": "#FF6347"     // Correct
   "fillColor": "#FF6347FF"   // Correct with alpha
   "fillColor": "FF6347"      // Missing # (will fail)
   "fillColor": "#F63"        // Invalid (must be 6 or 8 chars)
   ```

2. **Verify alpha channel**: Ensure alpha is not 00 (fully transparent)
   ```json
   "fillColor": "#FF634700"   // Fully transparent (invisible)
   "fillColor": "#FF6347FF"   // Fully opaque
   "fillColor": "#FF634780"   // 50% transparent
   ```

3. **Check opacity override**: `opacity` property overrides alpha channel
   ```json
   {
     "fillColor": "#FF6347FF",  // Alpha = FF (255)
     "opacity": 0.0             // Override to 0% (invisible)
   }
   ```

### Unique Value Renderer Not Working

**Problem**: All features render the same or use default symbol.

**Solutions**:

1. **Verify field name**: Field must exist in layer and match exactly
   ```json
   "uniqueValue": {
     "field": "land_use"  // Must match actual field name
   }
   ```

2. **Check value matching**: Values are string-compared (case-sensitive)
   ```json
   {
     "value": "residential"  // Will NOT match "Residential" or "RESIDENTIAL"
   }
   ```

3. **Add default symbol**: Provide fallback for unmatched values
   ```json
   "uniqueValue": {
     "field": "type",
     "defaultSymbol": { ... },  // Required for unmatched values
     "classes": [ ... ]
   }
   ```

### Scale-Dependent Rules Not Activating

**Problem**: Features visible at wrong scales or always/never visible.

**Solutions**:

1. **Check scale denominator order**: minScale < maxScale
   ```json
   {
     "minScale": 100000,  // Zoomed out (smaller scale)
     "maxScale": 10000    // ERROR: maxScale > minScale
   }

   // Correct:
   {
     "minScale": 0,       // Most zoomed in
     "maxScale": 100000   // Visible until 1:100,000
   }
   ```

2. **Understand scale semantics**:
   - **maxScale**: Feature visible when zoomed IN more than this
   - **minScale**: Feature visible when zoomed OUT more than this
   ```json
   {
     "maxScale": 50000   // Visible at scales SMALLER than 1:50,000 (zoomed in)
   }
   ```

3. **Verify scale ranges don't overlap** for same filter:
   ```json
   // Good: No gaps or overlaps
   { "filter": {"field": "type", "value": "A"}, "maxScale": 50000 }
   { "filter": {"field": "type", "value": "A"}, "minScale": 50000, "maxScale": 250000 }
   { "filter": {"field": "type", "value": "A"}, "minScale": 250000 }
   ```

### Style Validation Errors

**Problem**: Metadata fails to load with style validation errors.

**Common Errors**:

1. **Missing required properties**:
   ```
   Error: Style 'my-style' must specify a format.
   Solution: Add "format": "mvp-style"
   ```

2. **Renderer mismatch**:
   ```
   Error: Style 'my-style' with renderer 'simple' must include simple symbol details.
   Solution: Add "simple": { ... } configuration
   ```

3. **Invalid scale range**:
   ```
   Error: Style 'my-style' rule 'rule-1' has minScale greater than maxScale.
   Solution: Ensure minScale <= maxScale
   ```

4. **Empty classes**:
   ```
   Error: Style 'my-style' unique value renderer must include at least one class.
   Solution: Add at least one class to "classes": [ ... ]
   ```

5. **Missing rule properties**:
   ```
   Error: Style 'my-style' rule 'rule-1' is missing a symbolizer definition.
   Solution: Add "symbolizer": { ... } to each rule
   ```

### Legend Not Displaying

**Problem**: Legend endpoint returns empty or incorrect entries.

**Solutions**:

1. **Ensure style is assigned**: Layer must reference style via `defaultStyleId` or `styleIds`

2. **Check renderer type**: Legend generation varies by renderer
   - Simple: One entry
   - Unique Value: One entry per class + default
   - Rule-based: Entries based on rules (may not include scale-dependent variations)

3. **Verify symbol properties**: Legend requires valid `fillColor`, `strokeColor`, etc.

## Best Practices

### Style Organization

1. **Use descriptive IDs**: `landuse-zoning` instead of `style1`
2. **Provide titles**: Human-readable names for UI display
3. **Group related styles**: Keep styles for same layer together
4. **Document color schemes**: Use comments or descriptions to explain color choices

### Color Selection

1. **Use RGBA format for transparency**: `#RRGGBBAA` is explicit
2. **Consider accessibility**: Ensure sufficient contrast for colorblind users
3. **Limit color palette**: 5-7 distinct colors for categorical data
4. **Use standard color ramps**: Sequential (light to dark), diverging, categorical

### Performance

1. **Minimize rule complexity**: Fewer rules = faster rendering
2. **Use scale ranges wisely**: Hide detailed features at small scales
3. **Simplify geometries**: Match detail level to scale range
4. **Cache styled tiles**: Pre-render commonly used styles

### Validation

1. **Test all renderer types**: Ensure styles work with target clients
2. **Verify color rendering**: Check in different clients (web, desktop, mobile)
3. **Check scale transitions**: Ensure smooth scale-dependent rendering
4. **Validate filter expressions**: Test unique value field values exist

### Maintenance

1. **Version styles**: Use IDs like `landuse-v1`, `landuse-v2`
2. **Keep defaults simple**: Use simple renderer for default style
3. **Document field dependencies**: Note which fields are required for unique value/rule renderers
4. **Test after metadata changes**: Ensure style references remain valid

## API Reference

### Style Resolution Methods

```csharp
// Resolve style for a layer
StyleDefinition? StyleResolutionHelper.ResolveStyleForLayer(
    MetadataSnapshot snapshot,
    LayerDefinition layer,
    string? requestedStyleId)

// Resolve style for raster dataset
StyleDefinition? StyleResolutionHelper.ResolveStyleForRaster(
    MetadataSnapshot snapshot,
    RasterDatasetDefinition dataset,
    string? requestedStyleId)

// Get default style ID
string StyleResolutionHelper.GetDefaultStyleId(LayerDefinition layer)
```

### Style Format Conversion

```csharp
// Convert to SLD
string StyleFormatConverter.CreateSld(
    StyleDefinition style,
    string layerName,
    string? geometryType = null)

// Convert to GeoServices DrawingInfo format
JsonObject StyleFormatConverter.CreateEsriDrawingInfo(
    StyleDefinition style,
    string geometryType)

// Convert to KML Style
Style? StyleFormatConverter.CreateKmlStyle(
    StyleDefinition style,
    string styleId,
    string geometryType)
```

## Summary

Honua's styling system provides powerful, flexible cartographic control across multiple geospatial service protocols. By understanding the three renderer types (simple, unique value, rule-based), color specifications, and service integrations, you can create professional map visualizations that work seamlessly with OGC, GeoServices REST, and other GIS clients.

Key takeaways:
- Use **simple renderer** for uniform styling
- Use **unique value renderer** for categorical classification
- Use **rule-based renderer** for scale-dependent and complex symbolization
- Specify colors in **#RRGGBBAA** hex format for clarity
- Reference styles through **defaultStyleId** and **styleIds**
- Test styles across multiple clients and scales
- Follow validation rules to avoid configuration errors

For additional examples and implementation details, refer to:
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Styling/StyleFormatConverter.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Styling/StyleResolutionHelper.cs`
- `/home/mike/projects/HonuaIO/samples/ogc/metadata.json`
