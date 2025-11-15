# Configuration V2 (HCL) Reference Guide

## Overview

Honua.Server uses **Configuration V2** with HCL (HashiCorp Configuration Language) for declarative, infrastructure-as-code style configuration. This provides:

- **Version control friendly** (readable diffs, merge-friendly)
- **Type safety** (validated at load time)
- **Environment variable interpolation** (`env("VAR_NAME")`)
- **References** (data_source.primary, layer.features, etc.)
- **Comments** (inline `#` and block `/* */`)

---

## File Discovery

Honua.Server automatically discovers configuration files in this order:

1. **Environment variable**: `HONUA_CONFIG_PATH=/path/to/config.hcl`
2. **Environment-specific**: `honua.{environment}.hcl` (e.g., `honua.production.hcl`)
3. **Common names**:
   - `honua.config.hcl`
   - `honua.hcl`
   - `honua.honua` (alternative extension)

**Example**:
```bash
# Development
ASPNETCORE_ENVIRONMENT=Development dotnet run
# Loads: honua.development.hcl (if exists) or honua.config.hcl

# Production
ASPNETCORE_ENVIRONMENT=Production dotnet run
# Loads: honua.production.hcl (if exists) or honua.config.hcl
```

---

## Configuration V2 Blocks (HCL)

### 1. `honua` - Global Settings

**Full configuration**:
```hcl
honua {
  version     = "1.0"              # Config schema version
  environment = "production"       # Environment name
  log_level   = "warning"          # trace|debug|information|warning|error|critical

  allowed_hosts = ["*.example.com", "example.com"]  # Host header validation

  cors {
    allowed_origins   = ["https://app.example.com", "https://admin.example.com"]
    allow_credentials = true       # Allow cookies/auth headers
  }

  # High availability settings (Tier 2+)
  high_availability {
    enabled = true

    leader_election {
      enabled                  = true
      resource_name            = "honua-server"     # Unique name for this service
      lease_duration_seconds   = 30                 # How long leader lease lasts
      renewal_interval_seconds = 10                 # How often to renew
      key_prefix               = "honua:leader:"    # Redis key prefix
      enable_detailed_logging  = false              # Debug logging
    }
  }
}
```

**Minimal**:
```hcl
honua {
  version     = "1.0"
  environment = "development"
}
```

---

### 2. `data_source` - Database Connections

**PostgreSQL with PostGIS** (recommended):
```hcl
data_source "primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")  # Environment variable interpolation

  pool {
    min_size = 2
    max_size = 120    # 15 per CPU core recommended for production
    timeout  = 30     # Connection timeout in seconds
  }

  health_check = "SELECT 1"  # Optional health check query
}
```

**Read replica**:
```hcl
data_source "replica_1" {
  provider   = "postgresql"
  connection = env("DATABASE_REPLICA_URL")

  pool {
    min_size = 2
    max_size = 120
    timeout  = 30
  }

  settings = {
    read_only = true  # Mark as read-only replica
  }
}
```

**Other providers**:
```hcl
# SQLite (development)
data_source "sqlite" {
  provider   = "sqlite"
  connection = "Data Source=/data/honua.db"
}

# SQL Server
data_source "sqlserver" {
  provider   = "sqlserver"
  connection = env("SQLSERVER_CONNECTION")
  pool {
    min_size = 1
    max_size = 50
  }
}

# MySQL
data_source "mysql" {
  provider   = "mysql"
  connection = env("MYSQL_CONNECTION")
}
```

---

### 3. `cache` - Caching Configuration

**In-memory cache** (Tier 1, development):
```hcl
cache "memory" {
  type    = "memory"
  enabled = true
}
```

**Redis cache** (Tier 2+, production):
```hcl
cache "redis" {
  type       = "redis"
  enabled    = true
  connection = env("REDIS_URL")  # "redis-host:6379" or "redis://..."

  # Require Redis in certain environments
  required_in = ["production", "staging"]

  # Optional settings
  settings = {
    cluster_mode          = false  # true for Redis Cluster
    ssl                   = false  # true for TLS/SSL
    connect_timeout       = 5000   # milliseconds
    sync_timeout          = 5000
    abort_on_connect_fail = false  # Continue if Redis unavailable
  }
}
```

**CDN cache** (Tier 3, enterprise):
```hcl
cache "cdn" {
  type    = "cdn"
  enabled = true

  settings = {
    provider      = "cloudfront"  # cloudfront|fastly|akamai
    distribution  = env("CDN_DISTRIBUTION_ID")
    max_age       = 86400         # Browser cache: 1 day
    s_max_age     = 2592000       # Edge cache: 30 days
  }
}
```

---

### 4. `service` - Service Definitions

**OGC API - Features**:
```hcl
service "ogc_api" {
  enabled   = true
  base_path = "/ogc"

  settings = {
    max_page_size      = 1000
    default_page_size  = 100
    enable_caching     = true
    cache_ttl          = 300      # 5 minutes
    allow_transactions = true     # Enable POST/PUT/DELETE
  }
}
```

**WFS (Web Feature Service)**:
```hcl
service "wfs" {
  enabled = true

  settings = {
    lock_manager = "redis"           # "InMemory" (Tier 1) or "redis" (Tier 2+)
    lock_timeout = "00:05:00"        # TimeSpan format: 5 minutes
    max_features = 10000             # Max features per request
  }
}
```

**WMS (Web Map Service)**:
```hcl
service "wms" {
  enabled = true

  settings = {
    max_width  = 4096
    max_height = 4096
    formats    = ["image/png", "image/jpeg", "image/webp"]
  }
}
```

**WMTS (Web Map Tile Service)**:
```hcl
service "wmts" {
  enabled = true

  settings = {
    tile_size       = 256
    tile_formats    = ["image/png", "image/webp"]
    enable_caching  = true
    cache_ttl       = 3600  # 1 hour
  }
}
```

**STAC (SpatioTemporal Asset Catalog)**:
```hcl
service "stac" {
  enabled           = true
  provider          = "postgresql"  # postgresql|sqlite|mongodb
  connection_string = env("STAC_DATABASE_URL")

  settings = {
    enable_caching = true
    cache_ttl      = 600  # 10 minutes
  }
}
```

**Geoservices REST API** (ArcGIS-compatible):
```hcl
service "geoservices" {
  enabled = true

  settings = {
    base_path = "/rest/services"
    enable_metadata = true
  }
}
```

---

### 5. `layer` - Layer Definitions

**Basic layer**:
```hcl
layer "roads" {
  title        = "Road Network"
  description  = "Primary and secondary roads"
  data_source  = data_source.primary  # Reference to data source
  table        = "roads"
  id_field     = "road_id"
  display_field = "name"

  geometry {
    column = "geom"
    type   = "LineString"
    srid   = 4326  # WGS84
  }

  introspect_fields = true  # Auto-discover fields from database

  services = ["ogc_api", "wfs", "wms", "wmts", "geoservices"]
}
```

**Layer with explicit fields** (no introspection):
```hcl
layer "buildings" {
  title       = "Buildings"
  data_source = data_source.primary
  table       = "buildings"
  id_field    = "building_id"

  geometry {
    column = "geom"
    type   = "Polygon"
    srid   = 4326
  }

  introspect_fields = false  # Manually define fields

  fields = {
    building_id = { type = "int",      nullable = false }
    name        = { type = "string",   nullable = true  }
    height      = { type = "double",   nullable = true  }
    built_year  = { type = "int",      nullable = true  }
    occupied    = { type = "bool",     nullable = true  }
    updated_at  = { type = "datetime", nullable = false }
  }

  services = ["ogc_api", "wfs"]
}
```

**Layer using read replica**:
```hcl
layer "observations" {
  title       = "Sensor Observations"
  data_source = data_source.replica_1  # Route to read replica
  table       = "observations"
  id_field    = "observation_id"

  geometry {
    column = "location"
    type   = "Point"
    srid   = 4326
  }

  introspect_fields = true
  services = ["ogc_api", "stac"]
}
```

---

### 6. `rate_limit` - Rate Limiting

```hcl
rate_limit {
  enabled = true
  store   = "redis"  # "memory" (Tier 1) or "redis" (Tier 2+)

  rules = {
    # Default rule for anonymous users
    default = {
      requests = 1000
      window   = "1m"  # 1 minute
    }

    # Authenticated users get higher limits
    authenticated = {
      requests = 5000
      window   = "1m"
    }

    # Specific endpoint limits
    tile_generation = {
      requests = 10000
      window   = "1m"
    }

    bulk_operations = {
      requests = 100
      window   = "1h"  # 1 hour
    }
  }
}
```

**Window formats**: `1s`, `30s`, `1m`, `5m`, `1h`, `24h`, `1d`

---

### 7. `variable` - Reusable Variables

```hcl
variable "database_host" {
  default = "localhost"
}

variable "pool_size" {
  default = 50
}

data_source "primary" {
  provider   = "postgresql"
  connection = "Host=${var.database_host};Database=honua"
  pool {
    max_size = var.pool_size
  }
}
```

---

## Configuration Not Yet in HCL

Some features still require **environment variables** or **appsettings.json** until Configuration V2 support is added:

### Background Jobs Configuration

**Environment variables**:
```bash
# Mode: Polling (Tier 1-2) or MessageQueue (Tier 3)
BACKGROUND_JOBS__MODE=Polling
BACKGROUND_JOBS__POLLING_INTERVAL=00:00:05

# Message queue (Tier 3)
BACKGROUND_JOBS__MODE=MessageQueue
BACKGROUND_JOBS__PROVIDER=AwsSqs
BACKGROUND_JOBS__QUEUE__URL=https://sqs.us-east-1.amazonaws.com/123/honua-jobs
BACKGROUND_JOBS__QUEUE__MAX_CONCURRENCY=10
BACKGROUND_JOBS__ENABLE_IDEMPOTENCY=true
```

**Or appsettings.json**:
```json
{
  "BackgroundJobs": {
    "Mode": "Polling",
    "PollingInterval": "00:00:05",
    "LeaderElection": {
      "Enabled": true,
      "Provider": "Redis"
    }
  }
}
```

---

### Database Advanced Features

```bash
# Read replica routing
DATABASE__ENABLE_READ_REPLICA_ROUTING=true
DATABASE__READ_REPLICA_OPERATIONS=Features,Observations,Tiles
DATABASE__FALLBACK_TO_PRIMARY=true

# Connection pool auto-scaling
DATABASE__CONNECTION_POOL__AUTO_SCALE=true
DATABASE__CONNECTION_POOL__SCALE_FACTOR=15  # 15 per CPU core
```

---

### Resilience Features

```bash
# Load shedding
RESILIENCE__LOAD_SHEDDING__ENABLED=true
RESILIENCE__LOAD_SHEDDING__CPU_THRESHOLD=0.90
RESILIENCE__LOAD_SHEDDING__QUEUE_THRESHOLD=1000

# Graceful degradation
RESILIENCE__DEGRADATION__ENABLED=true
RESILIENCE__DEGRADATION__READ_ONLY_MODE=true
```

---

### SRE Features (SLIs/SLOs/Error Budgets)

```bash
# Enable SRE features
SRE__ENABLED=true

# SLO configuration
SRE__SLOS__LATENCY_SLO__ENABLED=true
SRE__SLOS__LATENCY_SLO__TARGET=0.99
SRE__SLOS__LATENCY_SLO__THRESHOLD_MS=500
SRE__SLOS__LATENCY_SLO__MEASUREMENT_WINDOW=28.00:00:00

SRE__SLOS__AVAILABILITY_SLO__ENABLED=true
SRE__SLOS__AVAILABILITY_SLO__TARGET=0.999

# Error budget
SRE__ERROR_BUDGET__ENABLED=true
SRE__ERROR_BUDGET__ALERT_WEBHOOK=https://alerts.example.com/honua
SRE__ERROR_BUDGET__DEPLOYMENT_GATING=true
SRE__ERROR_BUDGET__MINIMUM_BUDGET_REMAINING=0.25
```

---

## Environment Variable Precedence

Configuration sources are applied in this order (later sources override earlier):

1. **HCL configuration file** (`honua.config.hcl`)
2. **appsettings.json**
3. **appsettings.{Environment}.json**
4. **Environment variables**
5. **Command-line arguments**

**Example**:
```hcl
# honua.config.hcl
data_source "primary" {
  connection = env("DATABASE_URL")  # Reads from environment variable
  pool {
    max_size = 50  # Can be overridden by env var: DATABASE__POOL__MAX_SIZE=120
  }
}
```

---

## Best Practices

### 1. **Use Environment Variables for Secrets**

❌ **Bad** (secrets in config file):
```hcl
data_source "primary" {
  connection = "Host=db.example.com;Username=admin;Password=secret123"
}
```

✅ **Good** (secrets in environment):
```hcl
data_source "primary" {
  connection = env("DATABASE_URL")  # Set in environment or .env file
}
```

### 2. **Separate Configs by Environment**

```
honua.development.hcl   # Development settings (SQLite, in-memory cache)
honua.staging.hcl       # Staging (PostgreSQL, Redis)
honua.production.hcl    # Production (HA, read replicas, monitoring)
```

### 3. **Use References for DRY Configuration**

✅ **Good**:
```hcl
data_source "primary" {
  provider = "postgresql"
  connection = env("DATABASE_URL")
}

layer "roads" {
  data_source = data_source.primary  # Reference, not duplication
  table = "roads"
  # ...
}

layer "buildings" {
  data_source = data_source.primary  # Same reference
  table = "buildings"
  # ...
}
```

### 4. **Comment Complex Settings**

```hcl
service "ogc_api" {
  enabled = true

  settings = {
    # Increase for analytics queries that return many features
    max_page_size = 10000

    # Cache responses for 5 minutes to reduce database load
    # Disable in development to see changes immediately
    enable_caching = true
    cache_ttl      = 300
  }
}
```

### 5. **Version Control Configuration**

✅ **Commit to Git**:
- `honua.development.hcl`
- `honua.staging.hcl`
- `honua.production.hcl` (without secrets)

❌ **Don't commit**:
- `.env` (secrets)
- `honua.local.hcl` (local overrides)

**Example `.gitignore`**:
```
# Ignore local overrides and secrets
honua.local.hcl
.env
.env.local
```

---

## Migration from appsettings.json

### Before (appsettings.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=honua;Username=user;Password=pass"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Services": {
    "OgcApi": {
      "Enabled": true,
      "BasePath": "/ogc"
    }
  }
}
```

### After (honua.config.hcl):
```hcl
honua {
  version = "1.0"
}

data_source "primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")  # Set in environment instead
}

cache "redis" {
  type       = "redis"
  enabled    = true
  connection = env("REDIS_URL")
}

service "ogc_api" {
  enabled   = true
  base_path = "/ogc"
}
```

**Benefits**:
- ✅ Type safety (validated at load time)
- ✅ Comments and documentation inline
- ✅ References reduce duplication
- ✅ Version control friendly (readable diffs)

---

## Troubleshooting

### Configuration file not found
```
[INFO] No Configuration V2 file found.
       To use Configuration V2, create a honua.config.hcl file or set HONUA_CONFIG_PATH.
```

**Solution**: Create `honua.config.hcl` or set `HONUA_CONFIG_PATH`:
```bash
export HONUA_CONFIG_PATH=/path/to/config.hcl
dotnet run
```

### Validation errors
```
[ERROR] Failed to load Configuration V2 from honua.config.hcl.
        Parse error at line 15: Unexpected token 'services'
```

**Solution**: Check HCL syntax. Common issues:
- Missing `=` in assignments
- Unclosed blocks `{}`
- Typos in block names

### Environment variable not interpolated
```
[ERROR] Data source 'primary' connection string is empty
```

**Solution**: Ensure environment variable is set:
```bash
export DATABASE_URL="postgresql://localhost/honua"
```

Or check for typos: `env("DATABASE_URL")` (case-sensitive)

---

## Summary

| Feature | Configuration V2 (HCL) | Environment Variables | appsettings.json |
|---------|------------------------|----------------------|------------------|
| **Data sources** | ✅ Recommended | ✅ Supported | ✅ Legacy |
| **Services** | ✅ Recommended | ✅ Supported | ✅ Legacy |
| **Layers** | ✅ Recommended | ❌ Not supported | ❌ Not supported |
| **Cache** | ✅ Recommended | ✅ Supported | ✅ Legacy |
| **Rate limiting** | ✅ Recommended | ✅ Supported | ✅ Legacy |
| **Background jobs** | ⚠️ Coming soon | ✅ Use this | ✅ Use this |
| **SRE features** | ⚠️ Coming soon | ✅ Use this | ✅ Use this |
| **Secrets** | ❌ Use env vars | ✅ Recommended | ⚠️ Not secure |

**Recommendation**: Use **HCL for infrastructure**, **environment variables for secrets and advanced features**.
