# Honua Configuration 2.0 - Complete Reference Guide

**Version**: 1.0
**Last Updated**: 2025-11-11
**Status**: Production Ready

---

## Table of Contents

1. [Overview](#overview)
2. [File Format](#file-format)
3. [Global Settings](#global-settings)
4. [Data Sources](#data-sources)
5. [Services](#services)
6. [Layers](#layers)
7. [Caching](#caching)
8. [Rate Limiting](#rate-limiting)
9. [Variables & Interpolation](#variables--interpolation)
10. [Environment-Specific Configuration](#environment-specific-configuration)
11. [Validation](#validation)
12. [Examples](#examples)

---

## Overview

Honua Configuration 2.0 provides a single, declarative configuration format that replaces fragmented configuration across environment variables, `appsettings.json`, `metadata.json`, and `Program.cs`. It uses an HCL-style (HashiCorp Configuration Language) syntax that is:

- **Declarative** - Express what you want, not how to achieve it
- **Validated** - Catch errors before deployment
- **Type-safe** - Strong typing with schema validation
- **Version-controlled** - Configuration as code
- **Environment-aware** - Easy overrides for dev/staging/prod

### Benefits

- ✅ Single source of truth for all configuration
- ✅ Validation in seconds (not minutes after deployment)
- ✅ No manual service registration in `Program.cs`
- ✅ Database introspection generates configuration automatically
- ✅ Clear, self-documenting syntax

---

## File Format

Configuration files use the `.honua` or `.hcl` extension with HCL-style syntax:

```hcl
# Comments start with #

# Blocks have a type and optional label
block_type "label" {
  attribute = value
  nested_block {
    key = value
  }
}

# Attributes assign values
string_value  = "text"
number_value  = 123
boolean_value = true
list_value    = ["item1", "item2"]
```

### File Naming Conventions

- **Base configuration**: `honua.config.hcl` or `honua.config.honua`
- **Environment-specific**: `honua.{environment}.hcl`
  - `honua.development.hcl`
  - `honua.production.hcl`
  - `honua.test.hcl`

### Loading Priority

1. `honua.config.hcl` (base configuration)
2. `honua.{ENVIRONMENT}.hcl` (environment overlay)
3. Environment variables (final override)

---

## Global Settings

The `honua` block defines global server settings:

```hcl
honua {
  version     = "1.0"              # Config schema version
  environment = "development"       # Environment name
  log_level   = "information"       # Logging level

  # CORS settings
  cors {
    allow_any_origin = false        # Allow all origins (dev only!)
    allowed_origins  = [            # Specific origins
      "https://app.example.com",
      "https://admin.example.com"
    ]
    allow_credentials = true        # Allow cookies/auth
    max_age           = 3600        # Preflight cache (seconds)
  }
}
```

### Attributes

| Attribute     | Type   | Required | Default       | Description                        |
|---------------|--------|----------|---------------|------------------------------------|
| `version`     | string | Yes      | -             | Configuration schema version       |
| `environment` | string | Yes      | -             | Environment name (dev/prod/test)   |
| `log_level`   | string | No       | "information" | Logging level (see values below)   |

### Log Levels

- `trace` - Very detailed diagnostic information
- `debug` - Debugging information
- `information` - General informational messages (default)
- `warning` - Warnings and recoverable errors
- `error` - Errors and exceptions
- `critical` - Critical failures

### CORS Block

| Attribute            | Type     | Required | Default | Description                          |
|----------------------|----------|----------|---------|--------------------------------------|
| `allow_any_origin`   | bool     | No       | false   | Allow all origins (⚠️ dev only)      |
| `allowed_origins`    | list     | No       | []      | Specific allowed origins             |
| `allow_credentials`  | bool     | No       | false   | Allow cookies/authentication         |
| `max_age`            | int      | No       | 3600    | Preflight cache duration (seconds)   |

---

## Data Sources

Data sources define database connections:

```hcl
data_source "postgres_primary" {
  provider   = "postgresql"                    # Database provider
  connection = env("DATABASE_URL")             # Connection string

  # Connection pooling (optional)
  pool {
    min_size = 5                               # Minimum connections
    max_size = 20                              # Maximum connections
    timeout  = 30                              # Connection timeout (seconds)
  }

  # Health check query (optional)
  health_check = "SELECT 1"
}

data_source "sqlite_local" {
  provider   = "sqlite"
  connection = "Data Source=./data/local.db"
}
```

### Attributes

| Attribute      | Type   | Required | Description                               |
|----------------|--------|----------|-------------------------------------------|
| `provider`     | string | Yes      | Database provider (see supported values)  |
| `connection`   | string | Yes      | Connection string or env() reference      |
| `health_check` | string | No       | SQL query to verify connection            |

### Supported Providers

- `postgresql` - PostgreSQL (with PostGIS support)
- `sqlite` - SQLite (with SpatiaLite support)
- `sqlserver` - Microsoft SQL Server (with Spatial support)
- `mysql` - MySQL/MariaDB (with Spatial support)

### Connection Pool Block

| Attribute  | Type | Required | Default | Description                      |
|------------|------|----------|---------|----------------------------------|
| `min_size` | int  | No       | 5       | Minimum pool size                |
| `max_size` | int  | No       | 20      | Maximum pool size                |
| `timeout`  | int  | No       | 30      | Connection timeout (seconds)     |

---

## Services

Services define which OGC/geospatial APIs to enable:

```hcl
service "odata" {
  enabled       = true                    # Enable this service
  allow_writes  = false                   # Read-only in production
  max_page_size = 1000                    # Maximum page size
  default_page_size = 100                 # Default page size

  # OData-specific settings
  emit_wkt_shadow_properties = true       # Emit WKT geometry properties
  enable_case_insensitive    = true       # Case-insensitive filtering
}

service "ogc_api" {
  enabled     = true
  item_limit  = 10000                     # Max items per request
  default_crs = "EPSG:4326"               # Default CRS
  additional_crs = [                      # Additional supported CRS
    "EPSG:3857",
    "EPSG:4269"
  ]

  # OGC API Features conformance classes
  conformance = [
    "core",
    "geojson",
    "html",
    "crs",
    "filter",
    "simple-cql"
  ]
}

service "wfs" {
  enabled = true
  version = "2.0.0"                       # WFS version
}

service "wms" {
  enabled = true
  version = "1.3.0"                       # WMS version
}
```

### Common Service Attributes

| Attribute | Type | Required | Default | Description              |
|-----------|------|----------|---------|--------------------------|
| `enabled` | bool | Yes      | -       | Enable this service      |

### OData Service Settings

| Attribute                      | Type | Default | Description                           |
|--------------------------------|------|---------|---------------------------------------|
| `allow_writes`                 | bool | false   | Allow POST/PUT/DELETE operations      |
| `max_page_size`                | int  | 1000    | Maximum page size                     |
| `default_page_size`            | int  | 100     | Default page size                     |
| `emit_wkt_shadow_properties`   | bool | false   | Emit WKT for geometry properties      |
| `enable_case_insensitive`      | bool | false   | Enable case-insensitive filtering     |

### OGC API Features Settings

| Attribute        | Type     | Default      | Description                    |
|------------------|----------|--------------|--------------------------------|
| `item_limit`     | int      | 10000        | Max items per request          |
| `default_crs`    | string   | "EPSG:4326"  | Default CRS                    |
| `additional_crs` | list     | []           | Additional supported CRS       |
| `conformance`    | list     | ["core"]     | Conformance classes            |

---

## Layers

Layers define which database tables/views to expose:

```hcl
layer "roads_primary" {
  title         = "Primary Roads"           # Human-readable title
  description   = "Major road network"      # Optional description
  data_source   = data_source.postgres_primary
  table         = "public.roads_primary"    # Fully-qualified table name

  # Primary key
  id_field      = "road_id"

  # Display field (for labels)
  display_field = "name"

  # Geometry configuration
  geometry {
    column = "geom"                         # Geometry column name
    type   = "LineString"                   # Geometry type
    srid   = 3857                           # Spatial reference ID
  }

  # Field introspection (automatic field detection)
  introspect_fields = true

  # OR explicit field definitions
  # fields {
  #   road_id    = { type = "int", nullable = false }
  #   name       = { type = "string", nullable = true }
  #   status     = { type = "string", nullable = true }
  #   created_at = { type = "datetime", nullable = false }
  #   geom       = { type = "geometry", nullable = false }
  # }

  # Which services expose this layer
  services = [
    service.odata,
    service.ogc_api,
    service.wfs
  ]

  # Permissions (optional)
  permissions {
    allow_anonymous_read = true
    require_auth_write   = true
  }
}
```

### Layer Attributes

| Attribute           | Type    | Required | Description                          |
|---------------------|---------|----------|--------------------------------------|
| `title`             | string  | Yes      | Human-readable layer title           |
| `description`       | string  | No       | Layer description                    |
| `data_source`       | ref     | Yes      | Reference to data source             |
| `table`             | string  | Yes      | Table or view name                   |
| `id_field`          | string  | Yes      | Primary key column                   |
| `display_field`     | string  | No       | Column for labels/display            |
| `introspect_fields` | bool    | No       | Auto-detect fields (default: false)  |
| `services`          | list    | Yes      | Services that expose this layer      |

### Geometry Block

| Attribute | Type   | Required | Description                               |
|-----------|--------|----------|-------------------------------------------|
| `column`  | string | Yes      | Geometry column name                      |
| `type`    | string | Yes      | Geometry type (see supported types)       |
| `srid`    | int    | Yes      | Spatial Reference ID (e.g., 4326, 3857)   |

### Supported Geometry Types

- `Point`
- `LineString`
- `Polygon`
- `MultiPoint`
- `MultiLineString`
- `MultiPolygon`
- `GeometryCollection`

### Field Types

When using explicit field definitions:

| Type         | Description                  | Example                        |
|--------------|------------------------------|--------------------------------|
| `int`        | 32-bit integer               | `42`                           |
| `long`       | 64-bit integer               | `9223372036854775807`          |
| `float`      | Single-precision float       | `3.14`                         |
| `double`     | Double-precision float       | `3.141592653589793`            |
| `decimal`    | High-precision decimal       | `99.99`                        |
| `string`     | Text/varchar                 | `"Hello"`                      |
| `bool`       | Boolean                      | `true` or `false`              |
| `datetime`   | Date and time                | `2025-11-11T14:30:00Z`         |
| `date`       | Date only                    | `2025-11-11`                   |
| `time`       | Time only                    | `14:30:00`                     |
| `guid`       | UUID/GUID                    | `550e8400-e29b-41d4-a716-...`  |
| `geometry`   | Spatial geometry             | -                              |
| `binary`     | Binary data                  | -                              |
| `json`       | JSON data                    | `{"key": "value"}`             |

---

## Caching

Configure distributed caching (Redis, etc.):

```hcl
cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")

  # Cache options
  prefix     = "honua:"                   # Key prefix
  ttl        = 3600                       # Default TTL (seconds)

  # Required in specific environments
  required_in = ["production", "staging"]
}
```

### Cache Attributes

| Attribute     | Type   | Required | Default | Description                        |
|---------------|--------|----------|---------|------------------------------------|
| `enabled`     | bool   | Yes      | -       | Enable this cache                  |
| `connection`  | string | Yes      | -       | Connection string                  |
| `prefix`      | string | No       | ""      | Key prefix for all cache keys      |
| `ttl`         | int    | No       | 3600    | Default TTL (seconds)              |
| `required_in` | list   | No       | []      | Environments where cache required  |

---

## Rate Limiting

Configure request rate limiting:

```hcl
rate_limit {
  enabled = true
  store   = "redis"                       # "redis" or "memory"

  # Rate limit rules
  rules {
    default = {
      requests = 1000                     # Max requests
      window   = "1m"                     # Time window
    }

    authenticated = {
      requests = 10000
      window   = "1m"
    }

    admin = {
      requests = 100000
      window   = "1m"
    }
  }
}
```

### Rate Limit Attributes

| Attribute | Type   | Required | Default  | Description                      |
|-----------|--------|----------|----------|----------------------------------|
| `enabled` | bool   | Yes      | -        | Enable rate limiting             |
| `store`   | string | Yes      | "memory" | Storage backend (redis/memory)   |
| `rules`   | block  | Yes      | -        | Rate limit rules                 |

### Rule Attributes

| Attribute  | Type   | Required | Description                        |
|------------|--------|----------|------------------------------------|
| `requests` | int    | Yes      | Maximum requests                   |
| `window`   | string | Yes      | Time window (e.g., "1m", "1h")     |

### Time Window Formats

- `1s` - 1 second
- `1m` - 1 minute
- `1h` - 1 hour
- `1d` - 1 day

---

## Variables & Interpolation

Configuration supports variables and environment variable interpolation:

### Environment Variables

Two syntaxes are supported:

```hcl
# Syntax 1: ${env:VAR_NAME}
connection = "${env:DATABASE_URL}"

# Syntax 2: env("VAR_NAME")
connection = env("DATABASE_URL")
```

### Variable References

Define and reference variables:

```hcl
# Define variables
variable "use_redis" {
  default = false
}

variable "redis_connection" {
  default = "localhost:6379"
}

# Reference variables
cache "redis" {
  enabled    = var.use_redis
  connection = var.redis_connection
}
```

### Conditional Logic

```hcl
# Use ternary operator
rate_limit {
  store = cache.redis.enabled ? "redis" : "memory"
}
```

---

## Environment-Specific Configuration

Override settings per environment:

### Base Configuration (`honua.config.hcl`)

```hcl
honua {
  version     = "1.0"
  environment = "development"
  log_level   = "debug"
}

data_source "db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 5
    max_size = 20
  }
}
```

### Production Overrides (`honua.production.hcl`)

```hcl
# Override only what changes in production
honua {
  log_level = "warning"
}

data_source "db" {
  pool {
    min_size = 10
    max_size = 50
  }
}

# Enable Redis in production
cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")
}
```

---

## Validation

Validate configuration before deployment:

```bash
# Syntax validation only (fast)
honua config validate honua.config.hcl --syntax-only

# Full validation (includes database checks)
honua config validate honua.config.hcl --full

# With timeout for database checks
honua config validate honua.config.hcl --full --timeout 30
```

### Validation Levels

1. **Syntax Validation** (fast, ~1 second)
   - Schema correctness
   - Required fields present
   - Valid enums and types
   - Reference validity

2. **Semantic Validation** (fast, ~2 seconds)
   - Layer references valid data sources
   - Layer references valid services
   - No duplicate IDs
   - Production-specific checks

3. **Runtime Validation** (slower, ~10 seconds)
   - Database connectivity
   - Table existence
   - Column existence
   - Geometry column checks

---

## Examples

### Minimal Development Setup

```hcl
honua {
  version     = "1.0"
  environment = "development"
  log_level   = "information"

  cors {
    allow_any_origin = true
  }
}

data_source "local_db" {
  provider   = "sqlite"
  connection = "Data Source=./dev.db"
}

service "odata" {
  enabled = true
}

layer "test_features" {
  title            = "Test Features"
  data_source      = data_source.local_db
  table            = "features"
  id_field         = "id"
  display_field    = "name"
  introspect_fields = true

  geometry {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }

  services = [service.odata]
}
```

### Production Setup

```hcl
honua {
  version     = "1.0"
  environment = "production"
  log_level   = "warning"

  cors {
    allow_any_origin = false
    allowed_origins  = ["https://app.example.com"]
  }
}

data_source "postgres_primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 10
    max_size = 50
    timeout  = 30
  }

  health_check = "SELECT 1"
}

cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")
}

service "odata" {
  enabled       = true
  allow_writes  = false
  max_page_size = 500
}

service "ogc_api" {
  enabled     = true
  conformance = ["core", "geojson", "crs"]
}

layer "parcels" {
  title            = "Land Parcels"
  data_source      = data_source.postgres_primary
  table            = "public.parcels"
  id_field         = "parcel_id"
  display_field    = "parcel_number"
  introspect_fields = true

  geometry {
    column = "geom"
    type   = "Polygon"
    srid   = 3857
  }

  services = [service.odata, service.ogc_api]
}

rate_limit {
  enabled = true
  store   = "redis"

  rules {
    default = {
      requests = 1000
      window   = "1m"
    }

    authenticated = {
      requests = 10000
      window   = "1m"
    }
  }
}
```

---

## See Also

- [Quick Start Guide](./configuration-v2-quickstart.md)
- [Migration Guide](./configuration-v2-migration.md)
- [Best Practices](./configuration-v2-best-practices.md)
- [CLI Reference](./configuration-v2-cli.md)
