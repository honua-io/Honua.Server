# 2. Use PostgreSQL as Primary Database

Date: 2025-10-17

Status: Accepted

## Context

Honua is a geospatial server that requires robust spatial data storage and query capabilities. The choice of database directly impacts performance, feature support, operational complexity, and cloud deployment options.

**Key Requirements:**
- Native spatial data type support (geometries, geographic coordinates)
- Spatial indexing for efficient bounding box queries
- Standards compliance (OGC Simple Features, WKB/WKT)
- Transactional integrity for feature editing
- Vector tile generation (MVT format)
- Scalability to millions of features
- Good ecosystem support in .NET
- Cloud-native deployment options
- Cost-effective for various deployment sizes

**Existing Codebase Evidence:**
- PostgreSQL/PostGIS used throughout core data providers (`PostgresDataStoreProvider`)
- Npgsql and Npgsql.NetTopologySuite packages are primary dependencies
- ST_AsMVT function used for efficient vector tile generation
- Connection pooling and metrics implemented (`PostgresConnectionPoolMetrics`)
- Database retry policies using Polly for transient failures

## Decision

We will use **PostgreSQL with the PostGIS extension** as the recommended and best-supported database for Honua deployments.

**Specific commitments:**
- PostgreSQL 13+ is the target version (for ST_AsMVT support)
- PostGIS 3.0+ extension provides spatial capabilities
- Npgsql provider is the primary .NET driver
- Native MVT generation using `ST_AsMVT` for vector tiles
- Prepared statements and connection pooling for performance
- Spatial indexes (GIST) on all geometry columns
- Support for both geographic (lat/lon) and projected coordinate systems

**While PostgreSQL is primary**, we maintain the multi-database provider abstraction to support SQLite and SQL Server for specific use cases (see ADR-0004).

## Consequences

### Positive

- **Spatial Excellence**: PostGIS is the industry-leading open-source spatial database
- **Performance**: ST_AsMVT generates vector tiles at native database speed
- **Standards Compliance**: Full OGC Simple Features support
- **Rich Feature Set**: Spatial functions, topology, raster support, routing extensions
- **Proven Scalability**: Handles billions of geometries with proper indexing
- **Cloud Native**: Managed offerings on AWS RDS, Azure Database, GCP Cloud SQL
- **Cost Effective**: Open source, no licensing fees
- **Ecosystem**: Extensive tooling (pgAdmin, DBeaver, psql)
- **Community**: Large, active community with excellent documentation
- **.NET Support**: Npgsql is mature, well-maintained, supports NetTopologySuite

### Negative

- **Operational Complexity**: More complex to run than SQLite
- **Resource Requirements**: Higher memory/CPU baseline than lightweight alternatives
- **Learning Curve**: SQL + PostGIS functions require spatial expertise
- **Backup Complexity**: Larger backup sizes, longer restore times
- **Cost**: Managed instances have minimum costs (vs. serverless options)

### Neutral

- Requires PostGIS extension installation (simple but necessary step)
- PostgreSQL upgrades require testing for PostGIS compatibility
- Spatial index maintenance needed for large datasets

## Alternatives Considered

### 1. SQLite with SpatiaLite Extension

**Pros:**
- Embedded database (no server required)
- Zero configuration
- File-based (easy backup/restore)
- Lightweight (minimal resource usage)
- Great for development and small deployments

**Cons:**
- No native MVT generation (must build tiles in application)
- Limited spatial function support compared to PostGIS
- Poor concurrent write performance
- No built-in replication or high availability
- File locking issues with network storage
- Limited to single server (no horizontal scaling)

**Verdict:** Accepted as **secondary option** for embedded/development use cases (see ADR-0004)

### 2. SQL Server with Spatial Types

**Pros:**
- Native spatial types (geometry, geography)
- Good .NET integration via System.Data.SqlClient
- Enterprise features (Always On, clustering)
- Familiar to Windows/Azure shops
- Azure SQL Database managed offering

**Cons:**
- **No native MVT generation** (major limitation)
- Limited spatial functions vs PostGIS
- Expensive licensing for on-premises
- Windows-centric ecosystem
- Geometry vs Geography type confusion
- Less spatial ecosystem maturity

**Verdict:** Accepted as **secondary option** for enterprise Windows environments (see ADR-0004)

### 3. MySQL with Spatial Extensions

**Pros:**
- Spatial data type support (since 5.7)
- Widely deployed
- Managed offerings (AWS RDS, Azure Database)
- Good performance

**Cons:**
- **No native MVT generation**
- Weaker spatial function library than PostGIS
- Spatial indexing less mature
- Licensing uncertainty (Oracle ownership)
- Lesser spatial ecosystem
- Weaker .NET support compared to PostgreSQL

**Verdict:** Rejected - spatial capabilities insufficient

### 4. MongoDB with GeoJSON Support

**Pros:**
- Native GeoJSON storage
- Geospatial queries and indexing
- Flexible schema
- Horizontal scaling
- Cloud Atlas managed service

**Cons:**
- **No MVT generation**
- Not relational (complicates OGC standards compliance)
- Limited spatial operations vs PostGIS
- Requires application-layer joins
- OData and SQL APIs not native
- Higher operational complexity for spatial workloads

**Verdict:** Rejected - poor fit for relational spatial data model

### 5. Cloud-Native Spatial Databases (BigQuery GIS, Redshift Spatial)

**Pros:**
- Massive scale
- Serverless/managed
- Good for analytics

**Cons:**
- High costs at scale
- Not suitable for transactional workloads
- API/SQL dialect differences
- Vendor lock-in
- Overkill for typical GIS server use cases

**Verdict:** Rejected - optimized for analytics, not OLTP

## Implementation Details

### Connection String Example
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=honua;Username=honua;Password=***;Include Error Detail=true"
  }
}
```

### Required PostGIS Setup
```sql
-- Enable PostGIS extension
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

-- Verify version
SELECT PostGIS_Version();

-- Example spatial table
CREATE TABLE layers.roads (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom GEOMETRY(LineString, 4326)
);

-- Spatial index
CREATE INDEX idx_roads_geom ON layers.roads USING GIST (geom);
```

### MVT Generation (Key Differentiator)
```sql
-- PostgreSQL/PostGIS native MVT
SELECT ST_AsMVT(tile, 'layer_name', 4096, 'geom')
FROM (
    SELECT id, name,
           ST_AsMVTGeom(geom, bbox, 4096, 256, true) AS geom
    FROM layers.roads
    WHERE geom && bbox
) AS tile;
```

This single-query MVT generation is 10-100x faster than application-layer tile building required by other databases.

### Code Reference
- Primary implementation: `/src/Honua.Server.Core/Data/Postgres/PostgresDataStoreProvider.cs`
- Connection pooling: `/src/Honua.Server.Core/Data/Postgres/PostgresConnectionPoolMetrics.cs`
- Retry policies: `/src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs`

## Migration Path

For existing deployments on other databases:
1. Use multi-provider abstraction (ADR-0004) to support current database
2. Plan migration to PostgreSQL for production deployments
3. Provide migration tooling in future releases
4. Document performance differences to justify migration

## References

- [PostGIS Documentation](https://postgis.net/documentation/)
- [Npgsql Documentation](https://www.npgsql.org/)
- [ST_AsMVT Function Reference](https://postgis.net/docs/ST_AsMVT.html)
- [PostgreSQL Performance Tuning for PostGIS](https://postgis.net/workshops/postgis-intro/performance.html)

## Notes

This decision was evident in the codebase but not formally documented. PostgreSQL was chosen early and has proven to be the right choice. This ADR codifies that decision and provides context for future contributors.
