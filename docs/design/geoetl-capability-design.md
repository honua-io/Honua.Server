# Honua GeoETL: AI-Powered Cloud-Native Geospatial Data Pipeline Platform

**Version:** 1.0
**Date:** 2025-11-05
**Status:** Design Proposal

---

## Executive Summary

This document proposes **Honua GeoETL**, a next-generation geospatial Extract, Transform, Load (ETL) platform that combines the visual workflow capabilities of FME with modern cloud-native architecture, AI-powered intelligence, and streaming data processing. The system integrates tightly with Honua's existing geoprocessing capabilities while pushing the industry forward with innovative features.

### Key Innovations

1. **AI-Powered Workflow Design** - Natural language workflow creation and intelligent transformation suggestions
2. **Cloud-Native Streaming** - Real-time geospatial data pipelines with Kafka/GeoMesa integration
3. **Modern Data Formats** - First-class support for GeoParquet, GeoArrow, PMTiles, and cloud-optimized formats
4. **Intelligent Data Quality** - LLM-powered validation, cleansing, and schema evolution
5. **Unified Geoprocessing Integration** - Seamless integration with Honua's existing tiered execution system
6. **Web-Based Visual Designer** - Modern, collaborative workflow design with no client installation required

---

## 1. Competitive Analysis

### 1.1 FME (Safe Software)

**Strengths:**
- 355+ data format support
- Visual workflow designer (FME Workbench)
- Extensive transformation library (500+ transformers)
- Server-based automation (FME Flow)
- Strong enterprise adoption

**Weaknesses:**
- Desktop-first architecture (heavy client)
- Expensive licensing model
- Limited cloud-native capabilities
- No real-time streaming support
- No AI/LLM integration
- Proprietary format (.fmw files)

### 1.2 Alternative Solutions

**Open Source:**
- **GeoKettle** - Spatial ETL based on Pentaho, but development has stalled
- **HALE Studio** - INSPIRE data harmonization, limited scope
- **GDAL/OGR** - Command-line only, format conversion focused

**Commercial:**
- **Talend** - General ETL with some spatial support, but not geospatial-first
- **Alteryx** - Strong analytics but expensive and desktop-focused
- **Azure Data Factory** - Cloud integration but limited spatial capabilities

**Modern Orchestration:**
- **Apache Airflow** - Task orchestration, no visual design or spatial awareness
- **Prefect** - Modern Python-based, but not spatial-focused
- **Dagster** - Software-defined assets, requires coding

### 1.3 Gap Analysis

| Capability | FME | Open Source | Modern Orchestrators | **Honua GeoETL** |
|------------|-----|-------------|---------------------|------------------|
| Visual Workflow Design | ✅ Desktop | ❌ | ❌ | ✅ **Web-based** |
| Spatial Operations | ✅ | Partial | ❌ | ✅ **Enhanced** |
| Cloud-Native | ⚠️ Limited | ❌ | ✅ | ✅ |
| Real-Time Streaming | ❌ | ❌ | ⚠️ | ✅ **GeoMesa/Kafka** |
| AI-Powered Design | ❌ | ❌ | ❌ | ✅ **LLM Integration** |
| Modern Formats (GeoParquet) | ⚠️ | ⚠️ | ❌ | ✅ **First-class** |
| Intelligent Data Quality | ❌ | ❌ | ❌ | ✅ **AI Validation** |
| Multi-Tenant SaaS | ⚠️ | ❌ | ⚠️ | ✅ |
| OGC Standards | ⚠️ | Partial | ❌ | ✅ **Native** |
| Cost Model | 💰💰💰 | Free | 💰 | 💰 **Consumption-based** |

---

## 2. Architecture Overview

### 2.1 System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                     Honua GeoETL Platform                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │         Web-Based Visual Workflow Designer              │  │
│  │  • Drag-and-drop canvas                                 │  │
│  │  • AI-powered natural language workflow creation        │  │
│  │  • Real-time collaboration                              │  │
│  │  • Git-based version control                            │  │
│  └─────────────────────────────────────────────────────────┘  │
│                            ↓                                   │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │              Workflow Orchestration Engine               │  │
│  │  • DAG-based execution                                   │  │
│  │  • Dagster-inspired software-defined assets             │  │
│  │  • Data lineage tracking                                 │  │
│  │  • Dependency resolution                                 │  │
│  └─────────────────────────────────────────────────────────┘  │
│                            ↓                                   │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │           Intelligent Transformation Layer               │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │  │
│  │  │   AI Agent   │  │  Schema      │  │  Data        │ │  │
│  │  │   Assistant  │  │  Evolution   │  │  Quality     │ │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘ │  │
│  └─────────────────────────────────────────────────────────┘  │
│                            ↓                                   │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │              Execution Engine (3-Tier)                   │  │
│  │  ┌──────────┐  ┌──────────┐  ┌────────────────────┐   │  │
│  │  │  Batch   │  │ Streaming│  │   Geoprocessing    │   │  │
│  │  │  ETL     │  │   ETL    │  │   (Existing)       │   │  │
│  │  │  (Jobs)  │  │ (Kafka)  │  │   • NTS            │   │  │
│  │  │          │  │          │  │   • PostGIS        │   │  │
│  │  │          │  │          │  │   • Cloud Batch    │   │  │
│  │  └──────────┘  └──────────┘  └────────────────────┘   │  │
│  └─────────────────────────────────────────────────────────┘  │
│                            ↓                                   │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │           Data Connectors & Format Support               │  │
│  │  • Cloud-native: GeoParquet, GeoArrow, PMTiles, COG    │  │
│  │  • Traditional: Shapefile, GeoJSON, GeoPackage, KML    │  │
│  │  • Database: PostGIS, SpatiaLite, Oracle Spatial       │  │
│  │  • Cloud Storage: S3, Azure Blob, GCS                  │  │
│  │  • Streaming: Kafka, Kinesis, Pub/Sub                  │  │
│  │  • IoT: SensorThings API, MQTT                         │  │
│  │  • GDAL/OGR: 355+ formats                              │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Integration with Existing Geoprocessing

The GeoETL system **extends** rather than **replaces** the existing geoprocessing capabilities:

```
ETL Workflow Node (e.g., "Buffer Roads")
        ↓
   Validates Parameters
        ↓
   Calls IGeoprocessingService.ExecuteJobAsync()
        ↓
   TierExecutorCoordinator selects tier
        ↓
   ┌──────────┬───────────┬─────────────┐
   │   NTS    │  PostGIS  │ Cloud Batch │
   │ Executor │  Executor │  Executor   │
   └──────────┴───────────┴─────────────┘
        ↓
   Returns results to ETL workflow
        ↓
   Continues to next workflow node
```

**Key Integration Points:**

1. **Workflow Node Type: "GeoprocessingOperation"**
   - Exposes all existing operations (Buffer, Intersection, Union, etc.)
   - Leverages existing validation, estimation, and progress tracking
   - Inherits tiered execution strategy

2. **Resource Estimation**
   - Use `IGeoprocessingService.EstimateJobAsync()` for workflow planning
   - Display estimated time/cost before execution
   - Optimize workflow graph based on estimates

3. **Progress Tracking**
   - ETL workflow UI shows geoprocessing progress
   - Unified progress reporting across all node types

4. **Data Lineage**
   - Track which geoprocessing operations were used
   - Link ETL workflow runs to ProcessRun records
   - Maintain full audit trail

---

## 3. Core Capabilities

### 3.1 Visual Workflow Designer (Web-Based)

**Technology Stack:**
- **Frontend:** React + TypeScript
- **Canvas Library:** React Flow or Xyflow (modern, performant)
- **Code Editor:** Monaco Editor (VS Code engine)
- **Collaboration:** Yjs (CRDT-based real-time collaboration)

**Features:**

1. **Node Types:**
   - **Data Sources** - File upload, URL, database, S3, streaming
   - **Transformations** - Attribute operations, geometry operations, joins, aggregations
   - **Geoprocessing** - All existing operations (buffer, intersection, etc.)
   - **AI Nodes** - Smart validation, data quality, schema mapping
   - **Data Sinks** - File export, database, API, streaming output
   - **Control Flow** - Conditional logic, loops, error handling

2. **AI-Powered Design:**
   ```
   User: "Load SF building footprints, buffer by 10m, find intersections with parcels"

   AI generates workflow:
   [Load GeoJSON] → [Buffer: 10m] → [Spatial Join: Intersects] → [Export GeoParquet]
   ```

3. **Smart Suggestions:**
   - Detects schema mismatches and suggests transformations
   - Recommends optimal data formats based on use case
   - Suggests performance optimizations (e.g., simplify before buffer)

4. **Validation Before Execution:**
   - Static analysis of workflow graph
   - Schema validation across nodes
   - Resource estimation for entire workflow
   - Cost estimation with breakdown

### 3.2 Workflow Definition Format

**Format:** JSON-based, Git-friendly, human-readable

```json
{
  "version": "1.0",
  "metadata": {
    "id": "workflow-123",
    "name": "Urban Heat Island Analysis",
    "description": "Analyze temperature variations in urban areas",
    "author": "user@example.com",
    "created": "2025-11-05T10:00:00Z",
    "tags": ["climate", "urban-planning"]
  },
  "parameters": {
    "input_region": {
      "type": "geometry",
      "description": "Analysis boundary",
      "required": true
    },
    "buffer_distance": {
      "type": "number",
      "description": "Buffer distance in meters",
      "default": 100,
      "min": 0,
      "max": 5000
    }
  },
  "nodes": [
    {
      "id": "load_buildings",
      "type": "data_source.ogc_features",
      "parameters": {
        "collection": "buildings",
        "filter": "{{parameters.input_region}}"
      }
    },
    {
      "id": "buffer_buildings",
      "type": "geoprocessing.buffer",
      "parameters": {
        "input": "{{nodes.load_buildings.output}}",
        "distance": "{{parameters.buffer_distance}}",
        "unit": "meters"
      },
      "execution": {
        "tier_preference": "postgis",
        "timeout": 300
      }
    },
    {
      "id": "quality_check",
      "type": "ai.data_quality",
      "parameters": {
        "input": "{{nodes.buffer_buildings.output}}",
        "checks": ["geometry_validity", "topology", "duplicates"]
      }
    },
    {
      "id": "export_results",
      "type": "data_sink.geoparquet",
      "parameters": {
        "input": "{{nodes.quality_check.output}}",
        "path": "s3://results/{{metadata.id}}/output.parquet",
        "compression": "snappy"
      }
    }
  ],
  "edges": [
    {"from": "load_buildings", "to": "buffer_buildings"},
    {"from": "buffer_buildings", "to": "quality_check"},
    {"from": "quality_check", "to": "export_results"}
  ]
}
```

### 3.3 Streaming ETL (Real-Time)

**Architecture:**

```
IoT Sensors → Kafka Topic → GeoETL Stream Processor → Output
                                    ↓
                         Spatial Operations (windowed)
                         • Buffer
                         • Intersection
                         • Proximity detection
                         • Aggregation
```

**Use Cases:**
- Real-time vehicle tracking and geofencing
- Sensor data aggregation and spatial joins
- Live event detection (e.g., ships entering port zones)
- Streaming map updates

**Technology:**
- **Apache Kafka** - Message broker
- **GeoMesa Kafka DataStore** - Spatial indexing on streams
- **Stream Processing** - Custom .NET-based processor using Kafka Streams concepts

**Example Stream Workflow:**

```json
{
  "type": "streaming_workflow",
  "source": {
    "type": "kafka",
    "topic": "vehicle-positions",
    "bootstrap_servers": "kafka:9092",
    "format": "geojson"
  },
  "operations": [
    {
      "type": "geofence_check",
      "geofence_collection": "restricted_zones",
      "alert_on": "enter"
    },
    {
      "type": "spatial_aggregation",
      "window": "5 minutes",
      "operation": "count_by_region",
      "regions_collection": "grid_cells"
    }
  ],
  "sinks": [
    {
      "type": "kafka",
      "topic": "geofence-alerts"
    },
    {
      "type": "sensorthings_api",
      "datastream_id": "ds-123"
    }
  ]
}
```

### 3.4 AI-Powered Capabilities

#### 3.4.1 Natural Language Workflow Creation

**Implementation:**
- LLM integration (OpenAI GPT-4, Anthropic Claude, or self-hosted)
- Semantic understanding of geospatial operations
- Context-aware suggestions based on data schema

**Example Interactions:**

```
User: "Clean up the building footprints - remove duplicates and fix invalid geometries"

AI generates:
[Input] → [Detect Duplicates (AI)] → [Remove Duplicates]
       → [Validate Geometries] → [Fix Invalid Geometries] → [Output]

User: "Join these property parcels with tax records by parcel ID"

AI generates:
[Parcels (Spatial)] + [Tax Records (CSV)] → [Attribute Join: parcel_id] → [Output]
```

#### 3.4.2 Intelligent Data Quality

**AI-Powered Checks:**

1. **Automated Validation:**
   - Geometry validity (self-intersections, holes, etc.)
   - Topology consistency
   - Attribute completeness
   - Reference data consistency

2. **Smart Cleansing:**
   - LLM-powered address normalization
   - PII detection and masking
   - Fuzzy matching for entity resolution
   - Automated schema mapping

3. **Anomaly Detection:**
   - Statistical outliers in spatial distributions
   - Unexpected attribute patterns
   - Temporal anomalies in time-series data

**Example:**

```json
{
  "node": "ai_quality_check",
  "type": "ai.data_quality",
  "parameters": {
    "checks": [
      {
        "type": "llm_validation",
        "prompt": "Check if address field contains valid US addresses",
        "field": "address",
        "action_on_failure": "flag"
      },
      {
        "type": "geometry_validation",
        "rules": ["no_self_intersection", "valid_rings"],
        "action_on_failure": "fix_or_remove"
      },
      {
        "type": "completeness",
        "required_fields": ["name", "address", "geometry"],
        "action_on_failure": "flag"
      }
    ]
  }
}
```

#### 3.4.3 Intelligent Schema Mapping

**Problem:** Different data sources have different schemas for the same entity type.

**AI Solution:**
- Analyze source and target schemas
- Suggest attribute mappings based on semantic similarity
- Generate transformation code automatically

**Example:**

```
Source Schema:          Target Schema:
- addr_line_1          - address
- addr_line_2          - city
- city_name            - state
- st                   - zip_code
- zipcode

AI suggests:
address = CONCAT(addr_line_1, ' ', addr_line_2)
city = city_name
state = st
zip_code = zipcode
```

### 3.5 Cloud-Native Data Formats

**First-Class Support for Modern Formats:**

1. **GeoParquet**
   - Columnar storage for efficient queries
   - Cloud-native with S3 Select support
   - Spatial partitioning (GeoParquet 1.1)
   - 10-100x smaller than GeoJSON

2. **GeoArrow**
   - In-memory columnar format
   - Zero-copy sharing between processes
   - 23x faster than traditional formats with GDAL
   - Native geometry types

3. **PMTiles**
   - Single-file vector tile archives
   - No tile server required
   - HTTP range request support
   - Perfect for web map serving

4. **Cloud-Optimized GeoTIFF (COG)**
   - Already supported via existing raster capabilities
   - Enhanced integration with ETL workflows

**Format Conversion Node:**

```json
{
  "type": "format_converter",
  "input": "source.geojson",
  "output_format": "geoparquet",
  "options": {
    "compression": "snappy",
    "row_group_size": 100000,
    "spatial_partitioning": "hilbert",
    "geometry_encoding": "geoarrow"
  }
}
```

### 3.6 Advanced Transformation Library

**Categories:**

1. **Attribute Transformations**
   - Calculate fields (expressions, functions)
   - String operations (concat, split, regex)
   - Type conversions
   - Conditional logic

2. **Geometry Transformations**
   - Coordinate system reprojection
   - Geometry type conversion (e.g., polygon to centroid)
   - Generalization/simplification
   - Densification
   - Affine transformations (rotate, scale, translate)

3. **Spatial Operations** (leverages existing geoprocessing)
   - Buffer, intersection, union, difference
   - Dissolve, clip, erase
   - Convex hull, Voronoi diagrams
   - Thiessen polygons

4. **Spatial Relationships**
   - Spatial joins (intersects, within, contains, etc.)
   - Nearest neighbor analysis
   - Spatial clustering (DBSCAN, k-means)
   - Hot spot analysis

5. **Data Integration**
   - Attribute joins
   - Spatial joins
   - Merge/append datasets
   - Feature matching and conflation

6. **Aggregation & Statistics**
   - Group by (spatial or attribute)
   - Statistical summaries
   - Spatial aggregation (grid-based)
   - Time-series aggregation

7. **Data Quality**
   - Validation
   - Cleansing
   - Deduplication
   - Standardization

8. **Advanced Analytics**
   - Network analysis (routing, service areas)
   - Terrain analysis (slope, aspect, hillshade)
   - Viewshed analysis
   - Raster algebra

---

## 4. Technical Implementation

### 4.1 Backend Architecture

**Technology Stack:**
- **Language:** C# .NET 8+
- **API Framework:** ASP.NET Core (existing)
- **Orchestration:** Custom DAG engine (Dagster-inspired)
- **Streaming:** Kafka + custom processor
- **AI Integration:** LangChain.NET or Semantic Kernel
- **Data Processing:** NetTopologySuite, GDAL, PostGIS (existing)

**New Components:**

1. **Workflow Engine** (`Honua.Server.Enterprise.GeoETL`)
   ```
   /src/Honua.Server.Enterprise.GeoETL/
   ├── Engine/
   │   ├── IWorkflowEngine.cs
   │   ├── WorkflowExecutor.cs
   │   ├── DAGScheduler.cs
   │   └── NodeExecutor.cs
   ├── Nodes/
   │   ├── IWorkflowNode.cs
   │   ├── DataSourceNode.cs
   │   ├── TransformationNode.cs
   │   ├── GeoprocessingNode.cs (wraps IGeoprocessingService)
   │   ├── AINode.cs
   │   └── DataSinkNode.cs
   ├── Streaming/
   │   ├── IStreamProcessor.cs
   │   ├── KafkaStreamProcessor.cs
   │   └── GeoMesaIntegration.cs
   ├── AI/
   │   ├── ILLMService.cs
   │   ├── WorkflowGenerator.cs
   │   ├── SchemaMapper.cs
   │   └── DataQualityAgent.cs
   ├── Formats/
   │   ├── GeoParquetReader.cs
   │   ├── GeoParquetWriter.cs
   │   ├── GeoArrowConverter.cs
   │   └── PMTilesGenerator.cs
   └── Models/
       ├── WorkflowDefinition.cs
       ├── WorkflowRun.cs
       └── NodeResult.cs
   ```

2. **Database Schema Extensions**

```sql
-- Workflow definitions
CREATE TABLE geoetl_workflows (
    workflow_id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(tenant_id),
    name TEXT NOT NULL,
    description TEXT,
    definition JSONB NOT NULL, -- Full workflow JSON
    version INTEGER NOT NULL DEFAULT 1,
    created_by UUID NOT NULL REFERENCES users(user_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    tags TEXT[],
    is_published BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE
);

-- Workflow runs (execution history)
CREATE TABLE geoetl_workflow_runs (
    run_id UUID PRIMARY KEY,
    workflow_id UUID NOT NULL REFERENCES geoetl_workflows(workflow_id),
    tenant_id UUID NOT NULL REFERENCES tenants(tenant_id),
    status TEXT NOT NULL, -- pending, running, completed, failed, cancelled
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    triggered_by UUID REFERENCES users(user_id),
    trigger_type TEXT, -- manual, scheduled, api, event
    parameters JSONB,

    -- Metrics
    features_processed INTEGER,
    bytes_read BIGINT,
    bytes_written BIGINT,
    peak_memory_mb INTEGER,
    cpu_time_ms BIGINT,

    -- Cost tracking
    compute_cost_usd NUMERIC(10,4),
    storage_cost_usd NUMERIC(10,4),

    -- Results
    output_locations JSONB,
    error_message TEXT,
    error_stack TEXT,

    -- Lineage
    input_datasets JSONB,
    output_datasets JSONB
);

-- Node execution details
CREATE TABLE geoetl_node_runs (
    node_run_id UUID PRIMARY KEY,
    workflow_run_id UUID NOT NULL REFERENCES geoetl_workflow_runs(run_id),
    node_id TEXT NOT NULL,
    node_type TEXT NOT NULL,
    status TEXT NOT NULL,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_ms INTEGER,
    features_processed INTEGER,
    error_message TEXT,

    -- Link to geoprocessing runs if applicable
    geoprocessing_run_id UUID REFERENCES process_runs(run_id)
);

-- Streaming workflow state
CREATE TABLE geoetl_stream_processors (
    processor_id UUID PRIMARY KEY,
    workflow_id UUID NOT NULL REFERENCES geoetl_workflows(workflow_id),
    tenant_id UUID NOT NULL REFERENCES tenants(tenant_id),
    status TEXT NOT NULL, -- starting, running, paused, stopped, failed
    kafka_topics JSONB,
    consumer_group TEXT,
    last_offset JSONB,
    features_processed BIGINT,
    started_at TIMESTAMPTZ,
    last_heartbeat TIMESTAMPTZ
);

-- Indexes
CREATE INDEX idx_geoetl_workflows_tenant ON geoetl_workflows(tenant_id);
CREATE INDEX idx_geoetl_workflow_runs_workflow ON geoetl_workflow_runs(workflow_id);
CREATE INDEX idx_geoetl_workflow_runs_tenant ON geoetl_workflow_runs(tenant_id);
CREATE INDEX idx_geoetl_workflow_runs_status ON geoetl_workflow_runs(status);
CREATE INDEX idx_geoetl_node_runs_workflow_run ON geoetl_node_runs(workflow_run_id);
```

### 4.2 Frontend Architecture

**Technology Stack:**
- **Framework:** React 18+ with TypeScript
- **State Management:** Zustand or Jotai (lightweight)
- **Canvas Library:** Xyflow (React Flow v11+)
- **Code Editor:** Monaco Editor
- **UI Components:** Shadcn/ui or Mantine
- **Real-time Collaboration:** Yjs + WebSocket

**Component Structure:**

```
/web/src/geoetl/
├── components/
│   ├── WorkflowCanvas/
│   │   ├── WorkflowCanvas.tsx
│   │   ├── NodePalette.tsx
│   │   ├── NodeEditor.tsx
│   │   └── ConnectionEditor.tsx
│   ├── AIAssistant/
│   │   ├── ChatInterface.tsx
│   │   ├── WorkflowSuggestions.tsx
│   │   └── SmartValidation.tsx
│   ├── Execution/
│   │   ├── ExecutionPanel.tsx
│   │   ├── ProgressMonitor.tsx
│   │   └── ResultsViewer.tsx
│   └── Designer/
│       ├── PropertyPanel.tsx
│       ├── ValidationPanel.tsx
│       └── ResourceEstimator.tsx
├── nodes/
│   ├── DataSourceNode.tsx
│   ├── TransformationNode.tsx
│   ├── GeoprocessingNode.tsx
│   └── AINode.tsx
├── hooks/
│   ├── useWorkflow.ts
│   ├── useAIAssistant.ts
│   └── useCollaboration.ts
└── stores/
    ├── workflowStore.ts
    └── executionStore.ts
```

### 4.3 AI Integration Architecture

**LLM Provider Abstraction:**

```csharp
public interface ILLMService
{
    Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken);
    Task<WorkflowDefinition> GenerateWorkflowAsync(string userIntent, CancellationToken cancellationToken);
    Task<List<AttributeMapping>> SuggestSchemaMappingAsync(Schema source, Schema target, CancellationToken cancellationToken);
    Task<DataQualityReport> ValidateDataQualityAsync(IAsyncEnumerable<Feature> features, DataQualityRules rules, CancellationToken cancellationToken);
}

public class LLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _provider; // "openai", "anthropic", "azure"
    private readonly string _model;

    // Implementation supporting multiple providers
}
```

**Prompt Engineering:**

```csharp
public class WorkflowGenerationPrompt
{
    public static string Build(string userIntent, List<AvailableNode> availableNodes)
    {
        return $@"
You are a geospatial ETL workflow designer. Generate a workflow definition in JSON format.

Available node types:
{JsonSerializer.Serialize(availableNodes, new JsonSerializerOptions { WriteIndented = true })}

User request: {userIntent}

Generate a complete workflow definition with:
1. Appropriate nodes for the task
2. Correct connections between nodes
3. Proper parameter configuration
4. Data format specifications

Return only valid JSON matching the WorkflowDefinition schema.
";
    }
}
```

### 4.4 Streaming Architecture (Detailed)

**Note:** This section provides the architectural foundation for Phase 4 streaming capabilities.

#### 4.4.1 Deployment Topology

**Small Scale (MVP - Phase 4):**
```
┌─────────────────────────────────────────────────────┐
│  Kafka Cluster (3 brokers)                         │
│  - 3 partitions per topic                          │
│  - Replication factor: 2                           │
│  - Max throughput: ~500 features/sec               │
│  - Retention: 7 days                               │
└─────────────────────────────────────────────────────┘
              ↓                    ↑
┌─────────────────────────────────────────────────────┐
│  Stream Processor Pool (2 instances)                │
│  - .NET worker service                             │
│  - Consumer group: geoetl-processors               │
│  - Processing: 250 features/sec each               │
│  - Memory: 2 GB each                               │
│  - CPU: 2 cores each                               │
└─────────────────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────────────────┐
│  State Store (PostgreSQL + Redis)                  │
│  - Offset tracking                                 │
│  - Processor health                                │
│  - Metrics aggregation                             │
└─────────────────────────────────────────────────────┘
```

**Production Scale (Future):**
- Kafka: 5-9 brokers, 12-24 partitions, RF=3
- Stream Processors: Auto-scaling 5-50 instances
- Target: 5000+ features/sec
- High availability: Multi-AZ deployment

#### 4.4.2 Throughput Modeling

**Single Partition Performance:**

| Operation Type | Throughput (features/sec) | Latency (p95) | Notes |
|----------------|--------------------------|---------------|-------|
| Pass-through (no transform) | 1000 | 10ms | Baseline |
| Attribute filter | 800 | 15ms | Simple predicate |
| Geometry validation | 500 | 25ms | NetTopologySuite |
| Buffer (simple) | 200 | 80ms | Small geometries |
| Spatial join (cached) | 100 | 150ms | In-memory lookup |
| PostGIS operation | 50 | 300ms | Database round-trip |

**Scaling Formula:**
- Throughput = (Partitions × Single-partition throughput) × Processor efficiency
- Processor efficiency ≈ 0.7-0.8 (accounting for coordination overhead)
- Example: 3 partitions × 200 features/sec × 0.75 = **450 features/sec**

**Phase 4 MVP Target (Realistic):**
- **300-500 features/sec** sustained for simple transformations
- **p95 latency <200ms** for non-spatial operations
- **Uptime 99% (not 99.9%)** - allows 7.2 hours/month downtime for Phase 4

#### 4.4.3 State Management

**Offset Tracking:**
- Kafka consumer group handles partition offset commits
- Commit interval: Every 5 seconds or 1000 messages
- PostgreSQL backup of committed offsets (disaster recovery)

**Processor State:**
```csharp
public class StreamProcessorState
{
    public Guid ProcessorId { get; set; }
    public Guid WorkflowId { get; set; }
    public string Status { get; set; } // starting, running, paused, stopped
    public DateTime StartedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }

    // Kafka state
    public string ConsumerGroup { get; set; }
    public Dictionary<int, long> PartitionOffsets { get; set; }

    // Metrics
    public long FeaturesProcessed { get; set; }
    public long ErrorCount { get; set; }
    public double AvgLatencyMs { get; set; }
}
```

**Health Checks:**
- Heartbeat every 10 seconds to PostgreSQL
- Dead processor detection: No heartbeat for 30 seconds
- Auto-restart with offset recovery

#### 4.4.4 Error Handling & Resilience

**Failure Modes:**

1. **Transient Processing Error:**
   - Retry 3 times with exponential backoff
   - If still fails, write to dead-letter topic
   - Continue with next message

2. **Kafka Broker Failure:**
   - Consumer library handles reconnection
   - Processing pauses until broker recovers
   - Max pause: 5 minutes before alerting

3. **Processor Crash:**
   - Another processor in consumer group takes over partition
   - Reprocessing from last committed offset (at-least-once semantics)
   - Duplicate detection using feature ID + timestamp

4. **Schema Evolution:**
   - Validate message schema on consume
   - Log schema mismatches
   - Option to auto-adapt or reject

**Dead Letter Queue:**
```csharp
public class DeadLetterMessage
{
    public string OriginalTopic { get; set; }
    public int OriginalPartition { get; set; }
    public long OriginalOffset { get; set; }
    public GeoJsonFeature OriginalMessage { get; set; }
    public string ErrorType { get; set; }
    public string ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime FirstAttemptAt { get; set; }
    public DateTime LastAttemptAt { get; set; }
}
```

#### 4.4.5 Kafka Integration (Code)

```csharp
public interface IStreamProcessor
{
    Task StartAsync(WorkflowDefinition workflow, CancellationToken cancellationToken);
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task<StreamProcessorStatus> GetStatusAsync();
    Task<StreamMetrics> GetMetricsAsync();
}

public class GeoKafkaStreamProcessor : IStreamProcessor
{
    private readonly IConsumer<string, GeoJsonFeature> _consumer;
    private readonly IProducer<string, GeoJsonFeature> _producer;
    private readonly IProducer<string, DeadLetterMessage> _dlqProducer;
    private readonly IGeoprocessingService _geoprocessingService;
    private readonly ILogger<GeoKafkaStreamProcessor> _logger;
    private readonly StreamProcessorState _state;

    private const int MaxRetries = 3;
    private const int CommitIntervalMs = 5000;

    public async Task StartAsync(WorkflowDefinition workflow, CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaConfig.Brokers,
            GroupId = $"geoetl-{workflow.Id}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Manual commit for exactly-once
            MaxPollIntervalMs = 300000, // 5 minutes
            SessionTimeoutMs = 30000
        };

        _consumer.Subscribe(workflow.Source.Topics);
        var lastCommit = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                if (consumeResult == null) continue;

                var feature = consumeResult.Message.Value;

                // Execute workflow with retry logic
                var success = await TryExecuteWorkflowAsync(
                    feature,
                    workflow,
                    MaxRetries,
                    cancellationToken
                );

                if (success)
                {
                    _state.FeaturesProcessed++;
                }

                // Commit offsets periodically
                if ((DateTime.UtcNow - lastCommit).TotalMilliseconds > CommitIntervalMs)
                {
                    _consumer.Commit(consumeResult);
                    await UpdateStateAsync();
                    lastCommit = DateTime.UtcNow;
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
                await Task.Delay(1000, cancellationToken); // Back off
            }
        }
    }

    private async Task<bool> TryExecuteWorkflowAsync(
        GeoJsonFeature feature,
        WorkflowDefinition workflow,
        int retriesLeft,
        CancellationToken cancellationToken)
    {
        try
        {
            var transformedFeature = await ExecuteWorkflowAsync(feature, workflow);

            await _producer.ProduceAsync(
                workflow.Sink.Topic,
                new Message<string, GeoJsonFeature>
                {
                    Key = feature.Id,
                    Value = transformedFeature
                },
                cancellationToken
            );

            return true;
        }
        catch (Exception ex) when (retriesLeft > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, MaxRetries - retriesLeft)));
            return await TryExecuteWorkflowAsync(feature, workflow, retriesLeft - 1, cancellationToken);
        }
        catch (Exception ex)
        {
            // Final failure - send to DLQ
            await SendToDeadLetterQueueAsync(feature, ex);
            _state.ErrorCount++;
            return false;
        }
    }
}
```

#### 4.4.6 GeoMesa Integration (Optional - Phase 4+)

**Note:** GeoMesa integration is aspirational for Phase 4 and may be deferred.

```csharp
public class GeoMesaService
{
    // GeoMesa provides spatial indexing on Kafka streams
    // Enables spatial queries without materializing all data

    public async Task EnableSpatialIndexAsync(string topicName)
    {
        // Configure GeoMesa Kafka DataStore
        // This is a research spike - implementation TBD
    }

    public async Task<List<Feature>> SpatialQueryStreamAsync(Envelope bbox)
    {
        // Query streaming data with spatial filter
        // Returns recent features matching bbox
        // Implementation complexity: HIGH
    }
}
```

#### 4.4.7 Load Testing Plan

**Phase 4 Acceptance Criteria:**

1. **Load Test 1: Sustained Throughput**
   - Input: 500 features/sec for 1 hour
   - Workflow: Simple attribute filter
   - Expected: All messages processed, p95 latency <100ms, 0 dead letters

2. **Load Test 2: Burst Handling**
   - Input: 2000 features/sec for 5 minutes, then 100 features/sec
   - Expected: Consumer lag recovers within 10 minutes

3. **Load Test 3: Spatial Operations**
   - Input: 200 features/sec
   - Workflow: Buffer 100m
   - Expected: p95 latency <200ms

4. **Failure Test 1: Processor Restart**
   - Kill processor mid-stream
   - Expected: Another processor takes over within 30 seconds
   - Expected: No message loss (at-least-once delivery)

5. **Failure Test 2: Kafka Broker Failure**
   - Stop one Kafka broker
   - Expected: Processing continues with degraded performance
   - Expected: Full recovery when broker returns

**Test Environment:**
- Kafka: 3 brokers, 3 partitions
- Processors: 2 instances
- Test data: Synthetic GeoJSON features (realistic geometries)
- Duration: 8 hours continuous operation

#### 4.4.8 Monitoring & Observability

**Key Metrics:**
- Features per second (by processor, by partition)
- Processing latency (p50, p95, p99)
- Consumer lag (messages behind)
- Error rate and types
- Dead letter queue size
- Processor health and uptime

**Dashboards:**
- Real-time throughput graph
- Latency distribution histogram
- Consumer lag by partition
- Error rate alerts
- Processor status table

**Alerts:**
- Consumer lag >10,000 messages
- Error rate >5%
- No heartbeat for 30 seconds
- Dead letter queue >1000 messages

---

**Phase 4 Streaming Conclusion:**

The above architecture provides a realistic foundation for Phase 4. The targets have been adjusted:
- ~~1000+ features/sec~~ → **300-500 features/sec** (simple operations)
- ~~Sub-100ms latency~~ → **p95 <200ms** (more realistic)
- ~~99.9% uptime~~ → **99% uptime** (allows for iteration)

GeoMesa integration is marked as optional/aspirational to reduce Phase 4 risk.

---

## 5. User Experience & Workflows

### 5.1 Example User Journeys

#### Journey 1: First-Time User - Simple Data Conversion

1. User logs into Honua platform
2. Navigates to "GeoETL" → "New Workflow"
3. Types in AI assistant: "Convert my Shapefile to GeoParquet"
4. AI generates workflow: `[Upload Shapefile] → [Convert to GeoParquet] → [Download]`
5. User uploads file
6. Clicks "Run Workflow"
7. Downloads optimized GeoParquet file

**Time: 2 minutes**

#### Journey 2: Data Engineer - Complex Spatial Analysis

1. Opens workflow designer
2. Drags nodes from palette:
   - **Data Source**: PostGIS table "buildings"
   - **Geoprocessing**: Buffer 50m
   - **Data Source**: OGC API Features "flood_zones"
   - **Spatial Join**: Intersects
   - **AI Quality Check**: Validate results
   - **Data Sink**: GeoParquet to S3

3. Connects nodes in sequence
4. Configures parameters in property panel
5. Clicks "Validate" - sees resource estimates and warnings
6. Clicks "Run"
7. Monitors progress in real-time
8. Reviews results and data quality report
9. Saves workflow for future use

**Time: 10 minutes**

#### Journey 3: DevOps Engineer - Real-Time Geofencing

1. Creates new streaming workflow
2. Configures Kafka source: "vehicle-positions" topic
3. Adds spatial join with "restricted_zones" (PostGIS)
4. Adds filter: "Where relationship = 'within'"
5. Configures Kafka sink: "geofence-alerts" topic
6. Deploys streaming processor
7. Monitors metrics dashboard (features/sec, latency)

**Time: 15 minutes**

#### Journey 4: Analyst - Ad-hoc Query with Natural Language

1. Opens AI chat in workflow designer
2. Types: "Show me all buildings in San Francisco built after 2010 that are within 100m of a park, export as PMTiles"
3. AI generates complete workflow
4. User reviews and clicks "Run"
5. Receives PMTiles file for web visualization

**Time: 3 minutes**

### 5.2 Collaboration Features

**Real-Time Collaboration:**
- Multiple users can edit the same workflow simultaneously
- See cursor positions and selections of other users
- Built-in chat for discussing workflow design
- Automatic conflict resolution via CRDT

**Version Control:**
- Git-based workflow versioning
- Commit messages and change history
- Branch, merge, and revert workflows
- Diff view for workflow changes

**Sharing & Publishing:**
- Share workflows with team members
- Publish to organization template library
- Export/import workflows (JSON)
- Embed workflows in documentation

---

## 6. Differentiation & Innovation

### 6.1 What Makes Honua GeoETL Unique

| Feature | Innovation | Industry Impact |
|---------|-----------|----------------|
| **AI-Powered Design** | Natural language workflow creation, intelligent suggestions | Reduces workflow design time by 70% |
| **Web-Based Designer** | No client installation, real-time collaboration | Eliminates FME's desktop requirement |
| **Cloud-Native Formats** | First-class GeoParquet, GeoArrow, PMTiles support | Future-proofs data infrastructure |
| **Streaming ETL** | Real-time geospatial pipelines with Kafka/GeoMesa | Enables IoT and live mapping use cases |
| **Unified Platform** | ETL + Geoprocessing + OGC APIs in one system | Reduces tool sprawl |
| **Intelligent Data Quality** | LLM-powered validation and cleansing | Improves data quality automatically |
| **Consumption Pricing** | Pay per workflow execution, not seats | More accessible than FME |
| **Multi-Tenant SaaS** | Built for cloud from day one | Scalable and cost-effective |
| **Open Standards** | OGC-compliant, STAC-compatible | Interoperability with ecosystem |

### 6.2 Competitive Advantages

**vs. FME:**
- ✅ Modern web-based UI (no desktop client)
- ✅ AI-powered workflow design
- ✅ Real-time streaming support
- ✅ Cloud-native data formats
- ✅ Consumption-based pricing (vs. per-seat)
- ✅ Multi-tenant SaaS architecture
- ✅ OGC standards native integration

**vs. Open Source (GeoKettle, HALE):**
- ✅ Active development and support
- ✅ Visual workflow designer
- ✅ AI capabilities
- ✅ Streaming support
- ✅ Enterprise features (multi-tenancy, security)

**vs. General ETL (Talend, Alteryx):**
- ✅ Geospatial-first design
- ✅ Native spatial operations
- ✅ Optimized for geospatial data formats
- ✅ Lower cost
- ✅ Cloud-native architecture

**vs. Modern Orchestrators (Airflow, Dagster, Prefect):**
- ✅ No-code visual designer
- ✅ Geospatial awareness
- ✅ Built-in spatial operations
- ✅ AI assistance
- ✅ Easier for non-developers

### 6.3 Innovation Areas That Push the Industry Forward

1. **Conversational Workflow Design**
   - Natural language as primary interface
   - Democratizes geospatial ETL for non-experts
   - Reduces learning curve from weeks to minutes

2. **Cloud-Native First**
   - GeoParquet and GeoArrow as native formats
   - Designed for S3/cloud storage from ground up
   - No legacy format baggage

3. **Real-Time Geospatial Streaming**
   - First ETL tool to natively support streaming geospatial data
   - Enables new use cases (IoT, live tracking, real-time alerts)
   - Bridges batch and streaming worlds

4. **Intelligent Automation**
   - AI-powered schema mapping eliminates manual work
   - Automated data quality and cleansing
   - Self-healing workflows that adapt to schema changes

5. **Unified Platform Philosophy**
   - ETL, geoprocessing, and OGC APIs in one system
   - Eliminates need for multiple tools
   - Seamless integration between components

6. **Collaborative Workflow Development**
   - Real-time co-editing like Google Docs
   - Social features (sharing, comments, templates)
   - Workflow marketplace for community sharing

---

## 7. Phased Implementation Plan

### Phase 1: Foundation (Months 1-3)

**Goals:**
- Core workflow engine (MVP)
- Basic visual designer
- **Limited** integration with existing geoprocessing (prioritized subset)
- Support for common data formats

**Deliverables:**
- Workflow definition schema and engine (JSON-based)
- Web-based canvas with basic node types:
  - File upload/download nodes
  - PostgreSQL/PostGIS connector nodes
  - Basic transformation nodes (attribute operations)
- GeoprocessingNode wrapping IGeoprocessingService
- **Initial geoprocessing operations (3 only):**
  - Buffer (most commonly used)
  - Intersection (core spatial operation)
  - Union (frequently requested)
- Basic format support (GeoJSON, GeoPackage, Shapefile via GDAL)
- DAG validation and execution
- Simple progress tracking

**Success Metrics:**
- Execute simple 3-5 node workflows end-to-end
- Process 10K features in <30 seconds
- Successfully integrate 3 prioritized geoprocessing operations
- Validate workflow DAGs before execution
- Handle basic error scenarios gracefully

**Deferred to Phase 1.5 (see Migration Plan below):**
- Remaining geoprocessing operations (Difference, Simplify, ConvexHull, Dissolve)
- Custom parameter UIs for complex operations
- Long-running job management
- Advanced error recovery

### Phase 2: AI Research & Smart Features (Months 4-6)

**Goals:**
- Explore AI-powered workflow assistance (research phase)
- Add intelligent data quality capabilities
- Implement template-based workflow creation (non-AI fallback)

**Research Spikes:**

1. **AI Workflow Generation (4 weeks)**
   - Evaluate LLM providers (OpenAI, Anthropic, Azure OpenAI)
   - Build proof-of-concept with 10 common workflow patterns
   - Create evaluation dataset (50 test cases with ground truth workflows)
   - Measure accuracy, precision, and execution success rate
   - **Decision criteria:** If ≥70% of generated workflows are executable without errors, proceed to production; otherwise, fallback to template library

2. **Training Data Acquisition (2 weeks)**
   - Collect workflow examples from internal use cases
   - Curate 100+ workflow templates across common patterns
   - Document natural language descriptions for each
   - Build evaluation harness for ongoing measurement

3. **Schema Mapping Assistant (2 weeks)**
   - Prototype semantic field matching using LLM
   - Test with 20 real-world schema pairs
   - Measure accuracy vs. manual mapping
   - **Decision criteria:** If semantic matching shows ≥60% precision, invest further; otherwise, use rule-based fuzzy matching

**Concrete Deliverables:**

- **Template Library (non-AI path):** 25+ pre-built workflow templates
  - Data conversion workflows (10+)
  - Spatial analysis workflows (10+)
  - Data quality workflows (5+)
  - Searchable, parameterizable, one-click instantiation

- **Rule-Based Data Quality:** Deterministic validation (no AI required)
  - Geometry validation (topology, validity)
  - Completeness checks (required fields)
  - Range validation (numeric bounds, date ranges)
  - Pattern matching (regex for attributes)

- **AI Assistant (if research successful):**
  - LLM service abstraction (ILLMService)
  - Chat interface in designer
  - Workflow generation with confidence scores
  - User review/approval workflow
  - Fallback to template search if confidence <0.7

**Success Metrics (Research Phase):**
- Complete evaluation of 3 LLM providers with documented results
- Build evaluation dataset with 50+ annotated examples
- Achieve 70% executable workflow generation rate (research target)
- 100% of data quality rules implemented and tested
- Template library covers 80% of common use cases
- User can always fallback to manual design if AI fails

**Risk Mitigation:**
- AI features are **additive**, not required for core functionality
- Template library provides immediate value without AI
- Rule-based data quality works without LLM
- Clear metrics for go/no-go decisions on AI investment

### Phase 3: Cloud-Native & Formats (Months 7-9)

**Goals:**
- Modern data format support
- Cloud storage integration
- Performance optimization

**Deliverables:**
- GeoParquet reader/writer (Apache Arrow.NET)
- GeoArrow in-memory format support
- PMTiles generator
- S3/Azure Blob/GCS connectors
- Parallel processing engine
- Chunked data processing

**Success Metrics:**
- Process 1M features in <5 minutes
- GeoParquet files 10x smaller than GeoJSON
- Support all major cloud storage providers

### Phase 4: Streaming MVP (Months 10-12)

**Goals:**
- Real-time data pipelines (basic)
- Kafka integration
- Live monitoring

**Prerequisites:**
- Read section 4.4 "Streaming Architecture (Detailed)" for full technical foundation
- Complete Phases 1-3 successfully
- Kafka cluster provisioned and tested

**Deliverables:**

- **Kafka Infrastructure (Month 10):**
  - Kafka cluster: 3 brokers, 3 partitions, RF=2
  - Topic management API
  - Consumer group management
  - Offset monitoring

- **Stream Processor (Month 11):**
  - Kafka source/sink nodes in workflow designer
  - Basic streaming workflow executor (see 4.4.5)
  - Support for simple transformations (attribute filters, validation)
  - Dead letter queue for failed messages
  - State management (PostgreSQL + Redis)

- **Monitoring & Operations (Month 12):**
  - Real-time metrics dashboard (throughput, latency, lag)
  - Processor health monitoring
  - Basic alerting (lag, errors, downtime)
  - Load testing harness (see 4.4.7)

**Success Metrics (Adjusted for MVP):**
- **Throughput:** 300-500 features/sec sustained for simple transformations
- **Latency:** p95 <200ms for non-spatial operations
- **Uptime:** 99% availability (allows 7.2 hours/month downtime for iteration)
- **Load Tests:** Pass 3/5 tests from section 4.4.7
- **Error Handling:** <1% message loss, all failures go to DLQ

**Explicitly Out of Scope for Phase 4:**
- GeoMesa spatial indexing (deferred to Phase 4+)
- Complex spatial operations in streaming (buffer, intersection, etc.)
- Multi-region deployment
- Auto-scaling processors
- SensorThings API streaming (deferred)

### Phase 5: Enterprise & Scale (Months 13-15)

**Goals:**
- Production hardening
- Advanced features
- Performance at scale

**Deliverables:**
- Workflow scheduling (cron, event-driven)
- Workflow versioning and Git integration
- Advanced error handling and retry logic
- Distributed execution (multi-node)
- Advanced monitoring and alerting
- Cost optimization features

**Success Metrics:**
- Process 100M+ features
- Support 1000+ concurrent workflows
- 99.99% execution reliability

### Phase 6: Collaboration & Marketplace (Months 16-18)

**Goals:**
- Team collaboration
- Community features
- Ecosystem building

**Deliverables:**
- Real-time collaborative editing (Yjs)
- Workflow template marketplace
- Organization workflow library
- Embedded workflow widgets
- Public API for workflow execution
- SDKs (Python, JavaScript, .NET)

**Success Metrics:**
- 100+ community workflows published
- 50% of workflows created from templates
- Active community engagement

### Phase 1.5: Geoprocessing Migration (Months 4-6, parallel with Phase 2)

**Goal:** Complete integration of remaining geoprocessing operations

**Approach:**
- Incremental migration of remaining operations
- Address heterogeneous parameter UIs
- Build abstraction layer for complex operations

**Deliverables:**

**Month 4:**
- Difference operation integration
- Simplify operation integration
- Generic parameter UI framework for simple operations

**Month 5:**
- ConvexHull operation integration
- Dissolve operation integration
- Custom parameter UI components for complex operations
- Long-running job progress tracking

**Month 6:**
- Any remaining custom operations from geoprocessing catalog
- Batch operation support (run same operation on multiple inputs)
- Advanced error recovery and retry logic
- Documentation for all integrated operations

**Success Metrics:**
- 100% of existing geoprocessing operations integrated
- Custom parameter UIs for all complex operations
- Long-running jobs (>5 min) handled gracefully
- <5% error rate on complex operations

---

## 7.7 Geoprocessing Migration Plan

### 7.7.1 Current Geoprocessing Catalog Analysis

**Assumption:** The existing geoprocessing catalog contains dozens of operations with heterogeneous characteristics.

**Categorization by Complexity:**

**Tier 1 - Simple (Phase 1):**
- Buffer - Single distance parameter, well-defined
- Intersection - Two input geometries, boolean result
- Union - Two input geometries, merged result

**Tier 2 - Moderate (Phase 1.5, Month 4-5):**
- Difference - Two inputs, clear semantics
- Simplify - Tolerance parameter, straightforward
- ConvexHull - Single input, deterministic output
- Dissolve - Group-by field, aggregation logic

**Tier 3 - Complex (Phase 1.5, Month 6):**
- Operations with custom UIs (e.g., multi-step wizards)
- Operations requiring external data sources
- Operations with conditional logic based on data characteristics
- Operations with complex validation rules

### 7.7.2 Migration Strategy

**Step 1: Create GeoprocessingNode Abstraction (Phase 1)**

```csharp
public abstract class GeoprocessingNodeBase : IWorkflowNode
{
    protected readonly IGeoprocessingService _geoprocessingService;

    public abstract string OperationName { get; }
    public abstract ParameterDefinition[] Parameters { get; }

    public async Task<NodeResult> ExecuteAsync(NodeContext context)
    {
        // 1. Map workflow parameters to geoprocessing job parameters
        var jobParams = MapParameters(context.Parameters);

        // 2. Validate using existing IGeoprocessingService
        var validationResult = await _geoprocessingService.ValidateJobAsync(
            OperationName,
            jobParams,
            context.CancellationToken
        );

        if (!validationResult.IsValid)
        {
            return NodeResult.Failure(validationResult.Errors);
        }

        // 3. Execute job
        var jobResult = await _geoprocessingService.ExecuteJobAsync(
            OperationName,
            jobParams,
            context.ProgressCallback,
            context.CancellationToken
        );

        // 4. Return result
        return NodeResult.Success(jobResult.Output);
    }

    protected abstract Dictionary<string, object> MapParameters(
        Dictionary<string, object> workflowParams
    );
}
```

**Step 2: Generate Node Implementations Automatically (Phase 1.5)**

```csharp
public class GeoprocessingNodeGenerator
{
    // Reflect over IGeoprocessingOperation implementations
    // Generate node classes automatically
    // Emit parameter UIs based on operation metadata

    public List<Type> GenerateNodesForAllOperations()
    {
        var operationTypes = Assembly
            .GetAssembly(typeof(IGeoprocessingOperation))
            .GetTypes()
            .Where(t => typeof(IGeoprocessingOperation).IsAssignableFrom(t));

        var nodeTypes = new List<Type>();

        foreach (var opType in operationTypes)
        {
            var nodeType = GenerateNodeType(opType);
            nodeTypes.Add(nodeType);
        }

        return nodeTypes;
    }
}
```

**Step 3: Handle Complex Parameter UIs (Phase 1.5, Month 5-6)**

For operations with complex UIs, create custom React components:

```typescript
// Simple operation - auto-generated UI
interface BufferNodeParams {
  distance: number;
  unit: 'meters' | 'feet' | 'kilometers';
}

// Complex operation - custom UI component
interface DissolveNodeParams {
  groupByFields: string[];
  aggregations: {
    field: string;
    operation: 'sum' | 'avg' | 'min' | 'max' | 'count';
  }[];
  preserveHoles: boolean;
}

// Custom React component for Dissolve
export const DissolveNodeEditor: React.FC<NodeEditorProps> = ({ params, onChange }) => {
  // Custom UI with field selector, aggregation builder, etc.
};
```

**Step 4: Progressive Rollout**

- **Week 1-2:** Buffer, Intersection, Union (Phase 1)
- **Week 3-4:** Test with real workflows, gather feedback
- **Month 4:** Difference, Simplify (Phase 1.5)
- **Month 5:** ConvexHull, Dissolve (Phase 1.5)
- **Month 6:** Remaining operations, custom UIs (Phase 1.5)

### 7.7.3 Handling Long-Running Jobs

**Challenge:** Some geoprocessing operations may run for >30 minutes

**Solution:**

```csharp
public class LongRunningGeoprocessingNode : GeoprocessingNodeBase
{
    public override async Task<NodeResult> ExecuteAsync(NodeContext context)
    {
        // 1. Start job asynchronously
        var jobId = await _geoprocessingService.SubmitJobAsync(
            OperationName,
            MapParameters(context.Parameters)
        );

        // 2. Store job ID in workflow run state
        context.WorkflowRun.State["geoprocessing_job_id"] = jobId;

        // 3. Poll for completion (with timeout)
        var timeout = TimeSpan.FromMinutes(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var status = await _geoprocessingService.GetJobStatusAsync(jobId);

            if (status.IsCompleted)
            {
                return NodeResult.Success(status.Result);
            }
            else if (status.IsFailed)
            {
                return NodeResult.Failure(status.ErrorMessage);
            }

            // Report progress
            context.ProgressCallback?.Report(new NodeProgress
            {
                Percentage = status.ProgressPercentage,
                Message = status.ProgressMessage
            });

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return NodeResult.Failure("Operation timed out");
    }
}
```

### 7.7.4 Migration Risks & Mitigations

**Risk 1: Existing operations have undocumented behavior**
- Mitigation: Interview developers, document behavior, add tests

**Risk 2: Some operations have complex state management**
- Mitigation: Encapsulate state in IGeoprocessingService, don't expose to workflow engine

**Risk 3: Parameter validation may be inconsistent across operations**
- Mitigation: Standardize validation interface, centralize validation logic

**Risk 4: Performance characteristics vary widely**
- Mitigation: Use existing resource estimation, show estimates in UI before execution

### 7.7.5 Acceptance Criteria for Migration Completion

- [ ] All existing geoprocessing operations callable from workflow designer
- [ ] Parameter UIs generated or custom-built for all operations
- [ ] Validation works for all operations (leveraging existing validation)
- [ ] Long-running operations (>5 min) show progress
- [ ] Error messages are helpful and actionable
- [ ] Documentation exists for each operation
- [ ] Integration tests pass for all operations
- [ ] Performance is equivalent to direct IGeoprocessingService usage

---

## 8. Technical Risks & Mitigations

### Risk 1: Performance at Scale

**Risk:** Processing large datasets (100M+ features) may be slow

**Mitigations:**
- Leverage existing tiered execution (NTS → PostGIS → Cloud Batch)
- Implement chunked processing with parallelization
- Use streaming processing for very large datasets
- Optimize with columnar formats (GeoParquet, GeoArrow)
- Database-side processing where possible (PostGIS)

### Risk 2: LLM Reliability

**Risk:** AI-generated workflows may be incorrect or inefficient

**Mitigations:**
- Comprehensive workflow validation before execution
- User review and approval step
- Confidence scoring for AI suggestions
- Fallback to manual workflow design
- Continuous learning from user corrections
- Human-in-the-loop for critical workflows

### Risk 3: Streaming Complexity

**Risk:** Real-time processing is complex to build and maintain

**Mitigations:**
- Start with simple use cases
- Leverage proven technologies (Kafka, GeoMesa)
- Extensive testing and monitoring
- Graceful degradation on failures
- Clear documentation and examples

### Risk 4: Format Support Breadth

**Risk:** GDAL supports 355+ formats, difficult to match

**Mitigations:**
- Use GDAL/OGR for format conversion nodes
- Focus on most common formats first
- Prioritize cloud-native formats
- Community contributions for niche formats

### Risk 5: UI Complexity

**Risk:** Visual workflow designer may become cluttered and hard to use

**Mitigations:**
- User testing throughout development
- Progressive disclosure of features
- Contextual help and tutorials
- Node grouping and subworkflows
- Search and filter capabilities
- AI assistant to reduce manual node placement

---

## 9. Business Model & Pricing

### 9.1 Pricing Tiers

**Free Tier:**
- 10 workflow executions/month
- 100K features processed/month
- 1 GB data transfer
- Community support
- Public workflow sharing

**Professional ($99/month per user):**
- 1,000 workflow executions/month
- 10M features processed/month
- 100 GB data transfer
- Email support
- Private workflows
- Basic AI features
- Streaming (1 concurrent processor)

**Team ($499/month for 5 users):**
- 10,000 workflow executions/month
- 100M features processed/month
- 1 TB data transfer
- Priority support
- Real-time collaboration
- Advanced AI features
- Streaming (5 concurrent processors)
- Scheduled workflows
- Git integration

**Enterprise (Custom Pricing):**
- Unlimited executions
- Unlimited features
- Unlimited data transfer
- Dedicated support + SLA
- Self-hosted option
- Custom integrations
- Advanced security (SSO, audit logs)
- Unlimited streaming processors
- Multi-region deployment

### 9.2 Consumption-Based Add-Ons

- **Extra executions:** $0.10 per execution
- **Extra features:** $1 per 1M features
- **Extra data transfer:** $0.05 per GB
- **Streaming processor hours:** $0.50 per hour
- **AI-powered operations:** $0.01 per AI call
- **Cloud batch processing:** Cost pass-through + 20% margin

### 9.3 Competitive Pricing Analysis

**FME:**
- Desktop: ~$3,500/year per seat
- Server: ~$25,000/year base + per-engine costs
- Total for small team (5 users): ~$40,000+/year

**Honua GeoETL:**
- Team tier (5 users): $499/month = $5,988/year
- **Savings: 85% vs. FME**

---

## 10. Success Metrics & KPIs

### 10.1 Product Metrics

- **Adoption:** Number of active workflows per tenant
- **Usage:** Total features processed per month
- **Performance:** P95 workflow execution time
- **Reliability:** Workflow success rate (target: >99%)
- **AI Effectiveness:** % of AI-generated workflows that run successfully
- **Format Adoption:** % of workflows using modern formats (GeoParquet, etc.)

### 10.2 Business Metrics

- **Customer Acquisition:** New tenants per month
- **Expansion:** % of free users upgrading to paid
- **Retention:** Monthly recurring revenue (MRR) retention rate
- **Efficiency:** Cost per workflow execution
- **Support:** Average support ticket resolution time

### 10.3 Technical Metrics

- **Throughput:** Features processed per second
- **Latency:** P95 node execution time
- **Scalability:** Max concurrent workflow executions
- **Availability:** System uptime (target: 99.9%)
- **Cost:** Cloud infrastructure cost per 1M features

---

## 11. Integration Points

### 11.1 Integration with Existing Honua Components

**Geoprocessing:**
- GeoprocessingNode calls IGeoprocessingService
- Inherits tiered execution (NTS, PostGIS, Cloud Batch)
- Shares progress tracking and metrics

**OGC APIs:**
- DataSourceNode supports OGC API - Features
- DataSourceNode supports OGC API - Coverages (raster)
- Export to OGC API - Tiles (PMTiles)

**SensorThings API:**
- DataSourceNode reads from Datastreams
- DataSinkNode writes Observations
- Streaming workflows update real-time

**Raster Processing:**
- Import raster layers via existing RasterImportService
- Integrate with mosaic service for multi-raster workflows
- COG support leverages existing LibTiffCOGReader

**Data Ingestion:**
- ETL workflows can trigger via ingestion service
- Automated workflows on new data arrival
- Integration with STAC catalog updates

### 11.2 External Integrations

**Authentication:**
- OAuth 2.0 / OpenID Connect
- SAML 2.0 for enterprise SSO
- API keys for programmatic access

**Data Sources:**
- PostGIS, PostgreSQL
- MySQL (with spatial extensions)
- Oracle Spatial
- SQL Server with spatial
- S3, Azure Blob, GCS
- FTP/SFTP
- HTTP/HTTPS URLs
- ArcGIS REST Services
- WFS, WMS, WMTS
- OGC API - Features
- SensorThings API
- STAC catalogs

**Data Sinks:**
- All of the above
- Plus: Email (results as attachment)
- Webhooks (notify external systems)
- Cloud Data Warehouse (BigQuery, Redshift, Snowflake)

**Monitoring:**
- OpenTelemetry for distributed tracing
- Prometheus metrics export
- Custom webhooks for workflow events
- Email/Slack notifications

**CI/CD:**
- GitHub Actions integration
- GitLab CI integration
- Workflow deployment via API
- Automated testing of workflows

---

## 12. Documentation & Onboarding

### 12.1 Documentation Structure

1. **Getting Started**
   - Quick start guide (5 minutes)
   - First workflow tutorial
   - Video walkthroughs

2. **Concepts**
   - Workflow anatomy
   - Node types explained
   - Data formats overview
   - Execution tiers

3. **Node Reference**
   - Complete node catalog
   - Parameters and options
   - Examples for each node
   - Performance characteristics

4. **AI Assistant Guide**
   - Natural language tips
   - Example prompts
   - Prompt engineering for workflows

5. **Advanced Topics**
   - Streaming workflows
   - Custom nodes (plugin system)
   - Performance optimization
   - Error handling patterns

6. **API Reference**
   - REST API documentation
   - SDKs (Python, JavaScript, .NET)
   - Webhook payloads
   - Authentication

7. **Best Practices**
   - Workflow design patterns
   - Performance optimization
   - Error handling
   - Testing workflows

### 12.2 Onboarding Flow

**New User Experience:**

1. **Welcome Screen**
   - "What would you like to do?"
   - Common use case buttons (Convert formats, Clean data, Spatial analysis)

2. **Interactive Tutorial**
   - Guided 5-minute workflow creation
   - Real data processing
   - Immediate value demonstration

3. **Template Gallery**
   - Pre-built workflows for common tasks
   - One-click deployment
   - Customizable parameters

4. **AI Assistant Introduction**
   - "Try asking: Convert my Shapefile to GeoParquet"
   - Natural language examples
   - Instant workflow generation

5. **Progress Milestones**
   - First workflow created ✓
   - First workflow executed ✓
   - First data export ✓
   - First collaborative workflow ✓

---

## 13. Security & Compliance

### 13.1 Security Features

**Authentication & Authorization:**
- Multi-factor authentication (MFA)
- Role-based access control (RBAC)
- Workflow-level permissions
- API key management with scopes

**Data Security:**
- Encryption at rest (AES-256)
- Encryption in transit (TLS 1.3)
- Tenant data isolation
- Secure credential storage (Vault integration)

**Audit & Compliance:**
- Complete audit log of all operations
- Data lineage tracking
- Workflow execution history
- User action logging
- GDPR compliance features (data export, deletion)

**Network Security:**
- VPC isolation
- IP whitelisting
- Private endpoints for enterprise
- DDoS protection

### 13.2 Compliance Standards

- **SOC 2 Type II** - Target certification
- **GDPR** - EU data protection compliance
- **CCPA** - California privacy compliance
- **HIPAA** - Healthcare data (if needed)
- **FedRAMP** - Government cloud security (future)

---

## 14. Open Questions & Future Considerations

### 14.1 Critical Questions for Stakeholder Review

**Q1: What portion of the existing geoprocessing catalog is required for the MVP canvas integration?**

**Answer:** Based on Phase 1 planning:
- **Minimum Viable:** 3 operations (Buffer, Intersection, Union) - most commonly used
- **Preferred for MVP:** Add 4 more in Phase 1.5 (Difference, Simplify, ConvexHull, Dissolve)
- **Full catalog:** Complete migration in Month 6 (Phase 1.5)

**Decision needed:**
- Can we ship Phase 1 with only 3 operations, or do stakeholders require more for initial release?
- Are there specific operations that must be included for customer commitments?
- What is the priority order for remaining operations?

**Action:** Product team to audit existing usage analytics and customer contracts to determine minimum required operation set.

---

**Q2: How will we acquire and evaluate the labeled data needed to train and measure the NL workflow generator?**

**Answer:** Phase 2 research approach (see section 7, Phase 2):

**Data Acquisition Strategy:**

1. **Internal Sources (Week 1-2):**
   - Mine workflow definitions from internal testing and demos
   - Interview team members for common patterns
   - Document existing customer use cases
   - Target: 50+ workflow examples with natural language descriptions

2. **Synthetic Generation (Week 3-4):**
   - Create variations of common patterns programmatically
   - Use GPT-4 to generate paraphrases of descriptions
   - Target: 100+ total examples

3. **Community Contributions (Ongoing):**
   - Invite early beta users to contribute workflows
   - Crowdsource descriptions for existing workflows
   - Gamify contributions (leaderboard, badges)
   - Target: 200+ examples by end of Phase 2

**Evaluation Protocol:**

1. **Automated Evaluation:**
   - Split data: 70% training, 30% test set
   - Metrics: Execution success rate, structural similarity, parameter correctness
   - Baseline: Random workflow generation (expect ~5% success)
   - Target: 70% success rate on test set

2. **Human Evaluation:**
   - 3 evaluators review 50 generated workflows
   - Rate: Correctness (1-5), Efficiency (1-5), Understandability (1-5)
   - Inter-rater agreement measured
   - Target: Average rating ≥3.5/5

3. **Production Monitoring:**
   - Track user acceptance rate of AI suggestions
   - Measure edit distance between AI-generated and final workflow
   - Collect user feedback ("Was this helpful?")
   - Target: 60%+ acceptance rate

**Decision Criteria for AI Investment:**
- If evaluation shows ≥70% executable workflows AND ≥60% user acceptance: Proceed to production
- If 50-70%: Iterate on prompts and examples for 1 more month
- If <50%: Defer AI features, focus on template library instead

**Fallback Path:**
- Template library already provides value without AI (25+ curated workflows)
- Visual designer works regardless of AI success
- AI features are **additive**, not blocking for product launch

**Action:** Assign research lead to implement evaluation harness and data collection process in Phase 2, Month 1.

---

**Q3: How do we ensure streaming architecture is realistic for Phase 4?**

**Answer:** See section 4.4 "Streaming Architecture (Detailed)" for full technical foundation.

**Key adjustments made:**
- Reduced throughput target: ~~1000+~~ → 300-500 features/sec
- Realistic latency: ~~<100ms~~ → p95 <200ms
- Achievable uptime: ~~99.9%~~ → 99% (allows iteration)
- GeoMesa marked as optional/aspirational
- Added detailed deployment topology, throughput modeling, and load test plan

**Action:** Conduct 2-week architecture spike in Month 9 (before Phase 4) to validate:
- Kafka cluster sizing
- .NET Kafka client performance with geospatial data
- Realistic throughput for simple transformations
- Go/no-go decision before committing to Phase 4

---

### 14.2 Technical Decisions Needed

1. **LLM Provider:**
   - OpenAI GPT-4 (expensive, best quality)
   - Anthropic Claude (balanced)
   - Azure OpenAI (enterprise compliance)
   - Self-hosted (Llama 3, Mistral) - cost-effective but requires ML infrastructure
   - **Recommendation:** Start with OpenAI GPT-4 for research, evaluate cost/quality tradeoff

2. **Frontend Canvas Library:**
   - React Flow (proven, large community)
   - Xyflow (React Flow v11, modern)
   - Custom canvas (full control, more work)
   - **Recommendation:** Xyflow (modern, actively maintained)

3. **Workflow Definition Format:**
   - Custom JSON schema
   - Apache Airflow DAG format (compatibility)
   - Common Workflow Language (CWL) - standards-based
   - Hybrid approach
   - **Recommendation:** Custom JSON schema with CWL export option for interoperability

4. **Streaming Technology:**
   - Apache Kafka (industry standard)
   - AWS Kinesis (AWS-native)
   - Azure Event Hubs (Azure-native)
   - Multi-provider support
   - **Recommendation:** Start with Kafka (industry standard), add cloud-native options later

### 14.3 Future Enhancements

1. **Machine Learning Integration**
   - Train ML models on geospatial data
   - Prediction nodes (classification, regression)
   - Clustering and pattern detection

2. **3D Data Processing**
   - Point cloud operations (LiDAR)
   - 3D mesh generation
   - BIM data integration
   - CityGML processing

3. **Time-Series Analysis**
   - Temporal aggregations
   - Change detection
   - Trend analysis
   - Forecasting

4. **Network Analysis**
   - Routing and navigation
   - Service area analysis
   - Network optimization
   - Accessibility calculations

5. **Plugin System**
   - Custom node development
   - Community node marketplace
   - Language bindings (Python, R, Julia)
   - WebAssembly nodes

6. **Mobile App**
   - View workflows on mobile
   - Monitor execution progress
   - Trigger workflows from field
   - Review results and alerts

---

## 15. Conclusion

Honua GeoETL represents a significant leap forward in geospatial data processing technology. By combining:

- **Visual workflow design** (like FME)
- **AI-powered intelligence** (unique to market)
- **Cloud-native architecture** (modern data formats)
- **Real-time streaming** (IoT and live data)
- **Unified platform integration** (ETL + geoprocessing + OGC APIs)

We can create a product that not only competes with FME but surpasses it in key areas while being more accessible, affordable, and future-proof.

### Key Takeaways

1. **Tight Integration:** GeoETL extends existing geoprocessing rather than replacing it
2. **Innovation Focus:** AI, streaming, and cloud-native formats differentiate from competitors
3. **User-Centric:** Web-based design eliminates installation friction
4. **Scalable:** Built for cloud from day one with multi-tenancy
5. **Standards-Based:** OGC compliance ensures interoperability
6. **Phased Approach:** 18-month roadmap with clear milestones
7. **Business Model:** Consumption pricing is more accessible than FME

### Next Steps

1. **Stakeholder Review:** Gather feedback on design priorities
2. **Technical Spike:** Prototype workflow engine and visual designer
3. **LLM Evaluation:** Test workflow generation with different LLM providers
4. **Resource Planning:** Finalize team composition and timeline
5. **Phase 1 Kickoff:** Begin foundation development

---

## Appendices

### Appendix A: Related Standards

- OGC API - Processes (workflow execution)
- OGC API - Features (vector data access)
- OGC SensorThings API (IoT/sensor data)
- STAC (SpatioTemporal Asset Catalog)
- CWL (Common Workflow Language)
- WPS (Web Processing Service) - legacy

### Appendix B: Technology References

- **GeoParquet:** https://geoparquet.org/
- **GeoArrow:** https://github.com/geoarrow/geoarrow
- **PMTiles:** https://github.com/protomaps/PMTiles
- **GeoMesa:** https://www.geomesa.org/
- **Apache Kafka:** https://kafka.apache.org/
- **Dagster:** https://dagster.io/
- **React Flow:** https://reactflow.dev/
- **Yjs:** https://yjs.dev/

### Appendix C: Competitive Research Links

- FME 2024.0 announcement
- Talend alternatives analysis
- Modern orchestration tool comparisons
- Cloud-native geospatial format specifications

---

**Document Version:** 1.0
**Last Updated:** 2025-11-05
**Author:** Claude (AI Assistant)
**Reviewers:** [To be added]
**Status:** Draft for Review
