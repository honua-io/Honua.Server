# Honua Troubleshooting Guide

**Comprehensive guide to diagnosing and resolving common Honua issues**

This guide helps you quickly identify and fix common problems with Honua Server. For operational issues in production, see the [Operations Guide](../operations/README.md).

---

## Quick Diagnostic

**Start here if you're not sure what's wrong:**

```bash
# 1. Check if Honua is running
curl http://localhost:8080/health

# 2. Check logs
docker logs honua-server --tail 50

# 3. List collections (tests database + metadata)
curl http://localhost:8080/ogc/collections
```

---

## Table of Contents

- [Installation & Startup Issues](#installation--startup-issues)
- [Database Connection Issues](#database-connection-issues)
- [Authentication Issues](#authentication-issues)
- [Metadata & Configuration Issues](#metadata--configuration-issues)
- [API Request Issues](#api-request-issues)
- [Performance Issues](#performance-issues)
- [Docker Issues](#docker-issues)

---

## Installation & Startup Issues

### Issue: Honua won't start

**Symptoms**:
- Container exits immediately
- `dotnet run` fails
- Health check never becomes healthy

**Diagnosis**:
```bash
# Check logs
docker logs honua-server

# Check if port is already in use
lsof -i :8080
netstat -tuln | grep 8080
```

**Solutions**:

**Port conflict**:
```bash
# Use a different port
docker run -p 8081:8080 ghcr.io/honuaio/honua-server:latest
```

**Missing configuration**:
```bash
# Verify connection string is set
docker logs honua-server | grep -i "connection"

# Set connection string
docker run -e ConnectionStrings__DefaultConnection="Host=db;..." ghcr.io/honuaio/honua-server:latest
```

**Permission issues (file metadata)**:
```bash
# Check metadata directory permissions
ls -la metadata/

# Fix permissions
chmod -R 755 metadata/
```

---

### Issue: "Failed to bind to address"

**Symptoms**:
```
System.IO.IOException: Failed to bind to address http://0.0.0.0:8080
```

**Solutions**:

**Another process is using port 8080**:
```bash
# Find what's using the port
lsof -i :8080

# Kill the process
kill -9 <PID>

# Or use a different port
docker run -p 8081:8080 ...
```

---

## Database Connection Issues

### Issue: "Could not connect to database"

**Symptoms**:
```
Npgsql.NpgsqlException: Failed to connect to database
```

**Diagnosis**:
```bash
# 1. Test database connection directly
psql -h localhost -U postgres -d honua

# 2. Check if database is running
docker ps | grep postgres

# 3. Verify connection string
echo $ConnectionStrings__DefaultConnection
```

**Solutions**:

**Database not running**:
```bash
# Start PostgreSQL
docker compose up -d postgres
```

**Wrong host**:
```bash
# If using Docker Compose, use service name
ConnectionStrings__DefaultConnection="Host=postgres;..."

# If using host network, use localhost
ConnectionStrings__DefaultConnection="Host=localhost;..."

# If using external database, use IP/hostname
ConnectionStrings__DefaultConnection="Host=192.168.1.10;..."
```

**Wrong credentials**:
```bash
# Verify credentials
psql -h localhost -U postgres -d honua
# Password: <enter password>

# Update connection string with correct credentials
ConnectionStrings__DefaultConnection="Host=localhost;Database=honua;Username=postgres;Password=correctpassword"
```

**Database doesn't exist**:
```sql
-- Connect to PostgreSQL
psql -h localhost -U postgres

-- Create database
CREATE DATABASE honua;

-- Enable PostGIS
\c honua
CREATE EXTENSION postgis;
```

---

### Issue: "PostGIS extension not found"

**Symptoms**:
```
ERROR: PostGIS extension is not installed
```

**Solution**:
```sql
-- Connect to database
psql -h localhost -U postgres -d honua

-- Install PostGIS
CREATE EXTENSION postgis;

-- Verify
SELECT PostGIS_version();
```

---

## Authentication Issues

### Issue: "401 Unauthorized" on all requests

**Symptoms**:
- All API requests return 401
- Even public endpoints require authentication

**Diagnosis**:
```bash
# Check authentication mode
curl http://localhost:8080/ogc | jq

# Check logs
docker logs honua-server | grep -i "auth"
```

**Solutions**:

**QuickStart mode should allow access**:

Edit `appsettings.json`:
```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false
    }
  }
}
```

Or environment variable:
```bash
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false
```

---

### Issue: "Invalid token" or "Token expired"

**Symptoms**:
```
401 Unauthorized: Token is invalid or expired
```

**Diagnosis**:
```bash
# Decode token to check expiration
jq -R 'split(".") | .[1] | @base64d | fromjson' <<< "$TOKEN"
```

**Solutions**:

**Token expired - get a new token**:
```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"your-password"}' \
  | jq -r '.token')

# Use new token
curl http://localhost:8080/ogc/collections \
  -H "Authorization: Bearer $TOKEN"
```

**Wrong token format**:
```bash
# Correct format
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...

# Wrong - missing "Bearer"
Authorization: eyJhbGciOiJIUzI1NiIs...
```

---

### Issue: "Failed to login" or "Invalid credentials"

**Symptoms**:
```
400 Bad Request: Invalid username or password
```

**Solutions**:

**Reset admin password**:
```bash
# Delete user store
rm -rf data/users/*

# Set new password in appsettings.json
{
  "honua": {
    "authentication": {
      "bootstrap": {
        "adminUsername": "admin",
        "adminEmail": "admin@example.com",
        "adminPassword": "NewPassword123!"
      }
    }
  }
}

# Restart Honua
docker compose restart
```

**Account locked due to failed attempts**:

Wait 15 minutes (default lockout duration) or restart Honua to clear locks.

---

## Metadata & Configuration Issues

### Issue: "Collection not found"

**Symptoms**:
```
404 Not Found: Collection 'parks' does not exist
```

**Diagnosis**:
```bash
# 1. List available collections
curl http://localhost:8080/ogc/collections | jq -r '.collections[] | .id'

# 2. Check metadata files exist
ls -la metadata/services/*/

# 3. Check logs for metadata errors
docker logs honua-server | grep -i metadata
```

**Solutions**:

**Metadata file missing**:
```bash
# Create metadata file
mkdir -p metadata/services/myservice/
cat > metadata/services/myservice/parks.yaml <<EOF
id: parks
title: Parks
collections:
  - id: parks
    title: Parks
    table: public.parks
    idField: id
    geometryField: geom
layers:
  - id: parks-default
    title: Parks
    collection: parks
    vectorEnabled: true
EOF

# Restart Honua
docker compose restart
```

**Invalid YAML syntax**:
```bash
# Validate YAML
yamllint metadata/services/myservice/parks.yaml

# Common issues:
# - Incorrect indentation (use 2 spaces)
# - Missing colons
# - Unquoted strings with special characters
```

**Table doesn't exist in database**:
```sql
-- Verify table exists
\dt public.parks

-- Create table if missing
CREATE TABLE public.parks (...);
```

---

### Issue: "Failed to load metadata"

**Symptoms**:
- Honua starts but collections are empty
- Logs show metadata parsing errors

**Diagnosis**:
```bash
# Check logs for specific error
docker logs honua-server | grep -i "error.*metadata"

# Check file permissions
ls -la metadata/
```

**Solutions**:

**Permission denied**:
```bash
chmod -R 755 metadata/
chown -R $(whoami) metadata/
```

**Invalid metadata provider**:
```json
{
  "honua": {
    "metadata": {
      "provider": "yaml",  // Should be: yaml, json, or postgres
      "path": "metadata"    // Must exist
    }
  }
}
```

---

### Issue: "Geometry column not found"

**Symptoms**:
```
ERROR: Geometry column 'geom' not found in table 'parks'
```

**Solutions**:

**Wrong column name in metadata**:
```yaml
collections:
  - id: parks
    geometryField: geometry  # Change to match actual column name
```

**Verify actual column name**:
```sql
SELECT column_name, udt_name
FROM information_schema.columns
WHERE table_name = 'parks' AND table_schema = 'public';
```

---

## API Request Issues

### Issue: "404 Not Found" on valid endpoint

**Symptoms**:
- `/ogc` returns 404
- `/rest/services` returns 404

**Diagnosis**:
```bash
# Check what routes are available
curl http://localhost:8080/ | jq '.links'

# Check if service is enabled
docker logs honua-server | grep -i "ogc.*enabled"
```

**Solutions**:

**Service disabled in configuration**:
```json
{
  "honua": {
    "services": {
      "ogcApiFeatures": { "enabled": true },
      "wfs": { "enabled": true },
      "wms": { "enabled": true },
      "geoservices": { "enabled": true }
    }
  }
}
```

**Wrong path**:
```bash
# Correct
http://localhost:8080/ogc/collections

# Wrong
http://localhost:8080/api/ogc/collections
```

---

### Issue: "400 Bad Request" on filtered queries

**Symptoms**:
```
400 Bad Request: Invalid filter expression
```

**Diagnosis**:
```bash
# Test with simple query first
curl "http://localhost:8080/ogc/collections/parks/items"

# Test with bbox
curl "http://localhost:8080/ogc/collections/parks/items?bbox=-180,-90,180,90"
```

**Solutions**:

**Invalid CQL2 syntax**:
```bash
# Wrong
?filter=name=San Francisco

# Correct
?filter=name = 'San Francisco'

# Wrong
?filter=population>100000

# Correct
?filter=population > 100000
```

**URL encoding issues**:
```bash
# Wrong
?filter=name = 'San Francisco'

# Correct (URL encoded)
?filter=name%20%3D%20%27San%20Francisco%27

# Or use curl's --data-urlencode
curl -G "http://localhost:8080/ogc/collections/parks/items" \
  --data-urlencode "filter=name = 'San Francisco'"
```

---

### Issue: Export format not working

**Symptoms**:
- `?f=csv` returns JSON
- `?f=gpkg` returns error

**Solutions**:

**Check supported formats for endpoint**:
```bash
# Check collection metadata
curl "http://localhost:8080/ogc/collections/parks" | jq '.formats'
```

**Use Accept header instead**:
```bash
# Instead of ?f=csv
curl "http://localhost:8080/ogc/collections/parks/items" \
  -H "Accept: text/csv"

# Instead of ?f=gpkg
curl "http://localhost:8080/ogc/collections/parks/items" \
  -H "Accept: application/geopackage+sqlite3"
```

**See**: [Format Matrix](format-matrix.md) for supported formats

---

## Performance Issues

### Issue: Slow query responses

**Symptoms**:
- Queries take > 5 seconds
- Timeout errors

**Diagnosis**:
```bash
# Check query with timing
time curl "http://localhost:8080/ogc/collections/parks/items?limit=1000"

# Check database query performance
psql -h localhost -U postgres -d honua
EXPLAIN ANALYZE SELECT * FROM public.parks LIMIT 1000;
```

**Solutions**:

**Add spatial index**:
```sql
-- Create spatial index
CREATE INDEX idx_parks_geom ON public.parks USING GIST (geom);

-- Analyze table
ANALYZE public.parks;

-- Verify index is used
EXPLAIN SELECT * FROM public.parks WHERE ST_Intersects(geom, ST_MakeEnvelope(-123, 37, -122, 38, 4326));
```

**Enable caching**:
```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "redis": {
        "connectionString": "localhost:6379"
      }
    }
  }
}
```

**Use appropriate limits**:
```bash
# Always limit large datasets
curl "http://localhost:8080/ogc/collections/parks/items?limit=100"

# Use bbox to reduce dataset
curl "http://localhost:8080/ogc/collections/parks/items?bbox=-123,37,-122,38&limit=100"
```

**See**: [PostgreSQL Optimizations](../database/POSTGRESQL_OPTIMIZATIONS.md)

---

## Docker Issues

### Issue: Container keeps restarting

**Symptoms**:
```bash
docker ps  # Shows "Restarting"
```

**Diagnosis**:
```bash
# Check logs
docker logs honua-server

# Check exit code
docker inspect honua-server | jq '.[0].State'
```

**Solutions**:

**Configuration error - fix and restart**:
```bash
# Fix appsettings.json
vim appsettings.json

# Remove container and recreate
docker compose down
docker compose up -d
```

---

### Issue: "Image not found"

**Symptoms**:
```
Error: image 'ghcr.io/honuaio/honua-server:latest' not found
```

**Solutions**:
```bash
# Pull image explicitly
docker pull ghcr.io/honuaio/honua-server:latest

# Or use a specific version
docker pull ghcr.io/honuaio/honua-server:1.0.0
```

---

## Getting More Help

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Honua": "Debug",
      "Microsoft.EntityFrameworkCore": "Debug"
    }
  }
}
```

### Collect Diagnostic Information

```bash
# Version info
curl http://localhost:8080/ | jq '.version'

# Environment
docker inspect honua-server | jq '.[0].Config.Env'

# Logs
docker logs honua-server > honua.log 2>&1

# Database info
psql -h localhost -U postgres -d honua -c "SELECT version();"
psql -h localhost -U postgres -d honua -c "SELECT PostGIS_version();"
```

### Where to Get Help

- **GitHub Discussions**: [Ask questions](https://github.com/honua-io/Honua.Server/discussions)
- **GitHub Issues**: [Report bugs](https://github.com/honua-io/Honua.Server/issues)
- **Operations Guide**: [Advanced troubleshooting](../operations/README.md)
- **Runbooks**: [Incident response procedures](../operations/RUNBOOKS.md)

---

## Common Error Messages

| Error Message | Likely Cause | See Section |
|--------------|--------------|-------------|
| "Failed to connect to database" | Database connection | [Database Issues](#database-connection-issues) |
| "Collection not found" | Metadata missing/invalid | [Metadata Issues](#metadata--configuration-issues) |
| "401 Unauthorized" | Authentication required | [Authentication Issues](#authentication-issues) |
| "Invalid token" | Expired/invalid JWT | [Authentication Issues](#authentication-issues) |
| "404 Not Found" | Wrong URL or service disabled | [API Issues](#api-request-issues) |
| "PostGIS extension not found" | PostGIS not installed | [Database Issues](#database-connection-issues) |
| "Geometry column not found" | Wrong column in metadata | [Metadata Issues](#metadata--configuration-issues) |
| "400 Bad Request" | Invalid filter syntax | [API Issues](#api-request-issues) |

---

**Updated**: 2025-11-09
**Related**: [Getting Started](getting-started.md) | [Operations Guide](../operations/README.md) | [Configuration](configuration.md)
