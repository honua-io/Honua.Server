# AI-Assisted Map Creation & Styling Guide

**Purpose:** Enable AI agents (DevSecOps, Automation) to create, style, and configure maps in Honua.Server
**Audience:** AI assistants, automation systems, developers
**Version:** 1.0
**Date:** 2025-11-11

---

## Table of Contents

1. [Introduction](#introduction)
2. [Cartographic Principles](#cartographic-principles)
3. [Map Creation Workflow](#map-creation-workflow)
4. [Styling & Visualization](#styling--visualization)
5. [Data Source Configuration](#data-source-configuration)
6. [Component Configuration](#component-configuration)
7. [Automation Templates](#automation-templates)
8. [Best Practices](#best-practices)

---

## Introduction

### Purpose of This Guide

This guide provides AI agents with the knowledge to:
- **Create maps** from scratch based on requirements
- **Style layers** with appropriate symbology
- **Configure data sources** (WFS, WMS, GeoJSON, gRPC)
- **Set up visualizations** (heatmaps, clusters, 3D)
- **Apply cartographic best practices**
- **Generate complete map configurations** programmatically

### Target AI Capabilities

An AI reading this guide should be able to:
1. ✅ Parse user requirements (e.g., "Show property parcels in San Francisco")
2. ✅ Select appropriate data sources
3. ✅ Choose visualization methods (fill, symbol, heatmap, etc.)
4. ✅ Generate color schemes based on data attributes
5. ✅ Configure UI components (legend, filters, grids)
6. ✅ Output complete Honua Map Document (HMD) JSON
7. ✅ Deploy the map to Honua.Server

---

## Cartographic Principles

### Core Cartographic Concepts

#### 1. **Visual Hierarchy**

**Principle:** More important features should be more visually prominent.

**Implementation:**
- Use **size**: Larger symbols = more important
- Use **color**: Bright/saturated = more important, muted = less
- Use **z-order**: Important layers on top

**Example:**
```json
// Cities (important) - large, bright
{
  "type": "circle",
  "paint": {
    "circle-radius": 8,
    "circle-color": "#FF6B6B"
  }
}

// Towns (less important) - smaller, muted
{
  "type": "circle",
  "paint": {
    "circle-radius": 4,
    "circle-color": "#A0A0A0"
  }
}
```

#### 2. **Figure-Ground Relationship**

**Principle:** Data (figure) should stand out from the basemap (ground).

**Implementation:**
- **Basemap**: Muted colors, low contrast
- **Data layers**: Vibrant colors, high contrast
- **Background**: Neutral (white, light gray, dark gray)

**Recommended Basemap Styles:**
- **Light theme**: Use light gray or white basemap
- **Dark theme**: Use dark gray or black basemap
- **Data-focused**: Use monochrome basemap to emphasize data

#### 3. **Color Theory**

**Sequential Colors** (for ordered data: low → high)
- Use single-hue progression: light → dark
- Example: Population density (light blue → dark blue)

**Diverging Colors** (for data with meaningful midpoint)
- Use two hues meeting at neutral
- Example: Temperature change (blue ← white → red)

**Categorical Colors** (for unordered categories)
- Use distinct, visually balanced hues
- Maximum 7-12 categories for readability

**Recommended Palettes:**
```javascript
// Sequential (quantitative data)
const sequential = ["#ffffcc", "#c7e9b4", "#7fcdbb", "#41b6c4", "#2c7fb8", "#253494"];

// Diverging (e.g., change from baseline)
const diverging = ["#d7191c", "#fdae61", "#ffffbf", "#abd9e9", "#2c7bb6"];

// Categorical (land use, etc.)
const categorical = ["#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#ffff33"];

// ColorBrewer palettes (gold standard)
// https://colorbrewer2.org/
```

#### 4. **Symbol Design**

**Point Symbols:**
- **Simple shapes**: Circle, square, triangle for categories
- **Proportional symbols**: Size based on attribute value
- **Icons**: Use for specific feature types (airport, hospital)

**Line Symbols:**
- **Width**: Thicker = more important (highways vs. local roads)
- **Style**: Solid, dashed, dotted for categories
- **Color**: Match semantic meaning (blue = rivers, red = highways)

**Polygon Symbols:**
- **Fill**: Color based on attribute
- **Outline**: Use subtle outlines (1-2px) to define boundaries
- **Transparency**: Use 60-80% opacity for overlays

#### 5. **Label Placement**

**Principles:**
- **Readability**: Sufficient contrast with background
- **Placement**: Above/right of points, along lines, inside polygons
- **Hierarchy**: Larger text for more important features
- **Collision**: Avoid overlapping labels

**Font Recommendations:**
- **Sans-serif** (modern, clean): Roboto, Open Sans, Lato
- **Serif** (traditional): Merriweather, Noto Serif
- **Monospace** (data): Roboto Mono, Source Code Pro

---

## Map Creation Workflow

### Step-by-Step Process

#### Step 1: Parse User Requirements

**Input examples:**
- "Show property parcels in San Francisco with property values"
- "Create a heatmap of crime incidents in Chicago"
- "Display WFS layers from GeoServer with filtering"

**Extract:**
1. **Geographic extent**: San Francisco, Chicago, etc.
2. **Data type**: Parcels, crime, WFS layers
3. **Visualization type**: Choropleth, heatmap, simple features
4. **Attributes**: Property values, crime type, etc.
5. **Interactivity**: Filtering, popups, analysis

#### Step 2: Determine Data Sources

**Decision tree:**

```
Is the data already in Honua.Server?
├─ YES → Use grpc://api.honua.io/{dataset}
└─ NO
   ├─ Is it a WFS/WMS service?
   │  └─ YES → Configure OGC source
   ├─ Is it a public API (GeoJSON)?
   │  └─ YES → Configure HTTP GeoJSON source
   ├─ Is it a file upload?
   │  └─ YES → Import via HonuaImportWizard, then use grpc://
   └─ Is it generated data?
      └─ YES → Create synthetic data, store in Honua
```

**Data source template:**

```json
{
  "id": "parcels-source",
  "type": "grpc|wfs|wms|geojson|vector-tiles",
  "url": "grpc://api.honua.io/parcels",
  "authentication": {
    "type": "bearer|basic|api-key|none",
    "tokenUrl": "https://auth.honua.io/token"
  },
  "cache": {
    "enabled": true,
    "ttl": 3600
  }
}
```

#### Step 3: Choose Visualization Method

**Decision matrix:**

| Data Type | Geometry | Visualization | Use Case |
|-----------|----------|---------------|----------|
| **Categorical** | Point | Simple symbol | City types (capital, town) |
| **Categorical** | Point | Icon | POI (hospital, school) |
| **Quantitative** | Point | Proportional circle | Population by city |
| **Quantitative** | Point | Heatmap | Crime density |
| **Quantitative** | Point | Clustering | Many points (10k+) |
| **Categorical** | Line | Color-coded lines | Road types |
| **Quantitative** | Line | Width-based lines | Traffic volume |
| **Categorical** | Polygon | Simple fill | Land use categories |
| **Quantitative** | Polygon | Choropleth | Population density |
| **Quantitative** | Polygon | 3D extrusion | Building heights |

#### Step 4: Generate Styling

**For quantitative data (e.g., property values):**

```javascript
// 1. Determine data range
const min = 100000;
const max = 5000000;

// 2. Choose classification method
const method = "quantile"; // or "equal-interval", "natural-breaks", "jenks"

// 3. Generate breaks
const breaks = classifyData(data, method, 5); // 5 classes

// 4. Assign colors (sequential palette)
const colors = ["#ffffcc", "#a1dab4", "#41b6c4", "#2c7fb8", "#253494"];

// 5. Create MapLibre expression
const fillColor = [
  "step",
  ["get", "value"],
  colors[0],
  breaks[0], colors[1],
  breaks[1], colors[2],
  breaks[2], colors[3],
  breaks[3], colors[4]
];
```

**Output HMD layer:**

```json
{
  "id": "parcels-layer",
  "name": "Property Parcels",
  "sourceId": "parcels-source",
  "type": "fill",
  "visible": true,
  "opacity": 0.7,
  "paint": {
    "fill-color": [
      "step",
      ["get", "value"],
      "#ffffcc",
      500000, "#a1dab4",
      1000000, "#41b6c4",
      2000000, "#2c7fb8",
      3000000, "#253494"
    ],
    "fill-outline-color": "#333333"
  },
  "popupTemplate": {
    "title": "Parcel {parcel_id}",
    "content": "Owner: {owner}<br>Value: ${value:NumberFormat}"
  }
}
```

**For categorical data (e.g., land use):**

```json
{
  "paint": {
    "fill-color": [
      "match",
      ["get", "land_use"],
      "residential", "#FFEB3B",
      "commercial", "#FF5722",
      "industrial", "#9E9E9E",
      "agricultural", "#4CAF50",
      "open_space", "#8BC34A",
      "#CCCCCC" // default
    ]
  }
}
```

#### Step 5: Configure Components

**Always include:**
1. **Legend** - Explain symbology
2. **Popup** - Show feature details
3. **Layer list** - Toggle layers

**Optional based on use case:**
4. **Data grid** - For tabular view
5. **Chart** - For statistical analysis
6. **Filter panel** - For data exploration
7. **Timeline** - For temporal data
8. **Measurement tools** - For spatial analysis

**Example component config:**

```json
{
  "components": {
    "legend": {
      "enabled": true,
      "position": "bottom-right",
      "collapsible": true
    },
    "dataGrid": {
      "enabled": true,
      "syncWith": "map1",
      "columns": ["parcel_id", "owner", "value"],
      "pageSize": 50
    },
    "filterPanel": {
      "enabled": true,
      "filters": [
        {
          "field": "value",
          "type": "range",
          "min": 0,
          "max": 5000000,
          "label": "Property Value"
        }
      ]
    }
  }
}
```

#### Step 6: Set Initial Viewport

**Best practices:**
- **Center**: Geographic centroid of data
- **Zoom**: Fit all features with padding
- **Projection**: Mercator (default) or Globe (for global data)

**Calculate viewport:**

```javascript
// Given bounding box of data
const bbox = {
  west: -122.5,
  south: 37.7,
  east: -122.3,
  north: 37.9
};

// Calculate center
const center = [
  (bbox.west + bbox.east) / 2,
  (bbox.south + bbox.north) / 2
];

// Calculate zoom (approximation)
const latDiff = bbox.north - bbox.south;
const lngDiff = bbox.east - bbox.west;
const maxDiff = Math.max(latDiff, lngDiff);
const zoom = Math.floor(Math.log2(360 / maxDiff)) - 1;

// Output
{
  "viewport": {
    "center": center,
    "zoom": zoom,
    "bearing": 0,
    "pitch": 0
  }
}
```

#### Step 7: Generate Complete HMD

**Assemble all pieces:**

```json
{
  "$schema": "https://honua.io/schemas/map/v1.0.json",
  "honuaVersion": "1.0",
  "specVersion": "1.0",

  "metadata": {
    "id": "uuid",
    "name": "San Francisco Property Parcels",
    "description": "Property values and ownership in SF",
    "tags": ["parcels", "san-francisco", "real-estate"],
    "created": "2025-11-11T12:00:00Z",
    "createdBy": "ai-agent@honua.io"
  },

  "viewport": { /* from step 6 */ },
  "maplibreStyle": { /* basemap style */ },
  "dataSources": [ /* from step 2 */ ],
  "operationalLayers": [ /* from step 4 */ ],
  "components": { /* from step 5 */ },
  "bookmarks": [],
  "spatialReference": { "wkid": 3857 }
}
```

---

## Styling & Visualization

### Common Styling Patterns

#### Pattern 1: Choropleth Map

**Use case:** Show quantitative attribute across polygons

```json
{
  "type": "fill",
  "paint": {
    "fill-color": [
      "interpolate",
      ["linear"],
      ["get", "population_density"],
      0, "#ffffcc",
      100, "#c7e9b4",
      500, "#7fcdbb",
      1000, "#41b6c4",
      5000, "#253494"
    ],
    "fill-opacity": 0.7,
    "fill-outline-color": "#666666"
  }
}
```

#### Pattern 2: Proportional Symbols

**Use case:** Show magnitude with circle size

```json
{
  "type": "circle",
  "paint": {
    "circle-radius": [
      "interpolate",
      ["linear"],
      ["get", "population"],
      1000, 4,
      10000, 8,
      100000, 16,
      1000000, 32
    ],
    "circle-color": "#FF6B6B",
    "circle-opacity": 0.6,
    "circle-stroke-color": "#FFFFFF",
    "circle-stroke-width": 1
  }
}
```

#### Pattern 3: Heatmap

**Use case:** Show point density

```json
{
  "type": "heatmap",
  "paint": {
    "heatmap-weight": [
      "interpolate",
      ["linear"],
      ["get", "magnitude"],
      0, 0,
      6, 1
    ],
    "heatmap-intensity": 1,
    "heatmap-color": [
      "interpolate",
      ["linear"],
      ["heatmap-density"],
      0, "rgba(0, 0, 255, 0)",
      0.1, "royalblue",
      0.3, "cyan",
      0.5, "lime",
      0.7, "yellow",
      1, "red"
    ],
    "heatmap-radius": 30,
    "heatmap-opacity": 0.8
  }
}
```

#### Pattern 4: 3D Extrusion

**Use case:** Show building heights or data magnitude in 3D

```json
{
  "type": "fill-extrusion",
  "paint": {
    "fill-extrusion-color": [
      "interpolate",
      ["linear"],
      ["get", "height"],
      0, "#ffffcc",
      50, "#41b6c4",
      100, "#253494"
    ],
    "fill-extrusion-height": ["get", "height"],
    "fill-extrusion-base": 0,
    "fill-extrusion-opacity": 0.8
  }
}
```

#### Pattern 5: Data-Driven Line Width

**Use case:** Road network with traffic volume

```json
{
  "type": "line",
  "paint": {
    "line-color": [
      "step",
      ["get", "traffic_volume"],
      "#4CAF50",
      1000, "#FFC107",
      5000, "#FF5722"
    ],
    "line-width": [
      "interpolate",
      ["exponential", 1.5],
      ["zoom"],
      10, [
        "case",
        [">", ["get", "traffic_volume"], 5000], 4,
        [">", ["get", "traffic_volume"], 1000], 2,
        1
      ],
      16, [
        "case",
        [">", ["get", "traffic_volume"], 5000], 12,
        [">", ["get", "traffic_volume"], 1000], 6,
        3
      ]
    ]
  }
}
```

### Accessibility Considerations

#### Color Blindness

**Protanopia/Deuteranopia (red-green):**
- ❌ Avoid red-green color schemes
- ✅ Use blue-orange or purple-green instead

**Tritanopia (blue-yellow):**
- ❌ Avoid blue-yellow color schemes
- ✅ Use red-cyan or purple-orange instead

**Safe palette (ColorBrewer):**
```javascript
// Safe for all types of color blindness
const colorBlindSafe = [
  "#000000", // Black
  "#E69F00", // Orange
  "#56B4E9", // Sky Blue
  "#009E73", // Bluish Green
  "#F0E442", // Yellow
  "#0072B2", // Blue
  "#D55E00", // Vermillion
  "#CC79A7"  // Reddish Purple
];
```

#### High Contrast Mode

**Ensure sufficient contrast:**
- **Text on light**: Use dark colors (contrast ratio ≥ 4.5:1)
- **Text on dark**: Use light colors
- **Symbols**: Use outlines for visibility

**Example:**
```json
{
  "circle-color": "#FF6B6B",
  "circle-stroke-color": "#FFFFFF", // White outline
  "circle-stroke-width": 2 // Thick enough to see
}
```

---

## Data Source Configuration

### gRPC Data Sources (Honua Native)

**Best for:** Honua-hosted datasets, high-performance queries

```json
{
  "id": "parcels-grpc",
  "type": "grpc",
  "url": "grpc://api.honua.io/datasets/sf-parcels",
  "authentication": {
    "type": "bearer",
    "tokenUrl": "https://auth.honua.io/token"
  },
  "streaming": true,
  "cache": {
    "enabled": true,
    "ttl": 1800
  },
  "fields": ["parcel_id", "owner", "value", "land_use", "geometry"],
  "spatialReference": 3857
}
```

### WFS Data Sources

**Best for:** OGC-compliant servers (GeoServer, MapServer, QGIS Server)

```json
{
  "id": "buildings-wfs",
  "type": "wfs",
  "url": "https://geoserver.example.com/wfs",
  "version": "2.0.0",
  "typeName": "buildings:sf_buildings",
  "outputFormat": "application/json",
  "srsName": "EPSG:3857",
  "maxFeatures": 10000,
  "bbox": [-122.5, 37.7, -122.3, 37.9],
  "cql_filter": "height > 50" // Optional filtering
}
```

### WMS Data Sources

**Best for:** Pre-rendered map images, fast display

```json
{
  "id": "imagery-wms",
  "type": "wms",
  "url": "https://wms.example.com/service",
  "version": "1.3.0",
  "layers": ["ortho_imagery"],
  "styles": ["default"],
  "format": "image/png",
  "transparent": true,
  "srs": "EPSG:3857"
}
```

### GeoJSON Data Sources

**Best for:** Small datasets, APIs, static files

```json
{
  "id": "neighborhoods-geojson",
  "type": "geojson",
  "url": "https://api.example.com/neighborhoods.geojson",
  "authentication": {
    "type": "api-key",
    "key": "YOUR_API_KEY",
    "header": "X-API-Key"
  },
  "refresh": 60000 // Refresh every 60 seconds (real-time)
}
```

### Vector Tile Sources

**Best for:** Large datasets, fast rendering

```json
{
  "id": "osm-tiles",
  "type": "vector",
  "tiles": [
    "https://tiles.example.com/data/{z}/{x}/{y}.pbf"
  ],
  "minzoom": 0,
  "maxzoom": 14
}
```

---

## Component Configuration

### Legend Configuration

**Automatic legend generation:**

```json
{
  "legend": {
    "enabled": true,
    "position": "bottom-right",
    "title": "Property Values",
    "collapsible": true,
    "items": [
      {
        "type": "gradient",
        "label": "Property Value",
        "gradient": ["#ffffcc", "#253494"],
        "min": "$100,000",
        "max": "$5,000,000"
      }
    ]
  }
}
```

### Data Grid Configuration

**Synced with map:**

```json
{
  "dataGrid": {
    "enabled": true,
    "syncWith": "map1",
    "title": "Property Data",
    "columns": [
      { "field": "parcel_id", "header": "Parcel ID", "width": "100px" },
      { "field": "owner", "header": "Owner", "width": "200px" },
      { "field": "value", "header": "Value", "width": "120px", "format": "currency" },
      { "field": "land_use", "header": "Land Use", "width": "150px" }
    ],
    "enableSelection": true,
    "enableSorting": true,
    "enableFiltering": true,
    "pageSize": 50,
    "exportFormats": ["csv", "json", "geojson"]
  }
}
```

### Chart Configuration

**Histogram of values:**

```json
{
  "chart": {
    "type": "histogram",
    "title": "Property Value Distribution",
    "field": "value",
    "bins": 20,
    "syncWith": "map1",
    "colorScheme": "blues",
    "enableFilter": true,
    "height": "300px"
  }
}
```

### Filter Panel Configuration

**Multi-criteria filtering:**

```json
{
  "filterPanel": {
    "enabled": true,
    "position": "left",
    "title": "Filter Properties",
    "filters": [
      {
        "field": "value",
        "type": "range",
        "label": "Property Value",
        "min": 0,
        "max": 10000000,
        "defaultValue": [0, 1000000],
        "format": "currency"
      },
      {
        "field": "land_use",
        "type": "multiselect",
        "label": "Land Use",
        "options": ["residential", "commercial", "industrial", "agricultural"],
        "defaultValue": []
      },
      {
        "field": "sale_date",
        "type": "date-range",
        "label": "Sale Date",
        "min": "2020-01-01",
        "max": "2025-12-31"
      }
    ]
  }
}
```

---

## Automation Templates

### Template 1: Simple Feature Map

**Use case:** Display GeoJSON features with popups

```javascript
function createSimpleFeatureMap(options) {
  const { name, dataUrl, geometryType, centerField, colorField } = options;

  return {
    "$schema": "https://honua.io/schemas/map/v1.0.json",
    "metadata": {
      "id": generateUuid(),
      "name": name,
      "description": `Simple ${geometryType} map`,
      "createdBy": "ai-agent@honua.io"
    },
    "viewport": {
      "center": [0, 0], // Will be calculated from data
      "zoom": 2
    },
    "dataSources": [{
      "id": "main-source",
      "type": "geojson",
      "url": dataUrl
    }],
    "operationalLayers": [{
      "id": "main-layer",
      "name": name,
      "sourceId": "main-source",
      "type": geometryType === "Point" ? "circle" : geometryType === "LineString" ? "line" : "fill",
      "visible": true,
      "paint": generatePaint(geometryType, colorField),
      "popupTemplate": {
        "title": `{${centerField}}`,
        "content": "Click for details"
      }
    }],
    "components": {
      "legend": { "enabled": true },
      "dataGrid": {
        "enabled": true,
        "syncWith": "map1"
      }
    }
  };
}
```

### Template 2: Choropleth Map

**Use case:** Thematic map with data classification

```javascript
function createChoroplethMap(options) {
  const { name, dataUrl, valueField, breaks, colors } = options;

  const fillColor = [
    "step",
    ["get", valueField],
    colors[0],
    ...breaks.flatMap((b, i) => [b, colors[i + 1]])
  ];

  return {
    // ... metadata, viewport, dataSources
    "operationalLayers": [{
      "id": "choropleth-layer",
      "type": "fill",
      "paint": {
        "fill-color": fillColor,
        "fill-opacity": 0.7,
        "fill-outline-color": "#333333"
      }
    }],
    "components": {
      "legend": {
        "enabled": true,
        "items": breaks.map((b, i) => ({
          "label": i === 0 ? `< ${b}` : i === breaks.length ? `≥ ${breaks[i-1]}` : `${breaks[i-1]} - ${b}`,
          "color": colors[i]
        }))
      }
    }
  };
}
```

### Template 3: Heatmap

**Use case:** Point density visualization

```javascript
function createHeatmap(options) {
  const { name, dataUrl, weightField } = options;

  return {
    // ... metadata, viewport, dataSources
    "operationalLayers": [{
      "id": "heatmap-layer",
      "type": "heatmap",
      "paint": {
        "heatmap-weight": weightField ? ["get", weightField] : 1,
        "heatmap-intensity": 1,
        "heatmap-color": [
          "interpolate",
          ["linear"],
          ["heatmap-density"],
          0, "rgba(0, 0, 255, 0)",
          0.2, "royalblue",
          0.4, "cyan",
          0.6, "lime",
          0.8, "yellow",
          1, "red"
        ],
        "heatmap-radius": 30,
        "heatmap-opacity": 0.8
      }
    }],
    "components": {
      "legend": {
        "enabled": true,
        "type": "gradient",
        "gradient": ["blue", "cyan", "lime", "yellow", "red"],
        "label": "Density"
      }
    }
  };
}
```

### Template 4: 3D Building Map

**Use case:** 3D visualization with extrusion

```javascript
function create3DBuildingMap(options) {
  const { name, dataUrl, heightField } = options;

  return {
    // ... metadata
    "viewport": {
      "center": [-122.4, 37.8],
      "zoom": 15,
      "pitch": 60, // 3D perspective
      "bearing": -17.6
    },
    // ... dataSources
    "operationalLayers": [{
      "id": "buildings-3d",
      "type": "fill-extrusion",
      "paint": {
        "fill-extrusion-color": "#aaa",
        "fill-extrusion-height": ["get", heightField],
        "fill-extrusion-base": 0,
        "fill-extrusion-opacity": 0.8
      }
    }]
  };
}
```

---

## Best Practices

### 1. Performance Optimization

**For large datasets (>10,000 features):**
- ✅ Use clustering for points
- ✅ Use vector tiles for polygons/lines
- ✅ Implement server-side filtering
- ✅ Set appropriate `minZoom`/`maxZoom`

**Example:**
```json
{
  "cluster": {
    "enabled": true,
    "clusterRadius": 50,
    "clusterMaxZoom": 14
  },
  "minZoom": 8 // Don't show below zoom 8
}
```

### 2. Color Selection

**Test colors for accessibility:**
- ✅ Use tools like ColorBrewer, Adobe Color
- ✅ Check contrast ratios (WCAG AA: 4.5:1)
- ✅ Test with color blindness simulators
- ✅ Provide alternative encodings (size, pattern)

### 3. Popup Design

**Effective popups:**
- ✅ Show 3-5 key attributes
- ✅ Format numbers (currency, percentages)
- ✅ Use icons for visual interest
- ✅ Include links to detailed views

**Example:**
```html
<div class="popup">
  <h3>{name}</h3>
  <p><strong>Population:</strong> {population:NumberFormat}</p>
  <p><strong>Area:</strong> {area:DecimalFormat(2)} km²</p>
  <a href="/details/{id}">View Details</a>
</div>
```

### 4. Map Metadata

**Always include:**
- ✅ Descriptive name
- ✅ Purpose/description
- ✅ Tags for searchability
- ✅ Data source citations
- ✅ Creation date and author

### 5. Testing

**Before deploying:**
- ✅ Test at different zoom levels
- ✅ Test with different screen sizes
- ✅ Verify data loads correctly
- ✅ Check popup rendering
- ✅ Test component synchronization
- ✅ Validate JSON against schema

---

## AI Agent Workflow Example

### Full End-to-End Example

**User Request:**
> "Create a map showing San Francisco neighborhoods colored by median household income"

**AI Agent Process:**

```python
# Step 1: Parse requirements
requirements = {
    "location": "San Francisco",
    "data_type": "neighborhoods",
    "attribute": "median_household_income",
    "visualization": "choropleth"
}

# Step 2: Find/prepare data
data_source = find_dataset("sf_neighborhoods") or import_dataset(
    "https://data.sfgov.org/api/geospatial/neighborhoods.geojson"
)

# Step 3: Analyze data
stats = analyze_field(data_source, "median_household_income")
# { min: 45000, max: 250000, mean: 102000, std: 55000 }

# Step 4: Classify data
breaks = classify(stats, method="quantile", classes=5)
# [60000, 85000, 105000, 135000]

# Step 5: Generate color scheme
colors = generate_sequential_colors(5, "YlGnBu")
# ["#ffffcc", "#a1dab4", "#41b6c4", "#2c7fb8", "#253494"]

# Step 6: Calculate viewport
bbox = get_bbox(data_source)
viewport = calculate_viewport(bbox)

# Step 7: Generate HMD
hmd = {
    "$schema": "https://honua.io/schemas/map/v1.0.json",
    "metadata": {
        "id": generate_uuid(),
        "name": "SF Neighborhoods by Income",
        "description": "Median household income by neighborhood in San Francisco",
        "tags": ["san-francisco", "demographics", "income"],
        "createdBy": "ai-agent@honua.io"
    },
    "viewport": viewport,
    "dataSources": [{
        "id": "neighborhoods-source",
        "type": "grpc",
        "url": f"grpc://api.honua.io/datasets/{data_source.id}"
    }],
    "operationalLayers": [{
        "id": "income-choropleth",
        "name": "Median Household Income",
        "sourceId": "neighborhoods-source",
        "type": "fill",
        "visible": True,
        "opacity": 0.7,
        "paint": {
            "fill-color": [
                "step",
                ["get", "median_household_income"],
                colors[0],
                breaks[0], colors[1],
                breaks[1], colors[2],
                breaks[2], colors[3],
                breaks[3], colors[4]
            ],
            "fill-outline-color": "#333333"
        },
        "popupTemplate": {
            "title": "{neighborhood_name}",
            "content": "Median Income: ${median_household_income:NumberFormat}"
        }
    }],
    "components": {
        "legend": {
            "enabled": True,
            "title": "Median Household Income",
            "items": generate_legend_items(breaks, colors)
        },
        "dataGrid": {
            "enabled": True,
            "syncWith": "map1",
            "columns": ["neighborhood_name", "median_household_income", "population"]
        }
    }
}

# Step 8: Validate
validate_hmd(hmd)

# Step 9: Save to Honua
map_id = save_map(hmd)

# Step 10: Deploy
deploy_map(map_id)

# Return URL to user
return f"https://maps.honua.io/view/{map_id}"
```

---

## Conclusion

This guide enables AI agents to:
- ✅ Create maps programmatically from user requirements
- ✅ Apply cartographic best practices
- ✅ Generate appropriate styling and symbology
- ✅ Configure data sources and components
- ✅ Output complete, valid Honua Map Documents
- ✅ Deploy maps to Honua.Server

**Next Steps for AI Implementation:**
1. Train AI on this guide + example maps
2. Provide access to Honua API for data queries
3. Enable HMD generation and validation
4. Implement feedback loop for map quality

---

**Questions?** Contact the Honua AI/ML team.
