# Data Providers Configuration Reference

Complete guide to configuring all Honua data providers for spatial database connectivity, metadata caching, and storage management.

**Keywords**: data-providers, postgis, sql-server, sqlite, mysql, redis, connection-string, spatial-database, database-configuration, npgsql, spatialite, connection-pooling, spatial-indexes, geometry-storage, geography-types, srid-configuration, crs-configuration, performance-tuning, database-optimization

## Overview

Honua supports multiple data providers for storing and querying geospatial features. Each provider is optimized for specific use cases and offers different performance characteristics and capabilities.

### Supported Data Providers

- **PostGIS (PostgreSQL)** - Production-ready, full-featured spatial database
- **SQL Server** - Enterprise spatial database with geometry and geography types
- **SQLite/SpatiaLite** - Lightweight, file-based spatial database
- **MySQL** - Open-source spatial database with geometry support
- **Redis** - High-performance metadata caching layer

---

## 1. PostGIS Provider

PostGIS is the recommended provider for production deployments, offering comprehensive spatial functionality, excellent performance, and full OGC compliance.

### Provider Key

```
"provider": "postgis"
```

### Connection String Configuration

#### Basic Connection String

```json
{
  "dataSources": [
    {
      "id": "postgres-primary",
      "provider": "postgis",
      "connectionString": "Host=localhost;Database=honua;Username=honua;Password=secret"
    }
  ]
}
```

#### Advanced Connection String with Pooling

```json
{
  "dataSources": [
    {
      "id": "postgres-production",
      "provider": "postgis",
      "connectionString": "Host=db.example.com;Port=5432;Database=gis_production;Username=honua_app;Password=secure_password;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;Connection Idle Lifetime=300;Connection Pruning Interval=10;Application Name=Honua.Server"
    }
  ]
}
```

#### Environment Variable Configuration

```bash
# .env file
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=honua
POSTGRES_USER=honua
POSTGRES_PASSWORD=secret

# Connection string with environment variable interpolation
"connectionString": "Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
```

### Connection String Parameters

| Parameter | Description | Default | Example |
|-----------|-------------|---------|---------|
| `Host` | PostgreSQL server hostname | - | `localhost`, `db.example.com` |
| `Port` | PostgreSQL server port | `5432` | `5432`, `5433` |
| `Database` | Database name | - | `honua`, `gis_production` |
| `Username` | Database username | - | `honua`, `postgres` |
| `Password` | Database password | - | `secret`, `secure_password` |
| `Pooling` | Enable connection pooling | `true` | `true`, `false` |
| `Minimum Pool Size` | Minimum pool connections | `0` | `5`, `10` |
| `Maximum Pool Size` | Maximum pool connections | `100` | `50`, `200` |
| `Connection Idle Lifetime` | Seconds before idle connections are closed | `300` | `300`, `600` |
| `Connection Pruning Interval` | Seconds between pool cleanup | `10` | `10`, `30` |
| `Application Name` | Application identifier in pg_stat_activity | `Honua.Server` | `Honua.Server` |
| `Timeout` | Connection timeout in seconds | `15` | `30`, `60` |
| `Command Timeout` | Command execution timeout | `30` | `60`, `120` |
| `SSL Mode` | SSL/TLS connection mode | `Prefer` | `Disable`, `Require`, `VerifyFull` |
| `Trust Server Certificate` | Skip certificate validation | `false` | `true`, `false` |

### SRID and CRS Configuration

PostGIS supports full coordinate reference system (CRS) transformations. Configure SRID at the layer level:

```json
{
  "layers": [
    {
      "id": "parcels",
      "storage": {
        "table": "parcels",
        "geometryColumn": "geom",
        "srid": 4326
      },
      "crs": [
        "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        "http://www.opengis.net/def/crs/EPSG/0/3857",
        "http://www.opengis.net/def/crs/EPSG/0/2263"
      ]
    }
  ]
}
```

Common SRIDs:
- `4326` - WGS 84 (latitude/longitude)
- `3857` - Web Mercator (Google Maps, OpenStreetMap)
- `2263` - NAD83 / New York Long Island (US State Plane)
- `32633` - WGS 84 / UTM zone 33N

### Geometry vs Geography Types

PostGIS supports both `geometry` and `geography` types. Honua automatically detects and uses the appropriate type.

**Geometry Type** (Recommended for most use cases):
```sql
CREATE TABLE parcels (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom GEOMETRY(POLYGON, 4326)
);
```

**Geography Type** (For global datasets with accurate distance calculations):
```sql
CREATE TABLE global_locations (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom GEOGRAPHY(POINT, 4326)
);
```

### Spatial Index Requirements

Always create spatial indexes on geometry columns for optimal query performance:

```sql
-- GiST index (recommended)
CREATE INDEX idx_parcels_geom ON parcels USING GIST(geom);

-- BRIN index (for large, naturally ordered datasets)
CREATE INDEX idx_parcels_geom_brin ON parcels USING BRIN(geom);

-- Analyze table for query planning
ANALYZE parcels;
```

### Connection Pooling with Npgsql

Honua uses Npgsql's built-in connection pooling with `NpgsqlDataSource`. The provider automatically:

1. Creates a single `NpgsqlDataSource` per unique connection string
2. Reuses connections from the pool across requests
3. Disposes connections when the application shuts down

**Recommended Pooling Settings for Production:**

```
Pooling=true;
Minimum Pool Size=10;
Maximum Pool Size=100;
Connection Idle Lifetime=600;
Connection Pruning Interval=10
```

### Performance Tuning

#### Database Configuration

```sql
-- Increase shared buffers (25% of RAM)
ALTER SYSTEM SET shared_buffers = '4GB';

-- Increase work memory for spatial operations
ALTER SYSTEM SET work_mem = '64MB';

-- Enable parallel query execution
ALTER SYSTEM SET max_parallel_workers_per_gather = 4;

-- Optimize random page cost for SSD
ALTER SYSTEM SET random_page_cost = 1.1;

-- Reload configuration
SELECT pg_reload_conf();
```

#### Query Optimization

```sql
-- Vacuum and analyze regularly
VACUUM ANALYZE parcels;

-- Check index usage
SELECT schemaname, tablename, indexname, idx_scan
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan;

-- Monitor slow queries
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
WHERE mean_exec_time > 1000
ORDER BY mean_exec_time DESC
LIMIT 10;
```

### Troubleshooting

#### Connection Timeout

```
Error: Npgsql.NpgsqlException: Exception while connecting
```

**Solutions:**
- Verify network connectivity: `telnet db.example.com 5432`
- Check PostgreSQL is running: `systemctl status postgresql`
- Verify `pg_hba.conf` allows connections from your IP
- Increase `Timeout` parameter in connection string

#### Too Many Connections

```
Error: FATAL: sorry, too many clients already
```

**Solutions:**
- Reduce `Maximum Pool Size` in connection string
- Increase PostgreSQL `max_connections`: `ALTER SYSTEM SET max_connections = 200;`
- Check for connection leaks: `SELECT count(*) FROM pg_stat_activity WHERE application_name = 'Honua.Server';`

#### Slow Spatial Queries

```
Queries taking >5 seconds
```

**Solutions:**
- Verify spatial index exists: `\d+ table_name`
- Analyze query plan: `EXPLAIN ANALYZE SELECT * FROM parcels WHERE ST_Intersects(...);`
- Increase `work_mem` for complex spatial operations
- Consider table partitioning for large datasets (>10M rows)

---

## 2. SQL Server Provider

Microsoft SQL Server with spatial extensions provides enterprise-grade geospatial capabilities with both geometry and geography types.

### Provider Key

```
"provider": "sqlserver"
```

### Connection String Configuration

#### Basic Connection String

```json
{
  "dataSources": [
    {
      "id": "sqlserver-primary",
      "provider": "sqlserver",
      "connectionString": "Server=localhost;Database=Honua;User Id=honua_app;Password=secret;TrustServerCertificate=True"
    }
  ]
}
```

#### Windows Authentication

```json
{
  "dataSources": [
    {
      "id": "sqlserver-windows",
      "provider": "sqlserver",
      "connectionString": "Server=SQL-SERVER-01;Database=GIS;Integrated Security=True;TrustServerCertificate=True"
    }
  ]
}
```

#### Azure SQL Database

```json
{
  "dataSources": [
    {
      "id": "azure-sql",
      "provider": "sqlserver",
      "connectionString": "Server=tcp:honua-server.database.windows.net,1433;Database=honua-db;User Id=honua_admin@honua-server;Password=secure_password;Encrypt=True;Connection Timeout=30;Application Name=Honua.Server"
    }
  ]
}
```

### Connection String Parameters

| Parameter | Description | Default | Example |
|-----------|-------------|---------|---------|
| `Server` | SQL Server hostname/IP | - | `localhost`, `tcp:server.database.windows.net,1433` |
| `Database` | Database name | - | `Honua`, `GIS` |
| `User Id` | SQL Server username | - | `honua_app`, `sa` |
| `Password` | SQL Server password | - | `secret` |
| `Integrated Security` | Use Windows authentication | `false` | `true`, `false` |
| `TrustServerCertificate` | Skip certificate validation | `false` | `true`, `false` |
| `Encrypt` | Encrypt connection | `false` | `true`, `false` |
| `Connection Timeout` | Connection timeout in seconds | `15` | `30`, `60` |
| `Application Name` | Application identifier | `Honua.Server` | `Honua.Server` |
| `MultipleActiveResultSets` | Enable MARS | `false` | `true`, `false` |
| `Max Pool Size` | Maximum pool connections | `100` | `50`, `200` |
| `Min Pool Size` | Minimum pool connections | `0` | `5`, `10` |

### Geometry vs Geography Types

SQL Server supports both `geometry` (planar) and `geography` (geodetic) spatial types. Honua automatically detects the column type.

**Geometry Type** (Planar coordinate system):
```sql
CREATE TABLE Parcels (
    Id INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(255),
    Geom GEOMETRY
);

-- Specify SRID
CREATE TABLE Zones (
    Id INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(255),
    Geom GEOMETRY
);

-- Insert with SRID 4326
INSERT INTO Zones (Name, Geom)
VALUES ('Zone A', geometry::STGeomFromText('POLYGON((...))', 4326));
```

**Geography Type** (Ellipsoidal coordinate system for global data):
```sql
CREATE TABLE GlobalLocations (
    Id INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(255),
    Geom GEOGRAPHY
);

-- Insert geography data
INSERT INTO GlobalLocations (Name, Geom)
VALUES ('Location A', geography::STGeomFromText('POINT(-122.34 47.61)', 4326));
```

### Spatial Indexes

Create spatial indexes for optimal performance:

```sql
-- Geometry index with custom bounding box
CREATE SPATIAL INDEX SIndx_Parcels_Geom
ON Parcels(Geom)
WITH (
    BOUNDING_BOX = (xmin=-180, ymin=-90, xmax=180, ymax=90),
    GRIDS = (LEVEL_1 = MEDIUM, LEVEL_2 = MEDIUM, LEVEL_3 = MEDIUM, LEVEL_4 = MEDIUM),
    CELLS_PER_OBJECT = 16
);

-- Geography index (automatic bounding box)
CREATE SPATIAL INDEX SIndx_GlobalLocations_Geom
ON GlobalLocations(Geom)
WITH (
    GRIDS = (LEVEL_1 = HIGH, LEVEL_2 = HIGH, LEVEL_3 = HIGH, LEVEL_4 = HIGH),
    CELLS_PER_OBJECT = 16
);

-- Update statistics
UPDATE STATISTICS Parcels;
```

### SRID and CRS Configuration

SQL Server has limited CRS transformation support compared to PostGIS. Configure SRID at the layer level:

```json
{
  "layers": [
    {
      "id": "parcels",
      "storage": {
        "table": "dbo.Parcels",
        "geometryColumn": "Geom",
        "srid": 4326
      }
    }
  ]
}
```

**Note**: SQL Server's CRS support is limited. For complex transformations, consider using PostGIS or client-side transformation libraries.

### Azure SQL Database Considerations

#### Connection Resiliency

```json
{
  "connectionString": "Server=tcp:honua-server.database.windows.net,1433;Database=honua-db;User Id=honua_admin@honua-server;Password=secure_password;Encrypt=True;Connection Timeout=30;ConnectRetryCount=3;ConnectRetryInterval=10"
}
```

#### Firewall Rules

Ensure Azure SQL firewall allows your application's IP:

```bash
# Using Azure CLI
az sql server firewall-rule create \
  --resource-group honua-rg \
  --server honua-server \
  --name AllowHonuaApp \
  --start-ip-address 203.0.113.10 \
  --end-ip-address 203.0.113.10
```

#### Performance Tiers

Choose appropriate Azure SQL tier:
- **Basic**: Development/testing (5 DTU, 2GB)
- **Standard S2**: Small production (50 DTU, 250GB)
- **Premium P2**: Medium production (250 DTU, 500GB)
- **Hyperscale**: Large production (auto-scaling, up to 100TB)

### Performance Optimization

#### Database Configuration

```sql
-- Update compatibility level
ALTER DATABASE Honua SET COMPATIBILITY_LEVEL = 160; -- SQL Server 2022

-- Enable query store
ALTER DATABASE Honua SET QUERY_STORE = ON;

-- Set recovery model (SIMPLE for non-critical data)
ALTER DATABASE Honua SET RECOVERY SIMPLE;
```

#### Index Maintenance

```sql
-- Rebuild fragmented indexes
ALTER INDEX ALL ON Parcels REBUILD;

-- Update statistics with full scan
UPDATE STATISTICS Parcels WITH FULLSCAN;

-- Check index fragmentation
SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    ps.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'DETAILED') ps
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE ps.avg_fragmentation_in_percent > 30
ORDER BY ps.avg_fragmentation_in_percent DESC;
```

### Common Issues

#### Geometry Type Mismatch

```
Error: The specified input does not represent a valid geography instance
```

**Solution**: Ensure geometry data is valid and SRID matches:

```sql
-- Check if geometry is valid
SELECT Id, Geom.STIsValid() AS IsValid
FROM Parcels
WHERE Geom.STIsValid() = 0;

-- Fix invalid geometries
UPDATE Parcels
SET Geom = Geom.MakeValid()
WHERE Geom.STIsValid() = 0;
```

#### Spatial Index Not Used

**Solution**: Check execution plan and ensure statistics are up to date:

```sql
-- Show execution plan
SET SHOWPLAN_XML ON;
GO
SELECT * FROM Parcels WHERE Geom.STIntersects(geometry::STGeomFromText('POLYGON(...)', 4326)) = 1;
GO
SET SHOWPLAN_XML OFF;
```

---

## 3. SQLite Provider

SQLite with SpatiaLite extension provides a lightweight, file-based spatial database ideal for development, testing, and embedded scenarios.

### Provider Key

```
"provider": "sqlite"
```

### Connection String Configuration

#### Basic File Path

```json
{
  "dataSources": [
    {
      "id": "sqlite-primary",
      "provider": "sqlite",
      "connectionString": "Data Source=./data/honua.db"
    }
  ]
}
```

#### Advanced Configuration

```json
{
  "dataSources": [
    {
      "id": "sqlite-advanced",
      "provider": "sqlite",
      "connectionString": "Data Source=/var/lib/honua/gis.db;Cache=Shared;Mode=ReadWriteCreate;Foreign Keys=True;Default Timeout=30"
    }
  ]
}
```

#### In-Memory Database (Testing Only)

```json
{
  "dataSources": [
    {
      "id": "sqlite-memory",
      "provider": "sqlite",
      "connectionString": "Data Source=:memory:;Mode=Memory;Cache=Shared"
    }
  ]
}
```

### Connection String Parameters

| Parameter | Description | Default | Example |
|-----------|-------------|---------|---------|
| `Data Source` | Database file path | - | `./data/honua.db`, `/var/lib/honua/gis.db`, `:memory:` |
| `Mode` | File open mode | `ReadWriteCreate` | `ReadOnly`, `ReadWrite`, `ReadWriteCreate`, `Memory` |
| `Cache` | Cache mode | `Default` | `Private`, `Shared` |
| `Foreign Keys` | Enable foreign key constraints | `false` | `true`, `false` |
| `Pooling` | Enable connection pooling | `false` | `true`, `false` |
| `Default Timeout` | Command timeout in seconds | `30` | `30`, `60` |

### SpatiaLite Extension

SpatiaLite must be initialized for spatial functionality:

```sql
-- Load SpatiaLite extension (if not auto-loaded)
SELECT load_extension('mod_spatialite');

-- Initialize spatial metadata (first time only)
SELECT InitSpatialMetadata(1);

-- Create table with geometry
CREATE TABLE parcels (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT,
    geom GEOMETRY
);

-- Add geometry column
SELECT AddGeometryColumn('parcels', 'geom', 4326, 'POLYGON', 'XY');

-- Create spatial index
SELECT CreateSpatialIndex('parcels', 'geom');
```

### Geometry Storage

SQLite stores geometries as BLOB in WKT or WKB format. SpatiaLite provides conversion functions:

```sql
-- Insert geometry from GeoJSON
INSERT INTO parcels (name, geom)
VALUES ('Parcel A', GeomFromGeoJSON('{"type":"Polygon","coordinates":[...]}'));

-- Insert geometry from WKT
INSERT INTO parcels (name, geom)
VALUES ('Parcel B', GeomFromText('POLYGON((...))', 4326));

-- Query geometry as GeoJSON
SELECT id, name, AsGeoJSON(geom) AS geometry
FROM parcels;
```

### Performance Considerations

#### Database Optimization

```sql
-- Enable Write-Ahead Logging (WAL) mode for better concurrency
PRAGMA journal_mode = WAL;

-- Increase cache size (in pages, 1 page = ~4KB)
PRAGMA cache_size = -64000; -- 64MB cache

-- Set synchronous mode for performance
PRAGMA synchronous = NORMAL; -- Or OFF for non-critical data

-- Enable memory-mapped I/O
PRAGMA mmap_size = 268435456; -- 256MB

-- Analyze database for query optimization
ANALYZE;

-- Vacuum to reclaim space
VACUUM;
```

#### Spatial Index Usage

```sql
-- Verify spatial index exists
SELECT * FROM geometry_columns WHERE f_table_name = 'parcels';

-- Force spatial index usage with R-Tree
SELECT p.*
FROM parcels p
WHERE p.rowid IN (
    SELECT pkid FROM idx_parcels_geom
    WHERE xmin <= 180 AND xmax >= -180
      AND ymin <= 90 AND ymax >= -90
);
```

### Use Cases and Limitations

**Best Use Cases:**
- ✅ Development and testing environments
- ✅ Embedded applications
- ✅ Small to medium datasets (<100K features)
- ✅ Single-user applications
- ✅ Prototyping and demos
- ✅ Offline field data collection

**Limitations:**
- ❌ Limited concurrent write support (WAL helps)
- ❌ No native network access (file-based only)
- ❌ Performance degrades with large datasets (>1M features)
- ❌ No user/role-based security
- ❌ Limited horizontal scaling capabilities

**Maximum Recommended Dataset Sizes:**
- **Small**: <10K features - Excellent performance
- **Medium**: 10K-100K features - Good performance
- **Large**: 100K-1M features - Acceptable with optimization
- **Very Large**: >1M features - Consider PostGIS instead

### Backup Strategies

#### Simple File Backup

```bash
# Stop application
systemctl stop honua-server

# Copy database file
cp /var/lib/honua/gis.db /backup/gis.db.$(date +%Y%m%d)

# Start application
systemctl start honua-server
```

#### Online Backup (SQLite 3.27.0+)

```sql
-- Using VACUUM INTO (hot backup)
VACUUM INTO '/backup/gis.db.backup';
```

#### Automated Backup Script

```bash
#!/bin/bash
# backup-sqlite.sh

DB_PATH="/var/lib/honua/gis.db"
BACKUP_DIR="/backup/honua"
RETENTION_DAYS=30

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Perform backup
BACKUP_FILE="$BACKUP_DIR/gis.db.$(date +%Y%m%d_%H%M%S)"
sqlite3 "$DB_PATH" "VACUUM INTO '$BACKUP_FILE'"

# Compress backup
gzip "$BACKUP_FILE"

# Remove old backups
find "$BACKUP_DIR" -name "gis.db.*.gz" -mtime +$RETENTION_DAYS -delete

echo "Backup completed: ${BACKUP_FILE}.gz"
```

---

## 4. MySQL Provider

MySQL 8.0+ with spatial extensions provides open-source geospatial capabilities with full geometry support.

### Provider Key

```
"provider": "mysql"
```

### Connection String Configuration

#### Basic Connection String

```json
{
  "dataSources": [
    {
      "id": "mysql-primary",
      "provider": "mysql",
      "connectionString": "Server=localhost;Database=honua;User=honua_app;Password=secret"
    }
  ]
}
```

#### Advanced Configuration with Pooling

```json
{
  "dataSources": [
    {
      "id": "mysql-production",
      "provider": "mysql",
      "connectionString": "Server=db.example.com;Port=3306;Database=gis_production;User=honua_app;Password=secure_password;Pooling=true;MinimumPoolSize=5;MaximumPoolSize=100;ConnectionIdleTimeout=300;AllowUserVariables=true;CharSet=utf8mb4;SslMode=Required"
    }
  ]
}
```

### Connection String Parameters

| Parameter | Description | Default | Example |
|-----------|-------------|---------|---------|
| `Server` | MySQL server hostname | - | `localhost`, `db.example.com` |
| `Port` | MySQL server port | `3306` | `3306`, `3307` |
| `Database` | Database name | - | `honua`, `gis_production` |
| `User` | MySQL username | - | `honua_app`, `root` |
| `Password` | MySQL password | - | `secret` |
| `Pooling` | Enable connection pooling | `true` | `true`, `false` |
| `MinimumPoolSize` | Minimum pool connections | `0` | `5`, `10` |
| `MaximumPoolSize` | Maximum pool connections | `100` | `50`, `200` |
| `ConnectionIdleTimeout` | Seconds before idle connections close | `180` | `300`, `600` |
| `AllowUserVariables` | Allow user variables | `false` | `true` |
| `CharSet` | Character set | `utf8mb4` | `utf8mb4`, `latin1` |
| `SslMode` | SSL connection mode | `Preferred` | `None`, `Required`, `VerifyCA`, `VerifyFull` |
| `ConnectionTimeout` | Connection timeout in seconds | `15` | `30`, `60` |

### Spatial Data Types and Indexes

MySQL supports various geometry types with spatial indexing:

```sql
-- Create table with geometry column
CREATE TABLE parcels (
    id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(255),
    geom GEOMETRY NOT NULL,
    SPATIAL INDEX idx_geom (geom)
) ENGINE=InnoDB;

-- Specific geometry types
CREATE TABLE points_of_interest (
    id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(255),
    location POINT NOT NULL SRID 4326,
    SPATIAL INDEX idx_location (location)
) ENGINE=InnoDB;

CREATE TABLE boundaries (
    id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(255),
    boundary POLYGON NOT NULL SRID 4326,
    SPATIAL INDEX idx_boundary (boundary)
) ENGINE=InnoDB;
```

### SRID Configuration (MySQL 8.0+)

MySQL 8.0 introduced SRID support for spatial reference systems:

```sql
-- Create table with SRID constraint
CREATE TABLE locations (
    id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(255),
    geom GEOMETRY NOT NULL SRID 4326,
    SPATIAL INDEX idx_geom (geom)
);

-- Insert with SRID
INSERT INTO locations (name, geom)
VALUES ('Location A', ST_GeomFromGeoJSON('{"type":"Point","coordinates":[-122.34,47.61]}', 1, 4326));

-- Transform between SRIDs (MySQL 8.0.13+)
SELECT ST_Transform(geom, 3857) FROM locations;
```

Common MySQL SRIDs:
- `4326` - WGS 84
- `3857` - Web Mercator
- `0` - Cartesian (no SRID)

### Performance Tuning

#### MySQL Configuration

```ini
# my.cnf or my.ini

[mysqld]
# InnoDB buffer pool (50-70% of RAM)
innodb_buffer_pool_size = 4G

# InnoDB log file size
innodb_log_file_size = 512M

# Query cache (deprecated in MySQL 8.0, use ProxySQL instead)
# query_cache_type = 1
# query_cache_size = 256M

# Maximum connections
max_connections = 200

# Thread cache
thread_cache_size = 16

# Table cache
table_open_cache = 4000

# Optimize for spatial queries
max_seeks_for_key = 1000
```

#### Index Optimization

```sql
-- Create spatial index
CREATE SPATIAL INDEX idx_parcels_geom ON parcels(geom);

-- Analyze table
ANALYZE TABLE parcels;

-- Check index usage
EXPLAIN SELECT * FROM parcels WHERE ST_Intersects(geom, ST_GeomFromText('POLYGON(...)', 4326));

-- Optimize table
OPTIMIZE TABLE parcels;
```

### Common Issues

#### SRID Mismatch

```
Error: A parameter of a spatial function has a wrong SRID value
```

**Solution**: Ensure all geometries use the same SRID:

```sql
-- Check SRID
SELECT id, ST_SRID(geom) FROM parcels;

-- Set SRID
UPDATE parcels SET geom = ST_GeomFromWKB(ST_AsBinary(geom), 4326);
```

#### Spatial Index Not Used

**Solution**: Force index usage:

```sql
-- Use index hint
SELECT * FROM parcels FORCE INDEX (idx_geom)
WHERE MBRIntersects(geom, ST_GeomFromText('POLYGON(...)', 4326));
```

---

## 5. Redis Metadata Provider

Redis provides high-performance distributed caching for metadata, reducing database load and improving response times.

### Configuration

Redis is configured in the Honua server application settings:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false,connectRetry=3,connectTimeout=5000",
    "InstanceName": "Honua:"
  }
}
```

### Connection String Format

```
[host]:[port],[options]
```

#### Basic Connection

```
localhost:6379
```

#### Advanced Connection with Options

```
redis.example.com:6379,password=secret,abortConnect=false,connectRetry=3,connectTimeout=5000,syncTimeout=5000,defaultDatabase=0,ssl=true
```

### Connection String Options

| Option | Description | Default | Example |
|--------|-------------|---------|---------|
| `password` | Redis password (AUTH) | - | `password=secret` |
| `ssl` | Use SSL/TLS encryption | `false` | `ssl=true` |
| `abortConnect` | Abort on connection failure | `true` | `abortConnect=false` |
| `connectRetry` | Connection retry attempts | `3` | `connectRetry=5` |
| `connectTimeout` | Connection timeout (ms) | `5000` | `connectTimeout=10000` |
| `syncTimeout` | Synchronous operation timeout (ms) | `5000` | `syncTimeout=10000` |
| `defaultDatabase` | Default database index | `0` | `defaultDatabase=1` |
| `keepAlive` | TCP keepalive (seconds) | `-1` | `keepAlive=60` |

### Metadata Caching Strategies

Honua uses Redis to cache frequently accessed metadata:

#### Cache Profiles

```csharp
// Collections list: 5 minutes
Duration = 300

// Collection metadata: 10 minutes
Duration = 600

// Conformance classes: 1 hour
Duration = 3600

// Feature items: 1 minute
Duration = 60
```

#### Cache Keys

```
Honua:OgcCollections
Honua:OgcCollectionMetadata:{collectionId}
Honua:OgcConformance
Honua:OgcItems:{collectionId}:{queryHash}
```

### TTL and Expiration Policies

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "Honua:",
    "DefaultExpiration": "00:10:00",
    "Profiles": {
      "Collections": {
        "Expiration": "00:05:00"
      },
      "Metadata": {
        "Expiration": "00:10:00"
      },
      "Conformance": {
        "Expiration": "01:00:00"
      },
      "Items": {
        "Expiration": "00:01:00",
        "SlidingExpiration": true
      }
    }
  }
}
```

### Cluster Configuration

#### Redis Cluster Connection

```
redis-node1:6379,redis-node2:6379,redis-node3:6379,password=secret,abortConnect=false
```

#### Sentinel Configuration

```
sentinel1:26379,sentinel2:26379,sentinel3:26379,serviceName=honua-master,password=secret
```

### Failover and High Availability

#### Automatic Failover with Sentinel

```bash
# Start Redis Sentinel
redis-sentinel /etc/redis/sentinel.conf
```

**sentinel.conf:**
```
sentinel monitor honua-master redis-master 6379 2
sentinel down-after-milliseconds honua-master 5000
sentinel parallel-syncs honua-master 1
sentinel failover-timeout honua-master 10000
```

#### Connection String with Sentinel

```
sentinel1:26379,sentinel2:26379,sentinel3:26379,serviceName=honua-master,password=secret,abortConnect=false,connectRetry=5
```

### Performance Benefits

**Without Redis Caching:**
- Collections list: ~100ms (database query)
- Metadata fetch: ~50ms per collection
- Total for 20 collections: ~1100ms

**With Redis Caching:**
- Collections list: ~5ms (Redis GET)
- Metadata fetch: ~2ms per collection
- Total for 20 collections: ~45ms

**Performance Improvement: ~24x faster**

### Monitoring and Management

#### Redis CLI Commands

```bash
# Connect to Redis
redis-cli

# Check memory usage
INFO memory

# View all Honua keys
KEYS Honua:*

# Check key TTL
TTL Honua:OgcCollections

# Clear all Honua cache
EVAL "return redis.call('del', unpack(redis.call('keys', 'Honua:*')))" 0

# Monitor real-time commands
MONITOR
```

#### Memory Management

```
# Set max memory (in redis.conf)
maxmemory 2gb

# Eviction policy
maxmemory-policy allkeys-lru
```

---

## 6. Provider Comparison

### Feature Matrix

| Feature | PostGIS | SQL Server | SQLite | MySQL | Redis |
|---------|---------|------------|--------|-------|-------|
| **Native Geometry** | ✅ Yes | ✅ Yes | ✅ Yes (SpatiaLite) | ✅ Yes | ❌ N/A |
| **Native MVT** | ✅ Yes | ❌ No | ❌ No | ❌ No | ❌ N/A |
| **Transactions** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| **Spatial Indexes** | ✅ GiST, BRIN | ✅ Spatial | ✅ R-Tree | ✅ Spatial | ❌ N/A |
| **Geometry Operations** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ❌ N/A |
| **CRS Transformations** | ✅ Full | ⚠️ Limited | ✅ Full | ✅ Yes (8.0+) | ❌ N/A |
| **Max Query Parameters** | 32,767 | 2,100 | 32,766 | 65,535 | N/A |
| **RETURNING Clause** | ✅ Yes | ✅ OUTPUT | ✅ Yes (3.35+) | ❌ No | ❌ N/A |
| **Connection Pooling** | ✅ NpgsqlDataSource | ✅ Built-in | ⚠️ Limited | ✅ MySqlDataSource | ✅ Multiplexer |
| **Concurrent Writes** | ✅ Excellent | ✅ Excellent | ⚠️ Limited (WAL) | ✅ Good | ✅ Excellent |
| **Horizontal Scaling** | ✅ Yes (Citus) | ✅ Yes (Always On) | ❌ No | ✅ Yes (Group Replication) | ✅ Yes (Cluster) |

### Performance Characteristics

#### Query Performance (Spatial Intersection, 1M Features)

| Provider | Cold Cache | Warm Cache | Notes |
|----------|-----------|------------|-------|
| **PostGIS** | 150ms | 50ms | Excellent with GiST index |
| **SQL Server** | 200ms | 80ms | Good with spatial index |
| **SQLite** | 300ms | 100ms | Acceptable for small datasets |
| **MySQL** | 180ms | 70ms | Good with spatial index |
| **Redis** | N/A | 5ms | Metadata caching only |

#### Write Performance (1000 Inserts/sec)

| Provider | Single Transaction | Bulk Insert | Notes |
|----------|-------------------|-------------|-------|
| **PostGIS** | 800/sec | 5000/sec | Best for bulk operations |
| **SQL Server** | 700/sec | 4000/sec | Good with table partitioning |
| **SQLite** | 200/sec | 1000/sec | Limited by single-writer design |
| **MySQL** | 600/sec | 3500/sec | Good with InnoDB |

### When to Use Each Provider

#### PostGIS (Recommended for Production)

**Use When:**
- ✅ Production deployments requiring high reliability
- ✅ Large datasets (>1M features)
- ✅ Complex spatial operations and CRS transformations
- ✅ High concurrent read/write workloads
- ✅ Need for native MVT tile generation
- ✅ OGC compliance required

**Don't Use When:**
- ❌ Embedded/offline scenarios
- ❌ Minimal infrastructure (consider SQLite)

#### SQL Server

**Use When:**
- ✅ Existing Microsoft infrastructure
- ✅ Windows-based environments
- ✅ Enterprise support requirements
- ✅ Integration with other SQL Server databases
- ✅ Azure SQL Database deployment

**Don't Use When:**
- ❌ Need extensive CRS transformation support
- ❌ Budget constraints (licensing costs)

#### SQLite/SpatiaLite

**Use When:**
- ✅ Development and testing
- ✅ Embedded applications
- ✅ Small datasets (<100K features)
- ✅ Single-user scenarios
- ✅ Offline field data collection
- ✅ Prototyping and demos

**Don't Use When:**
- ❌ High concurrent write requirements
- ❌ Large datasets (>1M features)
- ❌ Production multi-user environments

#### MySQL

**Use When:**
- ✅ Existing MySQL infrastructure
- ✅ Open-source preference
- ✅ Medium-sized datasets
- ✅ Cost-sensitive deployments

**Don't Use When:**
- ❌ Need extensive spatial function support (PostGIS is better)
- ❌ Native MVT generation required

#### Redis

**Use When:**
- ✅ High-performance metadata caching needed
- ✅ Reducing database load
- ✅ Improving API response times
- ✅ Distributed caching requirements

**Always Use With:** Another primary data provider (PostGIS, SQL Server, etc.)

### Migration Between Providers

#### PostGIS to SQL Server

```sql
-- Export from PostGIS as WKT
COPY (
    SELECT id, name, ST_AsText(geom) AS geom_wkt
    FROM parcels
) TO '/tmp/parcels.csv' CSV HEADER;

-- Import to SQL Server
BULK INSERT Parcels
FROM '/tmp/parcels.csv'
WITH (FIRSTROW = 2, FIELDTERMINATOR = ',', ROWTERMINATOR = '\n');

-- Convert WKT to geometry
UPDATE Parcels
SET Geom = geometry::STGeomFromText(geom_wkt, 4326);
```

#### SQL Server to PostGIS

```sql
-- Export from SQL Server
SELECT id, name, Geom.STAsText() AS geom_wkt
INTO OUTFILE '/tmp/parcels.csv'
FROM Parcels;

-- Import to PostGIS
COPY parcels (id, name, geom_wkt)
FROM '/tmp/parcels.csv' CSV HEADER;

-- Convert WKT to geometry
UPDATE parcels
SET geom = ST_GeomFromText(geom_wkt, 4326);
```

---

## 7. Configuration Examples

### Environment Variables for Each Provider

#### PostGIS

```bash
# .env
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=honua
POSTGRES_USER=honua_app
POSTGRES_PASSWORD=secret
POSTGRES_POOL_MIN=10
POSTGRES_POOL_MAX=100
POSTGRES_SSL_MODE=Require

# Connection string template
"connectionString": "Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Minimum Pool Size=${POSTGRES_POOL_MIN};Maximum Pool Size=${POSTGRES_POOL_MAX};SSL Mode=${POSTGRES_SSL_MODE}"
```

#### SQL Server

```bash
# .env
SQLSERVER_HOST=localhost
SQLSERVER_DB=Honua
SQLSERVER_USER=honua_app
SQLSERVER_PASSWORD=secret
SQLSERVER_ENCRYPT=True

# Connection string template
"connectionString": "Server=${SQLSERVER_HOST};Database=${SQLSERVER_DB};User Id=${SQLSERVER_USER};Password=${SQLSERVER_PASSWORD};Encrypt=${SQLSERVER_ENCRYPT};TrustServerCertificate=True"
```

#### SQLite

```bash
# .env
SQLITE_DB_PATH=/var/lib/honua/gis.db

# Connection string template
"connectionString": "Data Source=${SQLITE_DB_PATH}"
```

#### MySQL

```bash
# .env
MYSQL_HOST=localhost
MYSQL_PORT=3306
MYSQL_DB=honua
MYSQL_USER=honua_app
MYSQL_PASSWORD=secret
MYSQL_POOL_MIN=5
MYSQL_POOL_MAX=100

# Connection string template
"connectionString": "Server=${MYSQL_HOST};Port=${MYSQL_PORT};Database=${MYSQL_DB};User=${MYSQL_USER};Password=${MYSQL_PASSWORD};MinimumPoolSize=${MYSQL_POOL_MIN};MaximumPoolSize=${MYSQL_POOL_MAX}"
```

#### Redis

```bash
# .env
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_PASSWORD=secret
REDIS_SSL=false

# Connection string template
"connectionString": "${REDIS_HOST}:${REDIS_PORT},password=${REDIS_PASSWORD},ssl=${REDIS_SSL},abortConnect=false"
```

### Docker Compose Examples

#### PostGIS with Redis

```yaml
version: '3.9'

services:
  postgres:
    image: postgis/postgis:16-3.4
    container_name: honua-postgres
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua_password
      POSTGRES_DB: honua
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: honua-redis
    command: redis-server --appendonly yes --requirepass honua_redis_password
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  honua:
    build: .
    container_name: honua-server
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=honua;Username=honua;Password=honua_password;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100"
      Redis__ConnectionString: "redis:6379,password=honua_redis_password,abortConnect=false"
      Redis__InstanceName: "Honua:"
    ports:
      - "8080:8080"

volumes:
  postgres-data:
  redis-data:
```

#### SQL Server

```yaml
version: '3.9'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: honua-sqlserver
    environment:
      ACCEPT_EULA: Y
      SA_PASSWORD: "YourStrong!Passw0rd"
      MSSQL_PID: Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql

  honua:
    build: .
    container_name: honua-server
    depends_on:
      - sqlserver
    environment:
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=Honua;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
    ports:
      - "8080:8080"

volumes:
  sqlserver-data:
```

#### MySQL

```yaml
version: '3.9'

services:
  mysql:
    image: mysql:8.0
    container_name: honua-mysql
    command: --default-authentication-plugin=mysql_native_password
    environment:
      MYSQL_ROOT_PASSWORD: root_password
      MYSQL_DATABASE: honua
      MYSQL_USER: honua_app
      MYSQL_PASSWORD: honua_password
    ports:
      - "3306:3306"
    volumes:
      - mysql-data:/var/lib/mysql

  honua:
    build: .
    container_name: honua-server
    depends_on:
      - mysql
    environment:
      ConnectionStrings__DefaultConnection: "Server=mysql;Database=honua;User=honua_app;Password=honua_password"
    ports:
      - "8080:8080"

volumes:
  mysql-data:
```

### Kubernetes Configuration

#### PostGIS StatefulSet

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: postgres-secret
type: Opaque
stringData:
  POSTGRES_USER: honua
  POSTGRES_PASSWORD: secure_password
  POSTGRES_DB: honua
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
spec:
  selector:
    app: postgres
  ports:
    - port: 5432
      targetPort: 5432
  clusterIP: None
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgis/postgis:16-3.4
        ports:
        - containerPort: 5432
        envFrom:
        - secretRef:
            name: postgres-secret
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
  volumeClaimTemplates:
  - metadata:
      name: postgres-storage
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 50Gi
```

#### Honua Deployment with ConfigMap

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
data:
  appsettings.json: |
    {
      "honua": {
        "metadata": {
          "provider": "filesystem",
          "path": "/app/metadata"
        }
      }
    }
---
apiVersion: v1
kind: Secret
metadata:
  name: honua-secret
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres;Database=honua;Username=honua;Password=secure_password;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100"
  Redis__ConnectionString: "redis:6379,password=redis_password,abortConnect=false"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua-server
        image: honua/server:latest
        ports:
        - containerPort: 8080
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: honua-secret
              key: ConnectionStrings__DefaultConnection
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: honua-secret
              key: Redis__ConnectionString
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.json
          subPath: appsettings.json
      volumes:
      - name: config
        configMap:
          name: honua-config
```

### Connection Pooling Settings

#### Production (High Traffic)

```json
{
  "dataSources": [
    {
      "id": "postgres-production",
      "provider": "postgis",
      "connectionString": "Host=db.example.com;Database=honua;Username=honua_app;Password=secret;Pooling=true;Minimum Pool Size=20;Maximum Pool Size=200;Connection Idle Lifetime=600;Connection Pruning Interval=10"
    }
  ]
}
```

#### Development (Low Traffic)

```json
{
  "dataSources": [
    {
      "id": "postgres-dev",
      "provider": "postgis",
      "connectionString": "Host=localhost;Database=honua_dev;Username=honua;Password=dev;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
    }
  ]
}
```

#### Testing (No Pooling)

```json
{
  "dataSources": [
    {
      "id": "postgres-test",
      "provider": "postgis",
      "connectionString": "Host=localhost;Database=honua_test;Username=test;Password=test;Pooling=false"
    }
  ]
}
```

---

## Summary

This comprehensive guide covers all Honua data providers with complete configuration examples, performance tuning, troubleshooting, and best practices. Choose the right provider based on your deployment requirements:

- **PostGIS**: Production deployments with high performance requirements
- **SQL Server**: Enterprise Microsoft environments
- **SQLite**: Development, testing, and embedded scenarios
- **MySQL**: Open-source deployments with moderate requirements
- **Redis**: Metadata caching to improve performance

For production deployments, we recommend **PostGIS with Redis caching** for optimal performance, scalability, and OGC compliance.
