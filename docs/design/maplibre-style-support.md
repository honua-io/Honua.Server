# MapLibre Style Support Implementation

**Date:** 2025-11-06
**Status:** Design Proposal
**Branch:** `claude/gis-standards-research-011CUr7Y2RHDguEgWXnmkxme`

## Executive Summary

This document proposes adding **MapLibre Style Specification v8** support to Honua's styling engine. MapLibre is the most popular open-source web mapping SDK (887K weekly NPM downloads) and uses the same style spec as Mapbox GL JS. Adding this support will:

1. Enable Honua to serve styles directly to MapLibre GL JS clients
2. Allow import of existing MapLibre/Mapbox styles into Honua
3. Support bidirectional conversion between Honua's MVP style format and MapLibre
4. Align with industry-standard vector tile styling

## Current State

### Existing Style Format Support

Honua currently supports:
- ✅ **MVP Style** (internal format) - Honua's custom JSON format
- ✅ **SLD** (OGC Styled Layer Descriptor) - XML-based standard
- ✅ **Esri DrawingInfo** - ArcGIS REST Services JSON
- ✅ **KML Styles** - Google Earth styling
- ✅ **CartoCSS** - Basic validation only
- ⚠️ **Mapbox Styles** - Validation only, **no conversion**

### Gap Analysis

The `StyleValidator` class has a `ValidateMapboxStyle()` method (line 178) but:
- ❌ No converter to generate MapLibre/Mapbox styles from MVP styles
- ❌ No parser to import MapLibre styles into MVP format
- ❌ No integration with OGC Styles API to serve MapLibre styles
- ❌ Limited validation (only checks version, layers, sources, name)

## MapLibre Style Specification Overview

### Core Structure

```json
{
  "version": 8,
  "name": "My Style",
  "metadata": {},
  "sources": {
    "my-source": {
      "type": "vector",
      "tiles": ["https://example.com/tiles/{z}/{x}/{y}.pbf"]
    }
  },
  "layers": [
    {
      "id": "layer-id",
      "type": "fill|line|circle|symbol|raster",
      "source": "my-source",
      "source-layer": "layer-name",
      "minzoom": 0,
      "maxzoom": 24,
      "filter": ["==", "field", "value"],
      "layout": {},
      "paint": {}
    }
  ]
}
```

### Layer Types

| MapLibre Type | Honua Geometry | Purpose |
|---------------|----------------|---------|
| `fill` | Polygon | Filled polygons |
| `line` | Line | Stroked lines |
| `circle` | Point | Circle markers |
| `symbol` | Point | Icons and text labels |
| `fill-extrusion` | Polygon | 3D extruded polygons |
| `raster` | Raster | Imagery and raster data |
| `heatmap` | Point | Density heatmaps |
| `hillshade` | Raster | Terrain hillshading |
| `background` | - | Canvas background |

### Paint Properties

**Fill Layer:**
```json
{
  "fill-color": "#FF0000",
  "fill-opacity": 0.8,
  "fill-outline-color": "#000000"
}
```

**Line Layer:**
```json
{
  "line-color": "#0000FF",
  "line-width": 2.5,
  "line-opacity": 1.0,
  "line-dasharray": [2, 1]
}
```

**Circle Layer:**
```json
{
  "circle-radius": 8,
  "circle-color": "#00FF00",
  "circle-opacity": 0.9,
  "circle-stroke-width": 2,
  "circle-stroke-color": "#000000"
}
```

## Proposed Implementation

### Phase 1: MVP-to-MapLibre Converter

Add `StyleFormatConverter.CreateMapLibreStyle()` method to generate MapLibre styles from Honua's MVP styles.

#### Mapping Strategy

**Simple Renderer:**
```csharp
// Honua MVP Style
{
  "renderer": "simple",
  "geometryType": "polygon",
  "simple": {
    "fillColor": "#4A90E2FF",
    "strokeColor": "#1F364DFF",
    "strokeWidth": 1.5
  }
}

// Maps to MapLibre
{
  "version": 8,
  "layers": [
    {
      "id": "layer-id",
      "type": "fill",
      "paint": {
        "fill-color": "#4A90E2",
        "fill-opacity": 1.0,
        "fill-outline-color": "#1F364D"
      }
    }
  ]
}
```

**Unique Value Renderer:**
```csharp
// Honua MVP Style
{
  "renderer": "uniqueValue",
  "uniqueValue": {
    "field": "zoning",
    "classes": [
      {"value": "R1", "symbol": {"fillColor": "#FFFFE0"}},
      {"value": "C1", "symbol": {"fillColor": "#FFB6C1"}}
    ]
  }
}

// Maps to MapLibre (data-driven styling)
{
  "version": 8,
  "layers": [
    {
      "id": "layer-id",
      "type": "fill",
      "paint": {
        "fill-color": [
          "match",
          ["get", "zoning"],
          "R1", "#FFFFE0",
          "C1", "#FFB6C1",
          "#E0E0E0"  // default
        ]
      }
    }
  ]
}
```

**Rule-Based Renderer (Scale-Dependent):**
```csharp
// Honua MVP Style
{
  "rules": [
    {
      "id": "highways-close",
      "filter": {"field": "type", "value": "highway"},
      "maxScale": 50000,
      "symbolizer": {"strokeColor": "#E74C3C", "strokeWidth": 8.0}
    },
    {
      "id": "highways-far",
      "filter": {"field": "type", "value": "highway"},
      "minScale": 250000,
      "symbolizer": {"strokeColor": "#E74C3C", "strokeWidth": 1.5}
    }
  ]
}

// Maps to multiple MapLibre layers with zoom ranges
{
  "version": 8,
  "layers": [
    {
      "id": "highways-close",
      "type": "line",
      "maxzoom": 13,  // ~50,000 scale
      "filter": ["==", ["get", "type"], "highway"],
      "paint": {
        "line-color": "#E74C3C",
        "line-width": 8.0
      }
    },
    {
      "id": "highways-far",
      "type": "line",
      "minzoom": 15,  // ~250,000 scale
      "filter": ["==", ["get", "type"], "highway"],
      "paint": {
        "line-color": "#E74C3C",
        "line-width": 1.5
      }
    }
  ]
}
```

#### Implementation Outline

```csharp
// StyleFormatConverter.cs

public static JsonObject CreateMapLibreStyle(
    StyleDefinition style,
    string layerId,
    string sourceId,
    string? sourceLayer = null,
    string? styleName = null)
{
    var mapLibreStyle = new JsonObject
    {
        ["version"] = 8,
        ["name"] = styleName ?? style.Title ?? style.Id
    };

    // Create source reference
    var sources = new JsonObject
    {
        [sourceId] = new JsonObject
        {
            ["type"] = "vector",
            ["tiles"] = new JsonArray("") // Placeholder, client provides
        }
    };
    mapLibreStyle["sources"] = sources;

    // Convert layers based on renderer type
    var layers = new JsonArray();

    if (style.Renderer == "simple" || style.Renderer == null)
    {
        var layer = CreateMapLibreSimpleLayer(
            layerId,
            sourceId,
            sourceLayer,
            style,
            NormalizeGeometryType(style.GeometryType)
        );
        layers.Add(layer);
    }
    else if (style.Renderer == "uniqueValue" && style.UniqueValue != null)
    {
        var layer = CreateMapLibreUniqueValueLayer(
            layerId,
            sourceId,
            sourceLayer,
            style,
            style.UniqueValue,
            NormalizeGeometryType(style.GeometryType)
        );
        layers.Add(layer);
    }
    else if (style.Rules.Count > 0)
    {
        // Multiple layers for rule-based rendering
        foreach (var rule in style.Rules)
        {
            var layer = CreateMapLibreRuleLayer(
                $"{layerId}-{rule.Id}",
                sourceId,
                sourceLayer,
                rule,
                NormalizeGeometryType(style.GeometryType)
            );
            layers.Add(layer);
        }
    }

    mapLibreStyle["layers"] = layers;
    return mapLibreStyle;
}

private static JsonObject CreateMapLibreSimpleLayer(
    string layerId,
    string sourceId,
    string? sourceLayer,
    StyleDefinition style,
    string geometryType)
{
    var symbol = ResolveSimpleSymbol(style) ?? new SimpleStyleDefinition();
    var layerType = MapGeometryToLayerType(geometryType);

    var layer = new JsonObject
    {
        ["id"] = layerId,
        ["type"] = layerType,
        ["source"] = sourceId
    };

    if (!string.IsNullOrWhiteSpace(sourceLayer))
    {
        layer["source-layer"] = sourceLayer;
    }

    // Create paint properties based on geometry type
    layer["paint"] = CreateMapLibrePaint(symbol, layerType);

    return layer;
}

private static JsonObject CreateMapLibreUniqueValueLayer(
    string layerId,
    string sourceId,
    string? sourceLayer,
    StyleDefinition style,
    UniqueValueStyleDefinition uniqueValue,
    string geometryType)
{
    var layerType = MapGeometryToLayerType(geometryType);

    var layer = new JsonObject
    {
        ["id"] = layerId,
        ["type"] = layerType,
        ["source"] = sourceId
    };

    if (!string.IsNullOrWhiteSpace(sourceLayer))
    {
        layer["source-layer"] = sourceLayer;
    }

    // Create data-driven paint using "match" expression
    var paint = new JsonObject();
    var colorProperty = GetColorPropertyName(layerType);

    // Build match expression: ["match", ["get", "field"], value1, color1, value2, color2, defaultColor]
    var matchExpression = new JsonArray();
    matchExpression.Add("match");
    matchExpression.Add(new JsonArray { "get", uniqueValue.Field });

    foreach (var classItem in uniqueValue.Classes)
    {
        matchExpression.Add(classItem.Value);
        matchExpression.Add(ParseColorToHex(classItem.Symbol.FillColor));
    }

    // Default color
    var defaultColor = uniqueValue.DefaultSymbol?.FillColor ?? "#E0E0E0";
    matchExpression.Add(ParseColorToHex(defaultColor));

    paint[colorProperty] = matchExpression;

    layer["paint"] = paint;
    return layer;
}

private static string MapGeometryToLayerType(string geometryType)
{
    return geometryType switch
    {
        "point" => "circle",
        "line" => "line",
        "polygon" => "fill",
        "raster" => "raster",
        _ => "fill"
    };
}

private static string GetColorPropertyName(string layerType)
{
    return layerType switch
    {
        "fill" => "fill-color",
        "line" => "line-color",
        "circle" => "circle-color",
        _ => "fill-color"
    };
}

private static JsonObject CreateMapLibrePaint(
    SimpleStyleDefinition symbol,
    string layerType)
{
    var paint = new JsonObject();

    switch (layerType)
    {
        case "fill":
            if (!string.IsNullOrWhiteSpace(symbol.FillColor))
            {
                paint["fill-color"] = ParseColorToHex(symbol.FillColor);
            }
            if (symbol.Opacity.HasValue)
            {
                paint["fill-opacity"] = symbol.Opacity.Value;
            }
            if (!string.IsNullOrWhiteSpace(symbol.StrokeColor))
            {
                paint["fill-outline-color"] = ParseColorToHex(symbol.StrokeColor);
            }
            break;

        case "line":
            if (!string.IsNullOrWhiteSpace(symbol.StrokeColor))
            {
                paint["line-color"] = ParseColorToHex(symbol.StrokeColor);
            }
            if (symbol.StrokeWidth.HasValue)
            {
                paint["line-width"] = symbol.StrokeWidth.Value;
            }
            if (symbol.Opacity.HasValue)
            {
                paint["line-opacity"] = symbol.Opacity.Value;
            }
            break;

        case "circle":
            if (symbol.Size.HasValue)
            {
                paint["circle-radius"] = symbol.Size.Value / 2; // Honua size is diameter
            }
            if (!string.IsNullOrWhiteSpace(symbol.FillColor))
            {
                paint["circle-color"] = ParseColorToHex(symbol.FillColor);
            }
            if (symbol.Opacity.HasValue)
            {
                paint["circle-opacity"] = symbol.Opacity.Value;
            }
            if (!string.IsNullOrWhiteSpace(symbol.StrokeColor))
            {
                paint["circle-stroke-color"] = ParseColorToHex(symbol.StrokeColor);
            }
            if (symbol.StrokeWidth.HasValue)
            {
                paint["circle-stroke-width"] = symbol.StrokeWidth.Value;
            }
            break;
    }

    return paint;
}

private static string ParseColorToHex(string? color)
{
    if (string.IsNullOrWhiteSpace(color))
        return "#000000";

    // Remove alpha channel if present (MapLibre uses opacity properties)
    var hex = color.Trim();
    if (hex.StartsWith('#') && hex.Length == 9)
    {
        return hex[..7]; // Keep only #RRGGBB
    }
    return hex;
}
```

### Phase 2: MapLibre-to-MVP Parser

Add `StyleFormatConverter.ParseMapLibreStyle()` to import MapLibre styles into Honua's MVP format.

```csharp
public static StyleDefinition ParseMapLibreStyle(
    string mapLibreJson,
    string styleId)
{
    using var doc = JsonDocument.Parse(mapLibreJson);
    var root = doc.RootElement;

    // Validate version
    if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 8)
    {
        throw new InvalidOperationException("Only MapLibre Style Spec v8 is supported");
    }

    // Extract layers
    var layers = root.GetProperty("layers");
    if (layers.GetArrayLength() == 0)
    {
        throw new InvalidOperationException("MapLibre style must have at least one layer");
    }

    // For simplicity, convert the first layer
    var firstLayer = layers[0];
    var layerType = firstLayer.GetProperty("type").GetString();
    var geometryType = MapLayerTypeToGeometry(layerType);

    // Determine renderer type based on paint properties
    var paint = firstLayer.GetProperty("paint");
    var colorProperty = GetColorPropertyName(layerType);

    if (paint.TryGetProperty(colorProperty, out var colorValue) &&
        colorValue.ValueKind == JsonValueKind.Array)
    {
        // Data-driven styling - likely unique value
        return ParseUniqueValueStyle(styleId, paint, geometryType, colorProperty);
    }
    else
    {
        // Simple renderer
        return ParseSimpleStyle(styleId, paint, geometryType, layerType);
    }
}

private static string MapLayerTypeToGeometry(string? layerType)
{
    return layerType switch
    {
        "fill" or "fill-extrusion" => "polygon",
        "line" => "line",
        "circle" or "symbol" => "point",
        "raster" or "hillshade" => "raster",
        _ => "polygon"
    };
}
```

### Phase 3: OGC Styles API Integration

Update OGC Styles API handlers to serve MapLibre styles.

```csharp
// OgcStylesHandlers.cs

// New endpoint: GET /ogc/styles/{styleId}?f=maplibre
public static async Task<IResult> GetStyleMapLibre(
    [FromRoute] string styleId,
    [FromServices] IMetadataProvider metadataProvider,
    [FromQuery] string? layerId = null)
{
    var snapshot = await metadataProvider.GetCurrentSnapshotAsync();

    if (!snapshot.TryGetStyle(styleId, out var style))
    {
        return Results.NotFound(new { error = $"Style '{styleId}' not found" });
    }

    // Determine source and layer IDs
    var sourceId = layerId ?? "honua-source";
    var sourceLayer = layerId;

    var mapLibreStyle = StyleFormatConverter.CreateMapLibreStyle(
        style,
        styleId,
        sourceId,
        sourceLayer,
        style.Title
    );

    return Results.Json(mapLibreStyle, statusCode: 200);
}
```

### Phase 4: Enhanced Validation

Improve `StyleValidator.ValidateMapboxStyle()` to validate MapLibre-specific features.

```csharp
public static ValidationResult ValidateMapLibreStyle(string mapLibreJson)
{
    var errors = new List<string>();
    var warnings = new List<string>();

    if (mapLibreJson.IsNullOrWhiteSpace())
    {
        errors.Add("MapLibre style JSON content is empty.");
        return new ValidationResult(errors, warnings);
    }

    try
    {
        using var doc = JsonDocument.Parse(mapLibreJson);
        var root = doc.RootElement;

        // Check version (REQUIRED)
        if (!root.TryGetProperty("version", out var versionElement))
        {
            errors.Add("MapLibre style must include a 'version' property.");
        }
        else if (versionElement.ValueKind == JsonValueKind.Number)
        {
            var version = versionElement.GetInt32();
            if (version != 8)
            {
                warnings.Add($"MapLibre style version {version} may not be fully supported. Expected: 8.");
            }
        }

        // Check sources (REQUIRED)
        if (!root.TryGetProperty("sources", out var sourcesElement))
        {
            errors.Add("MapLibre style must include a 'sources' object.");
        }
        else if (sourcesElement.ValueKind != JsonValueKind.Object)
        {
            errors.Add("MapLibre style 'sources' must be an object.");
        }
        else if (sourcesElement.EnumerateObject().Count() == 0)
        {
            errors.Add("MapLibre style 'sources' object is empty.");
        }

        // Check layers (REQUIRED)
        if (!root.TryGetProperty("layers", out var layersElement))
        {
            errors.Add("MapLibre style must include a 'layers' array.");
        }
        else if (layersElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add("MapLibre style 'layers' must be an array.");
        }
        else if (layersElement.GetArrayLength() == 0)
        {
            errors.Add("MapLibre style 'layers' array is empty.");
        }
        else
        {
            // Validate each layer
            var layerIds = new HashSet<string>();
            foreach (var layer in layersElement.EnumerateArray())
            {
                ValidateMapLibreLayer(layer, layerIds, errors, warnings);
            }
        }

        // Check for name (OPTIONAL but recommended)
        if (!root.TryGetProperty("name", out _))
        {
            warnings.Add("MapLibre style does not include a 'name' property.");
        }
    }
    catch (JsonException ex)
    {
        errors.Add($"Failed to parse MapLibre style JSON: {ex.Message}");
    }

    return new ValidationResult(errors, warnings);
}

private static void ValidateMapLibreLayer(
    JsonElement layer,
    HashSet<string> layerIds,
    List<string> errors,
    List<string> warnings)
{
    // Check required 'id'
    if (!layer.TryGetProperty("id", out var idElement))
    {
        errors.Add("Layer must have an 'id' property.");
    }
    else
    {
        var id = idElement.GetString();
        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add("Layer 'id' cannot be empty.");
        }
        else if (!layerIds.Add(id))
        {
            errors.Add($"Duplicate layer id '{id}'.");
        }
    }

    // Check required 'type'
    if (!layer.TryGetProperty("type", out var typeElement))
    {
        errors.Add("Layer must have a 'type' property.");
    }
    else
    {
        var type = typeElement.GetString();
        var validTypes = new[] { "fill", "line", "circle", "symbol", "raster",
                                 "fill-extrusion", "heatmap", "hillshade", "background" };
        if (!validTypes.Contains(type))
        {
            errors.Add($"Invalid layer type '{type}'. Expected one of: {string.Join(", ", validTypes)}");
        }

        // Background layers don't need source
        if (type != "background")
        {
            if (!layer.TryGetProperty("source", out _))
            {
                errors.Add($"Layer of type '{type}' must have a 'source' property.");
            }
        }
    }

    // Warn if missing paint or layout
    if (!layer.TryGetProperty("paint", out _) && !layer.TryGetProperty("layout", out _))
    {
        warnings.Add("Layer has neither 'paint' nor 'layout' properties.");
    }
}
```

## Testing Strategy

### Unit Tests

```csharp
// StyleFormatConverterTests.cs

[Fact]
public void CreateMapLibreStyle_SimplePolygon_GeneratesFillLayer()
{
    var style = new StyleDefinition
    {
        Id = "test-style",
        Title = "Test Style",
        Renderer = "simple",
        GeometryType = "polygon",
        Simple = new SimpleStyleDefinition
        {
            FillColor = "#4A90E2FF",
            StrokeColor = "#1F364DFF",
            StrokeWidth = 1.5,
            Opacity = 0.8
        }
    };

    var result = StyleFormatConverter.CreateMapLibreStyle(
        style, "test-layer", "test-source");

    result.Should().NotBeNull();
    result["version"]!.GetValue<int>().Should().Be(8);

    var layers = result["layers"]!.AsArray();
    layers.Should().HaveCount(1);

    var layer = layers[0]!.AsObject();
    layer["type"]!.GetValue<string>().Should().Be("fill");
    layer["source"]!.GetValue<string>().Should().Be("test-source");

    var paint = layer["paint"]!.AsObject();
    paint["fill-color"]!.GetValue<string>().Should().Be("#4A90E2");
    paint["fill-opacity"]!.GetValue<double>().Should().Be(0.8);
    paint["fill-outline-color"]!.GetValue<string>().Should().Be("#1F364D");
}

[Fact]
public void CreateMapLibreStyle_UniqueValue_GeneratesMatchExpression()
{
    var style = new StyleDefinition
    {
        Id = "zoning-style",
        Renderer = "uniqueValue",
        GeometryType = "polygon",
        UniqueValue = new UniqueValueStyleDefinition
        {
            Field = "zoning",
            Classes = new List<UniqueValueClass>
            {
                new() { Value = "R1", Symbol = new() { FillColor = "#FFFFE0" } },
                new() { Value = "C1", Symbol = new() { FillColor = "#FFB6C1" } }
            },
            DefaultSymbol = new() { FillColor = "#E0E0E0" }
        }
    };

    var result = StyleFormatConverter.CreateMapLibreStyle(
        style, "zoning", "parcels");

    var layers = result["layers"]!.AsArray();
    var layer = layers[0]!.AsObject();
    var paint = layer["paint"]!.AsObject();

    var fillColor = paint["fill-color"]!.AsArray();
    fillColor[0]!.GetValue<string>().Should().Be("match");
    // Verify match expression structure
}
```

### Integration Tests

```csharp
[Fact]
public async Task OgcStylesApi_MapLibreFormat_ReturnsValidStyle()
{
    var response = await _client.GetAsync(
        "/ogc/styles/parcels-standard?f=maplibre");

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var json = await response.Content.ReadAsStringAsync();
    var style = JsonDocument.Parse(json);

    style.RootElement.GetProperty("version").GetInt32().Should().Be(8);
    style.RootElement.GetProperty("layers").GetArrayLength().Should().BeGreaterThan(0);
}
```

## API Changes

### New Query Parameter

OGC Styles API will support `f=maplibre`:

```
GET /ogc/styles/{styleId}?f=maplibre
```

Returns:
```json
{
  "version": 8,
  "name": "Parcels Standard",
  "sources": {
    "honua-source": {
      "type": "vector",
      "tiles": [""]
    }
  },
  "layers": [
    {
      "id": "parcels-standard",
      "type": "fill",
      "source": "honua-source",
      "paint": {
        "fill-color": "#FFFFCC",
        "fill-opacity": 0.502,
        "fill-outline-color": "#666666"
      }
    }
  ]
}
```

## Documentation Updates

### User Guide

Add to `/docs/rag/03-architecture/map-styling.md`:

```markdown
## MapLibre Style Export

Honua can export styles in MapLibre Style Specification v8 format for use with MapLibre GL JS and compatible clients.

### Requesting MapLibre Styles

Use the `f=maplibre` format parameter:

```
GET /ogc/styles/{styleId}?f=maplibre
```

### Client Integration

```javascript
import maplibregl from 'maplibre-gl';

// Fetch style from Honua
const styleUrl = 'https://honua.example.com/ogc/styles/my-style?f=maplibre';

const map = new maplibregl.Map({
  container: 'map',
  style: styleUrl,
  center: [-122.4, 37.8],
  zoom: 12
});
```
```

## Benefits

1. **Industry Standard** - MapLibre Style Spec is the de-facto standard for modern web mapping
2. **Client Compatibility** - Works with MapLibre GL JS, Mapbox GL JS, and other compatible renderers
3. **Vector Tile Support** - Natural pairing with Honua's MVT (Mapbox Vector Tiles) generation
4. **Expression Support** - Data-driven styling with MapLibre expressions
5. **Bidirectional** - Import existing MapLibre styles or export Honua styles

## Implementation Timeline

- **Week 1**: Phase 1 - MVP-to-MapLibre converter
- **Week 2**: Phase 2 - MapLibre-to-MVP parser
- **Week 3**: Phase 3 - OGC API integration
- **Week 4**: Phase 4 - Enhanced validation + testing + documentation

## Open Questions

1. **Sources Configuration**: Should we auto-populate the `tiles` URL in the sources object, or leave it for the client?
2. **Multiple Layers**: How should we handle MVP styles with multiple rules? One MapLibre style with multiple layers, or separate style endpoints?
3. **Expressions**: Should we support full MapLibre expression syntax in imports, or just simple cases?
4. **Sprites**: Should we support sprite sheet generation for custom icons referenced in MVP styles?

## References

- [MapLibre Style Specification](https://maplibre.org/maplibre-style-spec/)
- [MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/)
- [Mapbox Vector Tiles](https://docs.mapbox.com/data/tilesets/guides/vector-tiles-standards/)
- [Honua MVT Implementation](src/Honua.Server.Core/Data/Postgres/PostgresVectorTileGenerator.cs)
