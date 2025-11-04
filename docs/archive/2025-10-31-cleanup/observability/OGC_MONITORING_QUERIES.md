# OGC API Monitoring Queries

**Keywords**: monitoring, prometheus, grafana, azure-insights, kql, promql, observability, dashboards, alerts, ogc-api

Quick reference for monitoring OGC API Features and OGC API Tiles using Prometheus/PromQL and Azure Application Insights KQL.

## Prometheus/PromQL Queries

### Request Rate and Volume

#### Total OGC API Request Rate
```promql
sum(rate(honua_api_requests_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

#### Request Rate by Protocol
```promql
sum by (api_protocol) (rate(honua_api_requests_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

#### Request Rate by Collection
```promql
sum by (service_id, layer_id) (rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m]))
```

#### Top 10 Collections by Request Volume
```promql
topk(10, sum by (layer_id) (rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m])))
```

### Latency and Performance

#### P50 Latency (Median)
```promql
histogram_quantile(0.50,
  sum by (le, api_protocol) (rate(honua_api_request_duration_bucket{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
)
```

#### P95 Latency
```promql
histogram_quantile(0.95,
  sum by (le, api_protocol) (rate(honua_api_request_duration_bucket{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
)
```

#### P99 Latency
```promql
histogram_quantile(0.99,
  sum by (le, api_protocol) (rate(honua_api_request_duration_bucket{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
)
```

#### P95 Latency by Collection
```promql
histogram_quantile(0.95,
  sum by (le, layer_id) (rate(honua_api_request_duration_bucket{api_protocol="ogc-api-features"}[5m]))
)
```

#### Average Request Duration
```promql
avg by (api_protocol) (honua_api_request_duration{api_protocol=~"ogc-api-features|ogc-api-tiles"})
```

#### Slow Requests (> 1 second)
```promql
sum(rate(honua_http_slow_requests_total{http_endpoint=~"/ogc/.*", latency_threshold="1s"}[5m]))
```

### Error Tracking

#### Total Error Rate
```promql
sum(rate(honua_api_errors_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

#### Error Rate by Protocol
```promql
sum by (api_protocol) (rate(honua_api_errors_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

#### Error Percentage
```promql
(
  sum(rate(honua_api_errors_total{api_protocol="ogc-api-features"}[5m]))
  /
  sum(rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m]))
) * 100
```

#### Errors by Type
```promql
sum by (error_type) (rate(honua_api_errors_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

#### Errors by Category
```promql
sum by (error_category) (rate(honua_api_errors_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

### Feature Statistics

#### Total Features Served per Minute
```promql
sum(rate(honua_api_features_returned_total{api_protocol="ogc-api-features"}[1m])) * 60
```

#### Features per Request (Average)
```promql
sum(rate(honua_api_features_returned_total{api_protocol="ogc-api-features"}[5m]))
/
sum(rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m]))
```

#### Features by Collection
```promql
sum by (layer_id) (rate(honua_api_features_returned_total{api_protocol="ogc-api-features"}[5m]))
```

### Tile-Specific Queries

#### Tile Request Rate
```promql
sum(rate(honua_api_requests_total{api_protocol="ogc-api-tiles"}[5m]))
```

#### Tile Rendering P95 Latency
```promql
histogram_quantile(0.95,
  sum by (le) (rate(honua_api_request_duration_bucket{api_protocol="ogc-api-tiles"}[5m]))
)
```

#### Tile Cache Hit Rate (if tracked separately)
```promql
rate(honua_tile_cache_hits_total[5m])
/
(rate(honua_tile_cache_hits_total[5m]) + rate(honua_tile_cache_misses_total[5m]))
```

### HTTP Status Codes

#### Request Count by Status Code
```promql
sum by (http_status_code) (rate(honua_http_requests_total{http_endpoint=~"/ogc/.*"}[5m]))
```

#### 4xx Error Rate
```promql
sum(rate(honua_http_requests_total{http_endpoint=~"/ogc/.*", http_status_code=~"4.."}[5m]))
```

#### 5xx Error Rate
```promql
sum(rate(honua_http_requests_total{http_endpoint=~"/ogc/.*", http_status_code=~"5.."}[5m]))
```

---

## Azure Application Insights KQL Queries

### Request Analysis

#### Total OGC Request Count (Last 24 Hours)
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName startswith "OgcFeatures" or customDimensions.ActivityName startswith "OgcTiles"
| summarize Count = count()
```

#### Request Rate Timeline
```kql
traces
| where timestamp > ago(1h)
| where customDimensions.ActivityName startswith "OgcFeatures" or customDimensions.ActivityName startswith "OgcTiles"
| summarize Count = count() by bin(timestamp, 1m), ActivityName = tostring(customDimensions.ActivityName)
| render timechart
```

#### Top Collections by Request Volume
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName startswith "OgcFeatures"
| extend Collection = tostring(customDimensions["collection.id"])
| summarize RequestCount = count() by Collection
| top 10 by RequestCount desc
```

### Performance Analysis

#### Slow Requests (> 1 second)
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName startswith "OgcFeatures" or customDimensions.ActivityName startswith "OgcTiles"
| extend Duration = todouble(customDimensions.Duration)
| where Duration > 1000
| project timestamp,
          ActivityName = customDimensions.ActivityName,
          Duration,
          Collection = customDimensions["collection.id"],
          Service = customDimensions["service.id"],
          Layer = customDimensions["layer.id"]
| order by Duration desc
```

#### Percentile Latencies by Operation
```kql
traces
| where timestamp > ago(1h)
| where customDimensions.ActivityName startswith "OgcFeatures" or customDimensions.ActivityName startswith "OgcTiles"
| extend Duration = todouble(customDimensions.Duration),
         ActivityName = tostring(customDimensions.ActivityName)
| summarize P50 = percentile(Duration, 50),
            P95 = percentile(Duration, 95),
            P99 = percentile(Duration, 99),
            AvgDuration = avg(Duration),
            MaxDuration = max(Duration)
  by ActivityName
```

#### Performance by Collection
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcFeatures.GetCollectionItems"
| extend Duration = todouble(customDimensions.Duration),
         Collection = tostring(customDimensions["collection.id"])
| summarize AvgDuration = avg(Duration),
            P95Duration = percentile(Duration, 95),
            RequestCount = count()
  by Collection
| order by AvgDuration desc
```

### Feature Statistics

#### Feature Count Analysis
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcFeatures.GetCollectionItems"
| extend FeaturesReturned = toint(customDimensions["features.returned"]),
         FeaturesMatched = toint(customDimensions["features.matched"]),
         Collection = tostring(customDimensions["collection.id"])
| summarize TotalFeaturesServed = sum(FeaturesReturned),
            AvgFeaturesPerRequest = avg(FeaturesReturned),
            TotalRequests = count()
  by Collection
```

#### Query Patterns
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcFeatures.GetCollectionItems"
| extend Format = tostring(customDimensions["response.format"]),
         HasFilter = tobool(customDimensions["query.filter.present"]),
         Limit = toint(customDimensions["query.limit"])
| summarize RequestCount = count(),
            AvgLimit = avg(Limit),
            FilteredQueries = countif(HasFilter == true)
  by Format
```

### Tile Analysis

#### Tile Request Distribution by Zoom Level
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcTiles.GetCollectionTile"
| extend Zoom = toint(customDimensions["tile.zoom"]),
         Format = tostring(customDimensions["tile.format"])
| summarize RequestCount = count() by Zoom, Format
| order by Zoom asc
```

#### Tile Performance by Zoom and Format
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcTiles.GetCollectionTile"
| extend Zoom = toint(customDimensions["tile.zoom"]),
         Format = tostring(customDimensions["tile.format"]),
         Duration = todouble(customDimensions.Duration)
| summarize AvgDuration = avg(Duration),
            P95Duration = percentile(Duration, 95),
            RequestCount = count()
  by Zoom, Format
| order by Zoom asc, Format asc
```

### Error Analysis

#### Error Count by Type
```kql
exceptions
| where timestamp > ago(24h)
| where operation_Name startswith "OgcFeatures" or operation_Name startswith "OgcTiles"
| extend ErrorType = tostring(customDimensions["error.type"]),
         ErrorCategory = tostring(customDimensions["error.category"])
| summarize ErrorCount = count() by ErrorType, ErrorCategory
| order by ErrorCount desc
```

#### Error Timeline
```kql
exceptions
| where timestamp > ago(1h)
| where operation_Name startswith "OgcFeatures" or operation_Name startswith "OgcTiles"
| summarize ErrorCount = count() by bin(timestamp, 5m), Operation = operation_Name
| render timechart
```

#### Failed Requests with Context
```kql
exceptions
| where timestamp > ago(24h)
| where operation_Name startswith "OgcFeatures" or operation_Name startswith "OgcTiles"
| project timestamp,
          Operation = operation_Name,
          Collection = customDimensions["collection.id"],
          Service = customDimensions["service.id"],
          ErrorType = customDimensions["error.type"],
          ErrorMessage = outerMessage
| order by timestamp desc
```

### CRS Transformation Analysis

#### CRS Usage Patterns
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcFeatures.GetCollectionItems"
| extend ContentCrs = tostring(customDimensions["crs.content"])
| where isnotempty(ContentCrs)
| summarize RequestCount = count() by ContentCrs
| order by RequestCount desc
```

### Format Usage

#### Export Format Distribution
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName == "OgcFeatures.GetCollectionItems"
| extend Format = tostring(customDimensions["response.format"])
| summarize RequestCount = count() by Format
| render piechart
```

### Edit Operations

#### Edit Operation Summary
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.ActivityName in ("OgcFeatures.PostCollectionItems", "OgcFeatures.PutCollectionItem", "OgcFeatures.PatchCollectionItem", "OgcFeatures.DeleteCollectionItem")
| extend Operation = tostring(customDimensions.operation),
         Collection = tostring(customDimensions["collection.id"])
| summarize OperationCount = count() by Operation, Collection
```

---

## Grafana Dashboard Panels

### Panel 1: OGC Request Rate
**Type**: Graph
**Query**:
```promql
sum by (api_protocol) (rate(honua_api_requests_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
```

### Panel 2: Request Latency Percentiles
**Type**: Graph
**Queries**:
```promql
# P50
histogram_quantile(0.50, sum by (le) (rate(honua_api_request_duration_bucket{api_protocol="ogc-api-features"}[5m])))

# P95
histogram_quantile(0.95, sum by (le) (rate(honua_api_request_duration_bucket{api_protocol="ogc-api-features"}[5m])))

# P99
histogram_quantile(0.99, sum by (le) (rate(honua_api_request_duration_bucket{api_protocol="ogc-api-features"}[5m])))
```

### Panel 3: Error Rate
**Type**: Gauge
**Query**:
```promql
(
  sum(rate(honua_api_errors_total{api_protocol="ogc-api-features"}[5m]))
  /
  sum(rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m]))
) * 100
```

### Panel 4: Features Served
**Type**: Counter
**Query**:
```promql
sum(increase(honua_api_features_returned_total{api_protocol="ogc-api-features"}[1h]))
```

### Panel 5: Top Collections
**Type**: Bar Chart
**Query**:
```promql
topk(10, sum by (layer_id) (rate(honua_api_requests_total{api_protocol="ogc-api-features"}[5m])))
```

### Panel 6: Tile Requests by Zoom
**Type**: Heatmap
**Query**: Azure KQL or Prometheus with custom labels

---

## Alert Rules

### Prometheus Alert Examples

```yaml
groups:
  - name: ogc_api_alerts
    rules:
      - alert: OgcApiHighErrorRate
        expr: |
          (
            sum(rate(honua_api_errors_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
            /
            sum(rate(honua_api_requests_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[5m]))
          ) > 0.05
        for: 5m
        labels:
          severity: warning
          component: ogc-api
        annotations:
          summary: "OGC API error rate is above 5%"
          description: "Error rate: {{ $value | humanizePercentage }}"

      - alert: OgcApiSlowRequests
        expr: |
          histogram_quantile(0.95,
            sum by (le) (rate(honua_api_request_duration_bucket{api_protocol="ogc-api-features"}[5m]))
          ) > 2000
        for: 5m
        labels:
          severity: warning
          component: ogc-api
        annotations:
          summary: "OGC API P95 latency is above 2 seconds"
          description: "P95 latency: {{ $value }}ms"

      - alert: OgcApiNoTraffic
        expr: |
          absent_over_time(honua_api_requests_total{api_protocol=~"ogc-api-features|ogc-api-tiles"}[15m])
        for: 15m
        labels:
          severity: info
          component: ogc-api
        annotations:
          summary: "No OGC API traffic detected"
          description: "No requests in the last 15 minutes"
```

---

## Related Documentation

- [OGC API Telemetry](./OGC_API_TELEMETRY.md)
- [Metrics Documentation](./METRICS.md)
- [OpenTelemetry ADR](../architecture/decisions/0005-opentelemetry-for-observability.md)
