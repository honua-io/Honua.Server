# Honua Troubleshooting Guide

**Keywords**: troubleshooting, diagnostics, errors, debugging, connection issues, performance problems, OGC API errors, PostGIS errors, authentication failures

**Related Topics**: [Performance Tuning](performance-tuning.md), [Environment Variables](../01-configuration/environment-variables.md), [Docker Deployment](../02-deployment/docker-deployment.md)

---

## Overview

This guide provides comprehensive troubleshooting workflows for common Honua deployment and operational issues. Each section includes diagnostic steps, root cause analysis, and production-tested solutions based on Honua's actual implementation.

---

## Table of Contents

1. [Quick Diagnostic Checklist](#quick-diagnostic-checklist)
2. [Database Connection Issues](#database-connection-issues)
3. [OGC API Endpoint Errors](#ogc-api-endpoint-errors)
4. [Performance and Timeout Problems](#performance-and-timeout-problems)
5. [Authentication and Authorization Failures](#authentication-and-authorization-failures)
6. [Memory and Resource Exhaustion](#memory-and-resource-exhaustion)
7. [Metadata Loading Errors](#metadata-loading-errors)
8. [Log Analysis Techniques](#log-analysis-techniques)
9. [Production Monitoring](#production-monitoring)

---

## Quick Diagnostic Checklist

Run these checks first when experiencing any Honua issue:

```bash
# 1. Verify server is running
docker ps | grep honua
# OR
systemctl status honua

# 2. Check basic endpoint connectivity
curl http://localhost:5000/

# 3. Test database connectivity
docker exec honua-postgis psql -U honua_user -d honua -c "SELECT PostGIS_version();"

# 4. Verify metadata provider configuration
echo $HONUA__METADATA__PROVIDER
echo $HONUA__METADATA__PATH

# 5. Check application logs for errors
docker logs honua-server --tail 50
# OR
journalctl -u honua -n 50 --no-pager

# 6. Verify collections are loading
curl http://localhost:5000/collections
```

**Common Quick Fixes**:
- Server not starting → Check logs for configuration errors
- 404 on all endpoints → Metadata provider not configured
- 500 errors → Database connection failure
- Slow responses → Missing spatial indexes

---

## Database Connection Issues

### Symptom: "Connection refused" or "Could not connect to database"

**Diagnostic Steps**:

```bash
# 1. Verify database container is running
docker ps | grep postgis

# 2. Test direct database connection
psql -h localhost -p 5432 -U honua_user -d honua

# 3. Check database logs
docker logs honua-postgis --tail 50

# 4. Verify connection string
echo $HONUA__DATABASE__CONNECTIONSTRING

# 5. Test network connectivity
telnet localhost 5432
```

**Common Causes and Solutions**:

| Cause | Solution | Honua Configuration |
|-------|----------|-------------------|
| Database not running | `docker start honua-postgis` OR `systemctl start postgresql` | - |
| Wrong host/port | Update connection string | `HONUA__DATABASE__CONNECTIONSTRING="Host=postgis;Port=5432;..."` |
| Incorrect credentials | Fix username/password | `HONUA__DATABASE__CONNECTIONSTRING="...Username=honua_user;Password=***"` |
| Firewall blocking | `sudo ufw allow 5432` | - |
| Connection pool exhausted | Increase MaxPoolSize | `HONUA__DATABASE__CONNECTIONSTRING="...MaxPoolSize=100;Pooling=true"` |
| SSL/TLS mismatch | Add SSL mode to connection string | `HONUA__DATABASE__CONNECTIONSTRING="...SSL Mode=Require"` or `SSL Mode=Disable` |

**Production Example** (Docker Compose):

```yaml
services:
  postgis:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_USER: honua_user
      POSTGRES_PASSWORD: ${DB_PASSWORD}  # From secrets
      POSTGRES_DB: honua
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua_user -d honua"]
      interval: 10s
      timeout: 5s
      retries: 5

  honua:
    image: honua:latest
    depends_on:
      postgis:
        condition: service_healthy
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;Database=honua;Username=honua_user;Password=${DB_PASSWORD};Pooling=true;MaxPoolSize=100"
```

---

## OGC API Endpoint Errors

### Landing Page (/) Issues

**Symptom**: 404 or empty response on `GET /`

```bash
# Diagnostic command
curl -v http://localhost:5000/
```

**Common Issues**:

1. **404 Not Found**
   - **Cause**: Routing middleware not configured
   - **Solution**: Verify `app.MapControllers()` is called in `Program.cs`
   - **Check**: Review Honua server startup logs

2. **Missing Links in Response**
   - **Cause**: Metadata provider not loaded
   - **Solution**: Verify metadata configuration:
   ```bash
   export HONUA__METADATA__PROVIDER=yaml
   export HONUA__METADATA__PATH=/app/config/metadata.yaml
   ```

3. **CORS Error** (in browser)
   - **Cause**: CORS policy blocking origin
   - **Solution**: Configure CORS in `appsettings.json`:
   ```json
   {
     "honua": {
       "cors": {
         "allowedOrigins": ["https://example.com"],
         "allowCredentials": true
       }
     }
   }
   ```

### Collections Endpoint (/collections) Issues

**Symptom**: Empty array or missing collections

```bash
# Diagnostic commands
curl http://localhost:5000/collections

# Check metadata file exists
ls -lh $HONUA__METADATA__PATH

# Validate metadata schema
honua metadata validate
```

**Common Issues**:

1. **Empty Collections Array**
   - **Cause**: No metadata configured
   - **Diagnosis**:
   ```bash
   # Check metadata provider
   echo $HONUA__METADATA__PROVIDER  # Should be "json", "yaml", or "database"

   # For JSON/YAML providers, verify file exists
   cat $HONUA__METADATA__PATH
   ```
   - **Solution**:
   ```bash
   # Set metadata provider
   export HONUA__METADATA__PROVIDER=yaml
   export HONUA__METADATA__PATH=/app/config/metadata.yaml

   # Ensure enabled: true in collection definitions
   ```

2. **Specific Collections Missing**
   - **Cause**: Collection disabled in metadata
   - **Solution**: Edit metadata file, set `enabled: true`:
   ```yaml
   collections:
     - id: parcels
       enabled: true  # Must be true
       title: "Parcel Boundaries"
   ```

3. **500 Internal Server Error**
   - **Cause**: Database connection failure
   - **Diagnosis**: Check logs for database errors
   - **Solution**: See [Database Connection Issues](#database-connection-issues)

### Items Endpoint (/collections/{id}/items) Issues

**Symptom**: 404, empty features, or slow responses

```bash
# Diagnostic commands
curl http://localhost:5000/collections/parcels/items?limit=10

# Verify collection exists in metadata
curl http://localhost:5000/collections | jq '.collections[].id'

# Check table exists in database
psql -h localhost -U honua_user -d honua -c "\dt parcels"

# Count features
psql -h localhost -U honua_user -d honua -c "SELECT COUNT(*) FROM parcels;"

# Check for spatial index
psql -h localhost -U honua_user -d honua -c "\d+ parcels"
```

**Common Issues**:

1. **404 Collection Not Found**
   - **Cause**: Collection ID not in metadata
   - **Solution**: Add collection to metadata.yaml:
   ```yaml
   collections:
     - id: parcels
       enabled: true
       title: "Parcel Boundaries"
       source:
         connectionName: default
         tableName: parcels
         geometryColumn: geom
   ```

2. **Empty Features Array**
   - **Cause**: Wrong table name or no data
   - **Diagnosis**:
   ```sql
   -- Verify table exists
   SELECT tablename FROM pg_tables WHERE schemaname = 'public';

   -- Check data exists
   SELECT COUNT(*) FROM parcels;
   ```
   - **Solution**: Fix `tableName` in metadata or import data

3. **Invalid Geometries (500 Error)**
   - **Cause**: Corrupt geometries in database
   - **Diagnosis**:
   ```sql
   -- Find invalid geometries
   SELECT gid, ST_IsValidReason(geom)
   FROM parcels
   WHERE NOT ST_IsValid(geom)
   LIMIT 10;
   ```
   - **Solution**: Fix geometries:
   ```sql
   -- Repair invalid geometries
   UPDATE parcels
   SET geom = ST_MakeValid(geom)
   WHERE NOT ST_IsValid(geom);

   -- Vacuum and analyze
   VACUUM ANALYZE parcels;
   ```

4. **Extremely Slow Response (>5 seconds)**
   - **Cause**: Missing spatial index
   - **Diagnosis**:
   ```sql
   -- Check for spatial index
   SELECT indexname, indexdef
   FROM pg_indexes
   WHERE tablename = 'parcels'
   AND indexdef LIKE '%GIST%';
   ```
   - **Solution**: Create spatial index:
   ```sql
   CREATE INDEX CONCURRENTLY idx_parcels_geom
   ON parcels USING GIST(geom);

   VACUUM ANALYZE parcels;
   ```

### Conformance Endpoint (/conformance) Issues

**Symptom**: Missing conformance classes

```bash
# Check conformance response
curl http://localhost:5000/conformance | jq '.conformsTo'
```

**Expected Conformance Classes**:
```json
{
  "conformsTo": [
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
    "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30"
  ]
}
```

**If Missing**:
- **Cause**: OGC implementation incomplete or routing issue
- **Solution**: Ensure using latest Honua version, check server logs

---

## Performance and Timeout Problems

### Symptom: Slow Queries (>2 seconds) or Timeouts

**Diagnostic Workflow**:

```bash
# 1. Identify slow endpoints (from logs)
grep "took [0-9]\{4,\}ms" /var/log/honua/app.log

# 2. Check for spatial indexes
psql -U honua_user -d honua <<EOF
SELECT
  t.tablename,
  COUNT(i.indexname) FILTER (WHERE i.indexdef LIKE '%GIST%') as spatial_indexes
FROM pg_tables t
LEFT JOIN pg_indexes i ON t.tablename = i.tablename
WHERE t.schemaname = 'public'
GROUP BY t.tablename;
EOF

# 3. Check database performance
psql -U honua_user -d honua -c "SELECT COUNT(*) FROM pg_stat_activity;"

# 4. Monitor query execution
psql -U honua_user -d honua <<EOF
SELECT pid, usename, state, query, query_start
FROM pg_stat_activity
WHERE state = 'active'
AND query NOT LIKE '%pg_stat_activity%'
ORDER BY query_start;
EOF
```

**Common Performance Issues**:

| Issue | Diagnostic | Solution | Honua Configuration |
|-------|-----------|----------|-------------------|
| **Missing Spatial Index** | No GIST index on geometry column | `CREATE INDEX CONCURRENTLY idx_geom ON table USING GIST(geom);` | - |
| **Large Result Sets** | Query returning >10,000 features | Add pagination: `?limit=100` | `HONUA__ODATA__MAXPAGESIZE=1000` |
| **Complex Geometries** | `SELECT AVG(ST_NPoints(geom))` returns >1000 | Simplify: `ST_SimplifyPreserveTopology(geom, 0.0001)` | Store simplified column |
| **Connection Pool Exhaustion** | `SELECT count(*) FROM pg_stat_activity` near max | Increase pool size | `HONUA__DATABASE__CONNECTIONSTRING="...MaxPoolSize=100"` |
| **Unbounded Queries** | Missing WHERE clause | Configure default limits | `HONUA__ODATA__DEFAULTPAGESIZE=100` |

**Performance Optimization Script**:

```sql
-- Run this on your Honua database for performance baseline

-- 1. Check table sizes
SELECT
  schemaname,
  tablename,
  pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size,
  pg_total_relation_size(schemaname||'.'||tablename) AS size_bytes
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY size_bytes DESC;

-- 2. Check for missing spatial indexes
SELECT
  t.tablename,
  a.attname as geom_column,
  EXISTS(
    SELECT 1 FROM pg_index idx
    JOIN pg_class c ON c.oid = idx.indexrelid
    JOIN pg_am am ON am.oid = c.relam
    WHERE idx.indrelid = (quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass
    AND a.attnum = ANY(idx.indkey)
    AND am.amname = 'gist'
  ) as has_spatial_index
FROM pg_tables t
JOIN pg_attribute a ON a.attrelid = (quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass
WHERE t.schemaname = 'public'
AND a.atttypid = 'geometry'::regtype
ORDER BY t.tablename;

-- 3. Check for bloat (needs VACUUM)
SELECT
  schemaname,
  tablename,
  n_dead_tup,
  n_live_tup,
  ROUND(n_dead_tup::numeric / NULLIF(n_live_tup, 0) * 100, 2) as dead_pct
FROM pg_stat_user_tables
WHERE n_dead_tup > 1000
ORDER BY dead_pct DESC NULLS LAST;

-- 4. Identify slow queries (if pg_stat_statements enabled)
SELECT
  query,
  calls,
  total_exec_time,
  mean_exec_time,
  max_exec_time
FROM pg_stat_statements
WHERE query NOT LIKE '%pg_stat%'
ORDER BY mean_exec_time DESC
LIMIT 10;
```

---

## Authentication and Authorization Failures

### Symptom: 401 Unauthorized or 403 Forbidden

**Diagnostic Steps**:

```bash
# 1. Check authentication mode
echo $HONUA__AUTHENTICATION__MODE  # Should be "Local", "Oidc", or "QuickStart"

# 2. Test without authentication (for diagnosis only)
export HONUA__AUTHENTICATION__MODE=QuickStart
export HONUA__AUTHENTICATION__ENFORCE=false

# 3. Validate JWT token (if using Local or Oidc)
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:5000/collections

# 4. Check user roles
honua auth list-users

# 5. Review authentication logs
grep -i "authentication\|authorization" /var/log/honua/app.log
```

**Authentication Modes in Honua**:

Honua supports three authentication modes (configured via `HONUA__AUTHENTICATION__MODE`):

1. **QuickStart** (Development Only)
   - No authentication required
   - Set `HONUA__AUTHENTICATION__MODE=QuickStart`
   - **Never use in production**

2. **Local** (Built-in Authentication)
   - Honua-managed users and JWT tokens
   - Configuration:
   ```bash
   HONUA__AUTHENTICATION__MODE=Local
   HONUA__AUTHENTICATION__ENFORCE=true
   ```
   - User management:
   ```bash
   # Create admin user
   honua auth create-user --username admin --password *** --role administrator

   # Create data publisher
   honua auth create-user --username publisher --password *** --role datapublisher

   # Get token
   honua auth login --username admin --password ***
   ```

3. **Oidc** (OAuth/OpenID Connect)
   - External identity provider (Azure AD, Okta, Auth0)
   - See [OAuth Setup Guide](../01-configuration/oauth-setup.md)

**Common Authorization Issues**:

| Error | Cause | Solution |
|-------|-------|----------|
| 401 Unauthorized | Missing or invalid token | Refresh token: `honua auth login` |
| 401 Token Expired | JWT expired (Honua Local tokens expire after 1 hour) | Re-login to get new token |
| 403 Forbidden | User lacks required role | `honua auth assign-role --user user1 --role datapublisher` |
| 401 with OIDC | Invalid OIDC configuration | Verify `HONUA__AUTHENTICATION__JWT__AUTHORITY` and `AUDIENCE` |
| CORS Error | Origin not allowed | Add origin to `HONUA__CORS__ALLOWEDORIGINS` |

**Token Validation Example**:

```bash
# Decode JWT token (without validation)
echo "YOUR_TOKEN" | base64 -d | jq .

# Expected claims for Honua Local mode:
# {
#   "sub": "username",
#   "role": "administrator",
#   "aud": "honua-api",
#   "iss": "honua-local",
#   "exp": 1735689600
# }

# Test authenticated request
TOKEN=$(honua auth login --username admin --password *** --json | jq -r .token)
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/collections
```

---

## Memory and Resource Exhaustion

### Symptom: OutOfMemoryException, Container Restart, or Slow Performance

**Diagnostic Commands**:

```bash
# 1. Monitor container memory usage
docker stats honua-server --no-stream

# 2. Check system memory
free -h

# 3. Monitor Honua process
top -p $(pgrep -f honua)

# 4. Check for memory leaks (heap dump analysis)
dotnet-dump collect -p $(pgrep -f honua)
dotnet-dump analyze core_dump
> dumpheap -stat

# 5. Review cache size (if using Redis)
redis-cli info memory
```

**Common Causes**:

1. **Large Raster Tile Generation**
   - **Symptom**: Memory spikes during tile requests
   - **Solution**: Configure tile cache and limits:
   ```bash
   HONUA__SERVICES__RASTERTILES__ENABLED=true
   HONUA__SERVICES__RASTERTILES__PROVIDER=s3
   HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles
   HONUA__SERVICES__RASTERTILES__CACHESIZEMB=512  # Limit in-memory cache
   ```

2. **Unbounded Result Sets**
   - **Symptom**: Memory grows with large queries
   - **Solution**: Configure pagination limits:
   ```bash
   HONUA__ODATA__ENABLED=true
   HONUA__ODATA__MAXPAGESIZE=1000  # Hard limit
   HONUA__ODATA__DEFAULTPAGESIZE=100
   ```

3. **Memory Cache Exhaustion**
   - **Symptom**: High memory usage in long-running processes
   - **Solution**: Configure cache eviction:
   ```json
   {
     "honua": {
       "caching": {
         "memoryCacheSizeLimitMb": 512,
         "expirationMinutes": 30
       }
     }
   }
   ```

4. **Too Many Database Connections**
   - **Symptom**: Memory grows with connection count
   - **Solution**: Tune connection pool:
   ```bash
   HONUA__DATABASE__CONNECTIONSTRING="Host=postgis;...MaxPoolSize=20;MinPoolSize=5"
   ```

**Resource Limits (Docker)**:

```yaml
services:
  honua:
    image: honua:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
```

**Resource Limits (Kubernetes)**:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  template:
    spec:
      containers:
      - name: honua
        image: honua:latest
        resources:
          requests:
            memory: "1Gi"
            cpu: "1000m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
```

---

## Metadata Loading Errors

### Symptom: "Failed to load metadata" or Empty Collections

**Diagnostic Workflow**:

```bash
# 1. Verify metadata provider is set
echo $HONUA__METADATA__PROVIDER  # Should be "json", "yaml", or "database"

# 2. Check metadata file exists (for JSON/YAML providers)
ls -lh $HONUA__METADATA__PATH

# 3. Validate metadata syntax
# For YAML:
yamllint $HONUA__METADATA__PATH

# For JSON:
jq . $HONUA__METADATA__PATH

# 4. Use Honua's built-in validator
honua metadata validate --path $HONUA__METADATA__PATH

# 5. Check server startup logs
docker logs honua-server 2>&1 | grep -i metadata
```

**Common Metadata Errors**:

1. **Provider Not Set**
   - **Error**: "Configuration missing metadata provider"
   - **Solution**:
   ```bash
   export HONUA__METADATA__PROVIDER=yaml
   export HONUA__METADATA__PATH=/app/config/metadata.yaml
   ```

2. **File Not Found**
   - **Error**: "Metadata file not found at /app/config/metadata.yaml"
   - **Solution**: Verify file path and Docker volume mount:
   ```yaml
   services:
     honua:
       volumes:
         - ./config/metadata.yaml:/app/config/metadata.yaml:ro
   ```

3. **Invalid YAML Syntax**
   - **Error**: "YAML parse error at line 42"
   - **Diagnosis**: Use YAML linter
   - **Common Issues**:
     - Incorrect indentation (use 2 spaces, not tabs)
     - Missing colons
     - Unquoted special characters

4. **Schema Validation Failure**
   - **Error**: "Collection 'parcels' missing required field 'extent'"
   - **Solution**: Add required fields:
   ```yaml
   collections:
     - id: parcels
       title: "Parcel Boundaries"
       extent:
         spatial:
           bbox: [[-180, -90, 180, 90]]
           crs: "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
       source:
         connectionName: default
         tableName: parcels
         geometryColumn: geom
   ```

5. **Database Metadata Provider Errors**
   - **Error**: "Failed to load metadata from database"
   - **Diagnosis**: Check database connectivity and metadata schema
   - **Solution**:
   ```bash
   # Verify metadata tables exist
   psql -U honua_user -d honua -c "\dt *metadata*"

   # Test metadata query
   SELECT * FROM metadata_collections LIMIT 1;
   ```

---

## Log Analysis Techniques

### Structured Log Parsing

Honua supports structured JSON logging for production environments.

**Enable JSON Logging** (`appsettings.json`):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Honua": "Debug"
    },
    "Console": {
      "FormatterName": "json"
    }
  }
}
```

**Parse JSON Logs with jq**:

```bash
# Extract all errors
cat honua.log | jq 'select(.level == "ERROR")'

# Count unique error messages
cat honua.log | jq -r 'select(.level == "ERROR") | .message' | sort | uniq -c | sort -rn

# Find slow requests (>1 second)
cat honua.log | jq 'select(.duration > 1000)'

# Filter by timestamp
cat honua.log | jq 'select(.timestamp | startswith("2025-10-04"))'

# Extract stack traces
cat honua.log | jq 'select(.exception != null) | .exception'
```

### Log Aggregation for Production

**Recommended Tools**:
- **Elasticsearch + Kibana** (ELK Stack)
- **Grafana Loki** (lightweight, cost-effective)
- **AWS CloudWatch Logs** (if on AWS)
- **Azure Log Analytics** (if on Azure)

**Docker Logging Driver Example**:

```yaml
services:
  honua:
    image: honua:latest
    logging:
      driver: "json-file"
      options:
        max-size: "50m"
        max-file: "5"
        labels: "service=honua,env=production"
```

**Log Correlation with Request IDs**:

Honua automatically adds correlation IDs to logs. Use these to trace requests:

```bash
# Find all logs for a specific request
CORRELATION_ID="abc123def456"
cat honua.log | jq "select(.correlationId == \"$CORRELATION_ID\")"
```

### Common Log Patterns to Monitor

| Pattern | Severity | Action |
|---------|----------|--------|
| `"level":"ERROR"` | Critical | Immediate investigation |
| `"exception"` | High | Review stack trace, identify root cause |
| `took [0-9]{4,}ms` | Medium | Performance optimization needed |
| `401\|403` | Medium | Authentication/authorization review |
| `connection.*timeout` | High | Database or network issue |
| `OutOfMemoryException` | Critical | Scale up or optimize memory usage |

---

## Production Monitoring

### Health Checks

Honua exposes health check endpoints for Kubernetes and Docker:

```bash
# Liveness probe (is server alive?)
curl http://localhost:5000/health/live

# Readiness probe (is server ready to serve traffic?)
curl http://localhost:5000/health/ready

# Full health status
curl http://localhost:5000/health
```

**Kubernetes Health Probes**:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  template:
    spec:
      containers:
      - name: honua
        livenessProbe:
          httpGet:
            path: /health/live
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3
```

### Metrics Collection

Honua supports Prometheus metrics:

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

**Key Metrics to Monitor**:

- `http_server_requests_total` - Request count
- `http_server_request_duration_seconds` - Request latency
- `process_working_set_bytes` - Memory usage
- `process_cpu_seconds_total` - CPU usage
- `dotnet_gc_collections_total` - Garbage collection pressure

### Alerting Rules

**Prometheus Alert Examples**:

```yaml
groups:
  - name: honua_alerts
    interval: 30s
    rules:
      - alert: HonuaDatabaseDown
        expr: honua_database_health_status == 0
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Honua cannot connect to database"

      - alert: HonuaHighErrorRate
        expr: rate(http_server_requests_total{status=~"5.."}[5m]) > 0.05
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Honua error rate exceeds 5%"

      - alert: HonuaSlowQueries
        expr: histogram_quantile(0.95, http_server_request_duration_seconds) > 1
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "95th percentile latency exceeds 1 second"

      - alert: HonuaHighMemory
        expr: process_working_set_bytes / (1024^3) > 1.5
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Honua memory usage exceeds 1.5GB"
```

---

## Advanced Troubleshooting

### Enable Debug Logging

Temporarily enable debug logging for troubleshooting:

**appsettings.Development.json**:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Honua": "Trace",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Environment Variable Override**:

```bash
export LOGGING__LOGLEVEL__DEFAULT=Debug
export LOGGING__LOGLEVEL__HONUA=Trace
```

### Distributed Tracing

For microservice deployments, enable OpenTelemetry tracing:

```json
{
  "honua": {
    "observability": {
      "tracing": {
        "enabled": true,
        "exporter": "jaeger",
        "endpoint": "http://jaeger:14268/api/traces"
      }
    }
  }
}
```

### Database Query Logging

Log all SQL queries for debugging:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

This will output SQL queries to logs:

```
[2025-10-04 10:15:32] Executed DbCommand (47ms) [Parameters=[@__limit_0='100'], CommandType='Text', CommandTimeout='30']
SELECT ST_AsGeoJSON(geom) as geometry, * FROM parcels LIMIT @__limit_0
```

---

## Getting Help

If you've exhausted this troubleshooting guide:

1. **Collect Diagnostic Information**:
   ```bash
   # Generate comprehensive diagnostic report
   honua diagnostic collect-info --output diagnostic-report.txt
   ```

2. **Search Honua Issues**: https://github.com/honua/honua/issues

3. **Ask in Community Forum**: Include diagnostic report and logs

4. **Contact Support**: For enterprise customers

---

**Last Updated**: 2025-10-04
**Honua Version**: 1.0+
**Related Documentation**: [Performance Tuning](performance-tuning.md), [OAuth Setup](../01-configuration/oauth-setup.md)
