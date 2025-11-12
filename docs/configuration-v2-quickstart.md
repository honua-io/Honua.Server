# Honua Configuration 2.0 - Quick Start Guide

Get up and running with Honua Configuration 2.0 in under 5 minutes.

---

## Prerequisites

- Honua Server installed
- `honua` CLI tool available
- A database (PostgreSQL, SQLite, SQL Server, or MySQL)

---

## Option 1: New Project (Fastest!)

### Step 1: Initialize Configuration

```bash
# Create a new configuration from template
honua config init:v2 --template minimal

# Output: honua.config.hcl created
```

### Step 2: Customize for Your Database

Edit `honua.config.hcl`:

```hcl
data_source "my_db" {
  provider   = "postgresql"  # or "sqlite", "sqlserver", "mysql"
  connection = env("DATABASE_URL")
}
```

### Step 3: Introspect Your Database

```bash
# Generate layer configuration from your database
honua config introspect "Host=localhost;Database=mydb;..." \
  --output layers.hcl

# Merge into main config
cat layers.hcl >> honua.config.hcl
```

### Step 4: Validate

```bash
honua config validate honua.config.hcl
```

### Step 5: Run!

```bash
dotnet run
```

**Done! Your server is running with auto-generated configuration.**

---

## Option 2: Start from Scratch

### Step 1: Create Configuration File

Create `honua.config.hcl`:

```hcl
honua {
  version     = "1.0"
  environment = "development"
  log_level   = "information"

  cors {
    allow_any_origin = true  # Development only!
  }
}

# Define your data source
data_source "main_db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
}

# Enable OData service
service "odata" {
  enabled      = true
  allow_writes = true
}

# Define a layer (table/view to expose)
layer "my_features" {
  title            = "My Features"
  data_source      = data_source.main_db
  table            = "public.features"
  id_field         = "id"
  display_field    = "name"
  introspect_fields = true  # Auto-detect fields!

  geometry {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }

  services = [service.odata]
}
```

### Step 2: Set Environment Variable

```bash
export DATABASE_URL="Host=localhost;Database=mydb;Username=user;Password=pass"
```

### Step 3: Validate

```bash
honua config validate honua.config.hcl
```

### Step 4: Preview

```bash
honua config plan honua.config.hcl --show-endpoints
```

### Step 5: Run!

```bash
dotnet run
```

---

## Common Scenarios

### Scenario 1: SQLite for Local Development

```hcl
honua {
  version     = "1.0"
  environment = "development"
}

data_source "local_db" {
  provider   = "sqlite"
  connection = "Data Source=./dev.db"
}

service "odata" {
  enabled = true
}

# Let introspection handle the rest!
```

Then introspect:

```bash
honua config introspect "Data Source=./dev.db" \
  --output layers.hcl \
  --data-source-id local_db \
  --layers-only
```

### Scenario 2: Multiple Services

```hcl
# Enable multiple OGC services
service "odata" {
  enabled = true
}

service "ogc_api" {
  enabled = true
  conformance = ["core", "geojson", "crs"]
}

service "wfs" {
  enabled = true
  version = "2.0.0"
}

# Expose layer through all services
layer "my_layer" {
  # ... layer config ...
  services = [
    service.odata,
    service.ogc_api,
    service.wfs
  ]
}
```

### Scenario 3: Production with Redis

```hcl
honua {
  version     = "1.0"
  environment = "production"
  log_level   = "warning"

  cors {
    allow_any_origin = false
    allowed_origins  = ["https://myapp.com"]
  }
}

data_source "prod_db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 10
    max_size = 50
  }
}

cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")
}

service "odata" {
  enabled       = true
  allow_writes  = false  # Read-only in production
  max_page_size = 500
}

rate_limit {
  enabled = true
  store   = "redis"

  rules {
    default = {
      requests = 1000
      window   = "1m"
    }
  }
}
```

---

## Workflow Tips

### Development Workflow

```bash
# 1. Initialize
honua config init:v2 --template minimal

# 2. Introspect database
honua config introspect "$DB_CONNECTION" --output layers.hcl

# 3. Merge configurations
cat layers.hcl >> honua.config.hcl

# 4. Validate
honua config validate honua.config.hcl

# 5. Preview
honua config plan honua.config.hcl --show-endpoints

# 6. Run
dotnet run
```

### Production Deployment

```bash
# 1. Create production config
honua config init:v2 --template production --output honua.production.hcl

# 2. Customize for production
vim honua.production.hcl

# 3. Validate (including database checks)
honua config validate honua.production.hcl --full

# 4. Preview
honua config plan honua.production.hcl

# 5. Deploy
docker build -t honua-server .
docker run -e DATABASE_URL="..." -e REDIS_URL="..." honua-server
```

### Iterative Development

```bash
# Make changes to config
vim honua.config.hcl

# Validate immediately (catches errors in seconds!)
honua config validate honua.config.hcl

# Preview changes
honua config plan honua.config.hcl

# Test
dotnet run
```

---

## Troubleshooting

### Error: "Configuration file not found"

```bash
# Check current directory
ls -la *.hcl *.honua

# Or specify path explicitly
honua config validate /path/to/honua.config.hcl
```

### Error: "Data source 'X' not accessible"

```bash
# Test connection
honua config validate honua.config.hcl --full --timeout 30

# Check environment variable
echo $DATABASE_URL

# Test database directly
psql "$DATABASE_URL" -c "SELECT 1"
```

### Error: "Service 'X' not found"

This means the service implementation doesn't exist yet. Available services:

- `odata` - OData v4 ✅
- `ogc_api` - OGC API Features ✅
- `wfs` - WFS (implementation pending)
- `wms` - WMS (implementation pending)
- `wmts` - WMTS (implementation pending)

### Validation Errors

```bash
# Get detailed validation output
honua config validate honua.config.hcl --verbose

# Check specific sections
honua config plan honua.config.hcl
```

---

## CLI Commands Reference

```bash
# Initialize new configuration
honua config init:v2 [--template NAME] [--output FILE]

# Validate configuration
honua config validate [FILE] [--syntax-only] [--full] [--timeout SECONDS]

# Preview configuration
honua config plan [FILE] [--validate] [--show-endpoints]

# Introspect database
honua config introspect CONNECTION_STRING \
  [--output FILE] \
  [--provider NAME] \
  [--table-pattern PATTERN] \
  [--services SERVICE1,SERVICE2] \
  [--explicit-fields]
```

---

## Next Steps

- **Learn More**: Read the [Complete Reference Guide](./configuration-v2-reference.md)
- **Migrate**: See the [Migration Guide](./configuration-v2-migration.md) if you have existing config
- **Best Practices**: Review [Best Practices Guide](./configuration-v2-best-practices.md)
- **Examples**: Browse the [Example Configurations](../examples/config-v2/)

---

## Quick Reference

### File Structure

```
project/
├── honua.config.hcl         # Base configuration
├── honua.development.hcl    # Dev overrides
├── honua.production.hcl     # Prod overrides
└── honua.test.hcl           # Test overrides
```

### Basic Configuration Template

```hcl
honua {
  version     = "1.0"
  environment = "development"
  log_level   = "information"
}

data_source "db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
}

service "odata" {
  enabled = true
}

layer "layer_name" {
  title            = "Layer Title"
  data_source      = data_source.db
  table            = "schema.table"
  id_field         = "id"
  introspect_fields = true

  geometry {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }

  services = [service.odata]
}
```

### Validation Workflow

```bash
# Quick syntax check (1 second)
honua config validate honua.config.hcl --syntax-only

# Full validation including DB (10 seconds)
honua config validate honua.config.hcl --full

# Preview what will be configured
honua config plan honua.config.hcl --show-endpoints
```

---

## Getting Help

- **Documentation**: `docs/configuration-v2-reference.md`
- **Examples**: `examples/config-v2/`
- **Issues**: Report issues on GitHub
- **CLI Help**: `honua config --help`
