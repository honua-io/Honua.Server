# OGC API Telemetry and Observability

**Keywords**: ogc-api-telemetry, opentelemetry, distributed-tracing, metrics, observability, monitoring, ogc-api-features, ogc-api-tiles, activity-tracing, performance-monitoring

This document describes the comprehensive telemetry and observability implementation for the OGC API endpoints in Honua Server.

## Table of Contents

1. [Overview](#overview)
2. [OpenTelemetry Activities](#opentelemetry-activities)
3. [Metrics](#metrics)
4. [Activity Tags](#activity-tags)
5. [Usage Examples](#usage-examples)
6. [Monitoring Queries](#monitoring-queries)
7. [Performance Optimization](#performance-optimization)

---

## Overview

The OGC API implementation in Honua Server is instrumented with comprehensive telemetry using:

- **OpenTelemetry Activities** for distributed tracing
- **Metrics** (counters and histograms) for operational insights
- **Structured tags** for filtering and analysis

All OGC operations emit traces and metrics that can be collected by OpenTelemetry-compatible backends like Jaeger, Zipkin, Prometheus, Azure Application Insights, AWS X-Ray, or Google Cloud Trace.

## OpenTelemetry Activities

### Activity Source

All OGC operations use the `HonuaTelemetry.OgcProtocols` ActivitySource:

```csharp
public static readonly ActivitySource OgcProtocols = new("Honua.Server.OgcProtocols", "1.0.0");
```

### OGC API Features Activities

#### GetCollectionItems
Traces feature collection queries including filtering, paging, and CRS transformations.

**Activity Name**: `OgcFeatures.GetCollectionItems`

**Example**:
```csharp
using var activity = HonuaTelemetry.OgcProtocols.StartActivity("OgcFeatures.GetCollectionItems");
activity?.SetTag("collection.id", collectionId);
activity?.SetTag("service.id", service.Id);
activity?.SetTag("layer.id", layer.Id);
activity?.SetTag("response.format", format.ToString());
activity?.SetTag("crs.content", contentCrs);
activity?.SetTag("query.limit", query.Limit);
activity?.SetTag("features.returned", numberReturned);
activity?.SetTag("features.matched", numberMatched);
```

#### GetCollectionItem
Traces single feature retrieval by ID.

**Activity Name**: `OgcFeatures.GetCollectionItem`

**Tags**:
- `collection.id`
- `feature.id`
- `service.id`
- `layer.id`
- `response.format`
- `crs.content`

#### PostCollectionItems
Traces feature creation operations.

**Activity Name**: `OgcFeatures.PostCollectionItems`

**Tags**:
- `collection.id`
- `service.id`
- `layer.id`
- `operation` = "create"
- `features.created`

#### PutCollectionItem
Traces feature update operations (full replacement).

**Activity Name**: `OgcFeatures.PutCollectionItem`

**Tags**:
- `collection.id`
- `feature.id`
- `service.id`
- `layer.id`
- `operation` = "update"

#### PatchCollectionItem
Traces feature partial update operations.

**Activity Name**: `OgcFeatures.PatchCollectionItem`

**Tags**:
- `collection.id`
- `feature.id`
- `service.id`
- `layer.id`
- `operation` = "patch"

#### DeleteCollectionItem
Traces feature deletion operations.

**Activity Name**: `OgcFeatures.DeleteCollectionItem`

**Tags**:
- `collection.id`
- `feature.id`
- `service.id`
- `layer.id`
- `operation` = "delete"

### OGC API Tiles Activities

#### GetCollectionTile
Traces tile rendering operations including zoom level, coordinates, and format.

**Activity Name**: `OgcTiles.GetCollectionTile`

**Tags**:
- `collection.id`
- `tileset.id`
- `service.id`
- `layer.id`
- `tile.matrix.set` (e.g., "WebMercatorQuad")
- `tile.zoom`
- `tile.row`
- `tile.col`
- `tile.format` (png, mvt, geojson, etc.)
- `tile.size` (256, 512, etc.)
- `crs.target`

---

## Metrics

All metrics are recorded using the `IApiMetrics` interface, which provides:

### Request Counting

```csharp
apiMetrics.RecordRequest("ogc-api-features", service.Id, layer.Id);
```

**Metric**: `honua.api.requests`

**Dimensions**:
- `api.protocol` - "ogc-api-features" or "ogc-api-tiles"
- `service.id` - Service identifier
- `layer.id` - Layer identifier

### Request Duration

```csharp
var duration = DateTimeOffset.UtcNow - startTime;
apiMetrics.RecordRequestDuration("ogc-api-features", service.Id, layer.Id, duration, StatusCodes.Status200OK);
```

**Metric**: `honua.api.request_duration` (histogram in milliseconds)

**Dimensions**:
- `api.protocol`
- `service.id`
- `layer.id`
- `http.status_code`

### Feature Counting

```csharp
apiMetrics.RecordFeatureCount("ogc-api-features", service.Id, layer.Id, numberReturned);
```

**Metric**: `honua.api.features_returned`

**Dimensions**:
- `api.protocol`
- `service.id`
- `layer.id`

### Error Tracking

```csharp
apiMetrics.RecordError("ogc-api-features", collectionId, layerId, ex);
```

**Metric**: `honua.api.errors`

**Dimensions**:
- `api.protocol`
- `service.id`
- `layer.id`
- `error.type` (e.g., "database_error", "timeout", "validation")
- `error.category` (e.g., "database", "network", "validation")

---

## Activity Tags

### Core Tags (All Operations)

| Tag | Description | Example |
|-----|-------------|---------|
| `collection.id` | OGC collection identifier | "countries" |
| `service.id` | Honua service identifier | "geodata" |
| `layer.id` | Layer identifier within service | "countries_layer" |

### Query-Related Tags

| Tag | Description | Example |
|-----|-------------|---------|
| `query.limit` | Maximum features to return | 10 |
| `query.filter.present` | Whether CQL filter is used | true |
| `query.result_type` | Result type (results/hits) | "results" |
| `features.returned` | Actual features returned | 8 |
| `features.matched` | Total matching features | 150 |

### Format and CRS Tags

| Tag | Description | Example |
|-----|-------------|---------|
| `response.format` | Output format | "GeoJson", "Kml", "Shapefile" |
| `crs.content` | Content CRS identifier | "EPSG:4326" |
| `crs.source` | Source CRS | "EPSG:3857" |
| `crs.target` | Target CRS | "EPSG:4326" |

### Tile-Specific Tags

| Tag | Description | Example |
|-----|-------------|---------|
| `tileset.id` | Tileset identifier | "satellite_imagery" |
| `tile.matrix.set` | Tile matrix set | "WebMercatorQuad" |
| `tile.zoom` | Zoom level | "12" |
| `tile.row` | Tile row coordinate | 1523 |
| `tile.col` | Tile column coordinate | 2048 |
| `tile.format` | Tile format | "png", "mvt" |
| `tile.size` | Tile size in pixels | 256 |

### Edit Operation Tags

| Tag | Description | Example |
|-----|-------------|---------|
| `operation` | Edit operation type | "create", "update", "patch", "delete" |
| `feature.id` | Feature identifier | "feature_12345" |
| `features.created` | Number of features created | 5 |

---

## Usage Examples

### Example 1: Tracing a Feature Query

When a client requests features from a collection, the system creates a trace span:

```
GET /ogc/collections/cities/items?limit=10&crs=EPSG:4326&filter=population > 1000000
```

**Generated Activity**:
- Name: `OgcFeatures.GetCollectionItems`
- Tags:
  - `collection.id`: "cities"
  - `service.id`: "geodata"
  - `layer.id`: "cities_layer"
  - `response.format`: "GeoJson"
  - `crs.content`: "EPSG:4326"
  - `query.limit`: 10
  - `query.filter.present`: true
  - `features.returned`: 10
  - `features.matched`: 47

### Example 2: Monitoring Tile Requests

```
GET /ogc/collections/basemap/tiles/world_imagery/WebMercatorQuad/12/1523/2048.png
```

**Generated Activity**:
- Name: `OgcTiles.GetCollectionTile`
- Tags:
  - `collection.id`: "basemap"
  - `tileset.id`: "world_imagery"
  - `tile.matrix.set`: "WebMercatorQuad"
  - `tile.zoom`: "12"
  - `tile.row`: 1523
  - `tile.col`: 2048
  - `tile.format`: "png"
  - `tile.size`: 256

### Example 3: Tracking Feature Edits

```
POST /ogc/collections/poi/items
{ "type": "Feature", "properties": {...}, "geometry": {...} }
```

**Generated Activity**:
- Name: `OgcFeatures.PostCollectionItems`
- Tags:
  - `collection.id`: "poi"
  - `operation`: "create"
  - `features.created`: 1

---

## Monitoring Queries

### Prometheus/OpenMetrics Examples

#### Request Rate by Protocol

```promql
rate(honua_api_requests_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m])
```

#### Average Request Duration by Collection

```promql
avg by (collection_id) (honua_api_request_duration{api_protocol="ogc-api-features"})
```

#### P95 Latency for OGC API Features

```promql
histogram_quantile(0.95,
  rate(honua_api_request_duration_bucket{api_protocol="ogc-api-features"}[5m])
)
```

#### Error Rate by Service

```promql
rate(honua_api_errors_total{api_protocol="ogc-api-features"}[5m])
/
rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m])
```

#### Total Features Served

```promql
sum(rate(honua_api_features_returned_total[5m]))
```

#### Tile Cache Hit Rate

```promql
rate(honua_tile_cache_hits_total[5m])
/
(rate(honua_tile_cache_hits_total[5m]) + rate(honua_tile_cache_misses_total[5m]))
```

### Azure Application Insights KQL Queries

#### Find Slow OGC Requests

```kql
traces
| where customDimensions.ActivityName startswith "OgcFeatures" or customDimensions.ActivityName startswith "OgcTiles"
| where customDimensions.Duration > 1000
| project timestamp, ActivityName = customDimensions.ActivityName,
          Duration = customDimensions.Duration,
          CollectionId = customDimensions["collection.id"],
          LayerId = customDimensions["layer.id"]
| order by Duration desc
```

#### Track Feature Query Patterns

```kql
traces
| where customDimensions.ActivityName == "OgcFeatures.GetCollectionItems"
| project timestamp,
          Collection = customDimensions["collection.id"],
          Format = customDimensions["response.format"],
          FeaturesReturned = toint(customDimensions["features.returned"]),
          FeaturesMatched = toint(customDimensions["features.matched"]),
          Limit = toint(customDimensions["query.limit"])
| summarize TotalQueries = count(),
            AvgReturned = avg(FeaturesReturned),
            TotalFeatures = sum(FeaturesReturned)
  by Collection, Format
```

#### Monitor Tile Rendering Performance

```kql
traces
| where customDimensions.ActivityName == "OgcTiles.GetCollectionTile"
| extend Zoom = toint(customDimensions["tile.zoom"]),
         Format = customDimensions["tile.format"],
         Duration = toint(customDimensions.Duration)
| summarize AvgDuration = avg(Duration),
            P95Duration = percentile(Duration, 95),
            Count = count()
  by Zoom, Format
| order by Zoom asc
```

---

## Performance Optimization

### Best Practices

1. **Use Sampling for High-Volume Endpoints**
   - Configure sampling rates for tile endpoints to reduce overhead
   - Example: Sample 10% of tile requests at zoom levels > 12

2. **Tag Cardinality**
   - Activity tags are designed with controlled cardinality
   - Collection IDs, service IDs, and layer IDs are normalized
   - Avoid adding high-cardinality tags (e.g., unique feature IDs in tags for bulk operations)

3. **Metrics Aggregation**
   - Metrics use histograms for duration tracking (enables percentile calculations)
   - Counters track totals for aggregation

4. **Error Handling**
   - All operations wrap in try-catch to record errors
   - Activities are marked with `ActivityStatusCode.Error` on failures
   - Error types are categorized for easier alerting

### Overhead

- **Activity Creation**: ~0.1-0.5 ms per request
- **Tag Assignment**: ~0.01 ms per tag
- **Metrics Recording**: ~0.05 ms per metric
- **Total Overhead**: < 2 ms per request (typical)

### Opt-Out

To disable telemetry for specific operations, set the ActivitySource listener to null or configure sampling rates in your OpenTelemetry SDK configuration.

---

## Related Documentation

- [OpenTelemetry for Observability ADR](../architecture/decisions/0005-opentelemetry-for-observability.md)
- [Metrics Documentation](./METRICS.md)
- [OGC API Features Guide](../rag/03-01-ogc-api-features.md)
- [Distributed Tracing Deployment](../../docker/process-testing/DISTRIBUTED_TRACING_DEPLOYMENT.md)

---

## Summary

The OGC API implementation now has comprehensive telemetry that enables:

- **End-to-end distributed tracing** across all OGC operations
- **Performance monitoring** with detailed duration metrics
- **Error tracking** with categorization
- **Usage analytics** for format preferences, CRS transformations, and feature counts
- **Operational insights** for cache effectiveness and tile rendering

This telemetry infrastructure enables proactive monitoring, debugging, and optimization of the OGC API services in production environments.
