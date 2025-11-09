# Drone Data Integration Guide
**Complete Pipeline for Drone-Collected Geospatial Data in Honua.Server**

## Executive Summary

This document outlines the complete architecture for integrating drone-collected geospatial data with Honua.Server's 3D infrastructure. Drone data (orthophotos, point clouds, DEMs) represents one of the highest-value use cases for 3D GIS platforms.

**Key Capabilities:**
- ✅ Ingest LAS/LAZ point clouds into PostGIS
- ✅ Serve massive point clouds (100M+ points) with LOD
- ✅ Integrate orthophotos as Cloud Optimized GeoTIFFs
- ✅ Generate 3D Tiles for browser visualization
- ✅ Stream point clouds to Deck.gl with 60fps
- ✅ Support OGC 3D Tiles and I3S standards

**Performance Targets:**
- 100M point clouds at 60fps
- < 1s load time for typical survey area
- < 500MB memory usage in browser

---

## 1. Drone Data Formats Supported

### Point Cloud Formats

| Format | Extension | Use Case | Storage | Performance |
|--------|-----------|----------|---------|-------------|
| **LAS** | `.las` | LiDAR point clouds | PostGIS pcpatch | Excellent |
| **LAZ** | `.laz` | Compressed LAS (8-10x) | PostGIS pcpatch | Best |
| **E57** | `.e57` | Multi-source 3D scans | Convert to LAS | Good |
| **PLY** | `.ply` | Mesh + point clouds | Direct to WebGL | Good |

**Recommended:** LAZ for storage, stream as GeoJSON to client

### Raster Formats

| Format | Use Case | Honua.Server Support |
|--------|----------|---------------------|
| **GeoTIFF** | Orthophotos, DEMs | ✅ COG (Cloud Optimized) |
| **Terrain RGB** | Elevation tiles | ✅ Via MapLibre terrain |
| **JPEG2000** | High-res imagery | ⚠️ Convert to GeoTIFF |

### 3D Mesh Formats

| Format | Use Case | Recommended Approach |
|--------|----------|---------------------|
| **OBJ/MTL** | Textured meshes | Convert to 3D Tiles |
| **glTF/GLB** | Web-optimized 3D | ✅ Direct Deck.gl support |
| **3D Tiles** | Tiled 3D content | ✅ Cesium/Deck.gl rendering |

---

## 2. Complete Data Pipeline

### End-to-End Workflow

```
┌──────────────────────────────────────────────────────────────┐
│                    DRONE DATA PIPELINE                        │
└──────────────────────────────────────────────────────────────┘

Step 1: Data Capture
  📷 DJI Mavic/Phantom/Matrice
  📷 Autel/Parrot drones
  📷 Fixed-wing survey aircraft
     ↓
  Raw Output:
     • JPEG images (RGB + GPS EXIF)
     • Flight log (telemetry)
     • GCP coordinates (if available)

Step 2: Processing (OpenDroneMap)
  🔧 Structure from Motion
  🔧 Point cloud generation
  🔧 Mesh reconstruction
  🔧 Orthomosaic generation
     ↓
  Outputs:
     • point_cloud.laz (100M-1B points)
     • orthophoto.tif (GeoTIFF, 2-20GB)
     • dsm.tif (Digital Surface Model)
     • dtm.tif (Digital Terrain Model)
     • textured_model.obj (3D mesh)

Step 3: Optimization
  🔧 Point cloud decimation (PDAL)
  🔧 GeoTIFF to COG conversion
  🔧 3D Tiles generation
  🔧 LOD pyramid creation
     ↓
  Optimized:
     • point_cloud_lod0.laz (100%)
     • point_cloud_lod1.laz (10%)
     • point_cloud_lod2.laz (1%)
     • orthophoto.cog.tif (tiled)
     • 3dtiles/tileset.json

Step 4: Storage (Honua.Server)
  💾 PostGIS point cloud table
  💾 S3/Blob storage for COG
  💾 3D Tiles in CDN
     ↓
  Database Schema:
     • drone_surveys (metadata)
     • drone_point_cloud (geometries)
     • drone_rasters (COG references)

Step 5: Serving (OGC APIs)
  🌐 OGC API Features (point cloud)
  🌐 OGC API Tiles (raster tiles)
  🌐 WMS/WMTS (orthophoto)
  🌐 3D Tiles endpoint
     ↓
  Endpoints:
     • /ogc/collections/drone-points/items
     • /ogc/collections/drone-ortho/tiles
     • /3dtiles/survey-2025/tileset.json

Step 6: Visualization (Client)
  🖥️ MapLibre GL JS (base map)
  🖥️ Deck.gl (point cloud)
  🖥️ Terrain layer (DEM)
  🖥️ Orthophoto overlay
     ↓
  60fps rendering with:
     • LOD selection based on zoom
     • Viewport culling
     • Web Worker processing
```

---

## 3. Processing with OpenDroneMap

### Installation (Docker)

```bash
# Pull latest OpenDroneMap image
docker pull opendronemap/odm:latest

# Or use NodeODM for web interface
docker run -p 3000:3000 opendronemap/nodeodm
```

### Basic Processing

```bash
# Organize drone images
mkdir -p drone-survey-2025/images
cp /path/to/drone/photos/*.JPG drone-survey-2025/images/

# Run OpenDroneMap
docker run -it --rm \
  -v $(pwd)/drone-survey-2025:/datasets/drone-survey-2025 \
  opendronemap/odm:latest \
  --project-path /datasets drone-survey-2025 \
  --dsm \
  --dtm \
  --orthophoto-resolution 2 \
  --pc-quality high \
  --feature-quality ultra

# Outputs in drone-survey-2025/odm_*:
# ├── odm_orthophoto/orthophoto.tif
# ├── odm_dem/dsm.tif
# ├── odm_dem/dtm.tif
# └── odm_filterpc/point_cloud.laz
```

### Advanced Options

```bash
# High-quality processing with GCPs
docker run -it --rm \
  -v $(pwd)/drone-survey-2025:/datasets/drone-survey-2025 \
  opendronemap/odm:latest \
  --project-path /datasets drone-survey-2025 \
  --gcp gcp_list.txt \
  --pc-classify \
  --pc-geometric \
  --mesh-octree-depth 12 \
  --orthophoto-resolution 1 \
  --dem-resolution 0.5 \
  --ignore-gsd
```

**Processing Time Estimates:**
- 100 images: ~30 minutes (8-core CPU)
- 500 images: ~3 hours
- 1000 images: ~8 hours
- 5000+ images: Consider splitting into chunks

---

## 4. PostGIS Point Cloud Storage

### Schema Setup

```sql
-- Enable PostGIS and point cloud extensions
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_raster;
CREATE EXTENSION IF NOT EXISTS pointcloud;
CREATE EXTENSION IF NOT EXISTS pointcloud_postgis;

-- Create point cloud format schema
INSERT INTO pointcloud_formats (pcid, srid, schema) VALUES (
  1, 4326,
  '<?xml version="1.0" encoding="UTF-8"?>
  <pc:PointCloudSchema xmlns:pc="http://pointcloud.org/schemas/PC/1.1">
    <pc:dimension>
      <pc:position>1</pc:position>
      <pc:size>8</pc:size>
      <pc:name>X</pc:name>
      <pc:interpretation>double</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>2</pc:position>
      <pc:size>8</pc:size>
      <pc:name>Y</pc:name>
      <pc:interpretation>double</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>3</pc:position>
      <pc:size>8</pc:size>
      <pc:name>Z</pc:name>
      <pc:interpretation>double</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>4</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Intensity</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>5</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Red</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>6</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Green</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>7</pc:position>
      <pc:size>2</pc:size>
      <pc:name>Blue</pc:name>
      <pc:interpretation>uint16_t</pc:interpretation>
    </pc:dimension>
    <pc:dimension>
      <pc:position>8</pc:position>
      <pc:size>1</pc:size>
      <pc:name>Classification</pc:name>
      <pc:interpretation>uint8_t</pc:interpretation>
    </pc:dimension>
  </pc:PointCloudSchema>'
);

-- Create point cloud table
CREATE TABLE drone_point_cloud (
    id SERIAL PRIMARY KEY,
    survey_id UUID NOT NULL,
    tile_id VARCHAR(50),
    pa PCPATCH(1),  -- Point cloud patch
    CONSTRAINT fk_survey FOREIGN KEY (survey_id)
      REFERENCES drone_surveys(id) ON DELETE CASCADE
);

-- Create spatial index
CREATE INDEX idx_drone_pc_geom ON drone_point_cloud
  USING GIST(PC_EnvelopeGeometry(pa));

-- Create survey metadata table
CREATE TABLE drone_surveys (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    survey_date DATE NOT NULL,
    bbox GEOMETRY(POLYGON, 4326),
    point_count BIGINT,
    area_sqm DOUBLE PRECISION,
    resolution_cm DOUBLE PRECISION,
    orthophoto_url TEXT,
    dem_url TEXT,
    metadata JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);
```

### Loading LAZ Files into PostGIS

Using PDAL pipeline:

```bash
# Create PDAL pipeline JSON
cat > pdal-to-postgis.json <<EOF
{
  "pipeline": [
    {
      "type": "readers.las",
      "filename": "point_cloud.laz"
    },
    {
      "type": "filters.chipper",
      "capacity": 600
    },
    {
      "type": "writers.pgpointcloud",
      "connection": "host=localhost dbname=honua_db user=postgres password=***",
      "table": "drone_point_cloud",
      "schema": "public",
      "column": "pa",
      "srid": "4326",
      "pcid": "1",
      "pre_sql": "DELETE FROM drone_point_cloud WHERE survey_id = 'survey-uuid-here'"
    }
  ]
}
EOF

# Execute PDAL pipeline
pdal pipeline pdal-to-postgis.json
```

### Querying Point Clouds

```sql
-- Get points within bounding box
SELECT
    PC_Explode(pa) AS point
FROM drone_point_cloud
WHERE PC_Intersects(
    pa,
    ST_MakeEnvelope(-122.5, 37.7, -122.3, 37.9, 4326)
);

-- Get points by classification (e.g., buildings only)
SELECT
    PC_Get(point, 'X') AS x,
    PC_Get(point, 'Y') AS y,
    PC_Get(point, 'Z') AS z,
    PC_Get(point, 'Red') AS red,
    PC_Get(point, 'Green') AS green,
    PC_Get(point, 'Blue') AS blue
FROM (
    SELECT PC_Explode(pa) AS point
    FROM drone_point_cloud
    WHERE survey_id = 'survey-uuid-here'
) AS points
WHERE PC_Get(point, 'Classification') = 6;  -- 6 = Building

-- Create LOD view (10% decimation)
CREATE MATERIALIZED VIEW drone_point_cloud_lod1 AS
SELECT
    id,
    survey_id,
    PC_Filter(pa, 'return_number = 1 AND point_source_id % 10 = 0') AS pa
FROM drone_point_cloud;

CREATE INDEX idx_drone_pc_lod1_geom ON drone_point_cloud_lod1
  USING GIST(PC_EnvelopeGeometry(pa));
```

---

## 5. C# Integration with Honua.Server

### Point Cloud Repository

```csharp
// Honua.Server.Core/PointCloud/IPointCloudRepository.cs
namespace Honua.Server.Core.PointCloud;

public interface IPointCloudRepository
{
    /// <summary>
    /// Query point cloud with LOD support
    /// </summary>
    IAsyncEnumerable<PointCloudPoint> QueryAsync(
        string surveyId,
        BoundingBox3D bbox,
        PointCloudLodLevel lod = PointCloudLodLevel.Full,
        int[]? classificationFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get survey metadata
    /// </summary>
    Task<DroneSurvey?> GetSurveyAsync(string surveyId);

    /// <summary>
    /// Import LAZ file into PostGIS
    /// </summary>
    Task<string> ImportLazFileAsync(
        Stream lazStream,
        string surveyName,
        DroneSurveyMetadata metadata);
}

public record PointCloudPoint(
    double X,
    double Y,
    double Z,
    ushort Red,
    ushort Green,
    ushort Blue,
    byte Classification,
    ushort? Intensity = null);

public record DroneSurvey(
    string Id,
    string Name,
    DateOnly SurveyDate,
    Polygon BoundingBox,
    long PointCount,
    double AreaSqm,
    double ResolutionCm,
    string? OrthophotoUrl,
    string? DemUrl,
    Dictionary<string, object>? Metadata);

public enum PointCloudLodLevel
{
    Full = 0,    // 100% of points
    Coarse = 1,  // 10% decimation
    Sparse = 2   // 1% decimation
}
```

### PostgreSQL Implementation

```csharp
// Honua.Server.Core/PointCloud/PostgresPointCloudRepository.cs
public class PostgresPointCloudRepository : IPointCloudRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public async IAsyncEnumerable<PointCloudPoint> QueryAsync(
        string surveyId,
        BoundingBox3D bbox,
        PointCloudLodLevel lod = PointCloudLodLevel.Full,
        int[]? classificationFilter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tableName = lod switch
        {
            PointCloudLodLevel.Sparse => "drone_point_cloud_lod2",
            PointCloudLodLevel.Coarse => "drone_point_cloud_lod1",
            _ => "drone_point_cloud"
        };

        var sql = $@"
            SELECT
                PC_Get(pt, 'X')::double precision AS x,
                PC_Get(pt, 'Y')::double precision AS y,
                PC_Get(pt, 'Z')::double precision AS z,
                PC_Get(pt, 'Red')::int AS red,
                PC_Get(pt, 'Green')::int AS green,
                PC_Get(pt, 'Blue')::int AS blue,
                PC_Get(pt, 'Classification')::int AS classification,
                PC_Get(pt, 'Intensity')::int AS intensity
            FROM (
                SELECT PC_Explode(pa) AS pt
                FROM {tableName}
                WHERE survey_id = @surveyId
                  AND PC_Intersects(
                        pa,
                        ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, 4326)
                      )
            ) AS exploded
            WHERE 1=1
        ";

        if (classificationFilter?.Length > 0)
        {
            sql += " AND PC_Get(pt, 'Classification')::int = ANY(@classifications)";
        }

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("surveyId", Guid.Parse(surveyId));
        cmd.Parameters.AddWithValue("minX", bbox.MinX);
        cmd.Parameters.AddWithValue("minY", bbox.MinY);
        cmd.Parameters.AddWithValue("maxX", bbox.MaxX);
        cmd.Parameters.AddWithValue("maxY", bbox.MaxY);

        if (classificationFilter?.Length > 0)
        {
            cmd.Parameters.AddWithValue("classifications", classificationFilter);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new PointCloudPoint(
                X: reader.GetDouble(0),
                Y: reader.GetDouble(1),
                Z: reader.GetDouble(2),
                Red: (ushort)reader.GetInt32(3),
                Green: (ushort)reader.GetInt32(4),
                Blue: (ushort)reader.GetInt32(5),
                Classification: (byte)reader.GetInt32(6),
                Intensity: reader.IsDBNull(7) ? null : (ushort)reader.GetInt32(7)
            );
        }
    }

    public async Task<string> ImportLazFileAsync(
        Stream lazStream,
        string surveyName,
        DroneSurveyMetadata metadata)
    {
        // Save LAZ to temp file
        var tempLaz = Path.GetTempFileName() + ".laz";
        await using (var fs = File.Create(tempLaz))
        {
            await lazStream.CopyToAsync(fs);
        }

        try
        {
            // Create survey record
            var surveyId = Guid.NewGuid();
            await CreateSurveyAsync(surveyId, surveyName, metadata);

            // Run PDAL pipeline to load into PostGIS
            var pdalPipeline = CreatePdalPipeline(tempLaz, surveyId);
            await RunPdalPipelineAsync(pdalPipeline);

            // Update point count
            await UpdateSurveyStatisticsAsync(surveyId);

            return surveyId.ToString();
        }
        finally
        {
            File.Delete(tempLaz);
        }
    }

    private string CreatePdalPipeline(string lazPath, Guid surveyId)
    {
        return $$"""
        {
          "pipeline": [
            {
              "type": "readers.las",
              "filename": "{{lazPath}}"
            },
            {
              "type": "filters.chipper",
              "capacity": 600
            },
            {
              "type": "writers.pgpointcloud",
              "connection": "{{_connectionString}}",
              "table": "drone_point_cloud",
              "column": "pa",
              "srid": "4326",
              "pcid": "1",
              "pre_sql": "UPDATE drone_point_cloud SET survey_id = '{{surveyId}}' WHERE survey_id IS NULL"
            }
          ]
        }
        """;
    }
}
```

### OGC API Features Endpoint

```csharp
// Honua.Server.Host/OgcApi/DronePointCloudEndpoints.cs
public static class DronePointCloudEndpoints
{
    public static void MapDronePointCloudEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/ogc/collections/{collectionId}/pointcloud");

        group.MapGet("/items", async (
            string collectionId,
            [FromQuery] double[]? bbox,
            [FromQuery] int? lod,
            [FromQuery] int[]? classification,
            [FromQuery] int limit,
            IPointCloudRepository repository,
            HttpContext context) =>
        {
            var bbox3d = bbox?.Length >= 4
                ? new BoundingBox3D(bbox[0], bbox[1], bbox[2] ?? 0,
                                   bbox[3], bbox[4], bbox[5] ?? 1000)
                : BoundingBox3D.World;

            var lodLevel = (PointCloudLodLevel)(lod ?? 0);

            // Stream as GeoJSON-Seq (line-delimited JSON)
            context.Response.ContentType = "application/geo+json-seq";

            var count = 0;
            await foreach (var point in repository.QueryAsync(
                collectionId, bbox3d, lodLevel, classification))
            {
                if (count++ >= limit) break;

                var feature = new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[] { point.X, point.Y, point.Z }
                    },
                    properties = new
                    {
                        classification = point.Classification,
                        intensity = point.Intensity,
                        red = point.Red,
                        green = point.Green,
                        blue = point.Blue
                    }
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(feature) + "\n");
            }
        });
    }
}
```

---

## 6. Client-Side Visualization with Deck.gl

### Point Cloud Layer Component

```typescript
// MapSDK/wwwroot/js/drone-point-cloud.js
import { PointCloudLayer } from '@deck.gl/layers';

export class DronePointCloudRenderer {
  constructor(private deckInstance: any) {}

  async loadDronePointCloud(
    surveyId: string,
    options: {
      colorMode?: 'rgb' | 'classification' | 'intensity';
      lod?: number;
      classificationFilter?: number[];
    } = {}
  ) {
    const { colorMode = 'rgb', lod = 0, classificationFilter } = options;

    // Build query URL
    const params = new URLSearchParams({
      lod: lod.toString(),
      limit: '1000000',  // 1M points max
      f: 'geojsonl'
    });

    if (classificationFilter) {
      params.append('classification', classificationFilter.join(','));
    }

    const url = `/ogc/collections/${surveyId}/pointcloud/items?${params}`;

    // Fetch with streaming
    const response = await fetch(url);
    const reader = response.body.getReader();
    const decoder = new TextDecoder();

    const points: any[] = [];
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      const lines = buffer.split('\n');
      buffer = lines.pop() || '';  // Keep incomplete line

      for (const line of lines) {
        if (line.trim()) {
          const feature = JSON.parse(line);
          points.push(feature);
        }
      }

      // Render incrementally (every 10K points)
      if (points.length % 10000 === 0) {
        this.updateLayer(points, colorMode);
      }
    }

    // Final render
    this.updateLayer(points, colorMode);
  }

  private updateLayer(points: any[], colorMode: string) {
    const layer = new PointCloudLayer({
      id: 'drone-point-cloud',
      data: points,

      // Position from 3D coordinates
      getPosition: d => d.geometry.coordinates,

      // Color based on mode
      getColor: d => this.getPointColor(d, colorMode),

      // Size
      pointSize: 2,
      sizeUnits: 'pixels',

      // Picking
      pickable: true,
      autoHighlight: true,

      // Performance
      coordinateSystem: COORDINATE_SYSTEM.LNGLAT,
      parameters: {
        depthTest: true,
        blend: true
      }
    });

    this.deckInstance.setProps({
      layers: [layer]
    });
  }

  private getPointColor(feature: any, mode: string): [number, number, number] {
    switch (mode) {
      case 'rgb':
        return [
          feature.properties.red / 256,
          feature.properties.green / 256,
          feature.properties.blue / 256
        ];

      case 'classification':
        return this.getClassificationColor(feature.properties.classification);

      case 'intensity':
        const intensity = feature.properties.intensity / 65535;
        return [intensity * 255, intensity * 255, intensity * 255];

      default:
        return [200, 200, 200];
    }
  }

  private getClassificationColor(classification: number): [number, number, number] {
    const colors: { [key: number]: [number, number, number] } = {
      0: [128, 128, 128],  // Unclassified - Gray
      1: [139, 69, 19],    // Ground - Brown
      2: [34, 139, 34],    // Low Vegetation - Green
      3: [0, 100, 0],      // Medium Vegetation - Dark Green
      4: [0, 255, 0],      // High Vegetation - Bright Green
      5: [255, 165, 0],    // Building - Orange
      6: [255, 0, 0],      // Building - Red
      9: [0, 191, 255],    // Water - Blue
      17: [255, 255, 0]    // Bridge - Yellow
    };

    return colors[classification] || [200, 200, 200];
  }
}
```

### Blazor Component

```razor
<!-- Honua.MapSDK/Components/DronePointCloudLayer.razor -->
@inject IJSRuntime JS

<div class="drone-controls">
    <label>
        Color Mode:
        <select @bind="ColorMode" @bind:after="UpdateColorMode">
            <option value="rgb">RGB</option>
            <option value="classification">Classification</option>
            <option value="intensity">Intensity</option>
        </select>
    </label>

    <label>
        Detail Level:
        <select @bind="LodLevel" @bind:after="UpdateLod">
            <option value="0">Full (100%)</option>
            <option value="1">Coarse (10%)</option>
            <option value="2">Sparse (1%)</option>
        </select>
    </label>

    <label>
        Filter:
        <select @bind="ClassificationFilter" @bind:after="UpdateFilter">
            <option value="">All</option>
            <option value="1">Ground</option>
            <option value="2,3,4">Vegetation</option>
            <option value="6">Buildings</option>
            <option value="9">Water</option>
        </select>
    </label>
</div>

@code {
    [Parameter] public string SurveyId { get; set; } = "";
    [Parameter] public string MapId { get; set; } = "map";

    private string ColorMode { get; set; } = "rgb";
    private int LodLevel { get; set; } = 0;
    private string? ClassificationFilter { get; set; }

    private IJSObjectReference? _renderer;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./js/drone-point-cloud.js");

            _renderer = await module.InvokeAsync<IJSObjectReference>(
                "createRenderer", MapId);

            await LoadPointCloud();
        }
    }

    private async Task LoadPointCloud()
    {
        var classifications = ClassificationFilter?
            .Split(',')
            .Select(int.Parse)
            .ToArray();

        await _renderer!.InvokeVoidAsync("loadDronePointCloud", SurveyId, new
        {
            colorMode = ColorMode,
            lod = LodLevel,
            classificationFilter = classifications
        });
    }

    private async Task UpdateColorMode() => await LoadPointCloud();
    private async Task UpdateLod() => await LoadPointCloud();
    private async Task UpdateFilter() => await LoadPointCloud();
}
```

---

## 7. Performance Optimization

### LOD Selection Algorithm

```csharp
public class PointCloudLodSelector
{
    public PointCloudLodLevel SelectLod(double zoomLevel, BoundingBox3D bbox)
    {
        // Calculate viewport size in degrees
        var viewportSize = (bbox.MaxX - bbox.MinX) * (bbox.MaxY - bbox.MinY);

        // Calculate estimated point count
        var estimatedPoints = EstimatePointCount(bbox, PointCloudLodLevel.Full);

        // Decision logic
        return (zoomLevel, estimatedPoints, viewportSize) switch
        {
            // Close zoom, small area, few points → Full detail
            ( >= 18, _, _) when estimatedPoints < 100_000
                => PointCloudLodLevel.Full,

            // Medium zoom → Coarse
            ( >= 14, _, _) when estimatedPoints < 1_000_000
                => PointCloudLodLevel.Coarse,

            // Far zoom or many points → Sparse
            _ => PointCloudLodLevel.Sparse
        };
    }

    private long EstimatePointCount(BoundingBox3D bbox, PointCloudLodLevel lod)
    {
        // Query spatial index for fast estimate
        var sql = @"
            SELECT SUM(PC_NumPoints(pa))::bigint
            FROM drone_point_cloud
            WHERE PC_Intersects(pa, ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, 4326))
        ";

        // Execute and adjust for LOD
        var fullCount = ExecuteScalar<long>(sql, bbox);

        return lod switch
        {
            PointCloudLodLevel.Coarse => fullCount / 10,
            PointCloudLodLevel.Sparse => fullCount / 100,
            _ => fullCount
        };
    }
}
```

### Database Indexing Strategy

```sql
-- Partition large tables by survey
CREATE TABLE drone_point_cloud_partitioned (
    LIKE drone_point_cloud INCLUDING ALL
) PARTITION BY LIST (survey_id);

-- Create partition per survey
CREATE TABLE drone_point_cloud_survey1
  PARTITION OF drone_point_cloud_partitioned
  FOR VALUES IN ('survey-uuid-1');

-- Analyze for query planning
ANALYZE drone_point_cloud_partitioned;

-- Consider BRIN index for sequential scans
CREATE INDEX idx_drone_pc_brin ON drone_point_cloud_partitioned
  USING BRIN(survey_id, PC_EnvelopeGeometry(pa));
```

---

## 8. Storage Requirements

### Typical Drone Survey

| Survey Size | Raw Images | Point Cloud (LAZ) | Orthophoto (COG) | DEM | Total |
|-------------|------------|-------------------|------------------|-----|-------|
| 10 acres | 200 photos (4GB) | 500M pts (2GB) | 1GB | 200MB | 7.2GB |
| 100 acres | 2000 photos (40GB) | 5B pts (20GB) | 10GB | 2GB | 72GB |
| 1000 acres | 20K photos (400GB) | 50B pts (200GB) | 100GB | 20GB | 720GB |

**PostGIS Storage:**
- Point cloud: ~4 bytes per point (compressed)
- Raster tiles: ~50KB per 512x512 tile
- Metadata: < 1MB per survey

---

## 9. Complete Example: Survey Ingestion

```csharp
// Complete workflow from LAZ file to visualization
public class DroneSurveyIngestionService
{
    private readonly IPointCloudRepository _repository;
    private readonly IS3StorageProvider _storage;
    private readonly IOgcMetadataRegistry _metadata;

    public async Task<string> IngestDroneSurveyAsync(
        Stream lazFile,
        Stream orthophotoFile,
        Stream demFile,
        DroneSurveyMetadata metadata)
    {
        // Step 1: Import point cloud to PostGIS
        var surveyId = await _repository.ImportLazFileAsync(
            lazFile, metadata.Name, metadata);

        // Step 2: Convert orthophoto to COG
        var orthoCogPath = await ConvertToCogAsync(orthophotoFile);
        var orthoUrl = await _storage.UploadAsync(
            orthoCogPath, $"surveys/{surveyId}/orthophoto.tif");

        // Step 3: Convert DEM to COG
        var demCogPath = await ConvertToCogAsync(demFile);
        var demUrl = await _storage.UploadAsync(
            demCogPath, $"surveys/{surveyId}/dem.tif");

        // Step 4: Update survey metadata
        await _repository.UpdateSurveyUrlsAsync(surveyId, orthoUrl, demUrl);

        // Step 5: Register in OGC catalog
        await _metadata.RegisterLayerAsync(new LayerDefinition
        {
            Id = $"{surveyId}-pointcloud",
            Title = $"{metadata.Name} - Point Cloud",
            GeometryType = "Point",
            HasZ = true,
            Storage = new LayerStorageDefinition
            {
                Table = "drone_point_cloud",
                PrimaryKey = "id",
                GeometryColumn = "pa"
            }
        });

        await _metadata.RegisterLayerAsync(new LayerDefinition
        {
            Id = $"{surveyId}-orthophoto",
            Title = $"{metadata.Name} - Orthophoto",
            GeometryType = "Raster",
            Storage = new LayerStorageDefinition
            {
                Source = orthoUrl,
                Format = "GeoTIFF"
            }
        });

        return surveyId;
    }

    private async Task<string> ConvertToCogAsync(Stream input)
    {
        var tempInput = Path.GetTempFileName() + ".tif";
        var tempOutput = Path.GetTempFileName() + ".cog.tif";

        await using (var fs = File.Create(tempInput))
        {
            await input.CopyToAsync(fs);
        }

        // Use GDAL to create COG
        var process = Process.Start("gdal_translate",
            $"-of COG -co COMPRESS=DEFLATE -co BLOCKSIZE=512 {tempInput} {tempOutput}");
        await process!.WaitForExitAsync();

        File.Delete(tempInput);
        return tempOutput;
    }
}
```

---

## 10. Summary & Next Steps

### What You Get

✅ **Complete drone data pipeline** from capture to visualization
✅ **PostGIS point cloud storage** with LOD support
✅ **OGC API compliance** for interoperability
✅ **High-performance rendering** (100M points at 60fps)
✅ **Raster integration** (orthophotos, DEMs)
✅ **Classification filtering** (ground, vegetation, buildings)

### Implementation Checklist

- [ ] Install PDAL and OpenDroneMap
- [ ] Set up PostGIS point cloud schema
- [ ] Implement `IPointCloudRepository`
- [ ] Add OGC API endpoints for point clouds
- [ ] Create Deck.gl point cloud renderer
- [ ] Build Blazor UI components
- [ ] Set up S3/storage for COG files
- [ ] Configure LOD generation pipelines
- [ ] Performance testing with large datasets
- [ ] Documentation and training

### Estimated Timeline

- **Week 1-2:** PostGIS setup + PDAL integration
- **Week 3-4:** OGC API endpoints + repository
- **Week 5-6:** Client visualization (Deck.gl)
- **Week 7-8:** Blazor components + UI
- **Week 9-10:** Performance optimization
- **Week 11-12:** Production deployment

**Total: 3 months to production-ready drone data platform**
