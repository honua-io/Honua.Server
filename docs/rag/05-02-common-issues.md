---
tags: [troubleshooting, debugging, errors, solutions, common-issues, faq, problems]
category: troubleshooting
difficulty: beginner
version: 1.0.0
last_updated: 2025-10-15
---

# Common Issues and Troubleshooting Guide

Solutions to frequently encountered problems with Honua Server.

## Table of Contents
- [Startup Issues](#startup-issues)
- [Authentication Problems](#authentication-problems)
- [Database Connection Issues](#database-connection-issues)
- [API Errors](#api-errors)
- [Performance Problems](#performance-problems)
- [Docker Issues](#docker-issues)
- [Configuration Errors](#configuration-errors)
- [Memory Issues](#memory-issues)
- [Raster Processing Problems](#raster-processing-problems)
- [Metadata Issues](#metadata-issues)
- [Observability Issues](#observability-issues)
- [Network and Connectivity](#network-and-connectivity)
- [Related Documentation](#related-documentation)

## Startup Issues

### Server Won't Start

**Symptom:** Application crashes immediately on startup.

**Common Causes:**

#### 1. Port Already in Use

```bash
# Error message:
# Unable to bind to http://localhost:5000 on the IPv4 loopback interface: 'Address already in use'.
```

**Solution:**
```bash
# Find process using port 5000
sudo lsof -i :5000

# Kill process
kill -9 <PID>

# Or change port in appsettings.json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5001"
      }
    }
  }
}
```

#### 2. Missing Configuration

```bash
# Error message:
# The configuration key 'honua:workspacePath' is required but was not found.
```

**Solution:**
```bash
# Ensure workspacePath is set
{
  "honua": {
    "workspacePath": "./metadata"
  }
}

# Create metadata directory
mkdir -p ./metadata
```

#### 3. Invalid Metadata

```bash
# Error message:
# Failed to load metadata: Invalid YAML syntax
```

**Solution:**
```bash
# Validate YAML syntax
yamllint metadata.yaml

# Check for common issues:
# - Incorrect indentation
# - Missing required fields
# - Invalid service/layer IDs
```

### QuickStart Mode Blocked

**Symptom:** Cannot enable QuickStart authentication.

```
QuickStart authentication mode is disabled. Set HONUA_ALLOW_QUICKSTART=true
```

**Solution:**
```bash
# Set environment variable
export HONUA_ALLOW_QUICKSTART=true
export ASPNETCORE_ENVIRONMENT=Development

# Run server
dotnet run

# Or in appsettings.Development.json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "quickStart": {
        "enabled": true
      },
      "allowQuickStart": true
    }
  }
}
```

**Important:** QuickStart is blocked in production for security.

### Schema Validation Failures

**Symptom:** Startup fails with schema mismatch errors.

```
Schema validation failed: Table 'cities' missing column 'population'
```

**Solution:**
```bash
# Option 1: Disable validation
{
  "SchemaValidation": {
    "Enabled": false
  }
}

# Option 2: Fix schema in database
ALTER TABLE cities ADD COLUMN population INTEGER;

# Option 3: Update metadata to match schema
# Edit metadata.yaml to remove or update layer definition
```

## Authentication Problems

### Login Always Fails

**Symptom:** Correct credentials rejected.

**Check Authentication Mode:**
```bash
# Verify auth configuration
curl http://localhost:5000/admin/config

# Ensure mode matches your setup
{
  "honua": {
    "authentication": {
      "mode": "Local",  # or "Jwt", "QuickStart"
      "enforce": true
    }
  }
}
```

**Solution for Local Auth:**
```bash
# Reset user password using CLI
honua user reset-password --username admin

# Or create new user
honua user create --username admin --password "SecurePassword123!" --role administrator
```

### JWT Token Invalid

**Symptom:** Bearer token rejected.

```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "Invalid or expired token"
}
```

**Solution:**
```bash
# Verify JWT configuration
{
  "honua": {
    "authentication": {
      "jwt": {
        "issuer": "Honua.Server",
        "audience": "Honua.Clients",
        "signingKey": "${JWT_SIGNING_KEY}",
        "expirationMinutes": 60
      }
    }
  }
}

# Ensure signing key is set
export JWT_SIGNING_KEY="your-secret-key-here"

# Generate new signing key
openssl rand -base64 32
```

### CORS Errors in Browser

**Symptom:** Browser console shows CORS error.

```
Access to fetch at 'http://localhost:5000/ogc/collections' from origin 'http://localhost:3000'
has been blocked by CORS policy
```

**Solution:**

Update metadata.yaml:
```yaml
services:
  - id: my-service
    cors:
      allowedOrigins:
        - http://localhost:3000
        - https://myapp.com
      allowedMethods: [GET, POST, PUT, DELETE]
      allowedHeaders: [Content-Type, Authorization]
      allowCredentials: true
```

Or allow all origins (development only):
```json
{
  "honua": {
    "cors": {
      "allowAnyOrigin": true
    }
  }
}
```

## Database Connection Issues

### Cannot Connect to PostgreSQL

**Symptom:** Connection refused errors.

```
Failed to connect to PostgreSQL server at localhost:5432
```

**Solution:**

**Check PostgreSQL is Running:**
```bash
# Local PostgreSQL
sudo systemctl status postgresql

# Docker PostgreSQL
docker compose ps postgres

# Test connection
psql -h localhost -U honua -d honua
```

**Verify Connection String:**
```bash
# Check appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;Database=honua;User Id=honua;Password=honua_password;"
  }
}

# Test with psql
psql "Server=localhost;Port=5432;Database=honua;User Id=honua;Password=honua_password;"
```

**Common Connection String Issues:**
- Wrong hostname (use `localhost` or `db` for Docker)
- Wrong port (default: 5432)
- Wrong database name
- Wrong username/password
- Firewall blocking connection

### PostGIS Extension Missing

**Symptom:** Spatial queries fail.

```
function st_geomfromtext(unknown, integer) does not exist
```

**Solution:**
```sql
-- Connect to database
psql -U honua -d honua

-- Enable PostGIS extension
CREATE EXTENSION IF NOT EXISTS postgis;

-- Verify installation
SELECT PostGIS_Version();
```

### Too Many Connections

**Symptom:** Connection pool exhausted.

```
FATAL: remaining connection slots are reserved
```

**Solution:**

**Increase PostgreSQL max_connections:**
```bash
# Edit postgresql.conf
max_connections = 200

# Restart PostgreSQL
sudo systemctl restart postgresql
```

**Or adjust connection pooling:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;...;Maximum Pool Size=50;"
  }
}
```

## API Errors

### 404 Collection Not Found

**Symptom:** Collection exists but returns 404.

```bash
curl http://localhost:5000/ogc/collections/cities
# {"status": 404, "detail": "Collection 'cities' was not found."}
```

**Solution:**

**Check Metadata:**
```bash
# Verify layer is defined in metadata.yaml
services:
  - id: my-service
    layers:
      - id: cities
        title: Cities
        table: cities
```

**Verify Metadata Loaded:**
```bash
# Check metadata endpoint
curl http://localhost:5000/admin/metadata

# Reload metadata
curl -X POST http://localhost:5000/admin/metadata/reload
```

### 500 Internal Server Error

**Symptom:** Unexpected server errors.

**Solution:**

**Check Logs:**
```bash
# View logs
docker compose logs honua

# Enable detailed logging
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Honua.Server": "Trace"
    }
  }
}
```

**Common Causes:**
- Missing required field in metadata
- Invalid SQL in custom queries
- Geometry type mismatch
- Missing database table

### 429 Rate Limit Exceeded

**Symptom:** Too many requests.

```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Rate limit exceeded. Retry after 60 seconds."
}
```

**Solution:**

**Adjust Rate Limits:**
```json
{
  "RateLimiting": {
    "Enabled": true,
    "OgcApi": {
      "PermitLimit": 500,  // Increase from 200
      "WindowMinutes": 1
    }
  }
}
```

**Or Disable Rate Limiting (Development):**
```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

### Invalid CQL Filter

**Symptom:** Filter syntax errors.

```bash
curl "http://localhost:5000/ogc/collections/cities/items?filter=population > 'abc'&filter-lang=cql-text"
# {"status": 400, "detail": "Invalid filter expression. [parser detail]"}
```

**Solution:**

**Fix Common Syntax Errors:**
```bash
# WRONG: String comparison without quotes
filter=name = New York

# RIGHT: Strings need quotes
filter=name = 'New York'

# WRONG: Invalid operator
filter=population >> 1000000

# RIGHT: Use correct operators (>, <, =, >=, <=, <>)
filter=population > 1000000

# WRONG: Missing parentheses
filter=population > 1000000 AND country = 'USA' OR country = 'Canada'

# RIGHT: Use explicit grouping
filter=(population > 1000000 AND country = 'USA') OR country = 'Canada'
```

**URL Encoding:**
```bash
# Spaces and special characters must be URL encoded
curl "http://localhost:5000/ogc/collections/cities/items?filter=name%20%3D%20%27New%20York%27&filter-lang=cql-text"

# Or use POST request with JSON body
curl -X POST http://localhost:5000/ogc/search \
  -H "Content-Type: application/json" \
  -d '{
    "collections": ["cities"],
    "filter": {"op": "=", "args": [{"property": "name"}, "New York"]},
    "filter-lang": "cql2-json"
  }'
```

## Performance Problems

### Slow Queries

**Symptom:** API responses take >5 seconds.

**Solution:**

**Add Database Indexes:**
```sql
-- Index frequently queried columns
CREATE INDEX idx_cities_population ON cities(population);
CREATE INDEX idx_cities_country ON cities(country);

-- Spatial index (PostGIS)
CREATE INDEX idx_cities_geom ON cities USING GIST(geometry);

-- Check existing indexes
SELECT * FROM pg_indexes WHERE tablename = 'cities';
```

**Enable Query Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Database": "Debug"
    }
  }
}
```

**Analyze Queries:**
```sql
-- Enable query timing
\timing

-- Explain query plan
EXPLAIN ANALYZE
SELECT * FROM cities WHERE population > 1000000;

-- Update table statistics
ANALYZE cities;
```

### Memory Usage High

**Symptom:** Server consuming excessive memory.

**Solution:**

**Limit Result Set Size:**
```json
{
  "honua": {
    "odata": {
      "maxPageSize": 100  // Reduce from 1000
    }
  }
}
```

**Reduce Cache Size:**
```json
{
  "honua": {
    "cache": {
      "maxSizeMb": 1024  // Reduce from 10240
    }
  }
}
```

**Configure Garbage Collection:**
```bash
# Environment variables
export DOTNET_gcServer=1
export DOTNET_GCHeapHardLimit=2000000000  # 2GB limit
```

### Tile Rendering Slow

**Symptom:** Raster tiles take >1s to render.

**Solution:**

**Enable Tile Caching:**
```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "provider": "FileSystem",
      "maxSizeMb": 10240
    }
  }
}
```

**Pre-seed Tiles:**
```bash
# Use admin API to pre-generate tiles
curl -X POST http://localhost:5000/admin/raster/preseed \
  -H "Content-Type: application/json" \
  -d '{
    "rasterSourceId": "my-raster",
    "minZoom": 0,
    "maxZoom": 10
  }'
```

**Optimize COG Files:**
```bash
# Create overviews for faster rendering
gdaladdo -r average input.tif 2 4 8 16

# Ensure proper tiling
gdal_translate -co TILED=YES -co COMPRESS=DEFLATE input.tif output.tif
```

## Docker Issues

### Container Exits Immediately

**Symptom:** Container starts then stops.

```bash
docker compose ps
# honua-server  Exited (1)
```

**Solution:**
```bash
# View logs
docker compose logs honua

# Common issues:
# 1. Database not ready - Add depends_on with health check
services:
  honua:
    depends_on:
      postgres:
        condition: service_healthy

# 2. Missing environment variables
# Check .env file exists and is correct

# 3. Configuration error
# Validate appsettings.json syntax
```

### Cannot Connect to Database in Docker

**Symptom:** Honua can't reach PostgreSQL.

```bash
# Error: Connection refused to localhost:5432
```

**Solution:**
```bash
# Use service name, not localhost
ConnectionStrings__DefaultConnection=Server=postgres;Port=5432;...

# NOT: Server=localhost;...

# Verify network
docker network inspect docker_default

# Test connectivity
docker compose exec honua ping postgres
```

### Volume Permissions

**Symptom:** Cannot write to mounted volumes.

```
Permission denied: '/app/data/cache'
```

**Solution:**
```bash
# Fix permissions on host
sudo chown -R 1000:1000 ./data

# Or run as root (not recommended)
services:
  honua:
    user: root
```

## Configuration Errors

### Invalid JSON Syntax

**Symptom:** Server won't start with JSON parse error.

```
Unhandled exception: System.Text.Json.JsonException
```

**Solution:**
```bash
# Validate JSON syntax
cat appsettings.json | jq .

# Common issues:
# - Missing commas
# - Trailing commas
# - Unquoted strings
# - Missing closing braces

# Use JSON linter
jsonlint appsettings.json
```

### Environment Variable Not Applied

**Symptom:** Configuration changes not taking effect.

**Solution:**
```bash
# Verify environment variable syntax
export honua__authentication__mode=Local  # Correct
export honua.authentication.mode=Local     # Wrong

# Check variable is set
env | grep honua

# Restart application after setting variables
```

### Connection String Format

**Symptom:** Invalid connection string.

**Correct Format:**
```bash
# PostgreSQL
Server=localhost;Port=5432;Database=honua;User Id=honua;Password=pass;

# SQLite
Data Source=/path/to/database.db;

# SQL Server
Server=localhost;Database=honua;User Id=sa;Password=pass;TrustServerCertificate=true;
```

## Memory Issues

### Out of Memory Exception

**Symptom:** Application crashes with OOM.

```
System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.
```

**Solution:**

**Increase Container Memory:**
```yaml
services:
  honua:
    deploy:
      resources:
        limits:
          memory: 4G
```

**Reduce Query Limits:**
```json
{
  "honua": {
    "odata": {
      "maxPageSize": 100,
      "defaultPageSize": 50
    }
  }
}
```

**Enable Server GC:**
```xml
<!-- In .csproj -->
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```

### Memory Leak

**Symptom:** Memory usage grows over time.

**Diagnosis:**
```bash
# Monitor memory usage
docker stats honua-server

# Collect memory dump
dotnet-dump collect --process-id <pid>

# Analyze with dotnet-dump
dotnet-dump analyze memory.dmp
```

**Common Causes:**
- Unclosed database connections
- Unbounded caching
- Event handler leaks
- Large result sets not paginated

## Raster Processing Problems

### COG File Won't Load

**Symptom:** Raster source fails to initialize.

```
Failed to open COG file: /data/rasters/myfile.tif
```

**Solution:**

**Verify File Format:**
```bash
# Check if file is valid COG
gdalinfo myfile.tif

# Should show:
# - TILED=YES
# - Overviews
# - Layout=COG

# Convert to COG if needed
gdal_translate -of COG -co COMPRESS=DEFLATE input.tif output_cog.tif
```

**Check File Permissions:**
```bash
# Ensure Honua can read file
chmod 644 myfile.tif

# Check ownership
ls -la myfile.tif
```

### Zarr Array Not Found

**Symptom:** Zarr dataset fails to load.

```
Failed to read Zarr array: /data/zarr/dataset/.zarray not found
```

**Solution:**
```bash
# Verify Zarr structure
ls -R /data/zarr/dataset/

# Should contain:
# .zgroup (root group)
# .zarray (array metadata)
# 0.0, 0.1, etc. (chunks)

# Validate with Python
python -c "import zarr; zarr.open('/data/zarr/dataset')"
```

### Tiles Rendering Incorrectly

**Symptom:** Tiles have wrong colors or nodata.

**Solution:**

**Check Styling Configuration:**
```yaml
rasterSources:
  - id: my-raster
    styling:
      renderer: stretch
      colorRamp: viridis
      minValue: 0
      maxValue: 100
      noDataValue: -9999
```

**Verify Data Type:**
```bash
gdalinfo -stats myfile.tif

# Check:
# - Data type (Byte, Int16, Float32, etc.)
# - NoData value
# - Value range
```

## Metadata Issues

### Metadata Won't Reload

**Symptom:** Changes to metadata.yaml not applied.

**Solution:**
```bash
# Force reload
curl -X POST http://localhost:5000/admin/metadata/reload

# Check reload endpoint response
# If errors, fix metadata.yaml

# Restart server as last resort
docker compose restart honua
```

### Invalid Service Definition

**Symptom:** Service validation error.

```
Service 'my-service' validation failed: Missing required field 'connectionString'
```

**Solution:**
```yaml
# Ensure all required fields present
services:
  - id: my-service           # Required
    title: My Service         # Required
    connectionString: "..."   # Required for database services
    layers:
      - id: my-layer          # Required
        title: My Layer       # Required
        table: my_table       # Required
```

### Layer Not Appearing in Collections

**Symptom:** Layer defined but not visible via API.

**Solution:**
```yaml
# Check layer is enabled
layers:
  - id: my-layer
    title: My Layer
    enabled: true  # Must be true (default)

# Check service is enabled
services:
  - id: my-service
    enabled: true  # Must be true (default)

# Verify table exists in database
psql -U honua -d honua -c "\dt"
```

## Observability Issues

### Metrics Not Appearing

**Symptom:** /metrics endpoint returns no data.

**Solution:**
```json
{
  "observability": {
    "metrics": {
      "enabled": true,
      "usePrometheus": true,
      "endpoint": "/metrics"
    }
  }
}
```

**Verify endpoint:**
```bash
curl http://localhost:5000/metrics

# Should return Prometheus format metrics
# honua_api_requests_total{...} 123
```

### Tracing Not Working

**Symptom:** No traces in Jaeger.

**Solution:**

**Enable OTLP Exporter:**
```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://jaeger:4317"
    }
  }
}
```

**Verify Jaeger Connection:**
```bash
# Test OTLP endpoint
curl http://jaeger:4317

# Check Jaeger UI
curl http://localhost:16686/api/services
```

### Logs Not Appearing

**Symptom:** No log output.

**Solution:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"  // Not "None"
    }
  },
  "observability": {
    "logging": {
      "jsonConsole": true
    }
  }
}
```

## Network and Connectivity

### Cannot Access from Remote Host

**Symptom:** Server works locally but not remotely.

**Solution:**

**Bind to All Interfaces:**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"  // Not 127.0.0.1
      }
    }
  }
}
```

**Check Firewall:**
```bash
# Allow port through firewall
sudo ufw allow 5000/tcp

# Verify port is listening
netstat -tlnp | grep 5000
```

### SSL/TLS Certificate Errors

**Symptom:** HTTPS connection fails.

```
The SSL connection could not be established
```

**Solution:**

**Development - Trust Certificate:**
```bash
# Trust development certificate
dotnet dev-certs https --trust
```

**Production - Valid Certificate:**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:443",
        "Certificate": {
          "Path": "/path/to/cert.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

**Use Let's Encrypt:**
```bash
# Install certbot
sudo apt install certbot

# Get certificate
sudo certbot certonly --standalone -d yourdomain.com

# Certificate location:
# /etc/letsencrypt/live/yourdomain.com/fullchain.pem
# /etc/letsencrypt/live/yourdomain.com/privkey.pem
```

## Getting Help

### Collect Diagnostic Information

```bash
# 1. Server version
curl http://localhost:5000/ogc | jq .version

# 2. Health status
curl http://localhost:5000/healthz/ready | jq .

# 3. Configuration
curl http://localhost:5000/admin/config | jq .

# 4. Recent logs
docker compose logs --tail=100 honua

# 5. System resources
docker stats --no-stream

# 6. Database connection
psql -h localhost -U honua -d honua -c "SELECT version();"
```

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Honua.Server": "Trace",
      "Microsoft.AspNetCore": "Debug"
    }
  }
}
```

### Common Log Patterns

**Database Query Slow:**
```
[Warning] Query execution took 5234ms: SELECT * FROM ...
```

**Authentication Failed:**
```
[Warning] Authentication failed for user 'admin': Invalid password
```

**Rate Limit Hit:**
```
[Information] Rate limit exceeded for IP 192.168.1.1
```

**Metadata Reload:**
```
[Information] Metadata reloaded successfully. Services: 3, Layers: 12
```

## Related Documentation

- [Architecture Overview](01-01-architecture-overview.md) - System design
- [Configuration Reference](02-01-configuration-reference.md) - Config options
- [OGC API Features](03-01-ogc-api-features.md) - API usage
- [Docker Deployment](04-01-docker-deployment.md) - Deployment guide

## Keywords for Search

troubleshooting, debugging, errors, common issues, problems, solutions, FAQ, startup issues, authentication, database connection, API errors, performance, Docker, configuration, memory, raster processing, metadata, observability, networking, PostgreSQL, PostGIS, CORS, rate limiting, logs, health checks

---

**Last Updated**: 2025-10-15
**Version**: 1.0.0
**Covers**: Honua Server 1.0.0-rc1
