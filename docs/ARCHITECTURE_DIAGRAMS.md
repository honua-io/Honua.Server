# Architecture Diagrams
**Visual Guide to Honua.Server 3D Geospatial Platform**

This document contains comprehensive architecture diagrams for all major components of the Honua.Server 3D platform.

---

## Table of Contents

1. [Client-Side 3D Architecture](#1-client-side-3d-architecture)
2. [Blazor Interop Data Flow](#2-blazor-interop-data-flow)
3. [Drone Data Pipeline](#3-drone-data-pipeline)
4. [Meta Quest WebXR Integration](#4-meta-quest-webxr-integration)
5. [3D Geometry Stack](#5-3d-geometry-stack)
6. [Point Cloud Rendering Pipeline](#6-point-cloud-rendering-pipeline)
7. [Performance Optimization Flow](#7-performance-optimization-flow)

---

## 1. Client-Side 3D Architecture

### Complete Technology Stack

```mermaid
graph TB
    subgraph "User Layer"
        USER[User Browser]
    end

    subgraph "Blazor Components (C#)"
        MAP3D[Map3DComponent.razor]
        TERRAIN[TerrainLayer.razor]
        EDITOR[Geometry3DEditor.razor]
        POINTCLOUD[PointCloud3DLayer.razor]
    end

    subgraph "JavaScript Interop Layer"
        JSINTEROP[IJSRuntime]
        OBJREF[IJSObjectReference]
    end

    subgraph "JavaScript 3D Modules"
        HONUA3D[honua-3d.js<br/>Orchestrator]
        GEOM3D[honua-geometry-3d.js<br/>Parser]
        TERRAIN_JS[honua-terrain.js<br/>Tiles]
        DRAW3D[honua-draw-3d.js<br/>Editor]
    end

    subgraph "Rendering Engines"
        MAPLIBRE[MapLibre GL JS<br/>Base Map + 2.5D]
        DECKGL[Deck.gl<br/>3D Visualization]
    end

    subgraph "Web Workers"
        WORKER[geometry-processor.js<br/>Background Processing]
    end

    subgraph "WebGL Layer"
        WEBGL[WebGL Context<br/>GPU Rendering]
    end

    subgraph "Server APIs"
        OGC[OGC API Features<br/>3D GeoJSON]
        WMS[WMS/WMTS<br/>Raster Tiles]
        TILES3D[3D Tiles<br/>Point Clouds]
    end

    USER --> MAP3D
    USER --> TERRAIN
    USER --> EDITOR
    USER --> POINTCLOUD

    MAP3D --> JSINTEROP
    TERRAIN --> JSINTEROP
    EDITOR --> JSINTEROP
    POINTCLOUD --> JSINTEROP

    JSINTEROP --> OBJREF
    OBJREF --> HONUA3D

    HONUA3D --> GEOM3D
    HONUA3D --> TERRAIN_JS
    HONUA3D --> DRAW3D

    GEOM3D --> WORKER
    WORKER --> GEOM3D

    HONUA3D --> DECKGL
    TERRAIN_JS --> MAPLIBRE
    DRAW3D --> DECKGL

    MAPLIBRE --> WEBGL
    DECKGL --> WEBGL

    GEOM3D -.fetch.-> OGC
    TERRAIN_JS -.fetch.-> WMS
    HONUA3D -.fetch.-> TILES3D

    WEBGL --> USER

    style USER fill:#e1f5ff
    style WEBGL fill:#ffe1e1
    style WORKER fill:#fff4e1
    style OGC fill:#e1ffe1
    style WMS fill:#e1ffe1
    style TILES3D fill:#e1ffe1
```

### Layer Interaction Flow

```mermaid
sequenceDiagram
    participant User
    participant Blazor as Blazor Component
    participant JSInterop as JS Interop
    participant JS as JavaScript Module
    participant Worker as Web Worker
    participant Server as Honua Server
    participant GPU as WebGL/GPU

    User->>Blazor: Click "Load 3D Buildings"
    Blazor->>JSInterop: InvokeAsync("loadLayer", url)
    Note over JSInterop: Only URL passes through interop<br/>(~100 bytes)

    JSInterop->>JS: loadLayer(url)
    JS->>Server: fetch(url) - Direct
    Note over JS,Server: Data bypasses Blazor<br/>(could be 10MB+)

    Server-->>JS: GeoJSON 3D (streaming)
    JS->>Worker: postMessage(geojson)
    Note over Worker: Background processing<br/>UI stays responsive

    Worker->>Worker: Parse, triangulate, LOD
    Worker-->>JS: Processed mesh (transferable)

    JS->>GPU: Deck.gl layer update
    GPU->>GPU: WebGL rendering
    GPU-->>User: 60fps 3D visualization

    Note over User,GPU: Total time: ~800ms for 100K features
```

---

## 2. Blazor Interop Data Flow

### ❌ Bad Approach (Naive)

```mermaid
graph LR
    subgraph "Catastrophic Performance"
        Server[Server API]
        Blazor[Blazor C#]
        JS[JavaScript]
        GPU[GPU]
    end

    Server -->|100K features<br/>20MB JSON| Blazor
    Blazor -->|100K interop calls<br/>Serialize each| JS
    JS -->|Render one by one| GPU

    style Server fill:#ffe1e1
    style Blazor fill:#ffe1e1
    style JS fill:#ffe1e1
    style GPU fill:#ffe1e1

    Result[Result: 180 seconds<br/>UI frozen<br/>2GB memory]

    style Result fill:#ff0000,color:#fff
```

### ✅ Good Approach (Optimized)

```mermaid
graph TB
    subgraph "Optimized Architecture"
        Server[Server API]
        Blazor[Blazor C#<br/>Control Only]
        JS[JavaScript<br/>Data Handler]
        Worker[Web Worker<br/>Processing]
        GPU[GPU<br/>Rendering]
    end

    Blazor -->|Single interop call<br/>URL only (~100 bytes)| JS
    JS -->|Direct fetch<br/>Bypass Blazor| Server
    Server -->|Streaming GeoJSON<br/>20MB| JS
    JS -->|Transfer to worker<br/>Zero-copy| Worker
    Worker -->|Processed mesh<br/>Transferable| JS
    JS -->|Batch render| GPU

    style Blazor fill:#e1ffe1
    style JS fill:#e1ffe1
    style Worker fill:#e1ffe1
    style GPU fill:#e1ffe1

    Result[Result: 0.8 seconds<br/>60fps<br/>200MB memory]

    style Result fill:#00ff00,color:#000
```

### Performance Comparison

```mermaid
graph LR
    subgraph "Naive Approach"
        N1[Load 100K features]
        N2[Time: 180 seconds]
        N3[Memory: 2GB]
        N4[FPS: 0 frozen]
    end

    subgraph "Optimized Approach"
        O1[Load 100K features]
        O2[Time: 0.8 seconds]
        O3[Memory: 200MB]
        O4[FPS: 60]
    end

    N1 --> N2 --> N3 --> N4
    O1 --> O2 --> O3 --> O4

    style N1 fill:#ffe1e1
    style N2 fill:#ffe1e1
    style N3 fill:#ffe1e1
    style N4 fill:#ffe1e1

    style O1 fill:#e1ffe1
    style O2 fill:#e1ffe1
    style O3 fill:#e1ffe1
    style O4 fill:#e1ffe1

    Improvement[225x faster!<br/>10x less memory]
    style Improvement fill:#ffd700,color:#000
```

---

## 3. Drone Data Pipeline

### End-to-End Workflow

```mermaid
graph TB
    subgraph "1. Data Capture"
        DRONE[DJI Drone Flight]
        IMAGES[JPEG Images<br/>GPS EXIF]
        LOG[Flight Log<br/>Telemetry]
        GCP[Ground Control Points<br/>Optional]
    end

    subgraph "2. Processing (OpenDroneMap)"
        ODM[OpenDroneMap<br/>Docker Container]
        SFM[Structure from Motion]
        PC_GEN[Point Cloud Generation]
        ORTHO[Orthomosaic Creation]
        DEM_GEN[DEM/DSM Generation]
    end

    subgraph "3. Outputs"
        LAZ[point_cloud.laz<br/>100M-1B points]
        ORTHO_TIF[orthophoto.tif<br/>GeoTIFF 2-20GB]
        DSM[dsm.tif<br/>Digital Surface Model]
        DTM[dtm.tif<br/>Digital Terrain Model]
        MESH[textured_model.obj<br/>3D Mesh]
    end

    subgraph "4. Optimization"
        PDAL[PDAL Pipeline<br/>Point Cloud Processing]
        COG[GDAL COG Conversion<br/>Cloud Optimized]
        TILES[3D Tiles Generator<br/>LOD Pyramid]
    end

    subgraph "5. Storage (Honua.Server)"
        POSTGIS[(PostGIS<br/>Point Cloud Tables)]
        S3[(S3/Blob Storage<br/>COG Files)]
        CDN[(CDN<br/>3D Tiles)]
    end

    subgraph "6. Serving"
        OGC_API[OGC API Features<br/>Point Cloud Query]
        WMS_API[WMS/WMTS<br/>Raster Tiles]
        TILES_API[3D Tiles Endpoint<br/>Streaming]
    end

    subgraph "7. Client Visualization"
        MAPLIBRE[MapLibre GL JS<br/>Base Map]
        DECKGL[Deck.gl<br/>Point Cloud]
        TERRAIN[Terrain Layer<br/>DEM]
        ORTHO_LAYER[Orthophoto Overlay]
    end

    DRONE --> IMAGES
    DRONE --> LOG
    DRONE --> GCP

    IMAGES --> ODM
    LOG --> ODM
    GCP --> ODM

    ODM --> SFM
    SFM --> PC_GEN
    SFM --> ORTHO
    SFM --> DEM_GEN

    PC_GEN --> LAZ
    ORTHO --> ORTHO_TIF
    DEM_GEN --> DSM
    DEM_GEN --> DTM
    PC_GEN --> MESH

    LAZ --> PDAL
    ORTHO_TIF --> COG
    DSM --> COG
    MESH --> TILES

    PDAL --> POSTGIS
    COG --> S3
    TILES --> CDN

    POSTGIS --> OGC_API
    S3 --> WMS_API
    CDN --> TILES_API

    OGC_API --> DECKGL
    WMS_API --> ORTHO_LAYER
    TILES_API --> DECKGL
    WMS_API --> TERRAIN

    DECKGL --> MAPLIBRE
    TERRAIN --> MAPLIBRE
    ORTHO_LAYER --> MAPLIBRE

    style DRONE fill:#e1f5ff
    style POSTGIS fill:#ffe1e1
    style S3 fill:#ffe1e1
    style CDN fill:#ffe1e1
    style MAPLIBRE fill:#e1ffe1
```

### Point Cloud LOD Strategy

```mermaid
graph TB
    subgraph "Full Point Cloud"
        FULL[100% Points<br/>100M points<br/>400MB]
    end

    subgraph "LOD Decimation"
        DECIMATE[Decimation Process<br/>Grid-based sampling]
    end

    subgraph "LOD Levels"
        LOD0[LOD 0: Full<br/>100% - 100M points<br/>For zoom > 18]
        LOD1[LOD 1: Coarse<br/>10% - 10M points<br/>For zoom 14-18]
        LOD2[LOD 2: Sparse<br/>1% - 1M points<br/>For zoom < 14]
    end

    subgraph "Storage"
        TABLE0[(drone_point_cloud<br/>Full table)]
        TABLE1[(drone_point_cloud_lod1<br/>Materialized view)]
        TABLE2[(drone_point_cloud_lod2<br/>Materialized view)]
    end

    subgraph "Client Selection"
        ZOOM[User Zoom Level]
        SELECT{Select LOD}
        RENDER[Render at 60fps]
    end

    FULL --> DECIMATE

    DECIMATE --> LOD0
    DECIMATE --> LOD1
    DECIMATE --> LOD2

    LOD0 --> TABLE0
    LOD1 --> TABLE1
    LOD2 --> TABLE2

    ZOOM --> SELECT

    SELECT -->|Zoom > 18| TABLE0
    SELECT -->|Zoom 14-18| TABLE1
    SELECT -->|Zoom < 14| TABLE2

    TABLE0 --> RENDER
    TABLE1 --> RENDER
    TABLE2 --> RENDER

    style FULL fill:#ffe1e1
    style LOD0 fill:#fff4e1
    style LOD1 fill:#e1ffe1
    style LOD2 fill:#e1f5ff
```

---

## 4. Meta Quest WebXR Integration

### WebXR Architecture

```mermaid
graph TB
    subgraph "Meta Quest Hardware"
        QUEST[Meta Quest 2/3/Pro<br/>Snapdragon XR2<br/>72-90Hz Display]
    end

    subgraph "Browser Engine"
        BROWSER[Meta Quest Browser<br/>Chromium-based]
        WEBXR_API[WebXR Device API]
    end

    subgraph "JavaScript 3D Framework"
        THREEJS[Three.js<br/>3D Scene Graph]
        WEBXR_LIB[WebXR Manager]
        CONTROLS[VR Controllers<br/>Input Handling]
    end

    subgraph "Honua Integration Layer"
        HONUA_VR[honua-vr.js<br/>VR Orchestrator]
        GEOM_VR[Geometry Loader<br/>OGC API Client]
        SPATIAL[Spatial Anchoring<br/>GPS Integration]
    end

    subgraph "Data Sources"
        OGC[OGC API Features<br/>3D Geometries]
        TERRAIN_DATA[Terrain Tiles<br/>Elevation]
        ORTHO[Orthophoto<br/>Imagery]
    end

    subgraph "Rendering Pipeline"
        SCENE[3D Scene<br/>Geometries + Terrain]
        CAMERA[Stereo Camera<br/>Left/Right Eye]
        RENDER[WebGL Renderer<br/>72-90fps]
    end

    QUEST --> BROWSER
    BROWSER --> WEBXR_API

    WEBXR_API --> THREEJS
    WEBXR_API --> WEBXR_LIB
    WEBXR_API --> CONTROLS

    THREEJS --> HONUA_VR
    WEBXR_LIB --> HONUA_VR
    CONTROLS --> HONUA_VR

    HONUA_VR --> GEOM_VR
    HONUA_VR --> SPATIAL

    GEOM_VR --> OGC
    GEOM_VR --> TERRAIN_DATA
    GEOM_VR --> ORTHO

    OGC --> SCENE
    TERRAIN_DATA --> SCENE
    ORTHO --> SCENE

    SCENE --> CAMERA
    CAMERA --> RENDER

    RENDER --> QUEST

    style QUEST fill:#e1f5ff
    style WEBXR_API fill:#ffe1e1
    style SCENE fill:#e1ffe1
    style RENDER fill:#fff4e1
```

### User Interaction Flow in VR

```mermaid
sequenceDiagram
    participant User as VR User
    participant Quest as Meta Quest
    participant WebXR as WebXR API
    participant Honua as Honua VR App
    participant GPS as GPS/Location
    participant Server as Honua Server

    User->>Quest: Put on headset
    Quest->>WebXR: Initialize VR session
    WebXR->>Honua: VR session ready

    Honua->>GPS: Get current location
    GPS-->>Honua: Lat/Lon coordinates

    Honua->>Server: Fetch 3D data for location
    Server-->>Honua: GeoJSON 3D features

    Honua->>Honua: Create spatial anchors<br/>Align with GPS
    Honua->>WebXR: Render 3D scene

    WebXR->>Quest: Display stereo view
    Quest-->>User: Immersive 3D visualization

    User->>Quest: Point controller at feature
    Quest->>WebXR: Controller raycast
    WebXR->>Honua: Feature selected

    Honua->>Honua: Show attribute panel
    Honua->>WebXR: Update UI overlay
    WebXR->>Quest: Render overlay
    Quest-->>User: See feature details

    User->>Quest: Walk around
    Quest->>GPS: Update position
    GPS-->>Honua: New coordinates
    Honua->>Honua: Update spatial anchors
    Honua->>Server: Stream new data (if needed)
    Server-->>Honua: Additional features
```

### Field Survey Use Case

```mermaid
graph TB
    subgraph "Traditional Workflow"
        T1[Walk to site]
        T2[Pull out tablet/phone]
        T3[Launch app & wait]
        T4[Navigate to location]
        T5[Tap to select feature]
        T6[Read attributes]
        T7[Type notes]
        T8[Take photo]
        T9[Upload later]
    end

    subgraph "VR Workflow (Meta Quest)"
        V1[Walk to site<br/>wearing Quest]
        V2[Auto-load based on GPS<br/>hands-free]
        V3[Look at feature<br/>auto-highlight]
        V4[Voice command<br/>read attributes]
        V5[Voice note<br/>hands-free]
        V6[Automatic sync<br/>real-time]
    end

    T1 --> T2 --> T3 --> T4 --> T5 --> T6 --> T7 --> T8 --> T9
    V1 --> V2 --> V3 --> V4 --> V5 --> V6

    T_TIME[Total Time: 8-10 minutes<br/>Many manual steps]
    V_TIME[Total Time: 2-3 minutes<br/>50-70% time savings]

    T9 --> T_TIME
    V6 --> V_TIME

    style T_TIME fill:#ffe1e1
    style V_TIME fill:#e1ffe1
```

---

## 5. 3D Geometry Stack

### Complete Server-to-Client Flow

```mermaid
graph TB
    subgraph "Storage Layer (Server)"
        POSTGIS[(PostGIS Database<br/>PointZ, LineStringZ, PolygonZ)]
        SPATIALITE[(SpatiaLite<br/>3D Geometries)]
        GPKG[(GeoPackage<br/>3D Extension)]
    end

    subgraph "Server Processing (C#)"
        READER[GeometryReader.cs<br/>WKB/WKT/GeoJSON Parser]
        VALIDATOR[ThreeDimensionalValidator.cs<br/>Validation]
        HELPER[GeometryTypeHelper.cs<br/>Z/M Detection]
        TRANSFORM[CoordinateTransform3D.cs<br/>CRS Transformation]
    end

    subgraph "Serialization"
        GEOJSON[GeoJSON 3D<br/>[lon, lat, z]]
        WKT[WKT Z<br/>POINT Z (x y z)]
        WKB[WKB<br/>Binary format]
    end

    subgraph "OGC APIs"
        FEATURES[OGC API Features<br/>3D Queries]
        WFS[WFS 2.0<br/>3D Bbox support]
        CRS84H[CRS84H Support<br/>Ellipsoidal heights]
    end

    subgraph "Network Transport"
        HTTP[HTTP/2 Streaming]
        GZIP[GZIP Compression]
    end

    subgraph "Client Parsing (JavaScript)"
        PARSER[honua-geometry-3d.js<br/>Parse 3D coordinates]
        WORKER_PARSE[Web Worker<br/>Background parsing]
    end

    subgraph "Client Rendering"
        DECKGL_LAYER[Deck.gl Layers<br/>GeoJsonLayer 3D]
        WEBGL_RENDER[WebGL Rendering<br/>GPU acceleration]
    end

    subgraph "Display"
        SCREEN[60fps Display<br/>3D Visualization]
    end

    POSTGIS --> READER
    SPATIALITE --> READER
    GPKG --> READER

    READER --> VALIDATOR
    READER --> HELPER
    VALIDATOR --> TRANSFORM

    TRANSFORM --> GEOJSON
    TRANSFORM --> WKT
    TRANSFORM --> WKB

    GEOJSON --> FEATURES
    WKT --> WFS
    GEOJSON --> CRS84H

    FEATURES --> HTTP
    WFS --> HTTP
    CRS84H --> HTTP

    HTTP --> GZIP
    GZIP --> PARSER

    PARSER --> WORKER_PARSE
    WORKER_PARSE --> DECKGL_LAYER

    DECKGL_LAYER --> WEBGL_RENDER
    WEBGL_RENDER --> SCREEN

    style POSTGIS fill:#ffe1e1
    style READER fill:#fff4e1
    style GEOJSON fill:#e1ffe1
    style PARSER fill:#e1f5ff
    style SCREEN fill:#e1ffe1
```

### Coordinate System Flow

```mermaid
graph LR
    subgraph "Input CRS"
        WGS84[WGS84 2D<br/>EPSG:4326<br/>lon, lat]
        WGS84_3D[WGS84 3D<br/>EPSG:4979<br/>lon, lat, h]
        UTM[UTM Zone<br/>EPSG:326XX<br/>x, y, z]
        LOCAL[Local CRS<br/>Custom SRID]
    end

    subgraph "Transformation Layer"
        PROJ[PROJ Library<br/>Datum shifts<br/>Height conversions]
    end

    subgraph "Output CRS"
        CRS84[CRS84 2D<br/>OGC Standard]
        CRS84H[CRS84H 3D<br/>Ellipsoidal heights]
        WEBMERC[Web Mercator 3D<br/>EPSG:3857 + Z]
    end

    subgraph "Client Rendering"
        LNGLAT[Lng/Lat/Alt<br/>MapLibre/Deck.gl]
    end

    WGS84 --> PROJ
    WGS84_3D --> PROJ
    UTM --> PROJ
    LOCAL --> PROJ

    PROJ --> CRS84
    PROJ --> CRS84H
    PROJ --> WEBMERC

    CRS84 --> LNGLAT
    CRS84H --> LNGLAT
    WEBMERC --> LNGLAT

    style WGS84_3D fill:#e1ffe1
    style CRS84H fill:#e1ffe1
    style LNGLAT fill:#e1f5ff
```

---

## 6. Point Cloud Rendering Pipeline

### High-Performance Rendering

```mermaid
graph TB
    subgraph "Data Source"
        SURVEY[Drone Survey<br/>100M points LAZ]
    end

    subgraph "Server Query Optimization"
        SPATIAL_INDEX[(PostGIS Spatial Index<br/>GIST/BRIN)]
        LOD_SELECT{LOD Selection<br/>Based on zoom}
        BBOX_FILTER[Bounding Box Filter<br/>Viewport culling]
    end

    subgraph "Data Transfer"
        STREAM[Streaming Response<br/>GeoJSON-Seq]
        BINARY[Binary Format<br/>TypedArray]
    end

    subgraph "Client Processing"
        INCREMENTAL[Incremental Loading<br/>Show partial results]
        WORKER_PROC[Web Worker<br/>Non-blocking parse]
    end

    subgraph "GPU Pipeline"
        INSTANCING[GPU Instancing<br/>Single draw call]
        CULLING[Frustum Culling<br/>GPU-side]
        DEPTH[Depth Testing<br/>Z-buffer]
    end

    subgraph "Rendering"
        POINTS[Point Sprites<br/>Textured quads]
        LIGHTING[Lighting<br/>Per-vertex normals]
        COLOR[Color Mapping<br/>Classification/RGB]
    end

    subgraph "Display"
        FPS[60 FPS Output<br/>1M+ points]
    end

    SURVEY --> SPATIAL_INDEX
    SPATIAL_INDEX --> LOD_SELECT

    LOD_SELECT -->|Zoom > 18| BBOX_FILTER
    LOD_SELECT -->|Zoom 14-18| BBOX_FILTER
    LOD_SELECT -->|Zoom < 14| BBOX_FILTER

    BBOX_FILTER --> STREAM
    BBOX_FILTER --> BINARY

    STREAM --> INCREMENTAL
    BINARY --> WORKER_PROC

    INCREMENTAL --> INSTANCING
    WORKER_PROC --> INSTANCING

    INSTANCING --> CULLING
    CULLING --> DEPTH

    DEPTH --> POINTS
    DEPTH --> LIGHTING
    DEPTH --> COLOR

    POINTS --> FPS
    LIGHTING --> FPS
    COLOR --> FPS

    style SURVEY fill:#ffe1e1
    style LOD_SELECT fill:#fff4e1
    style WORKER_PROC fill:#e1f5ff
    style FPS fill:#e1ffe1
```

### Memory Management

```mermaid
graph TB
    subgraph "Initial State"
        EMPTY[Browser: 50MB<br/>No data loaded]
    end

    subgraph "Data Loading"
        FETCH[Fetch 10MB GeoJSON<br/>Streaming]
        PARSE[Parse in Worker<br/>+15MB temp]
        TRANSFER[Transfer to main<br/>Zero-copy]
    end

    subgraph "GPU Upload"
        VERTEX_BUFFER[Vertex Buffer<br/>Position: 12MB]
        COLOR_BUFFER[Color Buffer<br/>RGB: 4MB]
        INDEX_BUFFER[Index Buffer<br/>2MB]
    end

    subgraph "Rendering State"
        ACTIVE[Active Memory:<br/>Browser: 50MB<br/>Worker: 0MB<br/>GPU: 18MB<br/>Total: 68MB]
    end

    subgraph "Cleanup"
        GC[Garbage Collection<br/>Worker temp freed]
        REUSE[Buffer reuse<br/>No reallocation]
    end

    EMPTY --> FETCH
    FETCH --> PARSE
    PARSE --> TRANSFER

    TRANSFER --> VERTEX_BUFFER
    TRANSFER --> COLOR_BUFFER
    TRANSFER --> INDEX_BUFFER

    VERTEX_BUFFER --> ACTIVE
    COLOR_BUFFER --> ACTIVE
    INDEX_BUFFER --> ACTIVE

    ACTIVE --> GC
    GC --> REUSE

    style EMPTY fill:#e1ffe1
    style ACTIVE fill:#fff4e1
    style REUSE fill:#e1ffe1
```

---

## 7. Performance Optimization Flow

### Request Lifecycle

```mermaid
sequenceDiagram
    participant User
    participant UI as UI Thread
    participant Worker as Web Worker
    participant Cache as Browser Cache
    participant CDN
    participant Server as Honua Server
    participant DB as PostGIS

    User->>UI: Pan map to new area

    UI->>Cache: Check cache
    alt Data in cache
        Cache-->>UI: Return cached data
        UI->>User: Render (50ms)
    else Cache miss
        UI->>CDN: Request data
        CDN->>CDN: Check CDN cache

        alt CDN hit
            CDN-->>UI: Return data (200ms)
        else CDN miss
            CDN->>Server: Forward request
            Server->>DB: PostGIS query with LOD

            DB->>DB: Spatial index lookup
            DB->>DB: Apply LOD filter
            DB-->>Server: Point cloud subset

            Server->>Server: GeoJSON serialization
            Server-->>CDN: Stream response
            CDN->>CDN: Cache for 1 hour
            CDN-->>UI: Stream to client (500ms)
        end

        UI->>Worker: Parse GeoJSON
        Worker->>Worker: Triangulation, LOD
        Worker-->>UI: Processed mesh

        UI->>Cache: Store in cache
        UI->>User: Render (800ms total)
    end
```

### Optimization Decision Tree

```mermaid
graph TB
    START[User requests 3D data]

    CHECK_SIZE{Dataset size?}

    SMALL[< 10K features]
    MEDIUM[10K - 100K features]
    LARGE[100K - 1M features]
    HUGE[> 1M features]

    FULL_LOAD[Load all features<br/>No LOD needed]
    LOD1[Use LOD 1<br/>10% decimation]
    LOD2[Use LOD 2<br/>1% decimation]
    TILING[Use 3D Tiles<br/>Spatial partitioning]

    RENDER_FULL[Direct rendering<br/>~100ms]
    RENDER_WORKER[Web Worker processing<br/>~500ms]
    RENDER_STREAM[Streaming + Worker<br/>~1000ms]
    RENDER_PROGRESSIVE[Progressive loading<br/>Show partial results]

    DISPLAY[Display to user<br/>60fps]

    START --> CHECK_SIZE

    CHECK_SIZE -->|< 10K| SMALL
    CHECK_SIZE -->|10K-100K| MEDIUM
    CHECK_SIZE -->|100K-1M| LARGE
    CHECK_SIZE -->|> 1M| HUGE

    SMALL --> FULL_LOAD
    MEDIUM --> LOD1
    LARGE --> LOD2
    HUGE --> TILING

    FULL_LOAD --> RENDER_FULL
    LOD1 --> RENDER_WORKER
    LOD2 --> RENDER_STREAM
    TILING --> RENDER_PROGRESSIVE

    RENDER_FULL --> DISPLAY
    RENDER_WORKER --> DISPLAY
    RENDER_STREAM --> DISPLAY
    RENDER_PROGRESSIVE --> DISPLAY

    style START fill:#e1f5ff
    style DISPLAY fill:#e1ffe1
    style FULL_LOAD fill:#e1ffe1
    style LOD1 fill:#fff4e1
    style LOD2 fill:#ffe1e1
    style TILING fill:#ffe1e1
```

### Progressive Enhancement

```mermaid
graph LR
    subgraph "Basic Support"
        B1[2D Map<br/>MapLibre GL]
        B2[Vector Tiles<br/>Standard rendering]
        B3[Raster overlays<br/>Orthophotos]
    end

    subgraph "3D Support"
        D1[3D Terrain<br/>MapLibre terrain layer]
        D2[Extrusion<br/>Building heights]
        D3[Pitch/Bearing<br/>Camera controls]
    end

    subgraph "Advanced 3D"
        A1[Deck.gl layers<br/>Point clouds]
        A2[3D Geometries<br/>Z coordinates]
        A3[Real-time rendering<br/>1M+ features]
    end

    subgraph "VR/AR"
        V1[WebXR support<br/>Meta Quest]
        V2[Spatial anchoring<br/>GPS integration]
        V3[Immersive mode<br/>Field work]
    end

    B1 --> B2 --> B3
    B3 --> D1
    D1 --> D2 --> D3
    D3 --> A1
    A1 --> A2 --> A3
    A3 --> V1
    V1 --> V2 --> V3

    style B1 fill:#e1ffe1
    style D1 fill:#e1f5ff
    style A1 fill:#fff4e1
    style V1 fill:#ffe1e1
```

---

## Summary

All diagrams are created using Mermaid syntax and will render beautifully on GitHub, in VS Code with the Mermaid extension, or any markdown viewer that supports Mermaid.

### Quick Reference

| Diagram | Purpose | Key Insight |
|---------|---------|-------------|
| Client 3D Architecture | Overall system design | MapLibre + Deck.gl stack |
| Blazor Interop | Performance optimization | 225x faster with proper pattern |
| Drone Pipeline | Data processing workflow | Capture to visualization in hours |
| Meta Quest | VR/AR integration | WebXR-first approach, 50-70% time savings |
| 3D Geometry Stack | Data flow | Server → Network → Client → GPU |
| Point Cloud Rendering | High-performance display | 60fps with millions of points |
| Performance Flow | Optimization strategy | Progressive enhancement |

### Rendering Tips

To view these diagrams:

1. **GitHub:** Automatically renders Mermaid
2. **VS Code:** Install "Markdown Preview Mermaid Support" extension
3. **Online:** Use [Mermaid Live Editor](https://mermaid.live)
4. **Export:** Use `mmdc` CLI to export as PNG/SVG

```bash
# Install mermaid CLI
npm install -g @mermaid-js/mermaid-cli

# Export diagram
mmdc -i ARCHITECTURE_DIAGRAMS.md -o diagrams.png
```
