# Seeded Honua Docker Deployment

This guide explains how to deploy a complete Honua instance with pre-populated sample data using Docker Compose.

## Overview

The seeded deployment provides:

- **PostgreSQL 16 with PostGIS 3.4**: Spatial database with geography/geometry support
- **Honua Server**: Full-featured GIS server with OGC API, WFS, WMS, STAC, and GeoServices REST APIs
- **Automatic Data Seeding**: Pre-populated with sample GIS features, geometries, and metadata

This setup is ideal for:
- Development and testing
- Demos and proof-of-concepts
- Learning and experimentation
- Integration testing

## Quick Start

### Prerequisites

- Docker 20.10+ and Docker Compose 1.29+
- At least 2GB of available RAM
- At least 5GB of available disk space

### Starting the Seeded Instance

1. **Navigate to the repository root:**
   ```bash
   cd /path/to/HonuaIO
   ```

2. **Start all services:**
   ```bash
   docker-compose -f docker-compose.seed.yml up
   ```

   This will:
   - Start PostgreSQL with PostGIS
   - Build and start the Honua server
   - Wait for services to be healthy
   - Run the data seeder to populate the database
   - Display available endpoints

3. **Access the server:**

   The Honua server will be available at `http://localhost:8080`

   **Sample Endpoints:**
   - OGC API Landing Page: http://localhost:8080/
   - Health Check: http://localhost:8080/health
   - WFS GetCapabilities: http://localhost:8080/wfs?service=WFS&request=GetCapabilities
   - WMS GetCapabilities: http://localhost:8080/wms?service=WMS&request=GetCapabilities
   - STAC Catalog: http://localhost:8080/stac

### Stopping the Instance

```bash
# Stop services (keep data)
docker-compose -f docker-compose.seed.yml down

# Stop services and remove volumes (delete all data)
docker-compose -f docker-compose.seed.yml down -v
```

## Configuration

### Environment Variables

Copy `.env.seed` to customize the deployment:

```bash
cp .env.seed .env.seed.local
```

Then start with:

```bash
docker-compose -f docker-compose.seed.yml --env-file .env.seed.local up
```

### Key Configuration Options

#### Database Configuration

```env
# Database credentials
POSTGRES_USER=honua
POSTGRES_PASSWORD=honua_seed_pass
POSTGRES_DB=honua
POSTGRES_PORT=5432

# Performance tuning
POSTGRES_SHARED_BUFFERS=128MB
POSTGRES_EFFECTIVE_CACHE_SIZE=512MB
```

#### Server Configuration

```env
# Server port
HONUA_PORT=8080

# Environment (Development, Staging, Production)
ASPNETCORE_ENVIRONMENT=Development

# Logging levels
LOG_LEVEL=Information
LOG_LEVEL_HONUA=Information
```

#### Authentication Configuration

```env
# Authentication mode: Local, OIDC, or None
AUTH_MODE=Local

# QuickStart mode - enables anonymous access
# WARNING: Set to false in production!
QUICKSTART_ENABLED=true

# Enforce authentication
AUTH_ENFORCE=false

# Optional API key
HONUA_API_KEY=your-secret-key-here
```

## Architecture

### Service Dependencies

```
postgres (PostGIS)
  ├─> honua-server (depends on: postgres healthy)
       └─> seed-loader (depends on: honua-server healthy)
```

### Health Checks

All services include health checks:

- **postgres**: Uses `pg_isready` to verify database readiness
- **honua-server**: Uses `/healthz/ready` endpoint (checks database connectivity)
- **seed-loader**: Waits for honua-server health check before seeding

### Data Volumes

Two named volumes persist data:

- `honua-postgres-seed-data`: PostgreSQL database files
- `honua-seed-data-dir`: Honua application data (attachments, cache)

## Seeded Data

The `seed-loader` service runs the DataSeeder tool to populate the database with:

### Sample Features

- **Points**: Cities, landmarks, and observation points
- **LineStrings**: Roads, rivers, and boundaries
- **Polygons**: Administrative areas, parks, and zones
- **Multi-geometries**: Complex feature collections

### Sample Attributes

Each feature includes realistic attributes:
- Unique IDs and names
- Status flags and categories
- Numeric values and measurements
- Timestamps and dates
- Spatial reference systems (SRID 4326 - WGS84)

### Sample Metadata

- Service definitions
- Layer configurations
- Field schemas
- Spatial extents

## Accessing Seeded Data

### OGC WFS (Web Feature Service)

```bash
# Get capabilities
curl "http://localhost:8080/wfs?service=WFS&version=2.0.0&request=GetCapabilities"

# Get features
curl "http://localhost:8080/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=test_features"
```

### OGC WMS (Web Map Service)

```bash
# Get capabilities
curl "http://localhost:8080/wms?service=WMS&version=1.3.0&request=GetCapabilities"

# Get map (returns image)
curl "http://localhost:8080/wms?service=WMS&version=1.3.0&request=GetMap&layers=test_layer&width=800&height=600&crs=EPSG:4326&bbox=-180,-90,180,90&format=image/png" > map.png
```

### OGC API - Features

```bash
# Get landing page
curl "http://localhost:8080/"

# Get collections
curl "http://localhost:8080/collections"

# Get features from a collection
curl "http://localhost:8080/collections/test_features/items"
```

### STAC (SpatioTemporal Asset Catalog)

```bash
# Get catalog
curl "http://localhost:8080/stac"

# Get collections
curl "http://localhost:8080/stac/collections"

# Get items from a collection
curl "http://localhost:8080/stac/collections/{collection-id}/items"
```

### GeoServices REST API

```bash
# Get services
curl "http://localhost:8080/rest/services"

# Get layer info
curl "http://localhost:8080/rest/services/test_service/FeatureServer/0"

# Query features
curl "http://localhost:8080/rest/services/test_service/FeatureServer/0/query?where=1=1&f=json"
```

## Troubleshooting

### Services Not Starting

Check service health:

```bash
docker-compose -f docker-compose.seed.yml ps
```

View logs:

```bash
# All services
docker-compose -f docker-compose.seed.yml logs

# Specific service
docker-compose -f docker-compose.seed.yml logs honua-server
docker-compose -f docker-compose.seed.yml logs postgres
docker-compose -f docker-compose.seed.yml logs seed-loader
```

### Seeding Failed

If the seed-loader fails:

1. Check the logs:
   ```bash
   docker-compose -f docker-compose.seed.yml logs seed-loader
   ```

2. Manually run the seeder:
   ```bash
   docker-compose -f docker-compose.seed.yml run --rm seed-loader
   ```

3. Or run it directly:
   ```bash
   dotnet run --project tools/DataSeeder/DataSeeder.csproj -- \
     --provider postgres \
     --postgres-connection "Host=localhost;Database=honua;Username=honua;Password=honua_seed_pass"
   ```

### Database Connection Issues

Verify PostgreSQL is running:

```bash
docker exec honua-postgres-seed pg_isready -U honua
```

Connect to the database:

```bash
docker exec -it honua-postgres-seed psql -U honua -d honua
```

Check PostGIS extension:

```sql
SELECT PostGIS_version();
```

### Port Conflicts

If port 8080 or 5432 is already in use, change it in `.env.seed`:

```env
HONUA_PORT=8081
POSTGRES_PORT=5433
```

## Advanced Usage

### Running in Detached Mode

```bash
docker-compose -f docker-compose.seed.yml up -d
```

### Viewing Real-time Logs

```bash
docker-compose -f docker-compose.seed.yml logs -f honua-server
```

### Rebuilding the Server

```bash
docker-compose -f docker-compose.seed.yml build honua-server
docker-compose -f docker-compose.seed.yml up -d honua-server
```

### Re-running the Seeder

```bash
# Stop the seed-loader if it's still running
docker-compose -f docker-compose.seed.yml rm -f seed-loader

# Run it again
docker-compose -f docker-compose.seed.yml run --rm seed-loader
```

### Accessing the Database Directly

```bash
# Using psql
docker exec -it honua-postgres-seed psql -U honua -d honua

# Example queries
\dt                  # List tables
\d+ table_name       # Describe table
SELECT * FROM spatial_ref_sys LIMIT 5;
```

### Exporting Seeded Data

```bash
# Export entire database
docker exec honua-postgres-seed pg_dump -U honua honua > honua_seeded.sql

# Export specific table
docker exec honua-postgres-seed pg_dump -U honua -t test_features honua > test_features.sql
```

### Performance Tuning

For larger datasets or production-like testing, increase PostgreSQL resources:

```env
POSTGRES_SHARED_BUFFERS=512MB
POSTGRES_EFFECTIVE_CACHE_SIZE=2GB
POSTGRES_MAX_CONNECTIONS=200
```

## Production Deployment

**WARNING:** This seeded deployment is designed for development and testing only.

For production deployment:

1. **Disable QuickStart mode:**
   ```env
   QUICKSTART_ENABLED=false
   AUTH_ENFORCE=true
   ```

2. **Use strong passwords:**
   ```env
   POSTGRES_PASSWORD=$(openssl rand -base64 32)
   HONUA_API_KEY=$(openssl rand -base64 32)
   ```

3. **Enable proper authentication:**
   ```env
   AUTH_MODE=OIDC
   # Configure OIDC settings...
   ```

4. **Use production environment:**
   ```env
   ASPNETCORE_ENVIRONMENT=Production
   ```

5. **Enable observability:**
   ```env
   METRICS_ENABLED=true
   TRACING_ENABLED=true
   ```

6. **See**: [docker-compose.full.yml](../../docker/docker-compose.full.yml) for a production-ready setup

## Related Documentation

- [Docker Deployment Guide](../DOCKER_DEPLOYMENT.md)
- [Docker Quick Reference](../DOCKER_QUICK_REFERENCE.md)
- [Health Checks](../health-checks/README.md)
- [Authentication Configuration](../SECURITY.md)
- [Performance Tuning](../performance/TUNING.md)

## Support

For issues or questions:
- Check the [troubleshooting section](#troubleshooting)
- Review logs: `docker-compose -f docker-compose.seed.yml logs`
- See [DOCKER_GOTCHAS.md](../DOCKER_GOTCHAS.md) for common issues
- Open an issue on GitHub
