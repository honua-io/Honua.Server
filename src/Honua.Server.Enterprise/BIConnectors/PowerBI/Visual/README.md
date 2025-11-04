# Honua Kepler.gl Power BI Visual

Advanced geospatial visualization custom visual for Power BI using Kepler.gl.

## Features

- 🗺️ **Advanced Mapping** - Powered by Uber's Kepler.gl
- 🎨 **Multiple Layer Types** - Point, Hexagon, Arc, Line, GeoJSON, Heatmap, Cluster
- 🌍 **4+ Map Styles** - Dark, Light, Muted, Satellite
- 📊 **3D Visualization** - Elevation and extrusion support
- ⏱️ **Temporal Animation** - Time-based filtering and animation
- 🎯 **Interactive** - Pan, zoom, rotate, tooltips, and filtering
- 📱 **Responsive** - Works on desktop and mobile
- 🎨 **Customizable** - Color scales, opacity, point sizes

## Why Kepler.gl?

Power BI's built-in map visuals are limited to point data. Kepler.gl enables:

- **Complex Geometries**: Polygons, lines, arcs, and multi-geometries
- **Large Datasets**: Handle millions of points efficiently
- **Advanced Styling**: Gradient colors, elevation, radius by data
- **Spatial Analysis**: Hexagon aggregation, clustering, heatmaps
- **Temporal Viz**: Animate data over time

## Requirements

- Power BI Desktop (latest version)
- Power BI Visuals Tools (for development)
- Node.js 16+ (for building)
- Mapbox Access Token (for satellite imagery)

## Installation

### Option 1: Install from AppSource (Recommended)

1. Open Power BI Desktop
2. Go to **Visualizations** pane
3. Click **Get more visuals** (...)
4. Search for "Honua Kepler Map"
5. Click **Add**

### Option 2: Import .pbiviz File

1. Download the latest `HonuaKeplerMap.pbiviz` from releases
2. In Power BI Desktop, go to **Visualizations** pane
3. Click **Import a visual from a file** (...)
4. Browse to the `.pbiviz` file
5. Click **OK**

### Option 3: Build from Source

#### Prerequisites

```bash
# Install Power BI Visuals Tools
npm install -g powerbi-visuals-tools

# Verify installation
pbiviz --version
```

#### Build Steps

```bash
# Navigate to Visual directory
cd src/Honua.Server.Enterprise/BIConnectors/PowerBI/Visual

# Install dependencies
npm install

# Create a development certificate (first time only)
pbiviz --install-cert

# Start dev server
npm start

# Or package for distribution
npm run package
```

## Usage

### 1. Add Visual to Report

1. Click the **Honua Kepler.gl Map** icon in Visualizations pane
2. Resize the visual on the canvas

### 2. Bind Data

Drag fields to these data roles:

| Data Role | Type | Required | Description |
|-----------|------|----------|-------------|
| **Latitude** | Measure | ✅ | Latitude coordinate (-90 to 90) |
| **Longitude** | Measure | ✅ | Longitude coordinate (-180 to 180) |
| **Category** | Dimension | ❌ | Grouping field (e.g., city, region) |
| **Size** | Measure | ❌ | Point size or aggregation value |
| **Color** | Measure | ❌ | Color intensity value |
| **Tooltip** | Any | ❌ | Additional tooltip fields |
| **Time** | DateTime | ❌ | For temporal filtering and animation |

**Example:**
```
Latitude: [GPS_Latitude]
Longitude: [GPS_Longitude]
Category: [City]
Size: [Population]
Color: [Temperature]
Time: [Timestamp]
```

### 3. Configure Visual

Click on the visual, then use the **Format** pane:

#### Map Settings
- **Map Style**: Dark, Light, Muted, Satellite
- **3D View**: Enable 3D perspective and pitch
- **Show Legend**: Display/hide legend panel
- **Show Tooltip**: Enable/disable tooltips on hover

#### Layer Settings
- **Layer Type**: Point, Hexagon, Arc, Line, GeoJSON, Heatmap, Cluster
- **Point Radius**: Size of points (1-100)
- **Opacity**: Layer transparency (0-1)
- **Elevation Scale**: Height multiplier for 3D (0-100)

#### Color Settings
- **Color Scale**: Quantize, Quantile, Ordinal
- **Color Range**: Blue-Green, Yellow-Red, Viridis, Uber

#### Filter Settings
- **Enable Time Filter**: Show time slider for temporal data
- **Animation Speed**: Speed of time animation (0.1-10x)

## Examples

### Example 1: Simple Point Map

**Data:**
- Latitude: Store location latitude
- Longitude: Store location longitude
- Category: Store name
- Size: Sales amount

**Settings:**
- Layer Type: Point
- Map Style: Light
- Point Radius: 15

**Result:** Shows stores as circles, sized by sales

### Example 2: Hexagon Aggregation

**Data:**
- Latitude: Customer latitude
- Longitude: Customer longitude
- Size: Purchase count

**Settings:**
- Layer Type: Hexagon
- Map Style: Dark
- Elevation Scale: 10
- 3D View: Enabled

**Result:** Hexagonal grid showing customer density in 3D

### Example 3: Heatmap

**Data:**
- Latitude: Incident latitude
- Longitude: Incident longitude
- Color: Severity score

**Settings:**
- Layer Type: Heatmap
- Map Style: Dark
- Opacity: 0.8

**Result:** Heat map showing incident concentration and severity

### Example 4: Arc Connections

**Data:** Two lat/lon pairs for origin and destination
- Create calculated columns for start and end points
- Use Line layer type

**Settings:**
- Layer Type: Arc
- Map Style: Dark
- Opacity: 0.6

**Result:** Curved lines showing connections between locations

### Example 5: Temporal Animation

**Data:**
- Latitude: Event latitude
- Longitude: Event longitude
- Time: Event timestamp
- Size: Event magnitude

**Settings:**
- Layer Type: Point
- Enable Time Filter: Yes
- Animation Speed: 2x

**Result:** Animated playback of events over time

### Example 6: GeoJSON Polygons

**Data:**
- Import GeoJSON data via Power Query
- Extract geometry, properties, and centroid

**Settings:**
- Layer Type: GeoJSON
- Map Style: Satellite
- 3D View: Enabled

**Result:** Rendered polygons with proper styling

## Working with Honua Data

### Using OGC Features

1. Connect to Honua using the Honua Power BI Connector
2. You'll get `Latitude`, `Longitude`, `GeometryType`, and properties
3. Drag fields to the Kepler visual:
   - `Latitude` → Latitude
   - `Longitude` → Longitude
   - `Prop_name` → Category
   - `FeatureId` → Tooltip

### Using STAC Items

1. Connect to Honua STAC catalog
2. You'll get temporal data with `DateTime`
3. Drag fields:
   - `Latitude` → Latitude
   - `Longitude` → Longitude
   - `DateTime` → Time
   - `Collection` → Category
   - `STAC_eo_cloud_cover` → Color

4. Enable time filter to animate satellite passes

### Handling Complex Geometries

For non-point geometries (polygons, lines):

1. Honua connector provides `Geometry` column
2. In Power Query, expand geometry to get coordinates array
3. Use calculated columns to extract paths:
   ```DAX
   GeometryJSON =
   '{
       "type": "Feature",
       "geometry": ' & [Geometry] & '
   }'
   ```
4. Set Layer Type to GeoJSON

## Performance Tips

### 1. Limit Data Volume
- Use Power BI filters to reduce rows to < 100K for best performance
- Kepler can handle millions, but Power BI visuals have limits

### 2. Use Aggregation Layers
- For dense data, use Hexagon or Cluster layer types
- These aggregate points and improve rendering

### 3. Optimize Refresh
- Configure incremental refresh for time-series data
- Use Import mode instead of DirectQuery when possible

### 4. Simplify Geometries
- For complex polygons, simplify in Power Query
- Reduce coordinate precision to 6 decimal places

### 5. Mapbox Token
- Provide your own Mapbox token for satellite imagery
- Free tier: 50K map loads/month

## Customization

### Custom Mapbox Token

To use satellite imagery or custom map styles:

1. Get a free token at https://www.mapbox.com/
2. Edit `visual.ts` and update `getMapboxToken()`
3. Rebuild the visual

### Custom Color Schemes

Edit `buildKeplerConfig()` in `visual.ts`:

```typescript
colorRange: {
    name: "Custom",
    type: "sequential",
    category: "Custom",
    colors: ["#FF0000", "#00FF00", "#0000FF"] // Your colors
}
```

### Custom Layers

Add custom layer configurations:

```typescript
layers: [
    {
        type: "point",
        config: {
            // Your layer config
        }
    },
    {
        type: "hexagon",
        config: {
            // Second layer config
        }
    }
]
```

## Troubleshooting

### Visual Doesn't Load

**Solution:**
- Check browser console (F12) for errors
- Verify Power BI Desktop is up to date
- Try re-importing the visual

### Map is Blank

**Solution:**
- Ensure Latitude and Longitude are bound
- Check data has valid coordinates (-90 to 90 lat, -180 to 180 lon)
- Verify data isn't filtered to zero rows

### No Satellite Imagery

**Solution:**
- Provide a valid Mapbox token
- Set Map Style to "Satellite"
- Check internet connectivity

### Performance Issues

**Solution:**
- Reduce data volume with filters
- Use aggregation layers (Hexagon, Cluster)
- Disable 3D view
- Simplify geometries

### Layers Not Visible

**Solution:**
- Check layer opacity (should be > 0)
- Verify color scale has contrast
- Ensure data values are in expected range

## Advanced Features

### Filter Integration

The visual supports cross-filtering:
- Click points to filter other visuals
- Other visuals can filter this map
- Use slicers to control what's displayed

### Drill-down

Enable drill-down on Category field:
- Add hierarchy (e.g., Country → State → City)
- Click points to drill into next level

### Bookmarks

Map state is preserved in bookmarks:
- Zoom level and position
- Layer configuration
- Filters

### Export

Export current view:
- Use Power BI's "Export data" feature
- Or take screenshot (Windows: Shift+Ctrl+S)

## API Reference

### Visual Settings Object

```typescript
interface VisualSettings {
    mapSettings: {
        mapStyle: "dark" | "light" | "muted" | "satellite";
        show3D: boolean;
        showLegend: boolean;
        showTooltip: boolean;
    };
    layerSettings: {
        layerType: "point" | "hexagon" | "arc" | "line" | "geojson" | "heatmap" | "cluster";
        pointRadius: number; // 1-100
        opacity: number; // 0-1
        elevationScale: number; // 0-100
    };
    colorSettings: {
        colorScale: "quantize" | "quantile" | "ordinal";
        colorRange: string;
    };
    filterSettings: {
        enableTimeFilter: boolean;
        animationSpeed: number; // 0.1-10
    };
}
```

## Best Practices

1. **Start Simple**: Begin with Point layer, then experiment
2. **Test with Sample**: Use small dataset first
3. **Optimize Early**: Apply filters before visual processing
4. **Use Aggregation**: Hexagon/Cluster for > 10K points
5. **Leverage Time**: Use temporal animation for insights
6. **Document Tokens**: Keep Mapbox tokens secure, don't commit to source control

## Comparison with Other Visuals

| Feature | Power BI Map | ArcGIS | Honua Kepler.gl |
|---------|-------------|--------|-----------------|
| Point Data | ✅ | ✅ | ✅ |
| Polygon/Line | ❌ | ✅ | ✅ |
| 3D Visualization | ❌ | ✅ | ✅ |
| Hexagon Aggregation | ❌ | ✅ | ✅ |
| Temporal Animation | ❌ | ✅ | ✅ |
| Free to Use | ✅ | ❌ | ✅ |
| Open Source | ❌ | ❌ | ✅ |

## Resources

- **Kepler.gl Docs**: https://docs.kepler.gl/
- **Honua Docs**: https://docs.honua.io/powerbi/kepler-visual
- **GitHub**: https://github.com/HonuaIO/powerbi-kepler-visual
- **Video Tutorials**: https://www.youtube.com/honuaio

## Support

- **Issues**: https://github.com/HonuaIO/powerbi-kepler-visual/issues
- **Community**: https://community.honua.io
- **Email**: support@honua.io

## License

MIT License - See LICENSE file for details

## Credits

- Built on [Kepler.gl](https://kepler.gl/) by Uber
- Powered by [Power BI Visuals API](https://github.com/microsoft/PowerBI-visuals)
- Developed by [HonuaIO](https://honua.io)

## Version History

### 1.0.0 (2025-02-01)
- Initial release
- 7 layer types supported
- 4 map styles
- 3D visualization
- Temporal animation
- Cross-filtering
- Drill-down support
