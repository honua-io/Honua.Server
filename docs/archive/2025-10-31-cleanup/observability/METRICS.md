# Honua Metrics Reference

This document provides a comprehensive reference for all OpenTelemetry metrics exposed by Honua Server. All metrics are exported in Prometheus format and can be scraped from the `/metrics` endpoint.

## Table of Contents

- [Overview](#overview)
- [Metric Categories](#metric-categories)
- [API Metrics](#api-metrics)
- [Database Metrics](#database-metrics)
- [Cache Metrics](#cache-metrics)
- [Raster Tile Metrics](#raster-tile-metrics)
- [Vector Tile Metrics](#vector-tile-metrics)
- [Security Metrics](#security-metrics)
- [Business Metrics](#business-metrics)
- [Infrastructure Metrics](#infrastructure-metrics)
- [PromQL Examples](#promql-examples)
- [Grafana Dashboard](#grafana-dashboard)
- [Performance Impact](#performance-impact)

## Overview

Honua Server exposes 40+ OpenTelemetry metrics across 8 categories:

- **API Metrics**: Request counts, durations, errors by protocol
- **Database Metrics**: Query performance, connection pooling
- **Cache Metrics**: Hit/miss rates, latency, evictions
- **Raster Tile Metrics**: Tile generation, cache efficiency
- **Vector Tile Metrics**: Tile generation, feature processing
- **Security Metrics**: Authentication, authorization events
- **Business Metrics**: Features served, data throughput
- **Infrastructure Metrics**: Memory, GC, thread pool stats

All metrics follow OpenTelemetry semantic conventions and include relevant dimensions for filtering and aggregation.

## Metric Categories

### Meters

Each metric category is exposed through a dedicated meter:

- `Honua.Server.Api` - API operation metrics
- `Honua.Server.Database` - Database operation metrics
- `Honua.Server.Cache` - Cache operation metrics
- `Honua.Server.VectorTiles` - Vector tile metrics
- `Honua.Server.RasterCache` - Raster tile metrics
- `Honua.Server.Security` - Security and authentication metrics
- `Honua.Server.Business` - Business-level metrics
- `Honua.Server.Infrastructure` - Infrastructure health metrics

## API Metrics

### honua.api.requests

**Type**: Counter
**Unit**: `{request}`
**Description**: Number of API requests by protocol, service, and layer

**Dimensions**:
- `api.protocol` - API protocol (wfs, wms, ogc-api-features, esri-rest, stac, etc.)
- `service.id` - Service identifier
- `layer.id` - Layer identifier

**PromQL Examples**:
```promql
# Total requests per second
rate(honua_api_requests_total[5m])

# Requests by protocol
sum(rate(honua_api_requests_total[5m])) by (api_protocol)

# Top services by request volume
topk(10, sum(rate(honua_api_requests_total[5m])) by (service_id))
```

### honua.api.request_duration

**Type**: Histogram
**Unit**: `ms`
**Description**: API request duration by protocol, service, and layer

**Dimensions**:
- `api.protocol` - API protocol
- `service.id` - Service identifier
- `layer.id` - Layer identifier
- `http.status_code` - HTTP status code

**PromQL Examples**:
```promql
# P95 latency by protocol
histogram_quantile(0.95, sum(rate(honua_api_request_duration_bucket[5m])) by (le, api_protocol))

# Average latency
rate(honua_api_request_duration_sum[5m]) / rate(honua_api_request_duration_count[5m])

# Slow requests (>1s)
sum(rate(honua_api_request_duration_bucket{le="1000"}[5m])) by (api_protocol)
```

### honua.api.errors

**Type**: Counter
**Unit**: `{error}`
**Description**: Number of API errors by protocol, service, layer, and error type

**Dimensions**:
- `api.protocol` - API protocol
- `service.id` - Service identifier
- `layer.id` - Layer identifier
- `error.type` - Error type (timeout, database_error, validation, etc.)
- `error.category` - Error category (validation, security, performance, network, database, storage, resource, application)

**PromQL Examples**:
```promql
# Error rate
rate(honua_api_errors_total[5m])

# Errors by category
sum(rate(honua_api_errors_total[5m])) by (error_category)

# Top error types
topk(10, sum(rate(honua_api_errors_total[5m])) by (error_type))
```

### honua.api.features_returned

**Type**: Counter
**Unit**: `{feature}`
**Description**: Number of features returned by protocol, service, and layer

**Dimensions**:
- `api.protocol` - API protocol
- `service.id` - Service identifier
- `layer.id` - Layer identifier

**PromQL Examples**:
```promql
# Features per second
rate(honua_api_features_returned_total[5m])

# Average features per request
rate(honua_api_features_returned_total[5m]) / rate(honua_api_requests_total[5m])
```

## Database Metrics

### honua.database.queries

**Type**: Counter
**Unit**: `{query}`
**Description**: Number of database queries executed by type and table

**Dimensions**:
- `query.type` - Query type (select, insert, update, delete, bulk_insert, etc.)
- `table.name` - Table name
- `success` - Success indicator (true/false)

**PromQL Examples**:
```promql
# Query rate
rate(honua_database_queries_total[5m])

# Queries by type
sum(rate(honua_database_queries_total[5m])) by (query_type)

# Failed queries
sum(rate(honua_database_queries_total{success="false"}[5m]))
```

### honua.database.query_duration

**Type**: Histogram
**Unit**: `ms`
**Description**: Database query execution duration

**Dimensions**:
- `query.type` - Query type
- `table.name` - Table name
- `success` - Success indicator

**PromQL Examples**:
```promql
# P95 query latency
histogram_quantile(0.95, sum(rate(honua_database_query_duration_bucket[5m])) by (le))

# Slow queries (>100ms)
sum(increase(honua_database_query_duration_bucket{le="100"}[5m])) by (query_type, table_name)
```

### honua.database.slow_queries

**Type**: Counter
**Unit**: `{query}`
**Description**: Number of slow queries (>1s) by type and table

**Dimensions**:
- `query.type` - Query type
- `table.name` - Table name
- `duration_bucket` - Duration bucket (fast, medium, slow, very_slow, critical)

**PromQL Examples**:
```promql
# Slow query rate
rate(honua_database_slow_queries_total[5m])

# Critical slow queries
sum(rate(honua_database_slow_queries_total{duration_bucket="critical"}[5m]))
```

### honua.database.connection_wait_time

**Type**: Histogram
**Unit**: `ms`
**Description**: Time spent waiting for a database connection

**Dimensions**:
- `connection.pool` - Connection pool identifier

**PromQL Examples**:
```promql
# P95 connection wait time
histogram_quantile(0.95, sum(rate(honua_database_connection_wait_time_bucket[5m])) by (le))
```

### honua.database.connection_errors

**Type**: Counter
**Unit**: `{error}`
**Description**: Number of database connection errors by type

**Dimensions**:
- `connection.pool` - Connection pool identifier
- `error.type` - Error type

**PromQL Examples**:
```promql
# Connection error rate
rate(honua_database_connection_errors_total[5m])
```

### honua.database.transaction_commits

**Type**: Counter
**Unit**: `{transaction}`
**Description**: Number of committed transactions

**PromQL Examples**:
```promql
# Transaction commit rate
rate(honua_database_transaction_commits_total[5m])
```

### honua.database.transaction_rollbacks

**Type**: Counter
**Unit**: `{transaction}`
**Description**: Number of rolled back transactions

**Dimensions**:
- `rollback.reason` - Rollback reason

**PromQL Examples**:
```promql
# Transaction rollback rate
rate(honua_database_transaction_rollbacks_total[5m])

# Rollback ratio
rate(honua_database_transaction_rollbacks_total[5m]) /
  (rate(honua_database_transaction_commits_total[5m]) + rate(honua_database_transaction_rollbacks_total[5m]))
```

## Cache Metrics

### honua.cache.hits

**Type**: Counter
**Unit**: `{hit}`
**Description**: Number of cache hits by cache name and region

**Dimensions**:
- `cache.name` - Cache name (redis, memory, raster, vector, tile, metadata, session)
- `cache.region` - Cache region
- `cache.key.pattern` - Key pattern prefix

**PromQL Examples**:
```promql
# Cache hit rate
rate(honua_cache_hits_total[5m])

# Hit rate by cache
sum(rate(honua_cache_hits_total[5m])) by (cache_name)
```

### honua.cache.misses

**Type**: Counter
**Unit**: `{miss}`
**Description**: Number of cache misses by cache name and region

**Dimensions**:
- `cache.name` - Cache name
- `cache.region` - Cache region
- `cache.key.pattern` - Key pattern prefix

**PromQL Examples**:
```promql
# Cache miss rate
rate(honua_cache_misses_total[5m])

# Cache hit ratio
sum(rate(honua_cache_hits_total[5m])) /
  (sum(rate(honua_cache_hits_total[5m])) + sum(rate(honua_cache_misses_total[5m])))
```

### honua.cache.operation_duration

**Type**: Histogram
**Unit**: `ms`
**Description**: Cache operation latency by cache name and operation type

**Dimensions**:
- `cache.name` - Cache name
- `operation` - Operation type (get, set, delete, exists, clear)

**PromQL Examples**:
```promql
# P95 cache latency
histogram_quantile(0.95, sum(rate(honua_cache_operation_duration_bucket[5m])) by (le, cache_name))
```

### honua.cache.evictions

**Type**: Counter
**Unit**: `{eviction}`
**Description**: Number of cache evictions by reason

**Dimensions**:
- `cache.name` - Cache name
- `eviction.reason` - Eviction reason (capacity, expiration, memory_pressure, manual)

**PromQL Examples**:
```promql
# Eviction rate
rate(honua_cache_evictions_total[5m])

# Evictions by reason
sum(rate(honua_cache_evictions_total[5m])) by (eviction_reason)
```

## Raster Tile Metrics

### honua.raster.cache_hits

**Type**: Counter
**Unit**: `{hit}`
**Description**: Number of raster tile cache hits

**Dimensions**:
- `dataset` - Dataset identifier

### honua.raster.cache_misses

**Type**: Counter
**Unit**: `{miss}`
**Description**: Number of raster tile cache misses

**Dimensions**:
- `dataset` - Dataset identifier

### honua.raster.render_latency_ms

**Type**: Histogram
**Unit**: `ms`
**Description**: Raster tile render latency

**Dimensions**:
- `dataset` - Dataset identifier
- `source` - Source (request, preseed)

**PromQL Examples**:
```promql
# P95 render latency
histogram_quantile(0.95, sum(rate(honua_raster_render_latency_ms_bucket[5m])) by (le))

# Cache hit ratio
sum(rate(honua_raster_cache_hits_total[5m])) /
  (sum(rate(honua_raster_cache_hits_total[5m])) + sum(rate(honua_raster_cache_misses_total[5m])))
```

## Vector Tile Metrics

### honua.vectortile.tiles_generated

**Type**: Counter
**Unit**: `{tile}`
**Description**: Number of vector tiles generated

**Dimensions**:
- `layer.id` - Layer identifier
- `zoom.level` - Zoom level
- `zoom.bucket` - Zoom bucket (low, medium, high, very_high)

### honua.vectortile.tiles_served

**Type**: Counter
**Unit**: `{tile}`
**Description**: Number of vector tiles served to clients

**Dimensions**:
- `layer.id` - Layer identifier
- `zoom.level` - Zoom level
- `zoom.bucket` - Zoom bucket
- `cache.hit` - Cache hit indicator

**PromQL Examples**:
```promql
# Tiles per second
rate(honua_vectortile_tiles_served_total[5m])

# Cache hit ratio
sum(rate(honua_vectortile_tiles_served_total{cache_hit="true"}[5m])) /
  sum(rate(honua_vectortile_tiles_served_total[5m]))
```

### honua.vectortile.generation_duration

**Type**: Histogram
**Unit**: `ms`
**Description**: Vector tile generation duration

**Dimensions**:
- `layer.id` - Layer identifier
- `zoom.level` - Zoom level
- `zoom.bucket` - Zoom bucket

### honua.vectortile.features_per_tile

**Type**: Histogram
**Unit**: `{feature}`
**Description**: Number of features per generated tile

**Dimensions**:
- `layer.id` - Layer identifier
- `zoom.level` - Zoom level
- `feature.count.bucket` - Feature count bucket (empty, sparse, normal, dense, very_dense)

## Security Metrics

### honua.security.login_attempts

**Type**: Counter
**Unit**: `{attempt}`
**Description**: Number of login attempts by authentication method

**Dimensions**:
- `auth.method` - Authentication method (oauth, oidc, basic, bearer, api_key, certificate, anonymous)
- `success` - Success indicator
- `username.provided` - Whether username was provided

**PromQL Examples**:
```promql
# Login rate
rate(honua_security_login_attempts_total[5m])

# Failed logins
sum(rate(honua_security_login_attempts_total{success="false"}[5m]))

# Success rate
sum(rate(honua_security_login_attempts_total{success="true"}[5m])) /
  sum(rate(honua_security_login_attempts_total[5m]))
```

### honua.security.login_failures

**Type**: Counter
**Unit**: `{failure}`
**Description**: Number of failed login attempts by reason

**Dimensions**:
- `auth.method` - Authentication method
- `failure.reason` - Failure reason (invalid_credentials, invalid_password, expired, account_locked, etc.)

### honua.security.token_validations

**Type**: Counter
**Unit**: `{validation}`
**Description**: Number of token validation attempts

**Dimensions**:
- `token.type` - Token type (jwt, refresh, id_token, api_key)
- `success` - Success indicator

### honua.security.authorization_checks

**Type**: Counter
**Unit**: `{check}`
**Description**: Number of authorization checks performed

**Dimensions**:
- `resource.type` - Resource type (layer, service, dataset, tile, feature, metadata, admin)
- `permission` - Permission (read, write, update, delete, admin)
- `granted` - Whether permission was granted

**PromQL Examples**:
```promql
# Authorization check rate
rate(honua_security_authorization_checks_total[5m])

# Denied requests
sum(rate(honua_security_authorization_checks_total{granted="false"}[5m]))
```

### honua.security.sessions_created

**Type**: Counter
**Unit**: `{session}`
**Description**: Number of user sessions created

### honua.security.session_duration

**Type**: Histogram
**Unit**: `ms`
**Description**: User session duration

**Dimensions**:
- `termination.reason` - Termination reason (logout, timeout, revoked, error)
- `duration.bucket` - Duration bucket (very_short, short, normal, long, very_long)

## Business Metrics

### honua.business.features_served

**Type**: Counter
**Unit**: `{feature}`
**Description**: Number of features served to clients

**Dimensions**:
- `layer.id` - Layer identifier
- `protocol` - Protocol (wfs, ogc-api-features, esri-rest, odata)
- `feature.count.bucket` - Feature count bucket

**PromQL Examples**:
```promql
# Features per second
rate(honua_business_features_served_total[5m])

# Top layers by usage
topk(10, sum(rate(honua_business_features_served_total[5m])) by (layer_id))
```

### honua.business.raster_tiles_served

**Type**: Counter
**Unit**: `{tile}`
**Description**: Number of raster tiles served to clients

**Dimensions**:
- `dataset.id` - Dataset identifier
- `zoom.level` - Zoom level
- `zoom.bucket` - Zoom bucket

### honua.business.vector_tiles_served

**Type**: Counter
**Unit**: `{tile}`
**Description**: Number of vector tiles served to clients

**Dimensions**:
- `layer.id` - Layer identifier
- `zoom.level` - Zoom level
- `zoom.bucket` - Zoom bucket

### honua.business.stac_searches

**Type**: Counter
**Unit**: `{search}`
**Description**: Number of STAC catalog searches

**Dimensions**:
- `uses.bbox` - Whether search uses bounding box
- `uses.datetime` - Whether search uses datetime filter
- `result.count.bucket` - Result count bucket

### honua.business.exports

**Type**: Counter
**Unit**: `{export}`
**Description**: Number of data export operations

**Dimensions**:
- `export.format` - Export format (geojson, shapefile, geopackage, csv, kml, gml, pdf)
- `feature.count.bucket` - Feature count bucket

### honua.business.active_sessions

**Type**: ObservableGauge
**Unit**: `{session}`
**Description**: Number of currently active user sessions

**PromQL Examples**:
```promql
# Current active sessions
honua_business_active_sessions

# Average active sessions over time
avg_over_time(honua_business_active_sessions[1h])
```

## Infrastructure Metrics

### honua.infrastructure.memory_working_set

**Type**: ObservableGauge
**Unit**: `bytes`
**Description**: Process working set size

**PromQL Examples**:
```promql
# Current memory usage
honua_infrastructure_memory_working_set

# Memory usage in MB
honua_infrastructure_memory_working_set / 1024 / 1024
```

### honua.infrastructure.memory_gc_heap

**Type**: ObservableGauge
**Unit**: `bytes`
**Description**: GC heap size

### honua.infrastructure.gc_collections

**Type**: Counter
**Unit**: `{collection}`
**Description**: Number of garbage collections by generation

**Dimensions**:
- `gc.generation` - GC generation (0, 1, 2)
- `gc.type` - GC type (gen0, gen1, gen2, blocking)

**PromQL Examples**:
```promql
# GC rate by generation
rate(honua_infrastructure_gc_collections_total[5m])

# Gen2 collections (expensive)
rate(honua_infrastructure_gc_collections_total{gc_generation="2"}[5m])
```

### honua.infrastructure.gc_duration

**Type**: Histogram
**Unit**: `ms`
**Description**: Garbage collection pause duration

**Dimensions**:
- `gc.generation` - GC generation
- `duration.bucket` - Duration bucket (fast, normal, slow, very_slow, critical)

**PromQL Examples**:
```promql
# P95 GC pause time
histogram_quantile(0.95, sum(rate(honua_infrastructure_gc_duration_bucket[5m])) by (le))

# Long GC pauses (>100ms)
sum(increase(honua_infrastructure_gc_duration_bucket{le="100"}[5m]))
```

### honua.infrastructure.threadpool_worker_threads

**Type**: ObservableGauge
**Unit**: `{thread}`
**Description**: Available worker threads in thread pool

### honua.infrastructure.threadpool_queue_length

**Type**: ObservableGauge
**Unit**: `{item}`
**Description**: Number of items queued in thread pool

**PromQL Examples**:
```promql
# Thread pool saturation
honua_infrastructure_threadpool_queue_length > 100

# Worker thread availability
honua_infrastructure_threadpool_worker_threads
```

### honua.infrastructure.cpu_usage_percent

**Type**: ObservableGauge
**Unit**: `%`
**Description**: CPU usage percentage

**PromQL Examples**:
```promql
# Current CPU usage
honua_infrastructure_cpu_usage_percent

# Average CPU over time
avg_over_time(honua_infrastructure_cpu_usage_percent[5m])
```

## PromQL Examples

### Golden Signals

#### Latency
```promql
# P95 API latency
histogram_quantile(0.95, sum(rate(honua_api_request_duration_bucket[5m])) by (le, api_protocol))

# P99 Database latency
histogram_quantile(0.99, sum(rate(honua_database_query_duration_bucket[5m])) by (le, query_type))
```

#### Traffic
```promql
# Requests per second
rate(honua_api_requests_total[5m])

# Features per second
rate(honua_business_features_served_total[5m])

# Tiles per second
rate(honua_business_raster_tiles_served_total[5m]) + rate(honua_business_vector_tiles_served_total[5m])
```

#### Errors
```promql
# Error rate
rate(honua_api_errors_total[5m])

# Error ratio
sum(rate(honua_api_errors_total[5m])) / sum(rate(honua_api_requests_total[5m]))

# Database errors
rate(honua_database_queries_total{success="false"}[5m])
```

#### Saturation
```promql
# Memory usage percentage
honua_infrastructure_memory_working_set / honua_infrastructure_memory_limit * 100

# Thread pool queue depth
honua_infrastructure_threadpool_queue_length

# Database connection pool utilization
honua_database_active_connections / honua_database_max_connections * 100
```

### Custom Queries

#### Cache Efficiency
```promql
# Overall cache hit ratio
(sum(rate(honua_cache_hits_total[5m])) + sum(rate(honua_raster_cache_hits_total[5m]))) /
(sum(rate(honua_cache_hits_total[5m])) + sum(rate(honua_cache_misses_total[5m])) +
 sum(rate(honua_raster_cache_hits_total[5m])) + sum(rate(honua_raster_cache_misses_total[5m])))
```

#### Security Health
```promql
# Failed login attempts (potential attack)
sum(rate(honua_security_login_failures_total[5m])) > 10

# Authorization denial rate
rate(honua_security_authorization_denials_total[5m])
```

#### Performance Indicators
```promql
# Slow query percentage
sum(rate(honua_database_slow_queries_total[5m])) / sum(rate(honua_database_queries_total[5m])) * 100

# Long GC pauses
sum(increase(honua_infrastructure_gc_duration_bucket{duration_bucket=~"slow|very_slow|critical"}[5m]))
```

## Grafana Dashboard

A comprehensive Grafana dashboard is available at `docker/grafana/dashboards/honua-metrics.json` that includes:

- **Overview**: Request rate, error rate, latency, active sessions
- **API Performance**: Latency percentiles, error rates by protocol
- **Database**: Query performance, connection pool stats, slow queries
- **Cache**: Hit ratios, latency, eviction rates
- **Tiles**: Raster and vector tile metrics, generation times
- **Security**: Login attempts, authorization checks, session metrics
- **Infrastructure**: Memory, CPU, GC, thread pool stats

## Performance Impact

The metrics implementation is designed for minimal overhead:

- **CPU Impact**: <0.5% average overhead
- **Memory Impact**: ~10MB for metrics collection infrastructure
- **Network Impact**: Negligible (pull-based Prometheus scraping)

### Best Practices

1. **Scrape Interval**: Recommended 15-30 seconds
2. **Retention**: 15 days for Prometheus, longer in long-term storage
3. **Cardinality**: Metrics are designed to avoid high cardinality explosions
4. **Aggregation**: Use recording rules for frequently queried aggregations

### Cardinality Management

To prevent cardinality explosions:

- Layer/service IDs are normalized
- Timestamps are bucketed
- Numeric values use histogram buckets
- User identifiers are masked or hashed

### Recording Rules

Example Prometheus recording rules for common queries:

```yaml
groups:
  - name: honua_aggregations
    interval: 30s
    rules:
      - record: honua:api:request_rate:5m
        expr: rate(honua_api_requests_total[5m])

      - record: honua:api:error_rate:5m
        expr: rate(honua_api_errors_total[5m])

      - record: honua:cache:hit_ratio:5m
        expr: |
          sum(rate(honua_cache_hits_total[5m])) /
          (sum(rate(honua_cache_hits_total[5m])) + sum(rate(honua_cache_misses_total[5m])))
```

## Troubleshooting

### No Metrics Visible

1. Check observability is enabled in configuration:
   ```json
   {
     "observability": {
       "metrics": {
         "enabled": true,
         "usePrometheus": true
       }
     }
   }
   ```

2. Verify metrics endpoint is accessible:
   ```bash
   curl http://localhost:5000/metrics
   ```

3. Check Prometheus scrape configuration

### High Cardinality Warnings

If you see high cardinality warnings:

1. Review label values for unbounded dimensions
2. Consider aggregating or bucketing high-cardinality labels
3. Use recording rules to pre-aggregate common queries

### Missing Metrics

If specific metrics aren't appearing:

1. Verify the corresponding feature is enabled
2. Check that metrics services are registered in DI
3. Ensure the meter name is added to OpenTelemetry configuration

## See Also

- [Observability Overview](README.md)
- [Managed Services Guide](managed-services-guide.md)
- [Performance Baselines](performance-baselines.md)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)
