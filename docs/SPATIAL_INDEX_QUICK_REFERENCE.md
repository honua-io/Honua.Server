# Spatial Index Diagnostics - Quick Reference

## Common Commands

### Check All Layers
```bash
honua diagnostics spatial-index
```

### Detailed Report with Statistics
```bash
honua diagnostics spatial-index --verbose
```

### Export to JSON
```bash
honua diagnostics spatial-index --output-json report.json
```

### Specify Custom Server
```bash
honua diagnostics spatial-index --server https://your-server.com
```

## API Endpoints

### All Layers
```bash
curl http://localhost:5000/admin/diagnostics/spatial-indexes
```

### Specific Data Source
```bash
curl http://localhost:5000/admin/diagnostics/spatial-indexes/datasource/production_db
```

### Specific Layer
```bash
curl http://localhost:5000/admin/diagnostics/spatial-indexes/layer/feature_service/parcels_layer
```

## Create Missing Indexes

### PostgreSQL/PostGIS
```sql
-- Basic GIST index
CREATE INDEX {table}_{column}_gist_idx
ON {schema}.{table}
USING GIST ({geometry_column});

-- For large tables (non-blocking)
CREATE INDEX CONCURRENTLY {table}_{column}_gist_idx
ON {schema}.{table}
USING GIST ({geometry_column});
```

### SQL Server
```sql
-- Points
CREATE SPATIAL INDEX {table}_{column}_spatial_idx
ON {schema}.{table} ({geometry_column})
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90),
    GRIDS = (LEVEL_1 = MEDIUM, LEVEL_2 = MEDIUM,
             LEVEL_3 = MEDIUM, LEVEL_4 = MEDIUM),
    CELLS_PER_OBJECT = 16
);

-- Polygons
CREATE SPATIAL INDEX {table}_{column}_spatial_idx
ON {schema}.{table} ({geometry_column})
USING GEOMETRY_GRID
WITH (
    BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90),
    GRIDS = (LEVEL_1 = HIGH, LEVEL_2 = HIGH,
             LEVEL_3 = HIGH, LEVEL_4 = HIGH),
    CELLS_PER_OBJECT = 16
);
```

### MySQL
```sql
ALTER TABLE {schema}.{table}
ADD SPATIAL INDEX {table}_{column}_spatial_idx ({geometry_column});
```

## Rebuild Indexes

### PostgreSQL
```sql
-- Regular rebuild
REINDEX INDEX {index_name};

-- Non-blocking rebuild
REINDEX INDEX CONCURRENTLY {index_name};
```

### SQL Server
```sql
-- Full rebuild
ALTER INDEX {index_name} ON {table} REBUILD;

-- Reorganize (less intensive)
ALTER INDEX {index_name} ON {table} REORGANIZE;
```

### MySQL
```sql
-- Drop and recreate
ALTER TABLE {table} DROP INDEX {index_name};
ALTER TABLE {table} ADD SPATIAL INDEX {index_name} ({column});
```

## Check Index Usage

### PostgreSQL
```sql
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan AS scans,
    idx_tup_read AS tuples_read,
    idx_tup_fetch AS tuples_fetched
FROM pg_stat_user_indexes
WHERE indexname LIKE '%gist%'
ORDER BY idx_scan DESC;
```

### SQL Server
```sql
SELECT
    OBJECT_NAME(i.object_id) AS table_name,
    i.name AS index_name,
    ius.user_seeks + ius.user_scans + ius.user_lookups AS reads,
    ius.user_updates AS writes,
    ps.used_page_count * 8 / 1024.0 AS size_mb
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats ius
    ON i.object_id = ius.object_id AND i.index_id = ius.index_id
LEFT JOIN sys.dm_db_partition_stats ps
    ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.type_desc = 'SPATIAL'
ORDER BY reads DESC;
```

## Automation

### Daily Cron Job
```bash
# Check indexes daily at 2 AM
0 2 * * * /usr/local/bin/honua diagnostics spatial-index --output-json /var/log/honua/spatial-$(date +\%Y\%m\%d).json
```

### CI/CD Integration
```yaml
# .github/workflows/spatial-index-check.yml
- name: Check Spatial Indexes
  run: |
    honua diagnostics spatial-index --output-json index-report.json
    if grep -q '"layersWithoutIndexes": [1-9]' index-report.json; then
      echo "::error::Missing spatial indexes detected"
      exit 1
    fi
```

### Monitor with Prometheus
```python
import requests
from prometheus_client import Gauge, push_to_gateway

response = requests.get('http://localhost:5000/admin/diagnostics/spatial-indexes')
data = response.json()

missing_indexes = Gauge('honua_spatial_indexes_missing', 'Layers missing spatial indexes')
missing_indexes.set(data['layersWithoutIndexes'])

push_to_gateway('localhost:9091', job='honua', registry=registry)
```

## Permissions Required

### PostgreSQL
```sql
GRANT SELECT ON pg_catalog.pg_class TO honua_user;
GRANT SELECT ON pg_catalog.pg_index TO honua_user;
GRANT SELECT ON pg_catalog.pg_namespace TO honua_user;
GRANT SELECT ON pg_catalog.pg_am TO honua_user;
GRANT SELECT ON pg_stat_user_indexes TO honua_user;
```

### SQL Server
```sql
GRANT VIEW DEFINITION TO honua_user;
```

### MySQL
```sql
GRANT SELECT ON INFORMATION_SCHEMA.STATISTICS TO 'honua_user'@'%';
GRANT SELECT ON INFORMATION_SCHEMA.TABLES TO 'honua_user'@'%';
```

## Performance Expectations

| Dataset Size | Query Time Without Index | Query Time With Index | Speedup |
|--------------|--------------------------|----------------------|---------|
| 10K features | 2.5s | 0.15s | **16x** |
| 100K features | 28s | 0.35s | **80x** |
| 1M features | 320s | 0.85s | **376x** |
| 10M features | 3200s | 2.1s | **1523x** |

## Index Size Overhead

- PostgreSQL GIST: ~20-40% of table size
- SQL Server SPATIAL: ~15-35% of table size
- MySQL SPATIAL: ~10-30% of table size

## Index Build Times

| Rows | PostgreSQL | SQL Server | MySQL |
|------|------------|------------|-------|
| 10K | 2s | 3s | 1s |
| 100K | 15s | 25s | 8s |
| 1M | 3m | 5m | 1.5m |
| 10M | 35m | 60m | 20m |

## Common Issues

### Issue: Index Not Valid
```sql
-- PostgreSQL
REINDEX INDEX {index_name};
```

### Issue: Index Disabled (SQL Server)
```sql
ALTER INDEX {index_name} ON {table} REBUILD;
```

### Issue: Low Performance Despite Index
1. Check index is being used: `EXPLAIN ANALYZE SELECT...`
2. Update statistics: `ANALYZE {table}` (PostgreSQL) or `UPDATE STATISTICS {table}` (SQL Server)
3. Check for index bloat: Consider rebuilding
4. Verify appropriate index type (GIST for PostgreSQL, SPATIAL for SQL Server)

## Best Practices

✓ Create indexes **after** bulk data loads
✓ Use `CONCURRENTLY` option for PostgreSQL in production
✓ Monitor index usage regularly
✓ Rebuild indexes quarterly for frequently updated tables
✓ Use tight bounding boxes for SQL Server indexes
✓ Set appropriate grid density (HIGH for polygons, MEDIUM for points)

## More Information

📖 Full documentation: [SPATIAL_INDEX_DIAGNOSTICS.md](./SPATIAL_INDEX_DIAGNOSTICS.md)
📖 Implementation details: [SPATIAL_INDEX_DIAGNOSTICS_IMPLEMENTATION.md](../SPATIAL_INDEX_DIAGNOSTICS_IMPLEMENTATION.md)
📖 Performance guide: [PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md](../PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md)
