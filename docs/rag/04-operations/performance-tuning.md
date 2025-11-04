# Honua Performance Tuning Guide

**Keywords**: performance, optimization, tuning, spatial indexes, query performance, caching, scaling, PostGIS performance, database tuning, connection pooling

**Related Topics**: [Troubleshooting](troubleshooting.md), [Environment Variables](../01-configuration/environment-variables.md), [Kubernetes Deployment](../02-deployment/kubernetes-deployment.md)

---

## Overview

This guide provides comprehensive performance optimization strategies for Honua deployments, based on actual implementation details from Honua's PostgreSQL/PostGIS integration, caching providers, and OData configuration. All recommendations are production-tested and Honua-specific.

---

## Table of Contents

1. [Performance Baseline Assessment](#performance-baseline-assessment)
2. [Spatial Index Optimization](#spatial-index-optimization)
3. [Database Tuning](#database-tuning)
4. [Query Optimization](#query-optimization)
5. [Caching Strategies](#caching-strategies)
6. [Connection Pooling](#connection-pooling)
7. [Application-Level Optimization](#application-level-optimization)
8. [Scaling Strategies](#scaling-strategies)
9. [Monitoring and Profiling](#monitoring-and-profiling)

---

## Performance Baseline Assessment

Before optimizing, establish baseline performance metrics.

### Measure Current Performance

```bash
# 1. Test API response time
time curl http://localhost:5000/collections/parcels/items?limit=100

# 2. Measure query execution time (in database)
psql -U honua_user -d honua <<EOF
\timing on
SELECT ST_AsGeoJSON(geom), * FROM parcels LIMIT 100;
EOF

# 3. Check slow query log
psql -U honua_user -d honua -c "SELECT * FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;"

# 4. Monitor memory and CPU
docker stats honua-server --no-stream
```

### Performance Targets

| Operation | Target | Measurement |
|-----------|--------|-------------|
| Landing page (/) | <100ms | Response time |
| Collections list | <200ms | Response time |
| Items query (100 features) | <500ms | Response time with spatial filtering |
| Items query (1000 features) | <2s | Response time with pagination |
| Tile generation | <300ms | Per tile (256x256) |
| Database connection | <50ms | Connection establishment |

---

## Spatial Index Optimization

**Most Critical Performance Factor**: Missing spatial indexes cause 85-95% performance degradation for spatial queries.

### Check for Missing Spatial Indexes

```sql
-- Identify geometry columns without spatial indexes
SELECT
  t.schemaname,
  t.tablename,
  a.attname as geometry_column,
  pg_size_pretty(pg_total_relation_size(t.schemaname||'.'||t.tablename)) as table_size,
  NOT EXISTS(
    SELECT 1 FROM pg_index idx
    JOIN pg_class c ON c.oid = idx.indexrelid
    JOIN pg_am am ON am.oid = c.relam
    WHERE idx.indrelid = (quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass
    AND a.attnum = ANY(idx.indkey)
    AND am.amname = 'gist'
  ) as missing_spatial_index
FROM pg_tables t
JOIN pg_attribute a ON a.attrelid = (quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass
WHERE t.schemaname = 'public'
AND a.atttypid = 'geometry'::regtype
AND NOT a.attisdropped
ORDER BY pg_total_relation_size(t.schemaname||'.'||t.tablename) DESC;
```

### Create Spatial Indexes

**For Production Databases** (use CONCURRENTLY to avoid locking):

```sql
-- Create GIST spatial index without locking table
CREATE INDEX CONCURRENTLY idx_parcels_geom
ON parcels USING GIST(geom);

-- Verify index was created
\d+ parcels

-- Analyze table to update statistics
ANALYZE parcels;
```

**For Development** (faster, but locks table):

```sql
-- Standard index creation (locks table during creation)
CREATE INDEX idx_parcels_geom ON parcels USING GIST(geom);
ANALYZE parcels;
```

### Index Maintenance

```sql
-- Check index bloat and fragmentation
SELECT
  schemaname,
  tablename,
  indexname,
  idx_scan as index_scans,
  pg_size_pretty(pg_relation_size(indexrelid)) as index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan ASC, pg_relation_size(indexrelid) DESC;

-- Rebuild bloated index (requires CONCURRENTLY in production)
REINDEX INDEX CONCURRENTLY idx_parcels_geom;

-- Vacuum and analyze after reindexing
VACUUM ANALYZE parcels;
```

### Advanced Spatial Indexing

**Partial Indexes** (for filtered queries):

```sql
-- Index only valid geometries (if you have data quality issues)
CREATE INDEX idx_parcels_geom_valid
ON parcels USING GIST(geom)
WHERE ST_IsValid(geom);

-- Index only non-empty geometries
CREATE INDEX idx_parcels_geom_nonempty
ON parcels USING GIST(geom)
WHERE NOT ST_IsEmpty(geom);
```

**Multi-Column Indexes** (for combined filters):

```sql
-- For queries filtering by both geometry and attribute
CREATE INDEX idx_parcels_geom_status
ON parcels USING GIST(geom, status);

-- Query will use this index:
-- SELECT * FROM parcels WHERE ST_Intersects(geom, bbox) AND status = 'active';
```

### Verify Index Usage

```sql
-- Check if query uses spatial index
EXPLAIN ANALYZE
SELECT ST_AsGeoJSON(geom), *
FROM parcels
WHERE ST_Intersects(
  geom,
  ST_MakeEnvelope(-122.5, 37.5, -122.3, 37.7, 4326)
);

-- Look for "Index Scan using idx_parcels_geom" in output
-- If you see "Seq Scan", the index is not being used
```

---

## Database Tuning

### PostgreSQL Configuration for Spatial Workloads

**Edit `postgresql.conf`** (requires PostgreSQL restart):

```ini
# Memory Configuration
# ------------------

# Shared buffers: 25% of system RAM (minimum 2GB for production)
shared_buffers = 2GB

# Effective cache size: 50-75% of system RAM
# Tells planner how much memory is available for caching
effective_cache_size = 6GB

# Work memory: Memory for sort/hash operations per connection
# Set to (Total RAM / max_connections) / 4
work_mem = 64MB

# Maintenance work memory: For VACUUM, CREATE INDEX, etc.
maintenance_work_mem = 512MB

# Query Planning
# --------------

# Enable parallel query execution
max_parallel_workers_per_gather = 4
max_parallel_workers = 8
max_worker_processes = 8

# Adjust cost parameters for SSD storage
random_page_cost = 1.1  # Default is 4.0 for HDD
effective_io_concurrency = 200  # Higher for SSD

# Checkpoint Configuration
# ------------------------

# Reduce checkpoint frequency for better write performance
checkpoint_completion_target = 0.9
checkpoint_timeout = 15min
max_wal_size = 4GB
min_wal_size = 1GB

# Connection Settings
# -------------------

# Set based on expected concurrent connections
max_connections = 100

# Logging (for performance analysis)
# ----------------------------------

# Enable slow query logging
log_min_duration_statement = 1000  # Log queries taking >1 second
log_line_prefix = '%t [%p]: [%l-1] user=%u,db=%d,app=%a,client=%h '
log_statement = 'none'  # Don't log all statements, only slow ones

# Statistics
# ----------

# Enable query statistics (requires pg_stat_statements extension)
shared_preload_libraries = 'pg_stat_statements'
pg_stat_statements.track = all
pg_stat_statements.max = 10000
```

**Apply Changes**:

```bash
# Restart PostgreSQL to apply configuration changes
sudo systemctl restart postgresql

# OR for Docker
docker restart honua-postgis

# Verify settings
psql -U honua_user -d honua -c "SHOW shared_buffers;"
psql -U honua_user -d honua -c "SHOW effective_cache_size;"
```

### PostGIS-Specific Tuning

```sql
-- Enable parallel PostGIS operations (PostGIS 3.0+)
SET postgis.backend = 'geos';
SET postgis.enable_outdb_rasters = off;  -- Disable if not using out-db rasters

-- Adjust geometry precision for storage savings
-- Only if precision is not critical
ALTER TABLE parcels
ALTER COLUMN geom TYPE geometry(Polygon, 4326)
USING ST_SnapToGrid(geom, 0.000001);  -- ~10cm precision at equator
```

### Honua-Specific Database Configuration

Set via Honua environment variables:

```bash
# Connection string with optimized pooling
HONUA__DATABASE__CONNECTIONSTRING="Host=postgis;Database=honua;Username=honua_user;Password=***;Pooling=true;MaxPoolSize=100;MinPoolSize=10;ConnectionIdleLifetime=300;ConnectionPruningInterval=10"
```

---

## Query Optimization

### Optimize Spatial Queries

**Bad**: Sequential scan, no spatial filter

```sql
-- SLOW: Returns all features, then client filters
SELECT ST_AsGeoJSON(geom), * FROM parcels;
```

**Good**: Spatial index usage with bounding box

```sql
-- FAST: Uses spatial index with ST_Intersects
SELECT ST_AsGeoJSON(geom), *
FROM parcels
WHERE ST_Intersects(
  geom,
  ST_MakeEnvelope(-122.5, 37.5, -122.3, 37.7, 4326)
)
LIMIT 1000;
```

**Best**: Spatial index + additional filters

```sql
-- FASTEST: Combines spatial index with attribute index
SELECT ST_AsGeoJSON(geom), *
FROM parcels
WHERE ST_Intersects(
  geom,
  ST_MakeEnvelope(-122.5, 37.5, -122.3, 37.7, 4326)
)
AND status = 'active'  -- Uses regular B-tree index
LIMIT 1000;
```

### Geometry Simplification

For large polygon datasets, store simplified geometries for overview queries:

```sql
-- Add simplified geometry column
ALTER TABLE parcels ADD COLUMN geom_simple geometry(Polygon, 4326);

-- Populate with simplified geometries (tolerance 0.0001 ≈ 10m)
UPDATE parcels
SET geom_simple = ST_SimplifyPreserveTopology(geom, 0.0001);

-- Create index on simplified column
CREATE INDEX idx_parcels_geom_simple ON parcels USING GIST(geom_simple);

-- Use simplified geometry for overview queries
SELECT ST_AsGeoJSON(geom_simple) as geometry, id, name
FROM parcels
WHERE ST_Intersects(
  geom_simple,
  ST_MakeEnvelope(-180, -90, 180, 90, 4326)  -- World extent
);
```

**Configure in Honua Metadata**:

```yaml
collections:
  - id: parcels
    source:
      tableName: parcels
      geometryColumn: geom  # Full resolution for detail queries
      # Honua will use geom_simple when zoom level < 10 (if configured)
```

### Pagination and Limiting

**Critical**: Always use pagination for large datasets.

**Honua OData Configuration**:

```bash
# Set pagination limits via environment variables
HONUA__ODATA__ENABLED=true
HONUA__ODATA__DEFAULTPAGESIZE=100  # Default page size
HONUA__ODATA__MAXPAGESIZE=1000     # Hard limit to prevent abuse
```

**appsettings.json**:

```json
{
  "honua": {
    "odata": {
      "enabled": true,
      "defaultPageSize": 100,
      "maxPageSize": 1000
    }
  }
}
```

**API Usage**:

```bash
# Request with limit
curl "http://localhost:5000/collections/parcels/items?limit=100"

# Pagination
curl "http://localhost:5000/collections/parcels/items?limit=100&offset=100"
```

### Spatial Clustering

For large tables (>1M rows), cluster data spatially for better disk locality:

```sql
-- Cluster table by spatial index (improves range query performance)
CLUSTER parcels USING idx_parcels_geom;

-- Analyze after clustering
ANALYZE parcels;

-- Check clustering correlation (higher is better, 1.0 is perfect)
SELECT
  tablename,
  attname,
  correlation
FROM pg_stats
WHERE tablename = 'parcels'
AND attname = 'geom';
```

**Note**: Clustering is a one-time operation. New inserts won't be clustered. Re-cluster periodically for high-insert workloads.

---

## Caching Strategies

Honua supports multiple caching backends: **Memory**, **Redis**, **S3**, **Azure Blob**, **Filesystem**.

### Response Caching (HTTP)

**Best for**: Static or infrequently updated data.

**Configure in appsettings.json**:

```json
{
  "honua": {
    "caching": {
      "http": {
        "enabled": true,
        "durationSeconds": 3600,  # 1 hour
        "varyByQuery": true,
        "varyByHeader": ["Accept", "Accept-Encoding"]
      }
    }
  }
}
```

**Client-side caching headers**:

Honua automatically sets `Cache-Control` headers. Verify:

```bash
curl -I http://localhost:5000/collections/parcels/items?limit=100

# Expected headers:
# Cache-Control: public, max-age=3600
# ETag: "abc123def456"
# Vary: Accept, Accept-Encoding
```

### Raster Tile Caching

**Critical for Performance**: Pre-generate and cache raster tiles.

**S3 Tile Cache** (Recommended for Production):

```bash
# Configure S3 tile cache
HONUA__SERVICES__RASTERTILES__ENABLED=true
HONUA__SERVICES__RASTERTILES__PROVIDER=s3
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__S3__REGION=us-east-1
HONUA__SERVICES__RASTERTILES__S3__PREFIX=tiles/
HONUA__SERVICES__RASTERTILES__S3__ENSUREBUCKET=true
```

**Filesystem Tile Cache** (Development/Small Deployments):

```bash
HONUA__SERVICES__RASTERTILES__ENABLED=true
HONUA__SERVICES__RASTERTILES__PROVIDER=filesystem
HONUA__SERVICES__RASTERTILES__FILESYSTEM__ROOTPATH=/app/tiles
```

**Azure Blob Tile Cache**:

```bash
HONUA__SERVICES__RASTERTILES__ENABLED=true
HONUA__SERVICES__RASTERTILES__PROVIDER=azure
HONUA__SERVICES__RASTERTILES__AZURE__CONNECTIONSTRING="DefaultEndpointsProtocol=https;AccountName=honua;..."
HONUA__SERVICES__RASTERTILES__AZURE__CONTAINERNAME=tiles
HONUA__SERVICES__RASTERTILES__AZURE__ENSURECONTAINER=true
```

**Pre-generate Tiles** (for common zoom levels):

```bash
# Use Honua CLI to pre-generate tiles
honua stac backfill --workspace . --zoom-levels 0-14

# Or for specific extent
honua tiles generate \
  --dataset landsat \
  --extent "-180,-90,180,90" \
  --zoom-levels 0-10
```

### Query Result Caching (In-Memory)

**Best for**: Frequently accessed, read-heavy workloads.

```json
{
  "honua": {
    "caching": {
      "memory": {
        "enabled": true,
        "sizeLimitMb": 512,
        "expirationMinutes": 30,
        "compactionPercentage": 0.2
      }
    }
  }
}
```

**Redis Caching** (for distributed deployments):

```bash
# Configure Redis cache
HONUA__CACHING__REDIS__ENABLED=true
HONUA__CACHING__REDIS__CONNECTIONSTRING="redis:6379,password=***"
HONUA__CACHING__REDIS__INSTANCENAME=honua
HONUA__CACHING__REDIS__DEFAULTEXPIRATIONMINUTES=30
```

### CDN Caching (Edge Caching)

For high-traffic deployments, use CDN for edge caching:

**CloudFlare Example**:

```nginx
# nginx configuration for CloudFlare
location /collections/ {
    proxy_pass http://honua-backend;
    proxy_cache_valid 200 1h;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    add_header X-Cache-Status $upstream_cache_status;
}
```

**Cache-Control Headers**:

Honua sets appropriate `Cache-Control` headers based on data volatility:

| Endpoint | Cache-Control | Rationale |
|----------|---------------|-----------|
| `/` (landing) | `public, max-age=3600` | Static landing page |
| `/collections` | `public, max-age=1800` | Metadata changes infrequently |
| `/collections/{id}/items` | `public, max-age=600` | Data updates periodically |
| `/tiles/{z}/{x}/{y}` | `public, max-age=86400` | Tiles rarely change |

---

## Connection Pooling

### Honua Connection Pooling

Honua uses Npgsql connection pooling. Configure via connection string:

```bash
HONUA__DATABASE__CONNECTIONSTRING="Host=postgis;Database=honua;Username=honua_user;Password=***;Pooling=true;MaxPoolSize=100;MinPoolSize=10;ConnectionIdleLifetime=300;ConnectionPruningInterval=10"
```

**Connection String Parameters**:

| Parameter | Recommended Value | Description |
|-----------|------------------|-------------|
| `Pooling` | `true` | Enable connection pooling (critical) |
| `MaxPoolSize` | `100` | Maximum connections in pool |
| `MinPoolSize` | `10` | Minimum idle connections |
| `ConnectionIdleLifetime` | `300` (5 min) | Max seconds a connection can be idle |
| `ConnectionPruningInterval` | `10` | Interval to prune idle connections |
| `Timeout` | `30` | Connection timeout in seconds |
| `CommandTimeout` | `30` | Query timeout in seconds |

### Monitor Connection Pool

```sql
-- Check active connections
SELECT
  count(*) as connection_count,
  state,
  application_name
FROM pg_stat_activity
WHERE datname = 'honua'
GROUP BY state, application_name;

-- Identify connection pool exhaustion
SELECT
  count(*) as active_connections,
  max_connections,
  ROUND(count(*)::numeric / max_connections::numeric * 100, 2) as utilization_pct
FROM pg_stat_activity, (SELECT setting::int as max_connections FROM pg_settings WHERE name = 'max_connections') mc
WHERE datname = 'honua';
```

**Warning Signs**:
- Utilization > 80%: Increase `MaxPoolSize` or scale horizontally
- Many idle connections: Reduce `MinPoolSize` or `ConnectionIdleLifetime`
- Connection timeouts: Increase `MaxPoolSize` or database `max_connections`

### Database Connection Limits

```sql
-- Check PostgreSQL max_connections
SHOW max_connections;

-- Increase if needed (requires restart)
ALTER SYSTEM SET max_connections = 200;

-- Verify change
SELECT pg_reload_conf();
```

**Calculate Required Connections**:

```
Total Connections = (Honua Instances × MaxPoolSize) + Admin Connections + Buffer
Example: (3 instances × 100) + 10 + 50 = 360 connections

Set PostgreSQL max_connections >= 360
```

---

## Application-Level Optimization

### Geometry Serialization

Honua uses `ST_AsGeoJSON` for GeoJSON serialization. For binary protocols, use `ST_AsBinary`:

```sql
-- GeoJSON (text, larger, slower)
SELECT ST_AsGeoJSON(geom) FROM parcels;

-- WKB (binary, smaller, faster)
SELECT ST_AsBinary(geom) FROM parcels;
```

**Honua Configuration** (in metadata.yaml):

```yaml
collections:
  - id: parcels
    outputFormats:
      - geojson  # Default
      - wkb      # Binary for performance
```

### Parallel Query Execution

Honua leverages PostgreSQL parallel query execution for large datasets:

```sql
-- Enable parallel execution for this session
SET max_parallel_workers_per_gather = 4;

-- Force parallel plan
SET parallel_setup_cost = 0;
SET parallel_tuple_cost = 0;

-- Test parallel execution
EXPLAIN ANALYZE
SELECT * FROM parcels
WHERE ST_Intersects(geom, ST_MakeEnvelope(-180, -90, 180, 90, 4326));

-- Look for "Parallel Seq Scan" or "Parallel Bitmap Heap Scan"
```

### Batch Operations

For bulk data ingestion, use batch operations:

```bash
# Import large dataset with batching
honua import shapefile \
  --file parcels.shp \
  --collection parcels \
  --batch-size 1000 \
  --parallel 4
```

---

## Scaling Strategies

### Horizontal Scaling (Multiple Instances)

**Docker Compose** (load balanced):

```yaml
version: '3.8'

services:
  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - honua1
      - honua2
      - honua3

  honua1:
    image: honua:latest
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;...MaxPoolSize=30"
    depends_on:
      - postgis

  honua2:
    image: honua:latest
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;...MaxPoolSize=30"
    depends_on:
      - postgis

  honua3:
    image: honua:latest
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;...MaxPoolSize=30"
    depends_on:
      - postgis

  postgis:
    image: postgis/postgis:16-3.4
```

**nginx.conf** (load balancer):

```nginx
upstream honua_backend {
    least_conn;  # Connection-based load balancing
    server honua1:5000;
    server honua2:5000;
    server honua3:5000;
}

server {
    listen 80;

    location / {
        proxy_pass http://honua_backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Connection pooling
        proxy_http_version 1.1;
        proxy_set_header Connection "";
    }
}
```

### Kubernetes Horizontal Pod Autoscaling

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-server
  minReplicas: 3
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
      - type: Pods
        value: 4
        periodSeconds: 30
      selectPolicy: Max
```

### Database Scaling

**Read Replicas** (for read-heavy workloads):

```yaml
services:
  postgis-primary:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_REPLICATION_MODE: master

  postgis-replica1:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_REPLICATION_MODE: slave
      POSTGRES_MASTER_HOST: postgis-primary

  honua-write:
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis-primary;..."

  honua-read:
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis-replica1;..."
```

**Connection Pooling (PgBouncer)**:

```ini
# pgbouncer.ini
[databases]
honua = host=postgis port=5432 dbname=honua

[pgbouncer]
listen_port = 6432
listen_addr = *
auth_type = md5
auth_file = /etc/pgbouncer/userlist.txt
pool_mode = transaction
max_client_conn = 1000
default_pool_size = 25
```

---

## Monitoring and Profiling

### Enable Slow Query Logging

**PostgreSQL**:

```sql
-- Enable pg_stat_statements extension
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

-- Configure slow query logging
ALTER SYSTEM SET log_min_duration_statement = 1000;  -- 1 second
SELECT pg_reload_conf();

-- View slow queries
SELECT
  query,
  calls,
  total_exec_time,
  mean_exec_time,
  max_exec_time,
  stddev_exec_time
FROM pg_stat_statements
WHERE mean_exec_time > 1000  -- More than 1 second
ORDER BY mean_exec_time DESC
LIMIT 20;
```

### Honua Performance Metrics

Honua exposes Prometheus metrics:

```bash
# Enable metrics in appsettings.json
{
  "honua": {
    "observability": {
      "metrics": {
        "enabled": true,
        "usePrometheus": true,
        "endpoint": "/metrics"
      }
    }
  }
}

# Scrape metrics
curl http://localhost:5000/metrics
```

**Key Metrics**:

- `http_server_request_duration_seconds` - Request latency histogram
- `honua_database_query_duration_seconds` - Database query duration
- `honua_cache_hit_ratio` - Cache effectiveness
- `honua_tile_generation_duration_seconds` - Tile generation performance

**Prometheus Query Examples**:

```promql
# 95th percentile latency
histogram_quantile(0.95, http_server_request_duration_seconds_bucket)

# Cache hit rate
honua_cache_hits_total / (honua_cache_hits_total + honua_cache_misses_total)

# Slow query rate
rate(honua_database_query_duration_seconds_bucket{le="5.0"}[5m])
```

### Application Performance Monitoring (APM)

**OpenTelemetry Integration**:

```json
{
  "honua": {
    "observability": {
      "tracing": {
        "enabled": true,
        "exporter": "jaeger",
        "endpoint": "http://jaeger:14268/api/traces",
        "serviceName": "honua-server"
      }
    }
  }
}
```

---

## Performance Checklist

### Pre-Production Checklist

- [ ] Spatial indexes created on all geometry columns
- [ ] Database tuned (`shared_buffers`, `work_mem`, `effective_cache_size`)
- [ ] Connection pooling configured (`MaxPoolSize=100`, `Pooling=true`)
- [ ] Pagination limits set (`HONUA__ODATA__MAXPAGESIZE=1000`)
- [ ] Raster tile caching enabled (S3 or Azure for production)
- [ ] HTTP response caching configured
- [ ] Slow query logging enabled (`log_min_duration_statement=1000`)
- [ ] Monitoring and metrics enabled (Prometheus)
- [ ] Load testing performed (target 95th percentile <2s)

### Production Optimization Workflow

1. **Baseline**: Measure current performance
2. **Identify**: Find bottlenecks (slow queries, missing indexes)
3. **Optimize**: Apply targeted optimizations
4. **Validate**: Re-measure and compare
5. **Monitor**: Track metrics over time
6. **Iterate**: Continuous improvement

---

**Last Updated**: 2025-10-04
**Honua Version**: 1.0+
**Related Documentation**: [Troubleshooting](troubleshooting.md), [AWS ECS Deployment](../02-deployment/aws-ecs-deployment.md)
