# Spatial Index Diagnostics Tool

## Overview

The Spatial Index Diagnostics tool verifies spatial index health across all layers in your Honua Server deployment. It supports PostgreSQL/PostGIS (GIST indexes), SQL Server (spatial indexes), and MySQL/MariaDB (R*Tree indexes).

This tool helps identify performance bottlenecks by:
- Detecting missing spatial indexes on geometry columns
- Checking index health and validity
- Providing usage statistics and size information
- Recommending CREATE INDEX statements for optimal performance
- Estimating performance impact of missing indexes

## Why Spatial Indexes Matter

Spatial indexes are critical for geospatial query performance. According to the [Performance Optimization Opportunities](../PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md) document, proper spatial indexes can provide **10-100x speedup** for spatial queries.

Without spatial indexes, spatial queries require full table scans, which becomes prohibitively slow for datasets with millions of features.

## Usage

### CLI Command

The CLI command provides rich console output with color-coded results and detailed recommendations.

#### Basic Usage

```bash
# Diagnose all layers across all data sources
honua diagnostics spatial-index

# Specify server URL if not configured
honua diagnostics spatial-index --server https://your-server.com

# Show detailed statistics
honua diagnostics spatial-index --verbose

# Output results to JSON file for automation
honua diagnostics spatial-index --output-json spatial-index-report.json
```

#### Command Options

| Option | Description |
|--------|-------------|
| `--server <URL>` | Honua server URL (defaults to configured host) |
| `--output-json <PATH>` | Output diagnostic results to JSON file |
| `--verbose` | Show detailed information including statistics |

#### Example Output

```
Spatial Index Diagnostics

Data Source: production_postgis (postgis)

┌──────────────────┬─────────────┬────────────┬──────────┐
│ Layer            │ Index Status│ Issues     │          │
├──────────────────┼─────────────┼────────────┼──────────┤
│ Parcels          │ ✓           │ None       │          │
│ Buildings        │ ✗           │ No GIST... │          │
│ Roads            │ ✓           │ None       │          │
└──────────────────┴─────────────┴────────────┴──────────┘

Recommendations:
Buildings (buildings_layer):
  CREATE INDEX public_buildings_geom_gist_idx ON public.buildings USING GIST (geom);
  Expected benefit: 10-100x speedup for spatial queries

Summary
Total Layers: 3
With Spatial Indexes: 2
Missing Spatial Indexes: 1
Layers with Issues: 1
```

### Admin HTTP Endpoints

The HTTP endpoints provide programmatic access to diagnostics for integration with monitoring systems and dashboards.

#### Get Diagnostics for All Layers

```http
GET /admin/diagnostics/spatial-indexes
```

**Response:**
```json
{
  "generatedAt": "2025-11-12T10:30:00Z",
  "totalLayers": 15,
  "layersWithIndexes": 13,
  "layersWithoutIndexes": 2,
  "layersWithIssues": 2,
  "results": [
    {
      "dataSourceId": "production_postgis",
      "provider": "postgis",
      "layerId": "parcels_layer",
      "layerTitle": "Property Parcels",
      "tableName": "public.parcels",
      "geometryColumn": "geom",
      "geometryType": "MultiPolygon",
      "hasSpatialIndex": true,
      "indexName": "parcels_geom_gist_idx",
      "indexType": "gist",
      "indexSize": "45 MB",
      "statistics": {
        "num_scans": "15234",
        "tuples_read": "3456789",
        "estimated_rows": "125000",
        "table_size": "512 MB"
      },
      "issues": [],
      "recommendations": []
    },
    {
      "dataSourceId": "production_postgis",
      "provider": "postgis",
      "layerId": "buildings_layer",
      "layerTitle": "Building Footprints",
      "tableName": "public.buildings",
      "geometryColumn": "geom",
      "geometryType": "Polygon",
      "hasSpatialIndex": false,
      "indexName": null,
      "indexType": null,
      "indexSize": null,
      "statistics": {},
      "issues": [
        "No GIST spatial index found on geometry column"
      ],
      "recommendations": [
        "CREATE INDEX public_buildings_geom_gist_idx ON public.buildings USING GIST (geom);",
        "Expected benefit: 10-100x speedup for spatial queries"
      ]
    }
  ]
}
```

#### Get Diagnostics for Specific Data Source

```http
GET /admin/diagnostics/spatial-indexes/datasource/{dataSourceId}
```

**Example:**
```bash
curl https://your-server.com/admin/diagnostics/spatial-indexes/datasource/production_postgis
```

#### Get Diagnostics for Specific Layer

```http
GET /admin/diagnostics/spatial-indexes/layer/{serviceId}/{layerId}
```

**Example:**
```bash
curl https://your-server.com/admin/diagnostics/spatial-indexes/layer/feature_service/parcels_layer
```

## Database-Specific Details

### PostgreSQL/PostGIS

**Index Type:** GIST (Generalized Search Tree)

**Checks Performed:**
- Existence of GIST index on geometry column
- Index validity (`indisvalid`)
- Index readiness (`indisready`)
- Index size and usage statistics
- Number of scans and tuples read

**Recommended Index Creation:**
```sql
CREATE INDEX {schema}_{table}_{column}_gist_idx
ON {schema}.{table}
USING GIST ({geometry_column});
```

**Advanced Options:**
```sql
-- For very large tables, create index concurrently
CREATE INDEX CONCURRENTLY parcels_geom_gist_idx
ON public.parcels
USING GIST (geom);

-- With custom fill factor for frequently updated tables
CREATE INDEX parcels_geom_gist_idx
ON public.parcels
USING GIST (geom)
WITH (fillfactor = 90);
```

**Performance Statistics:**
- `num_scans`: Number of index scans
- `tuples_read`: Total rows returned via index
- `estimated_rows`: Approximate row count
- `table_size`: Total table size including indexes

### SQL Server

**Index Type:** SPATIAL (Geometry Grid or Geography Grid)

**Checks Performed:**
- Existence of spatial index on geometry column
- Index disabled status
- Index size in MB
- Total reads (seeks + scans + lookups)
- Total writes (updates)
- Fill factor

**Recommended Index Creation:**
```sql
-- For point/multipoint geometries
CREATE SPATIAL INDEX {schema}_{table}_{column}_spatial_idx
ON {schema}.{table} ({geometry_column})
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90),
    GRIDS = (LEVEL_1 = MEDIUM, LEVEL_2 = MEDIUM,
             LEVEL_3 = MEDIUM, LEVEL_4 = MEDIUM),
    CELLS_PER_OBJECT = 16
);

-- For polygon/multipolygon geometries (use HIGH density)
CREATE SPATIAL INDEX parcels_geom_spatial_idx
ON dbo.parcels (geom)
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90),
    GRIDS = (LEVEL_1 = HIGH, LEVEL_2 = HIGH,
             LEVEL_3 = HIGH, LEVEL_4 = HIGH),
    CELLS_PER_OBJECT = 16
);
```

**Grid Density Recommendations:**
- **POINT/MULTIPOINT**: MEDIUM grid density
- **POLYGON/MULTIPOLYGON**: HIGH grid density
- **LINESTRING/MULTILINESTRING**: MEDIUM grid density

**Adjusting Bounding Box:**
For better performance with localized data, specify a tighter bounding box:
```sql
-- For data in continental US
CREATE SPATIAL INDEX us_parcels_geom_spatial_idx
ON dbo.parcels (geom)
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=-125, ymin=24, xmax=-66, ymax=49),
    GRIDS = (LEVEL_1 = HIGH, LEVEL_2 = HIGH,
             LEVEL_3 = HIGH, LEVEL_4 = HIGH),
    CELLS_PER_OBJECT = 16
);
```

### MySQL/MariaDB

**Index Type:** SPATIAL (R*Tree)

**Checks Performed:**
- Existence of spatial index on geometry column
- Index cardinality
- Table row count
- Table size

**Recommended Index Creation:**
```sql
ALTER TABLE {schema}.{table}
ADD SPATIAL INDEX {schema}_{table}_{column}_spatial_idx ({geometry_column});
```

**Example:**
```sql
ALTER TABLE gis.parcels
ADD SPATIAL INDEX parcels_geom_spatial_idx (geom);
```

**Note:** MySQL spatial indexes automatically use R*Tree implementation. No additional configuration is needed.

## Interpreting Results

### Index Health Indicators

| Status | Meaning | Action Required |
|--------|---------|-----------------|
| ✓ Indexed | Spatial index exists and is healthy | None |
| ✗ Missing | No spatial index found | Create index using provided SQL |
| ⚠ Invalid | Index exists but is not valid | Rebuild index |
| ⚠ Not Ready | Index is currently building | Wait for completion |
| ⚠ Disabled | Index is disabled (SQL Server) | Rebuild index |

### Performance Statistics Interpretation

#### PostgreSQL
- **High `num_scans` with low `tuples_read`**: Index is being used but may not be selective enough
- **Zero `num_scans`**: Index is not being used - check query patterns or consider dropping
- **Large `index_size` relative to `table_size`**: Normal for spatial indexes (typically 20-40% of table size)

#### SQL Server
- **High `total_reads` with high `total_writes`**: Index is heavily used - ensure it's not disabled
- **Low `fill_factor` (<80)**: May cause excessive page splits on updates - consider increasing

### Common Issues and Resolutions

#### Issue: "Index is not valid - may need rebuild"
**Cause:** Index corruption or incomplete build
**Resolution:**
```sql
-- PostgreSQL
REINDEX INDEX {index_name};

-- SQL Server
ALTER INDEX {index_name} ON {table} REBUILD;
```

#### Issue: "Index is disabled"
**Cause:** Index was manually disabled or rebuild failed
**Resolution:**
```sql
-- SQL Server
ALTER INDEX {index_name} ON {table} REBUILD;
```

#### Issue: "No spatial index found"
**Cause:** Index was never created or was dropped
**Resolution:** Use the provided CREATE INDEX statement from recommendations

## Automation and Monitoring

### Integration with CI/CD

Add spatial index checks to your deployment pipeline:

```yaml
# GitHub Actions example
- name: Check Spatial Indexes
  run: |
    honua diagnostics spatial-index --output-json index-report.json
    # Fail if any indexes are missing
    if grep -q '"layersWithoutIndexes": [1-9]' index-report.json; then
      echo "Missing spatial indexes detected!"
      exit 1
    fi
```

### Prometheus Metrics

Export diagnostics to Prometheus for monitoring:

```python
import requests
import json
from prometheus_client import Gauge, CollectorRegistry, push_to_gateway

# Fetch diagnostics
response = requests.get('https://your-server.com/admin/diagnostics/spatial-indexes')
data = response.json()

# Create metrics
registry = CollectorRegistry()
layers_total = Gauge('honua_spatial_indexes_layers_total',
                     'Total number of layers', registry=registry)
layers_with_indexes = Gauge('honua_spatial_indexes_present',
                             'Layers with spatial indexes', registry=registry)
layers_missing_indexes = Gauge('honua_spatial_indexes_missing',
                                'Layers missing spatial indexes', registry=registry)

layers_total.set(data['totalLayers'])
layers_with_indexes.set(data['layersWithIndexes'])
layers_missing_indexes.set(data['layersWithoutIndexes'])

# Push to Prometheus Pushgateway
push_to_gateway('localhost:9091', job='honua_diagnostics', registry=registry)
```

### Scheduled Checks

Set up cron job for regular diagnostics:

```bash
# Check spatial indexes daily at 2 AM
0 2 * * * /usr/local/bin/honua diagnostics spatial-index --output-json /var/log/honua/spatial-index-$(date +\%Y\%m\%d).json
```

## Best Practices

### 1. Create Indexes During Initial Data Load

Create spatial indexes **after** bulk loading data for optimal performance:

```sql
-- Load data first
COPY parcels FROM '/data/parcels.csv';

-- Then create index
CREATE INDEX parcels_geom_gist_idx ON parcels USING GIST (geom);
```

### 2. Monitor Index Usage

Regularly check index usage to identify unused indexes:

```sql
-- PostgreSQL: Check index usage
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
WHERE indexname LIKE '%gist%'
ORDER BY idx_scan;
```

### 3. Rebuild Indexes Periodically

For frequently updated tables, rebuild indexes quarterly:

```sql
-- PostgreSQL
REINDEX INDEX CONCURRENTLY parcels_geom_gist_idx;

-- SQL Server
ALTER INDEX parcels_geom_spatial_idx ON dbo.parcels REORGANIZE;
```

### 4. Use Appropriate Index Types

- **PostgreSQL**: Always use GIST for spatial data (not B-Tree or Hash)
- **SQL Server**: Use GEOMETRY_GRID for projected data, GEOGRAPHY_GRID for lat/lon
- **MySQL**: SPATIAL indexes automatically use R*Tree

### 5. Optimize Bounding Boxes (SQL Server)

Specify tight bounding boxes for better performance:

```sql
-- Get actual data extent
SELECT
    geometry::EnvelopeAggregate(geom).STAsText()
FROM parcels;

-- Use result to create optimized index
CREATE SPATIAL INDEX parcels_geom_spatial_idx
ON dbo.parcels (geom)
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=<actual_min_x>, ymin=<actual_min_y>,
                    xmax=<actual_max_x>, ymax=<actual_max_y>),
    GRIDS = (LEVEL_1 = HIGH, LEVEL_2 = HIGH,
             LEVEL_3 = HIGH, LEVEL_4 = HIGH),
    CELLS_PER_OBJECT = 16
);
```

## Performance Impact

### Expected Improvements

| Dataset Size | Without Index | With Index | Speedup |
|--------------|---------------|------------|---------|
| 10K features | 2.5s | 0.15s | 16x |
| 100K features | 28s | 0.35s | 80x |
| 1M features | 320s | 0.85s | 376x |
| 10M features | 3200s | 2.1s | 1523x |

*Based on typical spatial intersection queries on PostgreSQL/PostGIS*

### Index Size Overhead

- **PostgreSQL GIST**: Typically 20-40% of table size
- **SQL Server SPATIAL**: Typically 15-35% of table size
- **MySQL SPATIAL**: Typically 10-30% of table size

### Build Time Estimates

| Rows | PostgreSQL | SQL Server | MySQL |
|------|------------|------------|-------|
| 10K | 2s | 3s | 1s |
| 100K | 15s | 25s | 8s |
| 1M | 3m | 5m | 1.5m |
| 10M | 35m | 60m | 20m |

*Times are approximate and vary based on hardware and geometry complexity*

## Troubleshooting

### CLI Command Fails to Connect

**Error:** `Failed to fetch metadata from server`

**Solutions:**
1. Verify server is running: `curl https://your-server.com/health`
2. Check server URL configuration: `honua config init`
3. Ensure network connectivity and firewall rules
4. Verify authentication if required

### Database Connection Errors

**Error:** `Error diagnosing data source: timeout`

**Solutions:**
1. Verify database connection string in metadata
2. Check database server is accessible
3. Ensure connection pooling limits aren't exceeded
4. Verify credentials have SELECT permissions on system tables

### Permission Errors

**PostgreSQL Error:** `permission denied for table pg_indexes`

**Solution:** Grant necessary permissions:
```sql
GRANT SELECT ON pg_catalog.pg_class TO honua_user;
GRANT SELECT ON pg_catalog.pg_index TO honua_user;
GRANT SELECT ON pg_catalog.pg_namespace TO honua_user;
```

**SQL Server Error:** `The SELECT permission was denied on the object 'sys.indexes'`

**Solution:** Grant VIEW DEFINITION:
```sql
GRANT VIEW DEFINITION TO honua_user;
```

## Related Documentation

- [Performance Optimization Opportunities](../PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md)
- [Database Configuration Guide](./DATABASE_CONFIGURATION.md)
- [Performance Tuning Guide](./PERFORMANCE_TUNING.md)

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/honua-server/issues
- Documentation: https://docs.honua.io
- Community: https://community.honua.io
